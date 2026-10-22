using Inventario.Api.Contratos;
using Inventario.Api.Datos;
using Inventario.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

[ApiController]
[Route("api/movimientos")]
[Authorize]
[Produces("application/json")]
public class MovimientosController(IInventarioRepositorio inventario) : ControllerBase
{
    private const int TamanoMaximo = 100;

    /// <summary>Registra una ENTRADA: suma al stock e inserta el movimiento en una sola transaccion.</summary>
    /// <response code="201">Entrada registrada; devuelve el stock resultante.</response>
    /// <response code="404">El producto no existe.</response>
    [HttpPost("entradas")]
    [ProducesResponseType(typeof(RespuestaMovimiento), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RespuestaMovimiento>> Entrada([FromBody] SolicitudMovimiento solicitud, CancellationToken ct)
    {
        // El usuario sale del token, nunca del cuerpo de la peticion.
        var usuario = User.NombreUsuario();
        var resultado = await inventario.RegistrarEntradaAsync(solicitud.ProductoId, solicitud.Cantidad, usuario, solicitud.Observacion, ct);
        return Created($"/api/movimientos/{resultado.MovimientoId}",
            new RespuestaMovimiento(resultado.MovimientoId, solicitud.ProductoId, "ENTRADA", solicitud.Cantidad, resultado.StockResultante, usuario));
    }

    /// <summary>Registra una SALIDA. Si no hay stock suficiente responde 409 y no cambia nada.</summary>
    /// <response code="201">Salida registrada; devuelve el stock resultante.</response>
    /// <response code="404">El producto no existe.</response>
    /// <response code="409">Stock insuficiente.</response>
    [HttpPost("salidas")]
    [ProducesResponseType(typeof(RespuestaMovimiento), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RespuestaMovimiento>> Salida([FromBody] SolicitudMovimiento solicitud, CancellationToken ct)
    {
        var usuario = User.NombreUsuario();
        var resultado = await inventario.RegistrarSalidaAsync(solicitud.ProductoId, solicitud.Cantidad, usuario, solicitud.Observacion, ct);
        return Created($"/api/movimientos/{resultado.MovimientoId}",
            new RespuestaMovimiento(resultado.MovimientoId, solicitud.ProductoId, "SALIDA", solicitud.Cantidad, resultado.StockResultante, usuario));
    }

    /// <summary>Registra un AJUSTE por conteo fisico (solo ADMIN): se informa lo contado y la base calcula la diferencia.</summary>
    /// <response code="201">Ajuste registrado; devuelve la diferencia y el stock resultante.</response>
    /// <response code="400">Conteo negativo, sin motivo o igual al stock registrado.</response>
    /// <response code="403">El usuario no es ADMIN.</response>
    [HttpPost("ajustes")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(RespuestaAjuste), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RespuestaAjuste>> Ajuste([FromBody] SolicitudAjuste solicitud, CancellationToken ct)
    {
        var usuario = User.NombreUsuario();
        var r = await inventario.RegistrarAjusteAsync(solicitud.ProductoId, solicitud.CantidadContada, usuario, solicitud.Motivo.Trim(), ct);
        return Created($"/api/movimientos/{r.MovimientoId}",
            new RespuestaAjuste(r.MovimientoId, solicitud.ProductoId, solicitud.CantidadContada, r.Diferencia, r.StockResultante, usuario));
    }

    /// <summary>Historial de movimientos, del mas reciente al mas antiguo, paginado.</summary>
    /// <param name="productoId">Solo los movimientos de este producto.</param>
    /// <param name="tipo">ENTRADA, SALIDA o AJUSTE.</param>
    /// <param name="pagina">Numero de pagina, desde 1.</param>
    /// <param name="tamano">Elementos por pagina (1 a 100).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaDeMovimientos), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDeMovimientos>> Historial(
        [FromQuery] int? productoId, [FromQuery] string? tipo,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 20, CancellationToken ct = default)
    {
        if (tipo is not null && tipo.ToUpperInvariant() is not ("ENTRADA" or "SALIDA" or "AJUSTE"))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Filtro invalido",
                detail: "El tipo debe ser ENTRADA, SALIDA o AJUSTE.");

        pagina = Math.Max(pagina, 1);
        tamano = Math.Clamp(tamano, 1, TamanoMaximo);

        var filas = await inventario.ObtenerHistorialAsync(productoId, tipo?.ToUpperInvariant(), tamano, (pagina - 1) * tamano, ct);
        var total = filas.Count > 0 ? filas[0].Total : 0;

        return Ok(new PaginaDeMovimientos(
            filas.Select(f => new MovimientoResumen(f.Id, f.Fecha, f.ProductoId, f.Codigo, f.Producto, f.Tipo,
                f.Cantidad, f.Diferencia, f.StockResultante, f.Usuario, f.Observacion)).ToList(),
            total, pagina, tamano, (int)Math.Ceiling(total / (double)tamano)));
    }
}
