using AssambleaApi.Hubs;
using AssambleaApi.Models;
using AssambleaApi.Models.Dto;
using AssambleaApi.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace AssambleaApi.Controllers;

/// <summary>
/// Controller para gestionar asistentes a meetings.
/// Los endpoints públicos permiten acceso con código de meeting (para apps móviles).
/// Los endpoints administrativos requieren autenticación JWT.
/// </summary>
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

    // ==================== ENDPOINTS PÚBLICOS (Código de Meeting) ====================

    /// <summary>
    /// Registra un nuevo asistente. Requiere código de meeting válido.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterAttendee([FromBody] AttendeeDto attendeeDto)
    {
        try
        {
            var attendee = await _attendeeService.RegisterAttendeeAsync(
                attendeeDto.Name, 
                attendeeDto.SeatNumber, 
                attendeeDto.MeetingId, 
                attendeeDto.MeetingCode);
            
            await BroadcastMeetingAsync(attendeeDto.MeetingId);
            return Ok(attendee);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Marca asistencia de un participante
    /// </summary>
    [HttpPost("{id}/attendance")]
    [AllowAnonymous]
    public async Task<IActionResult> MarkAttendance(int id)
    {
        try
        {
            await _attendeeService.MarkAttendanceAsync(id);
            var attendee = await _attendeeService.GetByIdAsync(id);
            if (attendee?.MeetingId != null)
            {
                await BroadcastMeetingAsync(attendee.MeetingId);
            }
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Solicita turno para hablar
    /// </summary>
    [HttpPost("{id}/request-speak")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestToSpeak(int id)
    {
        try
        {
            await _attendeeService.RequestToSpeakAsync(id);
            var attendee = await _attendeeService.GetByIdAsync(id);
            if (attendee?.MeetingId != null)
            {
                await BroadcastMeetingAsync(attendee.MeetingId);
            }
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Emite un voto
    /// </summary>
    [HttpPost("{id}/vote")]
    [AllowAnonymous]
    public async Task<IActionResult> CastVote(int id, [FromBody] VoteOption vote)
    {
        try
        {
            await _attendeeService.CastVoteAsync(id, vote);
            var attendee = await _attendeeService.GetByIdAsync(id);
            if (attendee?.MeetingId != null)
            {
                await BroadcastMeetingAsync(attendee.MeetingId);
            }
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Confirma asistencia para primera votación
    /// </summary>
    [HttpPost("{id}/confirm-first-vote")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmFirstVote(int id)
    {
        try
        {
            await _attendeeService.ConfirmReadyForFirstVoteAsync(id);
            var attendee = await _attendeeService.GetByIdAsync(id);
            if (attendee?.MeetingId != null)
            {
                await _hubContext.Clients.Group(attendee.MeetingId.ToString())
                    .SendAsync("AttendeeReadyForVote", new
                    {
                        AttendeeId = attendee.Id,
                        Name = attendee.Name,
                        SeatNumber = attendee.SeatNumber,
                        VoteRound = "First",
                        ReadyForFirstVote = attendee.ReadyForFirstVote
                    });
                await BroadcastMeetingAsync(attendee.MeetingId);
            }
            return Ok(new { message = "Asistencia confirmada para primera votación" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Confirma asistencia para segunda votación
    /// </summary>
    [HttpPost("{id}/confirm-second-vote")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmSecondVote(int id)
    {
        try
        {
            await _attendeeService.ConfirmReadyForSecondVoteAsync(id);
            var attendee = await _attendeeService.GetByIdAsync(id);
            if (attendee?.MeetingId != null)
            {
                await _hubContext.Clients.Group(attendee.MeetingId.ToString())
                    .SendAsync("AttendeeReadyForVote", new
                    {
                        AttendeeId = attendee.Id,
                        Name = attendee.Name,
                        SeatNumber = attendee.SeatNumber,
                        VoteRound = "Second",
                        ReadyForSecondVote = attendee.ReadyForSecondVote
                    });
                await BroadcastMeetingAsync(attendee.MeetingId);
            }
            return Ok(new { message = "Asistencia confirmada para segunda votación" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene el orador current (público)
    /// </summary>
    [HttpGet("{meetingId}/current-speaker")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCurrentSpeaker(int meetingId)
    {
        var speaker = await _attendeeService.GetCurrentSpeakerAsync(meetingId);
        return Ok(speaker);
    }

    // ==================== ENDPOINTS ADMINISTRATIVOS (JWT Requerido) ====================

    /// <summary>
    /// Acepta una intervención solicitada (solo admin)
    /// </summary>
    [HttpPost("{id}/accept-intervention")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptIntervention(int id)
    {
        try
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cancela una intervención (solo admin)
    /// </summary>
    [HttpPost("{id}/cancel-intervention")]
    [AllowAnonymous]
    public async Task<IActionResult> CancelIntervention(int id)
    {
        try
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Avanza a la siguiente intervención (solo admin)
    /// </summary>
    [HttpPost("{meetingId}/next-intervention")]
    [Authorize(Roles = "Admin,User")]
    public async Task<IActionResult> MoveToNextIntervention(int meetingId)
    {
        var next = await _attendeeService.MoveToNextInterventionAsync(meetingId);
        if (next == null)
            return NotFound(new { message = "No hay intervenciones pendientes" });

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

    /// <summary>
    /// Obtiene lista de intervenciones pendientes (solo admin)
    /// </summary>
    [HttpGet("{meetingId}/pending-interventions")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPendingInterventions(int meetingId)
    {
        var list = await _attendeeService.GetPendingInterventionsAsync(meetingId);
        return Ok(list);
    }

    /// <summary>
    /// Procesa expiraciones de intervenciones (solo admin)
    /// </summary>
    [HttpPost("{meetingId}/process-expirations")]
    [Authorize(Roles = "Admin,User")]
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
            next = next?.Id,
            message = "Procesado manualmente (normalmente lo hace el background service)"
        });
    }

    private async Task BroadcastMeetingAsync(int meetingId)
    {
        var meetingDto = await _meetingService.GetMeetingUpdateDtoAsync(meetingId, includeAttendees: true);
        await _hubContext.Clients.Group(meetingId.ToString())
            .SendAsync("MeetingStatusUpdated", meetingDto);
    }
}
