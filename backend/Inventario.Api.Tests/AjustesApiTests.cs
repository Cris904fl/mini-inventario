using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Inventario.Api.Tests;

/// <summary>Ajuste de inventario por conteo fisico, sin base de datos.</summary>
public class AjustesApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _api;

    public AjustesApiTests(ApiFactory api) => _api = api;

    [Fact]
    public async Task El_admin_registra_un_ajuste_y_recibe_la_diferencia_calculada()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        // El producto 1 tiene 10; se contaron 7.
        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos/ajustes",
            new { productoId = 1, cantidadContada = 7, motivo = "Conteo fisico: dos unidades rotas" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(-3, cuerpo.GetProperty("diferencia").GetInt32());
        Assert.Equal(7, cuerpo.GetProperty("stockResultante").GetInt32());
        Assert.Equal("admin", cuerpo.GetProperty("usuario").GetString());
        Assert.Contains(_api.Inventario.Registrados, r => r is ("AJUSTE", 1, -3, "admin"));
    }

    [Fact]
    public async Task Un_operador_no_puede_ajustar_inventario()
    {
        var cliente = await _api.ClienteAutenticadoAsync("operador", "Operador123");

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos/ajustes",
            new { productoId = 1, cantidadContada = 7, motivo = "Conteo fisico" });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(-1, "Conteo fisico")]   // contado negativo
    [InlineData(5, "")]                  // sin motivo
    [InlineData(5, "ok")]                // motivo demasiado corto
    public async Task Datos_invalidos_responden_400_sin_llegar_a_la_base(int cantidadContada, string motivo)
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos/ajustes", new { productoId = 1, cantidadContada, motivo });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.DoesNotContain(_api.Inventario.Registrados, r => r.Tipo == "AJUSTE" && r.Cantidad == cantidadContada - 10);
    }

    [Fact]
    public async Task Un_conteo_igual_al_stock_responde_400_con_el_mensaje_del_procedimiento()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/movimientos/ajustes",
            new { productoId = 1, cantidadContada = 10, motivo = "Conteo fisico sin novedad" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("IN003", cuerpo.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task El_historial_acepta_el_filtro_AJUSTE()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/movimientos?tipo=AJUSTE");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
