namespace Inventario.Api.Seguridad;

/// <summary>Valores de la seccion <c>Jwt</c> de la configuracion.</summary>
public class OpcionesJwt
{
    public const string Seccion = "Jwt";

    /// <summary>Clave simetrica para firmar (HMAC-SHA256). Minimo 32 caracteres.</summary>
    public string Clave { get; set; } = string.Empty;
    public string Emisor { get; set; } = "mini-inventario";
    public string Audiencia { get; set; } = "mini-inventario-web";
    public int MinutosDeVigencia { get; set; } = 480;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Clave) || Clave.Length < 32)
            throw new InvalidOperationException(
                "Jwt:Clave debe tener al menos 32 caracteres. Configurala en appsettings.Development.json, " +
                "user-secrets o la variable de entorno Jwt__Clave.");
        if (MinutosDeVigencia <= 0)
            throw new InvalidOperationException("Jwt:MinutosDeVigencia debe ser mayor que cero.");
    }
}
