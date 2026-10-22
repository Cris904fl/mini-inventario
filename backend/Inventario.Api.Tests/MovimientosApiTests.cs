using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Inventario.Api.Tests;

/// <summary>
/// Pruebas de extremo a extremo HTTP (sin base de datos): autenticacion,
/// validacion de entrada y traduccion de los errores de negocio.
/// </summary>
public class MovimientosApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _api;

    public MovimientosApiTests(ApiFactory api) => _api = api;

    [Fact]
    public async Task Sin_token_responde_401()
    {
        var cliente = _api.CreateClient();

        var respuesta = await cliente.GetAsync("/api/productos");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Login_con_clave_incorrecta_responde_401_sin_token()
    {
        var cliente = _api.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new { usuario = "admin", clave = "otra" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Credenciales incorrectas", cuerpo.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Entrada_valida_responde_201_con_el_stock_resultante_y_el_usuario_del_token()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos/entradas",
            new { productoId = 1, cantidad = 5, observacion = "compra" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(15, cuerpo.GetProperty("stockResultante").GetInt32());
        Assert.Equal("admin", cuerpo.GetProperty("usuario").GetString());
        Assert.Contains(_api.Inventario.Registrados, r => r is ("ENTRADA", 1, 5, "admin"));
    }

    [Fact]
    public async Task Salida_sin_stock_responde_409_con_mensaje_claro()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos/salidas", new { productoId = 1, cantidad = 999 });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Stock insuficiente", cuerpo.GetProperty("title").GetString());
        Assert.Equal("IN001", cuerpo.GetProperty("codigo").GetString());
        Assert.Equal(10, cuerpo.GetProperty("stock").GetProperty("disponible").GetInt32());
        Assert.DoesNotContain(_api.Inventario.Registrados, r => r.Tipo == "SALIDA" && r.Cantidad == 999);
    }

    [Fact]
    public async Task Salida_de_producto_inexistente_responde_404()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos/salidas", new { productoId = 999, cantidad = 1 });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("IN002", cuerpo.GetProperty("codigo").GetString());
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 0)]
    [InlineData(1, -3)]
    public async Task Cantidad_o_producto_invalidos_responden_400_antes_de_llegar_a_la_base(int productoId, int cantidad)
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos/entradas", new { productoId, cantidad });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cuerpo.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Crear_producto_con_codigo_repetido_responde_409()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/productos",
            new { codigo = "EST-120", nombre = "Otra estiba", stockMinimo = 1, cantidadInicial = 0 });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task Historial_rechaza_un_tipo_que_no_exista()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/movimientos?tipo=DEVOLUCION");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}
