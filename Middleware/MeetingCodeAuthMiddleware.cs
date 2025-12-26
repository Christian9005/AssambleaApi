using AssambleaApi.Data;
using Microsoft.EntityFrameworkCore;

namespace AssambleaApi.Middleware;

/// <summary>
/// Middleware que valida el acceso mediante código de meeting para endpoints de Attendees.
/// Permite acceso si:
/// - El usuario está autenticado con JWT (Admin/User)
/// - El request tiene headers X-Meeting-Code y X-Meeting-Id válidos
/// </summary>
public class MeetingCodeAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MeetingCodeAuthMiddleware> _logger;

    public MeetingCodeAuthMiddleware(RequestDelegate next, ILogger<MeetingCodeAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        // Si ya está autenticado con JWT, continuar
        if (context.User.Identity?.IsAuthenticated == true)
        {
            _logger.LogDebug("Usuario autenticado con JWT: {User}", context.User.Identity.Name);
            await _next(context);
            return;
        }

        // Si el endpoint es de Attendees, validar código de meeting
        var path = context.Request.Path.Value?.ToLower() ?? "";
        
        if (path.StartsWith("/api/attendees"))
        {
            var meetingCode = context.Request.Headers["X-Meeting-Code"].FirstOrDefault();
            var meetingIdStr = context.Request.Headers["X-Meeting-Id"].FirstOrDefault();

            _logger.LogDebug("Validando acceso a Attendees. MeetingCode: {Code}, MeetingId: {Id}", 
                meetingCode, meetingIdStr);

            if (!string.IsNullOrEmpty(meetingCode) && int.TryParse(meetingIdStr, out var meetingId))
            {
                var meeting = await dbContext.Meetings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == meetingId && m.Code == meetingCode);
                
                if (meeting != null && meeting.Status != Models.MeetingStatus.Closed)
                {
                    _logger.LogInformation("Acceso permitido con código de meeting {MeetingId}", meetingId);
                    await _next(context);
                    return;
                }
                else
                {
                    _logger.LogWarning("Código de meeting inválido o meeting cerrado. MeetingId: {Id}, Code: {Code}", 
                        meetingId, meetingCode);
                }
            }
            else
            {
                _logger.LogWarning("Headers X-Meeting-Code o X-Meeting-Id faltantes o inválidos");
            }

            // Código inválido o faltante
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new 
            { 
                message = "No autorizado. Requiere autenticación JWT o código de meeting válido.",
                error = "UNAUTHORIZED",
                requiredHeaders = new[] { "X-Meeting-Code", "X-Meeting-Id" }
            });
            return;
        }

        // Para otros endpoints, continuar (serán validados por [Authorize])
        await _next(context);
    }
}