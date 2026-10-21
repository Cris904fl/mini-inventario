using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Inventario.Api.Middleware;

/// <summary>
/// Convierte las excepciones en respuestas ProblemDetails (RFC 9457) con un
/// mensaje claro y sin filtrar detalles internos. Los errores de negocio que
/// levantan los procedimientos de PostgreSQL llegan como PostgresException con
/// un SQLSTATE propio y se traducen por codigo, no por el texto del mensaje.
/// </summary>
public class ManejoErroresMiddleware(RequestDelegate siguiente, ILogger<ManejoErroresMiddleware> log)
{
    // Codigos definidos en database/inventario.sql.
    public const string StockInsuficiente = "IN001";
    public const string ProductoNoEncontrado = "IN002";
    public const string CantidadInvalida = "IN003";

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await siguiente(contexto);
        }
        catch (PostgresException ex)
        {
            var problema = Traducir(ex);
            if (problema.Status == StatusCodes.Status500InternalServerError)
                log.LogError(ex, "Error de PostgreSQL no previsto (SQLSTATE {SqlState})", ex.SqlState);
            else
                log.LogInformation("Regla de negocio rechazada ({SqlState}): {Mensaje}", ex.SqlState, ex.MessageText);

            await Responder(contexto, problema);
        }
        catch (OperationCanceledException) when (contexto.RequestAborted.IsCancellationRequested)
        {
            // El cliente cerro la conexion; no hay a quien responder.
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error no controlado en {Metodo} {Ruta}", contexto.Request.Method, contexto.Request.Path);
            await Responder(contexto, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno",
                Detail = "Ocurrio un error inesperado. Intenta de nuevo o contacta al administrador."
            });
        }
    }

    /// <summary>Mapa SQLSTATE a respuesta HTTP. Es puro para poder probarlo sin servidor.</summary>
    public static ProblemDetails Traducir(PostgresException ex)
    {
        var problema = ex.SqlState switch
        {
            StockInsuficiente => Problema(StatusCodes.Status409Conflict, "Stock insuficiente", ex.MessageText),
            ProductoNoEncontrado => Problema(StatusCodes.Status404NotFound, "Producto no encontrado", ex.MessageText),
            CantidadInvalida => Problema(StatusCodes.Status400BadRequest, "Datos invalidos", ex.MessageText),
            PostgresErrorCodes.UniqueViolation => Problema(StatusCodes.Status409Conflict, "Registro duplicado",
                "Ya existe un registro con ese valor unico (por ejemplo, el codigo del producto)."),
            PostgresErrorCodes.CheckViolation => Problema(StatusCodes.Status400BadRequest, "Datos invalidos",
                "Los datos no cumplen las restricciones de la base de datos."),
            PostgresErrorCodes.ForeignKeyViolation => Problema(StatusCodes.Status400BadRequest, "Referencia invalida",
                "El registro hace referencia a otro que no existe."),
            _ => Problema(StatusCodes.Status500InternalServerError, "Error de base de datos",
                "No fue posible completar la operacion en la base de datos.")
        };

        problema.Extensions["codigo"] = ex.SqlState;

        // sp_registrar_salida adjunta en DETAIL un JSON con disponible/solicitado.
        // Npgsql solo entrega el DETAIL si IncludeErrorDetail esta activo (Program.cs);
        // si no viene o no es JSON, la respuesta sale igual sin ese extra.
        if (ex.SqlState == StockInsuficiente && ex.Detail is { } detalle && detalle.TrimStart().StartsWith('{'))
        {
            try { problema.Extensions["stock"] = JsonSerializer.Deserialize<JsonElement>(detalle); }
            catch (JsonException) { /* el detalle no era JSON: se omite */ }
        }

        return problema;
    }

    private static ProblemDetails Problema(int estado, string titulo, string detalle) => new()
    {
        Status = estado,
        Title = titulo,
        Detail = detalle
    };

    private static async Task Responder(HttpContext contexto, ProblemDetails problema)
    {
        if (contexto.Response.HasStarted) return;

        contexto.Response.Clear();
        contexto.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;
        problema.Instance ??= contexto.Request.Path;
        await contexto.Response.WriteAsJsonAsync(problema, options: null,
            contentType: "application/problem+json", cancellationToken: contexto.RequestAborted);
    }
}
