using Dapper;
using Npgsql;

namespace Inventario.Api.Datos;

public interface IUsuarioRepositorio
{
    Task<Usuario?> BuscarPorNombreAsync(string nombreUsuario, CancellationToken ct);
}

public class UsuarioRepositorio(NpgsqlDataSource datos) : IUsuarioRepositorio
{
    public async Task<Usuario?> BuscarPorNombreAsync(string nombreUsuario, CancellationToken ct)
    {
        await using var conexion = await datos.OpenConnectionAsync(ct);
        return await conexion.QuerySingleOrDefaultAsync<Usuario>(new CommandDefinition(
            "SELECT id, nombre_usuario, nombre_completo, clave_hash, activo FROM usuarios WHERE nombre_usuario = @nombreUsuario",
            new { nombreUsuario },
            cancellationToken: ct));
    }
}
