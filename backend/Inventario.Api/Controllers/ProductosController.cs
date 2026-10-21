using Inventario.Api.Contratos;
using Inventario.Api.Datos;
using Inventario.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

[ApiController]
[Route("api/productos")]
[Authorize]
[Produces("application/json")]
public class ProductosController(IInventarioRepositorio inventario) : ControllerBase
{
    /// <summary>Inventario actual: cada producto con su cantidad y estado (DISPONIBLE, BAJO, AGOTADO).</summary>
    /// <param name="busqueda">Filtra por codigo o nombre.</param>
    /// <param name="soloBajo">Si es true, solo devuelve los productos en BAJO o AGOTADO.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProductoEnInventario>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductoEnInventario>>> Inventario(
        [FromQuery] string? busqueda, [FromQuery] bool soloBajo, CancellationToken ct)
        => Ok(await inventario.ObtenerInventarioAsync(busqueda, soloBajo, ct));

    /// <summary>Un producto del inventario por id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductoEnInventario), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoEnInventario>> Producto(int id, CancellationToken ct)
    {
        var producto = await inventario.ObtenerProductoAsync(id, ct);
        return producto is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Producto no encontrado",
                detail: $"No existe un producto con id {id}.")
            : Ok(producto);
    }

    /// <summary>Crea un producto. Si trae cantidad inicial, queda registrada como ENTRADA.</summary>
    /// <response code="201">Producto creado.</response>
    /// <response code="409">Ya existe un producto con ese codigo.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ProductoEnInventario), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductoEnInventario>> Crear([FromBody] SolicitudProducto solicitud, CancellationToken ct)
    {
        var id = await inventario.CrearProductoAsync(
            solicitud.Codigo, solicitud.Nombre, solicitud.Descripcion,
            string.IsNullOrWhiteSpace(solicitud.Unidad) ? "UND" : solicitud.Unidad,
            solicitud.StockMinimo, solicitud.CantidadInicial, User.NombreUsuario(), ct);

        var creado = await inventario.ObtenerProductoAsync(id, ct);
        return CreatedAtAction(nameof(Producto), new { id }, creado);
    }
}
