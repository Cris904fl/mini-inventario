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
    /// <summary>Efecto con signo sobre el stock: +entrada, -salida, ajuste tal cual.</summary>
    public int Diferencia { get; init; }
    public int StockResultante { get; init; }
    public string Usuario { get; init; } = string.Empty;
    public string? Observacion { get; init; }
    /// <summary>Total de filas que cumplen el filtro (para paginar).</summary>
    public long Total { get; init; }
}

/// <summary>Lo que devuelve <c>sp_registrar_ajuste</c>.</summary>
public record ResultadoAjuste(long MovimientoId, int Diferencia, int StockResultante);

/// <summary>Fila unica de <c>fn_resumen_inventario</c>.</summary>
public record ResumenInventario
{
    public long Productos { get; init; }
    public long Unidades { get; init; }
    public long EnBajo { get; init; }
    public long Agotados { get; init; }
    public long EntradasPeriodo { get; init; }
    public long SalidasPeriodo { get; init; }
    public long MovimientosPeriodo { get; init; }
}

/// <summary>Fila de <c>fn_movimientos_por_dia</c>.</summary>
public record MovimientosDelDia
{
    public DateTime Dia { get; init; }
    public long Entradas { get; init; }
    public long Salidas { get; init; }
}

/// <summary>Fila de <c>fn_productos_mas_movidos</c>.</summary>
public record ProductoMasMovido
{
    public int ProductoId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Unidad { get; init; } = "UND";
    public long Salidas { get; init; }
    public long Entradas { get; init; }
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
    /// <summary>ADMIN u OPERADOR.</summary>
    public string Rol { get; init; } = Roles.Operador;
    public bool Activo { get; init; }
    /// <summary>True cuando la clave es temporal y hay que cambiarla antes de seguir.</summary>
    public bool DebeCambiarClave { get; init; }
}

/// <summary>Fila de <c>fn_usuarios</c> (sin el hash).</summary>
public record UsuarioEnLista
{
    public int Id { get; init; }
    public string NombreUsuario { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string Rol { get; init; } = Roles.Operador;
    public bool Activo { get; init; }
    public bool DebeCambiarClave { get; init; }
    public DateTime CreadoEn { get; init; }
    public long Movimientos { get; init; }
}

public static class Roles
{
    public const string Admin = "ADMIN";
    public const string Operador = "OPERADOR";
    public static readonly string[] Todos = [Admin, Operador];
}
