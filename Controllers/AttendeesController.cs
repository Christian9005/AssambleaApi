using AssambleaApi.Hubs;
using AssambleaApi.Models;
using AssambleaApi.Models.Dto;
using AssambleaApi.Services;
using AssambleaApi.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace AssambleaApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AttendeesController : ControllerBase
{
    private readonly IAttendeeService _attendeeService;
    private readonly IMeetingService _meetingService;
    private readonly IHubContext<MeetingHub> _hubContext;

    public AttendeesController(IAttendeeService attendeeService, IMeetingService meetingService, IHubContext<MeetingHub> hubContext)
    {
        _attendeeService = attendeeService ?? throw new ArgumentNullException(nameof(attendeeService));
        _meetingService = meetingService ?? throw new ArgumentNullException(nameof(meetingService));
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
    }

    [HttpPost]
    public async Task<IActionResult> RegisterAttendee([FromBody] AttendeeDto attendeeDto)
    {
        var attendee = await _attendeeService.RegisterAttendeeAsync(attendeeDto.Name, attendeeDto.SeatNumber, attendeeDto.MeetingId, attendeeDto.MeetingCode);
    await BroadcastMeetingAsync(attendeeDto.MeetingId);

        return Ok(attendee);
    }

    [HttpPost("{id}/attendance")]
    public async Task<IActionResult> MarkAttendance(int id)
    {
        await _attendeeService.MarkAttendanceAsync(id);

        var attendee = await _attendeeService.GetByIdAsync(id);
        if (attendee?.MeetingId != null)
        {
            await BroadcastMeetingAsync(attendee.MeetingId);
        }

        return NoContent();
    }

    [HttpPost("{id}/request-speak")]
    public async Task<IActionResult> RequestToSpeak(int id)
    {
        await _attendeeService.RequestToSpeakAsync(id);

        var attendee = await _attendeeService.GetByIdAsync(id);
        if (attendee?.MeetingId != null)
        {
            await BroadcastMeetingAsync(attendee.MeetingId);
        }

        return NoContent();
    }

    [HttpPost("{id}/vote")]
    public async Task<IActionResult> CastVote(int id, [FromBody] VoteOption vote)
    {
        await _attendeeService.CastVoteAsync(id, vote);

        var attendee = await _attendeeService.GetByIdAsync(id);
        if (attendee?.MeetingId != null)
        {
            await BroadcastMeetingAsync(attendee.MeetingId);
        }

        return NoContent();
    }
    
    [HttpPost("{id}/accept-intervention")]
    public async Task<IActionResult> AcceptIntervention(int id)
    {
        await _attendeeService.AcceptInterventionAsync(id);
        var attendee = await _attendeeService.GetByIdAsync(id);
        await _hubContext.Clients.Group(attendee.MeetingId.ToString())
            .SendAsync("InterventionStarted", new {
                AttendeeId = attendee.Id,
                Name = attendee.Name,
                SeatNumber = attendee.SeatNumber,
                StartTime = attendee.InterventionStartTime,
                DurationMinutes = 5
            });
        await BroadcastMeetingAsync(attendee.MeetingId);
        return NoContent();
    }

    [HttpPost("{id}/cancel-intervention")]
    public async Task<IActionResult> CancelIntervention(int id)
    {
        await _attendeeService.CancelInterventionAsync(id);
        var attendee = await _attendeeService.GetByIdAsync(id);
        await _hubContext.Clients.Group(attendee.MeetingId.ToString())
            .SendAsync("InterventionCancelled", new {
                AttendeeId = attendee.Id,
                Name = attendee.Name,
                SeatNumber = attendee.SeatNumber,
                Reason = "Manual"
            });
        var next = await _attendeeService.MoveToNextInterventionAsync(attendee.MeetingId);
        if (next != null)
        {
            await _hubContext.Clients.Group(attendee.MeetingId.ToString())
                .SendAsync("NextInterventionRequested", new {
                    AttendeeId = next.Id,
                    Name = next.Name,
                    SeatNumber = next.SeatNumber,
                    AcceptDeadline = next.InterventionAcceptDeadline
                });
        }
        await BroadcastMeetingAsync(attendee.MeetingId);
        return NoContent();
    }

    [HttpPost("{meetingId}/next-intervention")]
    public async Task<IActionResult> MoveToNextIntervention(int meetingId)
    {
        var next = await _attendeeService.MoveToNextInterventionAsync(meetingId);
        if (next == null)
            return NotFound();
        await _hubContext.Clients.Group(meetingId.ToString())
            .SendAsync("NextInterventionRequested", new {
                AttendeeId = next.Id,
                Name = next.Name,
        SeatNumber = next.SeatNumber,
                AcceptDeadline = next.InterventionAcceptDeadline
            });
    await BroadcastMeetingAsync(meetingId);
        return Ok(next);
    }

    [HttpGet("{meetingId}/pending-interventions")]
    public async Task<IActionResult> GetPendingInterventions(int meetingId)
    {
        var list = await _attendeeService.GetPendingInterventionsAsync(meetingId);
        return Ok(list);
    }

    [HttpGet("{meetingId}/current-speaker")]
    public async Task<IActionResult> GetCurrentSpeaker(int meetingId)
    {
        var speaker = await _attendeeService.GetCurrentSpeakerAsync(meetingId);
        return Ok(speaker);
    }

    [HttpPost("{meetingId}/process-expirations")]
    public async Task<IActionResult> ProcessExpirations(int meetingId)
    {
        var (expiredSpeaker, next, changed) = await _attendeeService.ProcessExpirationsAsync(meetingId);
        if (expiredSpeaker != null)
        {
            await _hubContext.Clients.Group(meetingId.ToString())
                .SendAsync("InterventionEnded", new {
                    AttendeeId = expiredSpeaker.Id,
                    Name = expiredSpeaker.Name,
                    SeatNumber = expiredSpeaker.SeatNumber,
                    Reason = "TimeExpired"
                });
        }
        if (next != null)
        {
            await _hubContext.Clients.Group(meetingId.ToString())
                .SendAsync("NextInterventionRequested", new {
                    AttendeeId = next.Id,
                    Name = next.Name,
                    SeatNumber = next.SeatNumber,
                    AcceptDeadline = next.InterventionAcceptDeadline
                });
        }
        if (changed)
        {
            await BroadcastMeetingAsync(meetingId);
        }
        return Ok(new {
            expired = expiredSpeaker?.Id,
            next = next?.Id
        });
    }

    private async Task BroadcastMeetingAsync(int meetingId)
    {
        var meeting = await _meetingService.GetMeetingByIdAsync(meetingId);
        await _hubContext.Clients.Group(meetingId.ToString())
            .SendAsync("MeetingStatusUpdated", meeting);
    }
}
