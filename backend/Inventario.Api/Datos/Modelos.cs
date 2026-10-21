namespace Inventario.Api.Datos;

// Registros que devuelven las funciones de PostgreSQL. Se declaran con
// propiedades (no constructor posicional) porque Dapper empareja columnas
// snake_case con propiedades PascalCase solo por propiedad, con la opcion
// MatchNamesWithUnderscores que se activa en Program.cs.
// Las fechas vienen de columnas timestamptz y Npgsql las entrega en UTC.

/// <summary>Fila de <c>fn_inventario_actual</c>.</summary>
public record ProductoEnInventario
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public string Unidad { get; init; } = "UND";
    public int Cantidad { get; init; }
    public int StockMinimo { get; init; }
    /// <summary>DISPONIBLE, BAJO o AGOTADO; lo calcula la funcion.</summary>
    public string Estado { get; init; } = string.Empty;
    public DateTime? UltimoMovimiento { get; init; }
}

/// <summary>Fila de <c>fn_historial_movimientos</c>.</summary>
public record MovimientoEnHistorial
{
    public long Id { get; init; }
    public DateTime Fecha { get; init; }
    public int ProductoId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Producto { get; init; } = string.Empty;
    public string Tipo { get; init; } = string.Empty;
    public int Cantidad { get; init; }
    public int StockResultante { get; init; }
    public string Usuario { get; init; } = string.Empty;
    public string? Observacion { get; init; }
    /// <summary>Total de filas que cumplen el filtro (para paginar).</summary>
    public long Total { get; init; }
}

/// <summary>Lo que devuelven <c>sp_registrar_entrada</c> y <c>sp_registrar_salida</c>.</summary>
public record ResultadoMovimiento(long MovimientoId, int StockResultante);

/// <summary>Fila de la tabla <c>usuarios</c>.</summary>
public record Usuario
{
    public int Id { get; init; }
    public string NombreUsuario { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string ClaveHash { get; init; } = string.Empty;
    public bool Activo { get; init; }
}
