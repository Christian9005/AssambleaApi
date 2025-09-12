using AssambleaApi.Models;

namespace AssambleaApi.Services.Interfaces;

public interface IAttendeeService
{
    Task<Attendee> RegisterAttendeeAsync(string name, int seatNumber, int meetingId, string meetingCode);
    Task MarkAttendanceAsync(int attendeeId);
    Task RequestToSpeakAsync(int attendeeId);
    Task CastVoteAsync(int attendeeId, VoteOption vote);
    Task<Attendee> GetByIdAsync(int id);
    Task<bool> AcceptInterventionAsync(int attendeeId);
    Task<bool> CancelInterventionAsync(int attendeeId);
    Task<bool> EndInterventionAsync(int attendeeId);
    Task<List<Attendee>> GetPendingInterventionsAsync(int meetingId);
    Task<Attendee?> GetCurrentSpeakerAsync(int meetingId);
    Task<bool> CheckInterventionAcceptDeadlineAsync(int attendeeId);
    Task<Attendee?> MoveToNextInterventionAsync(int meetingId);
    Task<(Attendee? expiredSpeaker, Attendee? next, bool changed)> ProcessExpirationsAsync(int meetingId);
}
