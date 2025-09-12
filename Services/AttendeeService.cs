using AssambleaApi.Data;
using AssambleaApi.Models;
using AssambleaApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssambleaApi.Services;

public class AttendeeService : IAttendeeService
{
    private readonly AppDbContext _context;

    public AttendeeService(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<bool> AcceptInterventionAsync(int attendeeId)
    {
        var attendee = await _context.Attendees.FindAsync(attendeeId);
        if (attendee == null)
            throw new KeyNotFoundException($"Attendee with ID {attendeeId} not found.");

    attendee.InterventionAccepted = true;
    attendee.IsSpeaking = true;
    attendee.InterventionStartTime = DateTime.UtcNow;
    attendee.InterventionAcceptDeadline = null; // ya no aplica el deadline de aceptación
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> CancelInterventionAsync(int attendeeId)
    {
        var attendee = await _context.Attendees.FindAsync(attendeeId);
        if (attendee == null)
            throw new KeyNotFoundException($"Attendee with ID {attendeeId} not found.");

        attendee.InterventionAccepted = false;
        attendee.IsSpeaking = false;
        attendee.InterventionStartTime = null;
        attendee.RequestedToSpeak = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task CastVoteAsync(int attendeeId, VoteOption vote)
    {
        var attendee = await _context.Attendees.FindAsync(attendeeId);
        if (attendee == null)
        {
            throw new KeyNotFoundException($"Attendee with ID {attendeeId} not found.");
        }

        attendee.Vote = vote;
        await _context.SaveChangesAsync();
    }

    public async Task<bool> EndInterventionAsync(int attendeeId)
    {
        var attendee = await _context.Attendees.FindAsync(attendeeId);
        if (attendee == null)
            throw new KeyNotFoundException($"Attendee with ID {attendeeId} not found.");

        attendee.IsSpeaking = false;
        attendee.InterventionStartTime = null;
        attendee.RequestedToSpeak = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Attendee> GetByIdAsync(int id)
    {
        var attendee = await _context.Attendees.FindAsync(id);
        if (attendee == null)
        {
            throw new KeyNotFoundException($"Attendee with ID {id} not found.");
        }
        return attendee;
    }

    public async Task MarkAttendanceAsync(int attendeeId)
    {
        var attendee = await _context.Attendees.FindAsync(attendeeId);
        if (attendee == null)
        {
            throw new KeyNotFoundException($"Attendee with ID {attendeeId} not found.");
        }

        attendee.IsRegistered = true;
        await _context.SaveChangesAsync();
    }

    public async Task<Attendee> RegisterAttendeeAsync(string name, int seatNumber, int meetingId, string meetingCode)
    {
        var meeting = await _context.Meetings
            .FirstOrDefaultAsync(m => m.Id == meetingId && m.Code == meetingCode);

        if (meeting == null)
        {
            throw new KeyNotFoundException($"Codigo Incorrecto");
        }

        var attendee = new Attendee
        {
            Name = name,
            SeatNumber = seatNumber,
            IsRegistered = false,
            MeetingId = meetingId
        };

        _context.Attendees.Add(attendee);
        await _context.SaveChangesAsync();
        return attendee;
    }

    public async Task RequestToSpeakAsync(int attendeeId)
    {
        var attendee = await _context.Attendees.FindAsync(attendeeId);
        if (attendee == null)
        {
            throw new KeyNotFoundException($"Attendee with ID {attendeeId} not found.");
        }

        attendee.RequestedToSpeak = true;
        await _context.SaveChangesAsync();
    }

    public async Task<List<Attendee>> GetPendingInterventionsAsync(int meetingId)
    {
        return await _context.Attendees
            .Where(a => a.MeetingId == meetingId && a.RequestedToSpeak && !a.InterventionAccepted)
            .OrderBy(a => a.InterventionAcceptDeadline)
            .ToListAsync();
    }

    public async Task<Attendee?> GetCurrentSpeakerAsync(int meetingId)
    {
        return await _context.Attendees
            .Where(a => a.MeetingId == meetingId && a.InterventionAccepted && a.IsSpeaking)
            .OrderBy(a => a.InterventionStartTime)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> CheckInterventionAcceptDeadlineAsync(int attendeeId)
    {
        var attendee = await _context.Attendees.FindAsync(attendeeId);
        if (attendee == null || !attendee.RequestedToSpeak)
            return false;

        if (attendee.InterventionAccepted)
            return true;

        if (attendee.InterventionAcceptDeadline.HasValue && DateTime.UtcNow > attendee.InterventionAcceptDeadline.Value)
        {
            attendee.RequestedToSpeak = false;
            attendee.InterventionAcceptDeadline = null;
            await _context.SaveChangesAsync();
            return false;
        }
        return true;
    }

    public async Task<Attendee?> MoveToNextInterventionAsync(int meetingId)
    {
        var pending = await GetPendingInterventionsAsync(meetingId);
        if (pending.Count == 0)
            return null;

    var next = pending[0];
    next.InterventionAcceptDeadline = DateTime.UtcNow.AddMinutes(1); // 1 minuto para aceptar
        await _context.SaveChangesAsync();
        return next;
    }

    /// <summary>
    /// Revisa y aplica expiraciones: aceptación (1 min) y duración de intervención (5 min).
    /// Si algo expira, avanza al siguiente y devuelve el nuevo estado (next, ended, cancelled).
    /// </summary>
    public async Task<(Attendee? expiredSpeaker, Attendee? next, bool changed)> ProcessExpirationsAsync(int meetingId)
    {
        var now = DateTime.UtcNow;
        Attendee? expiredSpeaker = null;
        bool changed = false;

        // 1. Revisar si algún aceptante pendiente expiró su deadline de aceptación
        var pending = await _context.Attendees
            .Where(a => a.MeetingId == meetingId && a.RequestedToSpeak && !a.InterventionAccepted && a.InterventionAcceptDeadline != null && a.InterventionAcceptDeadline < now)
            .ToListAsync();
        if (pending.Any())
        {
            foreach (var p in pending)
            {
                p.RequestedToSpeak = false; // lo sacamos de la cola
                p.InterventionAcceptDeadline = null;
            }
            changed = true;
        }

        // 2. Revisar si el orador actual excedió 5 minutos
        var current = await GetCurrentSpeakerAsync(meetingId);
        if (current != null && current.InterventionStartTime.HasValue && current.InterventionStartTime.Value.AddMinutes(5) < now)
        {
            // Expiró su intervención
            current.IsSpeaking = false;
            current.InterventionAccepted = false; // ya terminó
            current.InterventionStartTime = null;
            current.RequestedToSpeak = false;
            expiredSpeaker = current;
            changed = true;
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }

        // 3. Si no hay orador activo, intentar asignar siguiente (si existe)
        current = await GetCurrentSpeakerAsync(meetingId); // refrescar
        Attendee? next = null;
        if (current == null)
        {
            next = await MoveToNextInterventionAsync(meetingId);
        }

    return (expiredSpeaker, next, changed);
    }
}
