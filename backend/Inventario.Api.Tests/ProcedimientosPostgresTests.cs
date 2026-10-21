using Inventario.Api.Datos;
using Npgsql;

namespace Inventario.Api.Tests;

/// <summary>
/// Pruebas contra PostgreSQL real: verifican que los procedimientos hacen lo
/// que promete el script. Corren solo si existe la variable de entorno
/// INVENTARIO_TEST_DB con la cadena de conexion (por ejemplo, la de
/// docker-compose.yml); si no, se omiten.
/// </summary>
public class ProcedimientosPostgresTests : IAsyncLifetime
{
    private static readonly string? Cadena = Environment.GetEnvironmentVariable("INVENTARIO_TEST_DB");

    private NpgsqlDataSource _datos = null!;
    private InventarioRepositorio _repositorio = null!;
    private int _productoId;

    public async Task InitializeAsync()
    {
        if (Cadena is null) return;
        _datos = NpgsqlDataSource.Create(Cadena);
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        _repositorio = new InventarioRepositorio(_datos);

        // Cada corrida crea su propio producto para no depender de los datos semilla.
        _productoId = await _repositorio.CrearProductoAsync(
            $"TST-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}", "Producto de prueba", null, "UND",
            stockMinimo: 2, cantidadInicial: 10, usuario: "tests", CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (Cadena is null) return;
        await using var conexion = await _datos.OpenConnectionAsync();
        await using var limpiar = new NpgsqlCommand(
            "DELETE FROM movimientos WHERE producto_id = @id; DELETE FROM productos WHERE id = @id;", conexion);
        limpiar.Parameters.AddWithValue("id", _productoId);
        await limpiar.ExecuteNonQueryAsync();
        await _datos.DisposeAsync();
    }

    [SkippableFact]
    public async Task La_entrada_suma_y_deja_el_movimiento_registrado()
    {
        var resultado = await _repositorio.RegistrarEntradaAsync(_productoId, 5, "tests", "compra", CancellationToken.None);

        Assert.Equal(15, resultado.StockResultante);
        var producto = await _repositorio.ObtenerProductoAsync(_productoId, CancellationToken.None);
        Assert.Equal(15, producto!.Cantidad);
        var historial = await _repositorio.ObtenerHistorialAsync(_productoId, "ENTRADA", 10, 0, CancellationToken.None);
        Assert.Contains(historial, m => m.Cantidad == 5 && m.StockResultante == 15 && m.Observacion == "compra");
    }

    [SkippableFact]
    public async Task La_salida_sin_stock_falla_con_IN001_y_no_cambia_nada()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _repositorio.RegistrarSalidaAsync(_productoId, 999, "tests", null, CancellationToken.None));

        Assert.Equal("IN001", ex.SqlState);
        var producto = await _repositorio.ObtenerProductoAsync(_productoId, CancellationToken.None);
        Assert.Equal(10, producto!.Cantidad);
        var historial = await _repositorio.ObtenerHistorialAsync(_productoId, "SALIDA", 10, 0, CancellationToken.None);
        Assert.Empty(historial);
    }

    [SkippableFact]
    public async Task La_salida_valida_descuenta_y_el_estado_pasa_a_AGOTADO_al_llegar_a_cero()
    {
        var resultado = await _repositorio.RegistrarSalidaAsync(_productoId, 10, "tests", null, CancellationToken.None);

        Assert.Equal(0, resultado.StockResultante);
        var producto = await _repositorio.ObtenerProductoAsync(_productoId, CancellationToken.None);
        Assert.Equal("AGOTADO", producto!.Estado);
    }

    [SkippableFact]
    public async Task Un_producto_inexistente_falla_con_IN002()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _repositorio.RegistrarEntradaAsync(-1, 1, "tests", null, CancellationToken.None));

        Assert.Equal("IN002", ex.SqlState);
    }

    [SkippableFact]
    public async Task Dos_salidas_concurrentes_no_dejan_el_stock_negativo()
    {
        // Hay 10 unidades. Dos salidas de 7 al mismo tiempo: una debe pasar y la
        // otra fallar con IN001, gracias al FOR UPDATE del procedimiento.
        var tareas = Enumerable.Range(0, 2).Select(async _ =>
        {
            try { await _repositorio.RegistrarSalidaAsync(_productoId, 7, "tests", null, CancellationToken.None); return "ok"; }
            catch (PostgresException ex) { return ex.SqlState; }
        });

        var resultados = await Task.WhenAll(tareas);

        Assert.Equal(["IN001", "ok"], resultados.Order());
        var producto = await _repositorio.ObtenerProductoAsync(_productoId, CancellationToken.None);
        Assert.Equal(3, producto!.Cantidad);
    }
}

/// <summary>Fact que se omite cuando no hay base de datos configurada.</summary>
public sealed class SkippableFactAttribute : FactAttribute
{
    public SkippableFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("INVENTARIO_TEST_DB") is null)
            Skip = "Define INVENTARIO_TEST_DB con la cadena de conexion para correr las pruebas contra PostgreSQL.";
    }
}
