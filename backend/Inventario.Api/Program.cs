using System.Text;
using Dapper;
using Inventario.Api.Datos;
using Inventario.Api.Middleware;
using Inventario.Api.Seguridad;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// --- Base de datos -----------------------------------------------------------
// Un unico NpgsqlDataSource para toda la aplicacion: administra el pool de
// conexiones y cada repositorio abre/cierra la suya por operacion.
var cadena = builder.Configuration.GetConnectionString("Inventario")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Inventario en la configuracion.");
// IncludeErrorDetail: los procedimientos adjuntan en DETAIL datos utiles para
// el cliente (stock disponible vs. solicitado); sin esto Npgsql lo oculta.
builder.Services.AddNpgsqlDataSource(cadena, opciones => opciones.ConnectionStringBuilder.IncludeErrorDetail = true);

// Las columnas de PostgreSQL van en snake_case (stock_minimo) y las propiedades
// de C# en PascalCase (StockMinimo); Dapper las empareja con esta opcion.
DefaultTypeMap.MatchNamesWithUnderscores = true;

builder.Services.AddScoped<IInventarioRepositorio, InventarioRepositorio>();
builder.Services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();

// --- Autenticacion JWT -------------------------------------------------------
var jwt = builder.Configuration.GetSection(OpcionesJwt.Seccion).Get<OpcionesJwt>()
    ?? throw new InvalidOperationException("Falta la seccion Jwt en la configuracion.");
jwt.Validar();
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<GeneradorTokens>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opciones =>
    {
        opciones.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Emisor,
            ValidateAudience = true,
            ValidAudience = jwt.Audiencia,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave))
        };
    });
// Politicas:
//  * la de por defecto ([Authorize] a secas, y tambien [Authorize(Roles=...)])
//    exige estar autenticado Y no tener una clave temporal pendiente;
//  * "Autenticado" solo exige el token: la usan ver la sesion y cambiar la
//    clave, que es lo unico permitido mientras la clave sea temporal.
builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireAssertion(ctx => !ctx.User.HasClaim(c => c.Type == GeneradorTokens.ClaimClavePendiente))
        .Build())
    .AddPolicy(Politicas.Autenticado, p => p.RequireAuthenticatedUser());

// --- API ---------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddProblemDetails();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opciones =>
{
    opciones.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Mini inventario API",
        Version = "v1",
        Description = "Entradas, salidas e inventario actual sobre procedimientos almacenados de PostgreSQL."
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "Inventario.Api.xml");
    if (File.Exists(xml)) opciones.IncludeXmlComments(xml);

    // Boton "Authorize" en Swagger para pegar el token del login.
    opciones.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Escribe solo el token; el prefijo Bearer lo agrega Swagger."
    });
    opciones.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var origenes = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(opciones =>
    opciones.AddDefaultPolicy(politica =>
        politica.WithOrigins(origenes).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// El middleware de errores va primero para envolver TODO lo que sigue.
app.UseMiddleware<ManejoErroresMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(opciones =>
{
    opciones.SwaggerEndpoint("/swagger/v1/swagger.json", "Mini inventario v1");
    opciones.DocumentTitle = "Mini inventario API";
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapGet("/salud", async (NpgsqlDataSource datos) =>
{
    await using var conexion = await datos.OpenConnectionAsync();
    return Results.Ok(new { estado = "ok", baseDeDatos = "conectada", hora = DateTimeOffset.UtcNow });
}).WithTags("Salud");

app.Run();

// Permite que los tests de integracion arranquen la aplicacion con WebApplicationFactory.
public partial class Program;
