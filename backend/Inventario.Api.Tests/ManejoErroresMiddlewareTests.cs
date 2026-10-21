using Inventario.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace Inventario.Api.Tests;

/// <summary>
/// La traduccion SQLSTATE -> HTTP es pura, asi que se prueba sin servidor ni base.
/// </summary>
public class ManejoErroresMiddlewareTests
{
    private static PostgresException Excepcion(string sqlState, string mensaje = "mensaje", string? detalle = null)
        => new(mensaje, "ERROR", "ERROR", sqlState, detail: detalle);

    [Theory]
    [InlineData("IN001", StatusCodes.Status409Conflict)]
    [InlineData("IN002", StatusCodes.Status404NotFound)]
    [InlineData("IN003", StatusCodes.Status400BadRequest)]
    [InlineData("23505", StatusCodes.Status409Conflict)]   // unique_violation
    [InlineData("23514", StatusCodes.Status400BadRequest)] // check_violation
    [InlineData("XX000", StatusCodes.Status500InternalServerError)]
    public void Traduce_cada_sqlstate_al_estado_http_esperado(string sqlState, int estadoEsperado)
    {
        var problema = ManejoErroresMiddleware.Traducir(Excepcion(sqlState));

        Assert.Equal(estadoEsperado, problema.Status);
        Assert.Equal(sqlState, problema.Extensions["codigo"]);
    }

    [Fact]
    public void Stock_insuficiente_conserva_el_mensaje_del_procedimiento_y_las_cantidades()
    {
        var ex = Excepcion("IN001", "Stock insuficiente: hay 5 y se intentan retirar 9", "{\"disponible\":5,\"solicitado\":9}");

        var problema = ManejoErroresMiddleware.Traducir(ex);

        Assert.Equal("Stock insuficiente", problema.Title);
        Assert.Equal(ex.MessageText, problema.Detail);
        var stock = Assert.IsType<System.Text.Json.JsonElement>(problema.Extensions["stock"]);
        Assert.Equal(5, stock.GetProperty("disponible").GetInt32());
        Assert.Equal(9, stock.GetProperty("solicitado").GetInt32());
    }

    [Fact]
    public void Un_error_desconocido_no_filtra_el_mensaje_interno()
    {
        var problema = ManejoErroresMiddleware.Traducir(Excepcion("XX000", "relation \"tabla_secreta\" does not exist"));

        Assert.DoesNotContain("tabla_secreta", problema.Detail);
    }
}
