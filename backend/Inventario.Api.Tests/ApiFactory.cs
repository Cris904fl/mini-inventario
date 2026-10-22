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

    /// <summary>Cliente ya autenticado (admin por defecto).</summary>
    public async Task<HttpClient> ClienteAutenticadoAsync(string usuario = "admin", string clave = "admin123")
    {
        var cliente = CreateClient();
        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { usuario, clave });
        respuesta.EnsureSuccessStatusCode();
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", cuerpo.GetProperty("token").GetString());
        return cliente;
    }
}

/// <summary>
/// Usuarios en memoria: admin/admin123 (ADMIN), operador/bodega123 (OPERADOR) y
/// temporal/temporal123 (OPERADOR con clave temporal pendiente de cambio).
/// </summary>
public class UsuariosFalsos : IUsuarioRepositorio
{
    public List<Usuario> Usuarios { get; } =
    [
        new() { Id = 1, NombreUsuario = "admin", NombreCompleto = "Administrador", Rol = Roles.Admin, Activo = true,
                ClaveHash = BCrypt.Net.BCrypt.HashPassword("admin123") },
        new() { Id = 2, NombreUsuario = "operador", NombreCompleto = "Operador", Rol = Roles.Operador, Activo = true,
                ClaveHash = BCrypt.Net.BCrypt.HashPassword("bodega123") },
        new() { Id = 3, NombreUsuario = "temporal", NombreCompleto = "Con clave temporal", Rol = Roles.Operador, Activo = true,
                DebeCambiarClave = true, ClaveHash = BCrypt.Net.BCrypt.HashPassword("temporal123") },
        // Usuarios reservados para las pruebas que cambian claves (el fixture es compartido).
        new() { Id = 4, NombreUsuario = "cambia", NombreCompleto = "Cambia su clave", Rol = Roles.Operador, Activo = true,
                DebeCambiarClave = true, ClaveHash = BCrypt.Net.BCrypt.HashPassword("cambia123") },
        new() { Id = 5, NombreUsuario = "restablecido", NombreCompleto = "Le restablecen la clave", Rol = Roles.Operador, Activo = true,
                ClaveHash = BCrypt.Net.BCrypt.HashPassword("restablecido123") }
    ];

    public List<string> Acciones { get; } = [];

    public Task<Usuario?> BuscarPorNombreAsync(string nombreUsuario, CancellationToken ct)
        => Task.FromResult(Usuarios.FirstOrDefault(u => u.NombreUsuario == nombreUsuario.ToLowerInvariant()));

    public Task<Usuario?> BuscarPorIdAsync(int id, CancellationToken ct)
        => Task.FromResult(Usuarios.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<UsuarioEnLista>> ListarAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<UsuarioEnLista>>(Usuarios.Select(u => new UsuarioEnLista
        {
            Id = u.Id, NombreUsuario = u.NombreUsuario, NombreCompleto = u.NombreCompleto, Rol = u.Rol,
            Activo = u.Activo, DebeCambiarClave = u.DebeCambiarClave
        }).ToList());

    public Task<int> CrearAsync(string nombreUsuario, string nombreCompleto, string claveHash, string rol, CancellationToken ct)
    {
        if (Usuarios.Any(u => u.NombreUsuario == nombreUsuario.ToLowerInvariant()))
            throw new PostgresException("duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
        var nuevo = new Usuario { Id = Usuarios.Count + 1, NombreUsuario = nombreUsuario.ToLowerInvariant(), NombreCompleto = nombreCompleto,
            ClaveHash = claveHash, Rol = rol, Activo = true, DebeCambiarClave = true };
        Usuarios.Add(nuevo);
        return Task.FromResult(nuevo.Id);
    }

    public Task CambiarEstadoAsync(int usuarioId, bool activo, int solicitanteId, CancellationToken ct)
    {
        if (usuarioId == solicitanteId && !activo)
            throw new PostgresException("No puedes desactivar tu propio usuario", "ERROR", "ERROR", "IN005");
        var i = Usuarios.FindIndex(u => u.Id == usuarioId);
        if (i < 0) throw new PostgresException($"El usuario {usuarioId} no existe", "ERROR", "ERROR", "IN004");
        Usuarios[i] = Usuarios[i] with { Activo = activo };
        return Task.CompletedTask;
    }

    public Task CambiarClaveAsync(int usuarioId, string claveHash, CancellationToken ct)
    {
        var i = Usuarios.FindIndex(u => u.Id == usuarioId);
        Usuarios[i] = Usuarios[i] with { ClaveHash = claveHash, DebeCambiarClave = false };
        Acciones.Add($"clave:{usuarioId}");
        return Task.CompletedTask;
    }

    public Task RestablecerClaveAsync(int usuarioId, string claveTemporalHash, int solicitanteId, CancellationToken ct)
    {
        var i = Usuarios.FindIndex(u => u.Id == usuarioId);
        if (i < 0) throw new PostgresException($"El usuario {usuarioId} no existe", "ERROR", "ERROR", "IN004");
        Usuarios[i] = Usuarios[i] with { ClaveHash = claveTemporalHash, DebeCambiarClave = true };
        Acciones.Add($"restablecer:{usuarioId}");
        return Task.CompletedTask;
    }
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

    public Task<ResultadoAjuste> RegistrarAjusteAsync(int productoId, int cantidadContada, string usuario, string motivo, CancellationToken ct)
    {
        var producto = Buscar(productoId);
        var diferencia = cantidadContada - producto.Cantidad;
        if (diferencia == 0)
            throw new PostgresException($"El conteo ({cantidadContada}) coincide con el stock registrado; no hay nada que ajustar", "ERROR", "ERROR", "IN003");
        Registrados.Add(("AJUSTE", productoId, diferencia, usuario));
        return Task.FromResult(new ResultadoAjuste(102, diferencia, cantidadContada));
    }

    public Task<IReadOnlyList<MovimientoEnHistorial>> ObtenerHistorialAsync(int? productoId, string? tipo, int limite, int desplazamiento, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MovimientoEnHistorial>>([]);

    public Task<ResumenInventario> ObtenerResumenAsync(int dias, string zona, CancellationToken ct)
        => Task.FromResult(new ResumenInventario
        {
            Productos = Productos.Count, Unidades = Productos.Sum(p => p.Cantidad),
            EnBajo = Productos.Count(p => p.Estado == "BAJO"), Agotados = Productos.Count(p => p.Estado == "AGOTADO"),
            EntradasPeriodo = 10, SalidasPeriodo = 4, MovimientosPeriodo = 3
        });

    public Task<IReadOnlyList<MovimientosDelDia>> ObtenerMovimientosPorDiaAsync(int dias, string zona, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MovimientosDelDia>>(Enumerable.Range(0, dias)
            .Select(i => new MovimientosDelDia { Dia = DateTime.Today.AddDays(i - dias + 1), Entradas = i, Salidas = 0 })
            .ToList());

    public Task<IReadOnlyList<ProductoMasMovido>> ObtenerMasMovidosAsync(int dias, int limite, string zona, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ProductoMasMovido>>(
            [new ProductoMasMovido { ProductoId = 1, Codigo = "EST-120", Nombre = "Estiba", Salidas = 4, Entradas = 10 }]);

    private ProductoEnInventario Buscar(int productoId)
        => Productos.FirstOrDefault(p => p.Id == productoId)
           ?? throw new PostgresException($"El producto {productoId} no existe o esta inactivo", "ERROR", "ERROR", "IN002");
}
