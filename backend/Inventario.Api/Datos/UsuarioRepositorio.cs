using Dapper;
using Npgsql;

namespace Inventario.Api.Datos;

public interface IUsuarioRepositorio
{
    Task<Usuario?> BuscarPorNombreAsync(string nombreUsuario, CancellationToken ct);
    Task<Usuario?> BuscarPorIdAsync(int id, CancellationToken ct);
    Task<IReadOnlyList<UsuarioEnLista>> ListarAsync(CancellationToken ct);
    Task<int> CrearAsync(string nombreUsuario, string nombreCompleto, string claveHash, string rol, CancellationToken ct);
    Task CambiarEstadoAsync(int usuarioId, bool activo, int solicitanteId, CancellationToken ct);
    Task CambiarClaveAsync(int usuarioId, string claveHash, CancellationToken ct);
    Task RestablecerClaveAsync(int usuarioId, string claveTemporalHash, int solicitanteId, CancellationToken ct);
}

/// <summary>
/// Acceso a <c>usuarios</c>. Las escrituras pasan por los procedimientos; la
/// base solo recibe hashes bcrypt, nunca claves en claro.
/// </summary>
public class UsuarioRepositorio(NpgsqlDataSource datos) : IUsuarioRepositorio
{
    private const string Columnas = "id, nombre_usuario, nombre_completo, clave_hash, rol, activo, debe_cambiar_clave";

    public async Task<Usuario?> BuscarPorNombreAsync(string nombreUsuario, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        return await conexion.QuerySingleOrDefaultAsync<Usuario>(new CommandDefinition(
            $"SELECT {Columnas} FROM usuarios WHERE nombre_usuario = @nombreUsuario",
            new { nombreUsuario = nombreUsuario.Trim().ToLowerInvariant() },
            cancellationToken: ct));
    }

    public async Task<Usuario?> BuscarPorIdAsync(int id, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        return await conexion.QuerySingleOrDefaultAsync<Usuario>(new CommandDefinition(
            $"SELECT {Columnas} FROM usuarios WHERE id = @id", new { id }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<UsuarioEnLista>> ListarAsync(CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        var filas = await conexion.QueryAsync<UsuarioEnLista>(new CommandDefinition(
            "SELECT * FROM fn_usuarios()", cancellationToken: ct));
        return filas.AsList();
    }

    public async Task<int> CrearAsync(string nombreUsuario, string nombreCompleto, string claveHash, string rol, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        // La clave inicial siempre es temporal: el usuario la cambia en su primer ingreso.
        return await conexion.ExecuteScalarAsync<int>(new CommandDefinition(
            "CALL sp_crear_usuario(@nombreUsuario, @nombreCompleto, @claveHash, @rol, TRUE, NULL)",
            new { nombreUsuario, nombreCompleto, claveHash, rol },
            cancellationToken: ct));
    }

    public async Task CambiarEstadoAsync(int usuarioId, bool activo, int solicitanteId, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        await conexion.ExecuteAsync(new CommandDefinition(
            "CALL sp_cambiar_estado_usuario(@usuarioId, @activo, @solicitanteId)",
            new { usuarioId, activo, solicitanteId },
            cancellationToken: ct));
    }

    public async Task CambiarClaveAsync(int usuarioId, string claveHash, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        await conexion.ExecuteAsync(new CommandDefinition(
            "CALL sp_cambiar_clave(@usuarioId, @claveHash)",
            new { usuarioId, claveHash },
            cancellationToken: ct));
    }

    public async Task RestablecerClaveAsync(int usuarioId, string claveTemporalHash, int solicitanteId, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        await conexion.ExecuteAsync(new CommandDefinition(
            "CALL sp_restablecer_clave(@usuarioId, @claveTemporalHash, @solicitanteId)",
            new { usuarioId, claveTemporalHash, solicitanteId },
            cancellationToken: ct));
    }
}
