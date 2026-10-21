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
}
