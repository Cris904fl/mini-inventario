using System.ComponentModel.DataAnnotations;

namespace Inventario.Api.Contratos;

// Lo que entra y sale por HTTP. Las validaciones de forma (rangos, longitudes)
// se hacen aqui con DataAnnotations; las de negocio (stock, existencia) las
// hace la base de datos en los procedimientos.

public record SolicitudLogin(
    [Required, StringLength(50)] string Usuario,
    [Required, StringLength(100)] string Clave);

public record RespuestaLogin(string Token, DateTimeOffset ExpiraEn, string Usuario, string NombreCompleto, string Rol, bool DebeCambiarClave);

public record RespuestaSesion(int Id, string Usuario, string NombreCompleto, string Rol, bool Activo, bool DebeCambiarClave);

public record SolicitudCambioClave(
    [Required] string ClaveActual,
    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "La clave nueva debe tener entre 8 y 100 caracteres.")] string ClaveNueva);

public record SolicitudUsuario(
    [Required, StringLength(50, MinimumLength = 3), RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "El usuario solo admite letras, numeros, punto, guion y guion bajo.")] string NombreUsuario,
    [Required, StringLength(120, MinimumLength = 3)] string NombreCompleto,
    [Required, RegularExpression("^(ADMIN|OPERADOR)$", ErrorMessage = "El rol debe ser ADMIN u OPERADOR.")] string Rol,
    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "La clave temporal debe tener entre 8 y 100 caracteres.")] string ClaveTemporal);

public record SolicitudEstadoUsuario(bool Activo);

public record SolicitudRestablecerClave(
    [Required, StringLength(100, MinimumLength = 8, ErrorMessage = "La clave temporal debe tener entre 8 y 100 caracteres.")] string ClaveTemporal);

public record SolicitudMovimiento(
    [Range(1, int.MaxValue, ErrorMessage = "Selecciona un producto.")] int ProductoId,
    [Range(1, 1_000_000, ErrorMessage = "La cantidad debe estar entre 1 y 1.000.000.")] int Cantidad,
    [StringLength(300, ErrorMessage = "La observacion no puede superar 300 caracteres.")] string? Observacion);

public record RespuestaMovimiento(long MovimientoId, int ProductoId, string Tipo, int Cantidad, int StockResultante, string Usuario);

public record SolicitudProducto(
    [Required, StringLength(20, MinimumLength = 2)] string Codigo,
    [Required, StringLength(120, MinimumLength = 2)] string Nombre,
    [StringLength(500)] string? Descripcion,
    [StringLength(20)] string? Unidad,
    [Range(0, 1_000_000)] int StockMinimo,
    [Range(0, 1_000_000)] int CantidadInicial);

public record PaginaDeMovimientos(
    IReadOnlyList<MovimientoResumen> Elementos,
    long Total,
    int Pagina,
    int Tamano,
    int TotalPaginas);

public record MovimientoResumen(
    long Id,
    DateTimeOffset Fecha,
    int ProductoId,
    string Codigo,
    string Producto,
    string Tipo,
    int Cantidad,
    int StockResultante,
    string Usuario,
    string? Observacion);
