using Dapper;
using Npgsql;

namespace Inventario.Api.Datos;

/// <summary>
/// Implementacion con Dapper. No hay SQL de negocio aqui: cada metodo invoca
/// un procedimiento (CALL) o una funcion (SELECT ... FROM fn_...) de la base.
/// </summary>
public class InventarioRepositorio(NpgsqlDataSource datos) : IInventarioRepositorio
{
    public async Task<IReadOnlyList<ProductoEnInventario>> ObtenerInventarioAsync(string? busqueda, bool soloBajo, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        var filas = await conexion.QueryAsync<ProductoEnInventario>(new CommandDefinition(
            "SELECT * FROM fn_inventario_actual(@busqueda, @soloBajo)",
            new { busqueda, soloBajo },
            cancellationToken: ct));
        return filas.AsList();
    }

    public async Task<ProductoEnInventario?> ObtenerProductoAsync(int productoId, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        return await conexion.QuerySingleOrDefaultAsync<ProductoEnInventario>(new CommandDefinition(
            "SELECT * FROM fn_inventario_actual() WHERE id = @productoId",
            new { productoId },
            cancellationToken: ct));
    }

    public async Task<int> CrearProductoAsync(string codigo, string nombre, string? descripcion, string unidad,
        int stockMinimo, int cantidadInicial, string usuario, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        // El ultimo argumento es el parametro INOUT p_producto_id: el CALL
        // responde una fila con el id asignado.
        return await conexion.ExecuteScalarAsync<int>(new CommandDefinition(
            "CALL sp_crear_producto(@codigo, @nombre, @descripcion, @unidad, @stockMinimo, @cantidadInicial, @usuario, NULL)",
            new { codigo, nombre, descripcion, unidad, stockMinimo, cantidadInicial, usuario },
            cancellationToken: ct));
    }

    public Task<ResultadoMovimiento> RegistrarEntradaAsync(int productoId, int cantidad, string usuario, string? observacion, CancellationToken ct)
        => RegistrarMovimientoAsync("sp_registrar_entrada", productoId, cantidad, usuario, observacion, ct);

    public Task<ResultadoMovimiento> RegistrarSalidaAsync(int productoId, int cantidad, string usuario, string? observacion, CancellationToken ct)
        => RegistrarMovimientoAsync("sp_registrar_salida", productoId, cantidad, usuario, observacion, ct);

    private async Task<ResultadoMovimiento> RegistrarMovimientoAsync(string procedimiento, int productoId, int cantidad,
        string usuario, string? observacion, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        // Los dos NULL finales son los parametros INOUT (p_movimiento_id,
        // p_stock_resultante): PostgreSQL los devuelve como una fila, que
        // Dapper lee por posicion en una tupla.
        // Si el procedimiento lanza RAISE EXCEPTION, Npgsql lo convierte en
        // PostgresException con el SQLSTATE (IN001, IN002, IN003) y lo
        // traduce ManejoErroresMiddleware.
        var (movimientoId, stockResultante) = await conexion.QuerySingleAsync<(long, int)>(new CommandDefinition(
            $"CALL {procedimiento}(@productoId, @cantidad, @usuario, @observacion, NULL, NULL)",
            new { productoId, cantidad, usuario, observacion },
            cancellationToken: ct));
        return new ResultadoMovimiento(movimientoId, stockResultante);
    }

    public async Task<ResumenInventario> ObtenerResumenAsync(int dias, string zona, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        return await conexion.QuerySingleAsync<ResumenInventario>(new CommandDefinition(
            "SELECT * FROM fn_resumen_inventario(@dias, @zona)", new { dias, zona }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<MovimientosDelDia>> ObtenerMovimientosPorDiaAsync(int dias, string zona, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        var filas = await conexion.QueryAsync<MovimientosDelDia>(new CommandDefinition(
            "SELECT * FROM fn_movimientos_por_dia(@dias, @zona)", new { dias, zona }, cancellationToken: ct));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<ProductoMasMovido>> ObtenerMasMovidosAsync(int dias, int limite, string zona, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        var filas = await conexion.QueryAsync<ProductoMasMovido>(new CommandDefinition(
            "SELECT * FROM fn_productos_mas_movidos(@dias, @limite, @zona)", new { dias, limite, zona }, cancellationToken: ct));
        return filas.AsList();
    }

    public async Task<ResultadoAjuste> RegistrarAjusteAsync(int productoId, int cantidadContada, string usuario, string motivo, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        // Tres INOUT: id del movimiento, diferencia calculada y stock resultante.
        var (movimientoId, diferencia, stockResultante) = await conexion.QuerySingleAsync<(long, int, int)>(new CommandDefinition(
            "CALL sp_registrar_ajuste(@productoId, @cantidadContada, @usuario, @motivo, NULL, NULL, NULL)",
            new { productoId, cantidadContada, usuario, motivo },
            cancellationToken: ct));
        return new ResultadoAjuste(movimientoId, diferencia, stockResultante);
    }

    public async Task<IReadOnlyList<MovimientoEnHistorial>> ObtenerHistorialAsync(int? productoId, string? tipo, int limite, int desplazamiento, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        var filas = await conexion.QueryAsync<MovimientoEnHistorial>(new CommandDefinition(
            "SELECT * FROM fn_historial_movimientos(@productoId, @tipo, @limite, @desplazamiento)",
            new { productoId, tipo, limite, desplazamiento },
            cancellationToken: ct));
        return filas.AsList();
    }
}
