namespace Inventario.Api.Datos;

/// <summary>
/// Acceso al inventario. Toda escritura pasa por los procedimientos almacenados
/// de PostgreSQL; las lecturas, por las funciones de consulta.
/// </summary>
public interface IInventarioRepositorio
{
    Task<IReadOnlyList<ProductoEnInventario>> ObtenerInventarioAsync(string? busqueda, bool soloBajo, CancellationToken ct);

    Task<ProductoEnInventario?> ObtenerProductoAsync(int productoId, CancellationToken ct);

    Task<int> CrearProductoAsync(string codigo, string nombre, string? descripcion, string unidad,
        int stockMinimo, int cantidadInicial, string usuario, CancellationToken ct);

    Task<ResultadoMovimiento> RegistrarEntradaAsync(int productoId, int cantidad, string usuario, string? observacion, CancellationToken ct);

    Task<ResultadoMovimiento> RegistrarSalidaAsync(int productoId, int cantidad, string usuario, string? observacion, CancellationToken ct);

    Task<IReadOnlyList<MovimientoEnHistorial>> ObtenerHistorialAsync(int? productoId, string? tipo, int limite, int desplazamiento, CancellationToken ct);
}
