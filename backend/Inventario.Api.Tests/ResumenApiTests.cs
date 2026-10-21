using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Inventario.Api.Tests;

public class ResumenApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _api;

    public ResumenApiTests(ApiFactory api) => _api = api;

    [Fact]
    public async Task Sin_token_responde_401()
    {
        var respuesta = await _api.CreateClient().GetAsync("/api/resumen");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Devuelve_totales_serie_por_dia_mas_movidos_bajo_minimo_y_ultimos()
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/resumen?dias=7");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, cuerpo.GetProperty("dias").GetInt32());
        Assert.Equal("America/Bogota", cuerpo.GetProperty("zona").GetString());
        Assert.Equal(2, cuerpo.GetProperty("totales").GetProperty("productos").GetInt64());
        Assert.Equal(7, cuerpo.GetProperty("porDia").GetArrayLength());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", cuerpo.GetProperty("porDia")[0].GetProperty("dia").GetString());
        Assert.Equal("EST-120", cuerpo.GetProperty("masMovidos")[0].GetProperty("codigo").GetString());
        Assert.True(cuerpo.TryGetProperty("bajoMinimo", out _));
        Assert.True(cuerpo.TryGetProperty("ultimos", out _));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, 90)]
    public async Task El_periodo_se_acota_entre_1_y_90_dias(int pedido, int esperado)
    {
        var cliente = await _api.ClienteAutenticadoAsync();

        var cuerpo = await cliente.GetFromJsonAsync<JsonElement>($"/api/resumen?dias={pedido}");

        Assert.Equal(esperado, cuerpo.GetProperty("dias").GetInt32());
    }
}
