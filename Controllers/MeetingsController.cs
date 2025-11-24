using AssambleaApi.Hubs;
using AssambleaApi.Models;
using AssambleaApi.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace AssambleaApi.Controllers;

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

    [HttpPost]
    public async Task<IActionResult> CreateMeeting()
    {
        var meeting = await _meetingService.CreateMeetingAsync();
        return CreatedAtAction(nameof(GetMeetingById), new { id = meeting.Id }, meeting);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetMeetingById(int id)
    {
        var meeting = await _meetingService.GetMeetingByIdAsync(id);
        if (meeting == null)
        {
            return NotFound();
        }
        return Ok(meeting);
    }

    [HttpGet("last")]
    public async Task<IActionResult> GetLastMeeting()
    {
        var meeting = await _meetingService.GetLastMeetingAsync();
        if (meeting == null)
        {
            return NotFound();
        }
        return Ok(meeting);
    }

    [HttpGet("{id}/summary")]
    public async Task<IActionResult> GetMeetingSummary(int id, [FromQuery] bool includeAttendees = false)
    {
        var meetingDto = await _meetingService.GetMeetingUpdateDtoAsync(id, includeAttendees);
        if (meetingDto == null)
        {
            return NotFound();
        }
        return Ok(meetingDto);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] MeetingStatus status)
    {
        await _meetingService.UpdateStatusAsync(id, status);

        var meetingDto = await _meetingService.GetMeetingUpdateDtoAsync(id, includeAttendees: false);
        await _hubContext.Clients.Group(id.ToString()).SendAsync("MeetingStatusUpdated", meetingDto);

        if (status == MeetingStatus.Closed)
        {
            // Para el PDF necesitamos el Meeting completo
            var meeting = await _meetingService.GetMeetingByIdAsync(id);
            var pdfService = new MeetingMetricsPdfService();
            var pdfBytes = pdfService.GenerateMetricsPdf(meeting);

            return File(pdfBytes, "application/pdf", $"Meeting_{meeting.Id}_Metrics.pdf");
        }

        return Ok();
    }
}
