using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Inventario.Api.Datos;
using Microsoft.IdentityModel.Tokens;

namespace Inventario.Api.Seguridad;

/// <summary>Emite los JWT con los que el frontend se identifica ante la API.</summary>
public class GeneradorTokens(OpcionesJwt opciones, TimeProvider reloj)
{
    public GeneradorTokens(OpcionesJwt opciones) : this(opciones, TimeProvider.System) { }

    public (string Token, DateTimeOffset ExpiraEn) Generar(Usuario usuario)
    {
        var ahora = reloj.GetUtcNow();
        var expira = ahora.AddMinutes(opciones.MinutosDeVigencia);

        // Las claims identifican al usuario en cada peticion: el nombre se usa
        // para firmar los movimientos (campo usuario en la tabla movimientos).
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, usuario.NombreUsuario),
            new Claim(JwtRegisteredClaimNames.Name, usuario.NombreCompleto),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

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
