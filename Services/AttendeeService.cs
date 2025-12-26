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
        attendee.InterventionStartTime = DateTimeOffset.UtcNow;
        attendee.InterventionAcceptDeadline = null;
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
        var attendee = await _context.Attendees
            .Include(a => a.Meeting)
            .FirstOrDefaultAsync(a => a.Id == attendeeId);

        if (attendee == null)
            throw new KeyNotFoundException($"Asistente con ID {attendeeId} not found.");

        var meeting = attendee.Meeting;
        if (meeting == null)
            throw new KeyNotFoundException($"Meeting no encontrada.");

        // Validar que el asistente esté listo para votar
        if (meeting.Status == MeetingStatus.FirstVoting)
        {
            if (!attendee.ReadyForFirstVote)
                throw new InvalidOperationException("Debe confirmar su asistencia para la primera votación");

            if (attendee.Vote.HasValue)
                throw new InvalidOperationException("Ya registró su voto en la primera votación");

            attendee.Vote = vote;
        }
        else if (meeting.Status == MeetingStatus.SecondVoting)
        {
            if (!attendee.ReadyForSecondVote)
                throw new InvalidOperationException("Debe confirmar su asistencia para la segunda votación");

            if (attendee.SecondVote.HasValue)
                throw new InvalidOperationException("Ya registró su voto en la segunda votación");

            attendee.SecondVote = vote;
        }
        else
        {
            throw new InvalidOperationException("La reunión no está en periodo de votación");
        }

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
            throw new UnauthorizedAccessException("Código de meeting inválido");
        }

        if (meeting.Status == MeetingStatus.Closed)
        {
            throw new InvalidOperationException("El meeting está cerrado");
        }

        // Verificar que no exista otro asistente con el mismo número de asiento
        var existingAttendee = await _context.Attendees
            .FirstOrDefaultAsync(a => a.MeetingId == meetingId && a.SeatNumber == seatNumber);

        if (existingAttendee != null)
        {
            throw new InvalidOperationException($"El asiento {seatNumber} ya está ocupado");
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

        if (!attendee.IsRegistered)
        {
            throw new InvalidOperationException("Debe marcar asistencia antes de solicitar la palabra");
        }

        attendee.RequestedToSpeak = true;
        await _context.SaveChangesAsync();
    }

    public async Task<List<Attendee>> GetPendingInterventionsAsync(int meetingId)
    {
        return await _context.Attendees
            .AsNoTracking()
            .Where(a => a.MeetingId == meetingId && a.RequestedToSpeak && !a.InterventionAccepted)
            .OrderBy(a => a.InterventionAcceptDeadline)
            .ToListAsync();
    }

    public async Task<Attendee?> GetCurrentSpeakerAsync(int meetingId)
    {
        return await _context.Attendees
            .AsNoTracking()
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

        if (attendee.InterventionAcceptDeadline.HasValue && DateTimeOffset.UtcNow > attendee.InterventionAcceptDeadline.Value)
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
        next.InterventionAcceptDeadline = DateTimeOffset.UtcNow.AddMinutes(1);
        await _context.SaveChangesAsync();
        return next;
    }

    public async Task<(Attendee? expiredSpeaker, Attendee? next, bool changed)> ProcessExpirationsAsync(int meetingId)
    {
        var now = DateTimeOffset.UtcNow;
        Attendee? expiredSpeaker = null;
        bool changed = false;

        // 1. Revisar pendientes que expiraron deadline de aceptación
        var pending = await _context.Attendees
            .Where(a => a.MeetingId == meetingId && a.RequestedToSpeak && !a.InterventionAccepted && a.InterventionAcceptDeadline != null && a.InterventionAcceptDeadline < now)
            .ToListAsync();
        
        if (pending.Any())
        {
            foreach (var p in pending)
            {
                p.RequestedToSpeak = false;
                p.InterventionAcceptDeadline = null;
            }
            changed = true;
        }

        // 2. Revisar si el orador actual excedió 5 minutos
        var current = await GetCurrentSpeakerAsync(meetingId);
        if (current != null && current.InterventionStartTime.HasValue && current.InterventionStartTime.Value.AddMinutes(5) < now)
        {
            current.IsSpeaking = false;
            current.InterventionAccepted = false;
            current.InterventionStartTime = null;
            current.RequestedToSpeak = false;
            expiredSpeaker = current;
            changed = true;
        }

        if (changed)
        {
            await _context.SaveChangesAsync();
        }

        // 3. Si no hay orador activo, intentar asignar siguiente
        current = await GetCurrentSpeakerAsync(meetingId);
        Attendee? next = null;
        if (current == null)
        {
            next = await MoveToNextInterventionAsync(meetingId);
        }

        return (expiredSpeaker, next, changed);
    }

    public async Task ConfirmReadyForFirstVoteAsync(int id)
    {
        var attendee = await _context.Attendees.FindAsync(id);
        if (attendee == null)
            throw new InvalidOperationException("Asistente no encontrado");

        if (!attendee.IsRegistered)
            throw new InvalidOperationException("Debe marcar asistencia primero");

        attendee.ReadyForFirstVote = true;
        await _context.SaveChangesAsync();
    }

    public async Task ConfirmReadyForSecondVoteAsync(int id)
    {
        var attendee = await _context.Attendees.FindAsync(id);
        if (attendee == null)
            throw new InvalidOperationException("Asistente no encontrado");

        if (!attendee.IsRegistered)
            throw new InvalidOperationException("Debe marcar asistencia primero");

        attendee.ReadyForSecondVote = true;
        await _context.SaveChangesAsync();
    }
}
