using AssambleaApi.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using AssambleaApi.Hubs;
using Microsoft.EntityFrameworkCore;
using AssambleaApi.Data;

namespace AssambleaApi.Background;

public class InterventionMonitorService : BackgroundService
{
    private readonly ILogger<InterventionMonitorService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<MeetingHub> _hubContext;
    private const int IntervalSeconds = 20; // frecuencia de escaneo

    public InterventionMonitorService(ILogger<InterventionMonitorService> logger,
        IServiceProvider serviceProvider,
        IHubContext<MeetingHub> hubContext)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InterventionMonitorService iniciado");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanMeetingsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en InterventionMonitorService");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Ignorado: cancelación solicitada
            }
        }
    }

    private async Task ScanMeetingsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attendeeService = scope.ServiceProvider.GetRequiredService<IAttendeeService>();

        var meetingIds = await db.Meetings.Select(m => m.Id).ToListAsync(ct);
        foreach (var meetingId in meetingIds)
        {
            var (expiredSpeaker, next, changed) = await attendeeService.ProcessExpirationsAsync(meetingId);
            if (expiredSpeaker != null)
            {
                await _hubContext.Clients.Group(meetingId.ToString())
                    .SendAsync("InterventionEnded", new {
                        AttendeeId = expiredSpeaker.Id,
                        Name = expiredSpeaker.Name,
                        SeatNumber = expiredSpeaker.SeatNumber,
                        Reason = "TimeExpired"
                    }, ct);
            }
            if (next != null)
            {
                await _hubContext.Clients.Group(meetingId.ToString())
                    .SendAsync("NextInterventionRequested", new
                    {
                        AttendeeId = next.Id,
                        Name = next.Name,
                        SeatNumber = next.SeatNumber,
                        AcceptDeadline = next.InterventionAcceptDeadline
                    }, ct);
            }
            if (changed)
            {
                // broadcast meeting status after any change
                var meeting = await db.Meetings.Include(m => m.Attendees).FirstOrDefaultAsync(m => m.Id == meetingId, ct);
                if (meeting != null)
                {
                    await _hubContext.Clients.Group(meetingId.ToString())
                        .SendAsync("MeetingStatusUpdated", meeting, ct);
                }
            }
        }
    }
}
