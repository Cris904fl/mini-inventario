using Inventario.Api.Contratos;
using Inventario.Api.Datos;
using Inventario.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IUsuarioRepositorio usuarios, GeneradorTokens tokens, ILogger<AuthController> log) : ControllerBase
{
    /// <summary>Inicia sesion y devuelve un JWT.</summary>
    /// <remarks>Si <c>debeCambiarClave</c> es true, el token solo sirve para cambiar la clave.</remarks>
    /// <response code="200">Credenciales correctas.</response>
    /// <response code="401">Usuario o clave incorrectos.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(RespuestaLogin), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RespuestaLogin>> Login([FromBody] SolicitudLogin solicitud, CancellationToken ct)
    {
        var usuario = await usuarios.BuscarPorNombreAsync(solicitud.Usuario, ct);

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

        return Ok(Respuesta(usuario));
    }

    /// <summary>Datos del usuario del token, leidos de la base (sirve para validar la sesion al abrir la aplicacion).</summary>
    [HttpGet("sesion")]
    [Authorize(Policy = Politicas.Autenticado)]
    [ProducesResponseType(typeof(RespuestaSesion), StatusCodes.Status200OK)]
    public async Task<ActionResult<RespuestaSesion>> Sesion(CancellationToken ct)
    {
        var usuario = await usuarios.BuscarPorIdAsync(User.UsuarioId(), ct);
        if (usuario is null || !usuario.Activo)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sesion no valida",
                detail: "El usuario ya no existe o fue desactivado.");

        return Ok(new RespuestaSesion(usuario.Id, usuario.NombreUsuario, usuario.NombreCompleto, usuario.Rol, usuario.Activo, usuario.DebeCambiarClave));
    }

    /// <summary>Cambia la clave del usuario autenticado y devuelve un token nuevo sin la marca de clave temporal.</summary>
    /// <response code="200">Clave cambiada.</response>
    /// <response code="400">La clave actual no coincide o la nueva no cumple el minimo.</response>
    [HttpPost("cambiar-clave")]
    [Authorize(Policy = Politicas.Autenticado)]
    [ProducesResponseType(typeof(RespuestaLogin), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RespuestaLogin>> CambiarClave([FromBody] SolicitudCambioClave solicitud, CancellationToken ct)
    {
        var usuario = await usuarios.BuscarPorIdAsync(User.UsuarioId(), ct);
        if (usuario is null || !usuario.Activo)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sesion no valida",
                detail: "El usuario ya no existe o fue desactivado.");

        if (!BCrypt.Net.BCrypt.Verify(solicitud.ClaveActual, usuario.ClaveHash))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Clave actual incorrecta",
                detail: "La clave actual no coincide.");

        if (solicitud.ClaveActual == solicitud.ClaveNueva)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Clave repetida",
                detail: "La clave nueva debe ser distinta de la actual.");

        await usuarios.CambiarClaveAsync(usuario.Id, BCrypt.Net.BCrypt.HashPassword(solicitud.ClaveNueva), ct);
        log.LogInformation("El usuario {Usuario} cambio su clave", usuario.NombreUsuario);

        // Token nuevo: el anterior podia llevar la marca de clave pendiente.
        return Ok(Respuesta(usuario with { DebeCambiarClave = false }));
    }

    private RespuestaLogin Respuesta(Usuario usuario)
    {
        var (token, expira) = tokens.Generar(usuario);
        return new RespuestaLogin(token, expira, usuario.NombreUsuario, usuario.NombreCompleto, usuario.Rol, usuario.DebeCambiarClave);
    }

    // Hash bcrypt de una cadena aleatoria; solo sirve para igualar tiempos.
    private const string HashSenuelo = "$2a$10$7EqJtq98hPqEX7fNZaFWoOhi5XIG9Hq4aHPt1J8n8w1oY7bYr0vXe";
}
