using Microsoft.EntityFrameworkCore;
using AssambleaApi.Data;
using AssambleaApi.Services.Interfaces;
using AssambleaApi.Services;
using AssambleaApi.Hubs;
using AssambleaApi.Background;
using AssambleaApi.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configurar puerto para Railway
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(int.Parse(port));
});

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey no configurada");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/meetingHub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddScoped<IAttendeeService, AttendeeService>();
builder.Services.AddScoped<IMeetingService, MeetingService>();
builder.Services.AddHostedService<InterventionMonitorService>();

builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

// Configurar Swagger con soporte JWT
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Asamblea API - Sistema de Voto Electrónico", 
        Version = "v1",
        Description = "API para gestión de asambleas con autenticación JWT y código de meeting"
    });
    
    // Definir esquema de seguridad JWT
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando esquema Bearer. Ejemplo: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Definir esquema de headers para código de meeting
    c.AddSecurityDefinition("MeetingCode", new OpenApiSecurityScheme
    {
        Description = "Headers requeridos: X-Meeting-Code y X-Meeting-Id",
        Name = "X-Meeting-Code",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
        if (options.JsonSerializerOptions.TypeInfoResolver == null)
        {
            options.JsonSerializerOptions.TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
        }
    });

// CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ??
                             new[] 
                             { 
                                 "http://localhost:3000",
                                 "http://localhost",
                                 "https://asambleadashboard.netlify.app"
                             };

        if (allowedOrigins.Length == 1 && allowedOrigins[0] == "*")
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .WithExposedHeaders("Content-Disposition", "Content-Length", "Content-Type");
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .SetIsOriginAllowedToAllowWildcardSubdomains()
                  .WithExposedHeaders("Content-Disposition", "Content-Length", "Content-Type");
        }
    });
});

var app = builder.Build();

// Aplicar migraciones automáticamente
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var maxRetries = 10;
    var delay = TimeSpan.FromSeconds(5);

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            logger.LogInformation("Intentando aplicar migraciones: intento {Attempt}/{Max}", attempt, maxRetries);
            var db = services.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
            logger.LogInformation("Migraciones aplicadas correctamente.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error al aplicar migraciones en el intento {Attempt}. Esperando {Delay}s antes de reintentar.", attempt, delay.TotalSeconds);
            if (attempt == maxRetries)
            {
                logger.LogError(ex, "No se pudo aplicar migraciones después de {Max} intentos.", maxRetries);
            }
            await Task.Delay(delay);
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Asamblea API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowFrontend");

app.UseAuthentication();

// Middleware personalizado para validar código de meeting
app.UseMiddleware<MeetingCodeAuthMiddleware>();

app.UseAuthorization();

app.MapControllers();
app.MapHub<MeetingHub>("/meetingHub").RequireCors("AllowFrontend");

app.Logger.LogInformation("🚀 Asamblea API iniciada en el puerto {Port}", port);
app.Logger.LogInformation("📖 Swagger UI disponible en: /swagger");
app.Logger.LogInformation("🔐 Autenticación: JWT Bearer Token o Meeting Code");

await app.RunAsync();
