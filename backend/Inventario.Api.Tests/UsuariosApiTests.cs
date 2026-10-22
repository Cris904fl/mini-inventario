using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Inventario.Api.Tests;

/// <summary>
/// Roles, clave temporal y administracion de usuarios, sin base de datos.
/// </summary>
public class UsuariosApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _api;

    public UsuariosApiTests(ApiFactory api) => _api = api;

    [Fact]
    public async Task El_login_informa_el_rol_y_si_la_clave_es_temporal()
    {
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { usuario = "temporal", clave = "temporal123" });

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("OPERADOR", cuerpo.GetProperty("rol").GetString());
        Assert.True(cuerpo.GetProperty("debeCambiarClave").GetBoolean());
    }

    [Fact]
    public async Task Un_operador_no_puede_administrar_usuarios()
    {
        var cliente = await _api.ClienteAutenticadoAsync("operador", "Operador123");

        var respuesta = await cliente.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Un_operador_si_puede_registrar_movimientos()
    {
        var cliente = await _api.ClienteAutenticadoAsync("operador", "Operador123");

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos/entradas", new { productoId = 1, cantidad = 1 });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task Con_clave_temporal_solo_se_puede_ver_la_sesion_y_cambiar_la_clave()
    {
        var cliente = await _api.ClienteAutenticadoAsync("temporal", "temporal123");

        var inventario = await cliente.GetAsync("/api/productos");
        var sesion = await cliente.GetAsync("/api/auth/sesion");

        Assert.Equal(HttpStatusCode.Forbidden, inventario.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);
    }

    [Fact]
    public async Task Cambiar_la_clave_devuelve_un_token_que_ya_permite_entrar()
    {
        var cliente = await _api.ClienteAutenticadoAsync("cambia", "cambia123");

        var cambio = await cliente.PostAsJsonAsync("/api/auth/cambiar-clave", new { claveActual = "cambia123", claveNueva = "definitiva456" });

        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
        var cuerpo = await cambio.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(cuerpo.GetProperty("debeCambiarClave").GetBoolean());

        cliente.DefaultRequestHeaders.Authorization = new("Bearer", cuerpo.GetProperty("token").GetString());
        var inventario = await cliente.GetAsync("/api/productos");
        Assert.Equal(HttpStatusCode.OK, inventario.StatusCode);
        Assert.Contains("clave:4", _api.Usuarios.Acciones);
    }

    [Fact]
    public async Task Cambiar_la_clave_con_la_actual_incorrecta_responde_400_y_no_cambia_nada()
    {
        var cliente = await _api.ClienteAutenticadoAsync("operador", "Operador123");

        var cambio = await cliente.PostAsJsonAsync("/api/auth/cambiar-clave", new { claveActual = "mala", claveNueva = "definitiva456" });

        Assert.Equal(HttpStatusCode.BadRequest, cambio.StatusCode);
        Assert.DoesNotContain("clave:2", _api.Usuarios.Acciones);
    }

    [Fact]
    public async Task El_admin_crea_un_usuario_con_clave_temporal_y_un_nombre_repetido_da_409()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var creado = await cliente.PostAsJsonAsync("/api/usuarios",
            new { nombreUsuario = "nuevo", nombreCompleto = "Persona Nueva", rol = "OPERADOR", claveTemporal = "temporal123" });
        var repetido = await cliente.PostAsJsonAsync("/api/usuarios",
            new { nombreUsuario = "nuevo", nombreCompleto = "Otra", rol = "OPERADOR", claveTemporal = "temporal123" });

        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var cuerpo = await creado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cuerpo.GetProperty("debeCambiarClave").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        // La clave nunca viaja a la base en claro.
        var guardado = _api.Usuarios.Usuarios.Single(u => u.NombreUsuario == "nuevo");
        Assert.NotEqual("temporal123", guardado.ClaveHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("temporal123", guardado.ClaveHash));
    }

    [Theory]
    [InlineData("ab", "OPERADOR", "temporal123")]      // usuario muy corto
    [InlineData("usuario", "JEFE", "temporal123")]     // rol invalido
    [InlineData("usuario", "OPERADOR", "corta")]       // clave muy corta
    public async Task Crear_usuario_con_datos_invalidos_responde_400(string nombreUsuario, string rol, string claveTemporal)
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios",
            new { nombreUsuario, nombreCompleto = "Persona", rol, claveTemporal });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task El_admin_no_puede_desactivarse_a_si_mismo()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.PatchAsJsonAsync("/api/usuarios/1/estado", new { activo = false });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("IN005", cuerpo.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task El_admin_restablece_la_clave_de_otro_usuario_y_queda_como_temporal()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios/5/restablecer-clave", new { claveTemporal = "temporal999" });

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        var usuario = _api.Usuarios.Usuarios.Single(u => u.Id == 5);
        Assert.True(usuario.DebeCambiarClave);
        Assert.True(BCrypt.Net.BCrypt.Verify("temporal999", usuario.ClaveHash));
    }
}
