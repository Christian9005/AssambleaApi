using Microsoft.EntityFrameworkCore;
using AssambleaApi.Data;
using AssambleaApi.Services.Interfaces;
using AssambleaApi.Services;
using AssambleaApi.Hubs;
using AssambleaApi.Background;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

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

    // Para SignalR
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
builder.Services.AddSwaggerGen();

builder.Services.AddControllers()
    .AddJsonOptions(options => { });

// CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ??
                             new[] { "http://localhost:3000", "http://localhost", "http://127.0.0.1" };

        if (allowedOrigins.Length == 1 && allowedOrigins[0] == "*")
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  // Expose headers needed by browser clients to handle downloads
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

// Aplicar migraciones automáticamente al iniciar (con reintentos mientras la BD se inicia)
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

// Configure the HTTP request pipeline.
if (app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<MeetingHub>("/meetingHub").RequireCors("AllowFrontend");

await app.RunAsync();
