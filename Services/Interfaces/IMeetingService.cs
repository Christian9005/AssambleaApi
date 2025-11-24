using AssambleaApi.Models;
using AssambleaApi.Models.Dto;

namespace AssambleaApi.Services.Interfaces;

public interface IMeetingService
{
    Task<Meeting> CreateMeetingAsync();
    Task UpdateStatusAsync(int meetingId, MeetingStatus status);
    Task<Meeting?> GetMeetingByIdAsync(int meetingId);
    Task<Meeting> GetLastMeetingAsync();
    Task<MeetingUpdateDto?> GetMeetingUpdateDtoAsync(int meetingId, bool includeAttendees = false);
}
