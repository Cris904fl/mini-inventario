using Inventario.Api.Contratos;
using Inventario.Api.Datos;
using Inventario.Api.Seguridad;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IUsuarioRepositorio usuarios, GeneradorTokens tokens, ILogger<AuthController> log) : ControllerBase
{
    /// <summary>Inicia sesion y devuelve un JWT.</summary>
    /// <response code="200">Credenciales correctas.</response>
    /// <response code="401">Usuario o clave incorrectos.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(RespuestaLogin), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RespuestaLogin>> Login([FromBody] SolicitudLogin solicitud, CancellationToken ct)
    {
        var usuario = await usuarios.BuscarPorNombreAsync(solicitud.Usuario.Trim(), ct);

        // Se verifica contra un hash aunque el usuario no exista, para que el
        // tiempo de respuesta no revele si el nombre esta registrado.
        var hash = usuario?.ClaveHash ?? HashSenuelo;
        var claveCorrecta = BCrypt.Net.BCrypt.Verify(solicitud.Clave, hash);

        if (usuario is null || !usuario.Activo || !claveCorrecta)
        {
            log.LogWarning("Intento de login fallido para {Usuario}", solicitud.Usuario);
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Credenciales incorrectas",
                detail: "El usuario o la clave no son validos.");
        }

        var (token, expira) = tokens.Generar(usuario);
        return Ok(new RespuestaLogin(token, expira, usuario.NombreUsuario, usuario.NombreCompleto));
    }

    // Hash bcrypt de una cadena aleatoria; solo sirve para igualar tiempos.
    private const string HashSenuelo = "$2a$10$7EqJtq98hPqEX7fNZaFWoOhi5XIG9Hq4aHPt1J8n8w1oY7bYr0vXe";
}
