using Microsoft.EntityFrameworkCore;
using AssambleaApi.Data;
using AssambleaApi.Services.Interfaces;
using AssambleaApi.Services;
using AssambleaApi.Hubs;
using AssambleaApi.Background;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAttendeeService, AttendeeService>();
builder.Services.AddScoped<IMeetingService, MeetingService>();
builder.Services.AddHostedService<InterventionMonitorService>();

builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers()
    .AddJsonOptions(options => { });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// 1. Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

// 2. Use CORS policy (before UseAuthorization and MapControllers)
app.UseCors("AllowFrontend");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<MeetingHub>("/meetingHub").RequireCors("AllowFrontend");

await app.RunAsync();
