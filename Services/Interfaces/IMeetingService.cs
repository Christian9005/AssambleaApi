using AssambleaApi.Models;

namespace AssambleaApi.Services.Interfaces;

public interface IMeetingService
{
    Task<Meeting> CreateMeetingAsync();
    Task UpdateStatusAsync(int meetingId, MeetingStatus status);
    Task<Meeting?> GetMeetingByIdAsync(int meetingId);
    Task<Meeting> GetLastMeetingAsync();
}
