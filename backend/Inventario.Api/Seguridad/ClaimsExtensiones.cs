using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Inventario.Api.Seguridad;

public static class ClaimsExtensiones
{
    /// <summary>Nombre de usuario que viaja en el token (claim unique_name).</summary>
    public static string NombreUsuario(this ClaimsPrincipal usuario)
        => usuario.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
           ?? usuario.FindFirstValue(ClaimTypes.Name)
           ?? throw new InvalidOperationException("El token no trae el nombre de usuario.");

    /// <summary>Id del usuario (claim sub).</summary>
    public static int UsuarioId(this ClaimsPrincipal usuario)
    {
        var sub = usuario.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? usuario.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(sub, out var id)
            ? id
            : throw new InvalidOperationException("El token no trae el id de usuario.");
    }
}
