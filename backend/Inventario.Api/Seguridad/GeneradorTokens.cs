using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Inventario.Api.Datos;
using Microsoft.IdentityModel.Tokens;

namespace Inventario.Api.Seguridad;

/// <summary>Emite los JWT con los que el frontend se identifica ante la API.</summary>
public class GeneradorTokens(OpcionesJwt opciones, TimeProvider reloj)
{
    /// <summary>Claim que marca un token emitido con clave temporal: solo sirve para cambiarla.</summary>
    public const string ClaimClavePendiente = "clave_pendiente";

    public GeneradorTokens(OpcionesJwt opciones) : this(opciones, TimeProvider.System) { }

    public (string Token, DateTimeOffset ExpiraEn) Generar(Usuario usuario)
    {
        var ahora = reloj.GetUtcNow();
        var expira = ahora.AddMinutes(opciones.MinutosDeVigencia);

        // Las claims identifican al usuario en cada peticion: el nombre firma los
        // movimientos, el rol decide que puede hacer y clave_pendiente lo limita
        // a cambiar la clave mientras tenga una temporal.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, usuario.NombreUsuario),
            new(JwtRegisteredClaimNames.Name, usuario.NombreCompleto),
            new(ClaimTypes.Role, usuario.Rol),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (usuario.DebeCambiarClave)
            claims.Add(new Claim(ClaimClavePendiente, "true"));

        var credenciales = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opciones.Clave)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: opciones.Emisor,
            audience: opciones.Audiencia,
            claims: claims,
            notBefore: ahora.UtcDateTime,
            expires: expira.UtcDateTime,
            signingCredentials: credenciales);

        return (new JwtSecurityTokenHandler().WriteToken(token), expira);
    }
}
