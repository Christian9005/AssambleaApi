using AssambleaApi.Hubs;
using AssambleaApi.Models;
using AssambleaApi.Services;
using AssambleaApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace AssambleaApi.Controllers;

/// <summary>
/// Controller para gestionar meetings.
/// La mayoría de endpoints requieren autenticación JWT (Admin/User).
/// Solo endpoints de lectura son públicos.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingService _meetingService;
    private readonly IHubContext<MeetingHub> _hubContext;

    public MeetingsController(IMeetingService meetingService, IHubContext<MeetingHub> hubContext)
    {
        _meetingService = meetingService ?? throw new ArgumentNullException(nameof(meetingService));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }

    /// <summary>
    /// Crea un nuevo meeting (solo admin)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> CreateMeeting()
    {
        var meeting = await _meetingService.CreateMeetingAsync();
        return CreatedAtAction(nameof(GetMeetingById), new { id = meeting.Id }, meeting);
    }

    /// <summary>
    /// Obtiene un meeting por ID (público)
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMeetingById(int id)
    {
        var meeting = await _meetingService.GetMeetingByIdAsync(id);
        if (meeting == null)
        {
            return NotFound(new { message = "Meeting no encontrado" });
        }
        return Ok(meeting);
    }

    /// <summary>
    /// Obtiene el último meeting creado (público)
    /// </summary>
    [HttpGet("last")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLastMeeting()
    {
        try
        {
            var meeting = await _meetingService.GetLastMeetingAsync();
            return Ok(meeting);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene resumen de un meeting (público)
    /// </summary>
    [HttpGet("{id}/summary")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMeetingSummary(int id, [FromQuery] bool includeAttendees = false)
    {
        var meetingDto = await _meetingService.GetMeetingUpdateDtoAsync(id, includeAttendees);
        if (meetingDto == null)
        {
            return NotFound(new { message = "Meeting no encontrado" });
        }
        return Ok(meetingDto);
    }

    /// <summary>
    /// Actualiza el estado de un meeting (solo admin)
    /// </summary>
    [HttpPut("{id}/status")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] MeetingStatus status)
    {
        try
        {
            await _meetingService.UpdateStatusAsync(id, status);

            var meetingDto = await _meetingService.GetMeetingUpdateDtoAsync(id, includeAttendees: false);
            await _hubContext.Clients.Group(id.ToString()).SendAsync("MeetingStatusUpdated", meetingDto);

            if (status == MeetingStatus.Closed)
            {
                var meeting = await _meetingService.GetMeetingByIdAsync(id);
                var pdfService = new MeetingMetricsPdfService();
                var pdfBytes = pdfService.GenerateMetricsPdf(meeting);

                return File(pdfBytes, "application/pdf", $"Meeting_{meeting.Id}_Metrics.pdf");
            }

            return Ok(new { message = "Estado actualizado correctamente" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
