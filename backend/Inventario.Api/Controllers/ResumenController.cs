using Inventario.Api.Contratos;
using Inventario.Api.Datos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

/// <summary>Datos del tablero de inicio.</summary>
[ApiController]
[Route("api/resumen")]
[Authorize]
[Produces("application/json")]
public class ResumenController(IInventarioRepositorio inventario) : ControllerBase
{
    private const int DiasMaximo = 90;
    private const string ZonaPorDefecto = "America/Bogota";

    /// <summary>Cifras del inventario, movimientos por dia, productos que mas rotan, bajo minimo y ultimos movimientos.</summary>
    /// <param name="dias">Tamano del periodo en dias (1 a 90), contando hoy.</param>
    /// <param name="zona">Zona horaria IANA para agrupar por dia (por defecto America/Bogota).</param>
    [HttpGet]
    [ProducesResponseType(typeof(RespuestaResumen), StatusCodes.Status200OK)]
    public async Task<ActionResult<RespuestaResumen>> Obtener(
        [FromQuery] int dias = 7, [FromQuery] string? zona = null, CancellationToken ct = default)
    {
        dias = Math.Clamp(dias, 1, DiasMaximo);
        zona = string.IsNullOrWhiteSpace(zona) ? ZonaPorDefecto : zona.Trim();

        // Las cinco consultas son independientes; se lanzan en paralelo, cada una con su conexion del pool.
        var totales = inventario.ObtenerResumenAsync(dias, zona, ct);
        var porDia = inventario.ObtenerMovimientosPorDiaAsync(dias, zona, ct);
        var masMovidos = inventario.ObtenerMasMovidosAsync(dias, 5, zona, ct);
        var bajoMinimo = inventario.ObtenerInventarioAsync(null, soloBajo: true, ct);
        var ultimos = inventario.ObtenerHistorialAsync(null, null, 5, 0, ct);
        await Task.WhenAll(totales, porDia, masMovidos, bajoMinimo, ultimos);

        return Ok(new RespuestaResumen(
            dias, zona, totales.Result,
            porDia.Result.Select(d => new DiaDelTablero(d.Dia.ToString("yyyy-MM-dd"), d.Entradas, d.Salidas)).ToList(),
            masMovidos.Result,
            bajoMinimo.Result,
            ultimos.Result.Select(f => new MovimientoResumen(f.Id, f.Fecha, f.ProductoId, f.Codigo, f.Producto, f.Tipo,
                f.Cantidad, f.StockResultante, f.Usuario, f.Observacion)).ToList()));
    }
}
