using Inventario.Api.Contratos;
using Inventario.Api.Datos;
using Inventario.Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Inventario.Api.Controllers;

/// <summary>Administracion de usuarios. Solo para el rol ADMIN.</summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class UsuariosController(IUsuarioRepositorio usuarios, ILogger<UsuariosController> log) : ControllerBase
{
    /// <summary>Lista de usuarios con su rol, estado y cuantos movimientos han registrado.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioEnLista>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UsuarioEnLista>>> Listar(CancellationToken ct)
        => Ok(await usuarios.ListarAsync(ct));

    /// <summary>Crea un usuario con una clave temporal que debera cambiar en su primer ingreso.</summary>
    /// <response code="201">Usuario creado.</response>
    /// <response code="409">Ya existe un usuario con ese nombre.</response>
    [HttpPost]
    [ProducesResponseType(typeof(UsuarioEnLista), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioEnLista>> Crear([FromBody] SolicitudUsuario solicitud, CancellationToken ct)
    {
        // El hash se calcula aqui: la base nunca recibe la clave en claro.
        var hash = BCrypt.Net.BCrypt.HashPassword(solicitud.ClaveTemporal);
        var id = await usuarios.CrearAsync(solicitud.NombreUsuario, solicitud.NombreCompleto, hash, solicitud.Rol, ct);
        log.LogInformation("{Admin} creo el usuario {Usuario} con rol {Rol}", User.NombreUsuario(), solicitud.NombreUsuario, solicitud.Rol);

        var creado = (await usuarios.ListarAsync(ct)).First(u => u.Id == id);
        return CreatedAtAction(nameof(Listar), null, creado);
    }

    /// <summary>Activa o desactiva un usuario. No permite desactivarse a si mismo ni al ultimo ADMIN activo.</summary>
    /// <response code="204">Estado cambiado.</response>
    /// <response code="409">La regla de usuarios no lo permite.</response>
    [HttpPatch("{id:int}/estado")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] SolicitudEstadoUsuario solicitud, CancellationToken ct)
    {
        await usuarios.CambiarEstadoAsync(id, solicitud.Activo, User.UsuarioId(), ct);
        log.LogInformation("{Admin} {Accion} al usuario {Id}", User.NombreUsuario(), solicitud.Activo ? "activo" : "desactivo", id);
        return NoContent();
    }

    /// <summary>Asigna una clave temporal a otro usuario; tendra que cambiarla al ingresar.</summary>
    /// <response code="204">Clave restablecida.</response>
    [HttpPost("{id:int}/restablecer-clave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestablecerClave(int id, [FromBody] SolicitudRestablecerClave solicitud, CancellationToken ct)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(solicitud.ClaveTemporal);
        await usuarios.RestablecerClaveAsync(id, hash, User.UsuarioId(), ct);
        log.LogInformation("{Admin} restablecio la clave del usuario {Id}", User.NombreUsuario(), id);
        return NoContent();
    }
}
