using Inventario.Api.Datos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net.Http.Json;
using Npgsql;

namespace Inventario.Api.Tests;

/// <summary>
/// Arranca la API en memoria con un repositorio falso: asi las pruebas de los
/// controladores y del middleware no necesitan PostgreSQL.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public RepositorioFalso Inventario { get; } = new();
    public UsuariosFalsos Usuarios { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Inventario", "Host=localhost;Database=no_se_usa");
        builder.UseSetting("Jwt:Clave", "clave-de-pruebas-con-mas-de-treinta-y-dos-caracteres");
        builder.ConfigureServices(servicios =>
        {
            servicios.RemoveAll<IInventarioRepositorio>();
            servicios.RemoveAll<IUsuarioRepositorio>();
            servicios.AddSingleton<IInventarioRepositorio>(Inventario);
            servicios.AddSingleton<IUsuarioRepositorio>(Usuarios);
        });
    }

    /// <summary>Cliente ya autenticado como admin.</summary>
    public async Task<HttpClient> ClienteAutenticadoAsync()
    {
        var cliente = CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { usuario = "admin", clave = "admin123" });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", cuerpo.GetProperty("token").GetString());
        return cliente;
    }
}

public class UsuariosFalsos : IUsuarioRepositorio
{
    // admin / admin123
    private static readonly Usuario Admin = new()
    {
        Id = 1, NombreUsuario = "admin", NombreCompleto = "Administrador",
        ClaveHash = BCrypt.Net.BCrypt.HashPassword("admin123"), Activo = true
    };

    public Task<Usuario?> BuscarPorNombreAsync(string nombreUsuario, CancellationToken ct)
        => Task.FromResult(nombreUsuario == "admin" ? Admin : null);
}

/// <summary>Imita el comportamiento de los procedimientos, incluidos sus errores.</summary>
public class RepositorioFalso : IInventarioRepositorio
{
    public List<ProductoEnInventario> Productos { get; } =
    [
        new() { Id = 1, Codigo = "EST-120", Nombre = "Estiba", Cantidad = 10, StockMinimo = 5, Estado = "DISPONIBLE" },
        new() { Id = 2, Codigo = "PRE-SEG", Nombre = "Precinto", Cantidad = 0, StockMinimo = 5, Estado = "AGOTADO" }
    ];

    public List<(string Tipo, int ProductoId, int Cantidad, string Usuario)> Registrados { get; } = [];

    public Task<IReadOnlyList<ProductoEnInventario>> ObtenerInventarioAsync(string? busqueda, bool soloBajo, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ProductoEnInventario>>(Productos);

    public Task<ProductoEnInventario?> ObtenerProductoAsync(int productoId, CancellationToken ct)
        => Task.FromResult(Productos.FirstOrDefault(p => p.Id == productoId));

    public Task<int> CrearProductoAsync(string codigo, string nombre, string? descripcion, string unidad, int stockMinimo, int cantidadInicial, string usuario, CancellationToken ct)
    {
        if (Productos.Any(p => p.Codigo == codigo))
            throw new PostgresException("duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
        var nuevo = new ProductoEnInventario
        {
            Id = Productos.Count + 1, Codigo = codigo, Nombre = nombre, Descripcion = descripcion, Unidad = unidad,
            Cantidad = cantidadInicial, StockMinimo = stockMinimo, Estado = "DISPONIBLE"
        };
        Productos.Add(nuevo);
        return Task.FromResult(nuevo.Id);
    }

    public Task<ResultadoMovimiento> RegistrarEntradaAsync(int productoId, int cantidad, string usuario, string? observacion, CancellationToken ct)
    {
        var producto = Buscar(productoId);
        Registrados.Add(("ENTRADA", productoId, cantidad, usuario));
        return Task.FromResult(new ResultadoMovimiento(100, producto.Cantidad + cantidad));
    }

    public Task<ResultadoMovimiento> RegistrarSalidaAsync(int productoId, int cantidad, string usuario, string? observacion, CancellationToken ct)
    {
        var producto = Buscar(productoId);
        if (producto.Cantidad < cantidad)
            throw new PostgresException($"Stock insuficiente: hay {producto.Cantidad} y se intentan retirar {cantidad}",
                "ERROR", "ERROR", "IN001", detail: $"{{\"disponible\":{producto.Cantidad},\"solicitado\":{cantidad}}}");
        Registrados.Add(("SALIDA", productoId, cantidad, usuario));
        return Task.FromResult(new ResultadoMovimiento(101, producto.Cantidad - cantidad));
    }

    public Task<IReadOnlyList<MovimientoEnHistorial>> ObtenerHistorialAsync(int? productoId, string? tipo, int limite, int desplazamiento, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MovimientoEnHistorial>>([]);

    private ProductoEnInventario Buscar(int productoId)
        => Productos.FirstOrDefault(p => p.Id == productoId)
           ?? throw new PostgresException($"El producto {productoId} no existe o esta inactivo", "ERROR", "ERROR", "IN002");
}
