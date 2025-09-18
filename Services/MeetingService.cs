using AssambleaApi.Data;
using AssambleaApi.Models;
using AssambleaApi.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssambleaApi.Services;

public class MeetingService : IMeetingService
{
    private readonly AppDbContext _context;

    public MeetingService(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Meeting> CreateMeetingAsync()
    {
        var meeting = new Meeting
        {
            Code = Guid.NewGuid().ToString("N").Substring(0, 6),
            StartTime = DateTime.UtcNow,
            Status = MeetingStatus.Created
        };

        _context.Meetings.Add(meeting);
        await _context.SaveChangesAsync();
        return meeting;
    }

    public async Task<Meeting> GetLastMeetingAsync()
    {
        return await _context.Meetings
            .Include(m => m.Attendees)
            .OrderByDescending(m => m.StartTime)
            .FirstOrDefaultAsync() ?? throw new InvalidOperationException("No meetings found.");
    }

    public async Task<Meeting?> GetMeetingByIdAsync(int meetingId)
    {
        return await _context.Meetings
            .Include(m => m.Attendees)
            .FirstOrDefaultAsync(m => m.Id == meetingId);
    }

    public async Task UpdateStatusAsync(int meetingId, MeetingStatus status)
    {
        var meeting = await _context.Meetings.FindAsync(meetingId);

        if (meeting == null) throw new KeyNotFoundException($"Meeting with ID {meetingId} not found.");

        meeting.Status = status;

        if (status == MeetingStatus.Closed)
        {
            meeting.EndTime = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}
