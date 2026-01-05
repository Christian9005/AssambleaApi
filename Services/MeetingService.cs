using AssambleaApi.Data;
using AssambleaApi.Models;
using AssambleaApi.Models.Dto;
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
            Code = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper(),
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
            .AsNoTracking()
            .Include(m => m.Attendees)
            .OrderByDescending(m => m.StartTime)
            .FirstOrDefaultAsync() ?? throw new InvalidOperationException("No meetings found.");
    }

    public async Task<Meeting?> GetMeetingByIdAsync(int meetingId)
    {
        return await _context.Meetings
            .AsNoTracking()
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

    public async Task<MeetingUpdateDto?> GetMeetingUpdateDtoAsync(int meetingId, bool includeAttendees = false)
    {
        var meeting = await _context.Meetings
            .AsNoTracking()
            .Where(m => m.Id == meetingId)
            .Select(m => new
            {
                m.Id,
                m.Code,
                m.Status,
                m.StartTime,
                m.EndTime,
                Attendees = m.Attendees
            })
            .FirstOrDefaultAsync();

        if (meeting == null) return null;

        var currentSpeaker = await _context.Attendees
            .AsNoTracking()
            .Where(a => a.MeetingId == meetingId && a.InterventionAccepted && a.IsSpeaking)
            .Select(a => new AttendeeResponseDto
            {
                Id = a.Id,
                Name = a.Name,
                SeatNumber = a.SeatNumber,
                IsRegistered = a.IsRegistered,
                RequestedToSpeak = a.RequestedToSpeak,
                Vote = a.Vote,
                SecondVote = a.SecondVote,
                IsSpeaking = a.IsSpeaking,
                InterventionAccepted = a.InterventionAccepted,
                InterventionStartTime = a.InterventionStartTime,
                InterventionAcceptDeadline = a.InterventionAcceptDeadline,
                ReadyForFirstVote = a.ReadyForFirstVote,
                ReadyForSecondVote = a.ReadyForSecondVote,
                MeetingId = a.MeetingId
            })
            .FirstOrDefaultAsync();

        var dto = new MeetingUpdateDto
        {
            Id = meeting.Id,
            Code = meeting.Code,
            Status = meeting.Status,
            StartTime = meeting.StartTime,
            EndTime = meeting.EndTime,
            TotalAttendees = meeting.Attendees.Count,
            RegisteredCount = meeting.Attendees.Count(a => a.IsRegistered),
            PendingInterventions = meeting.Attendees.Count(a => a.RequestedToSpeak && !a.InterventionAccepted),
            YesVotes = meeting.Attendees.Count(a => a.Vote == VoteOption.Yes),
            NoVotes = meeting.Attendees.Count(a => a.Vote == VoteOption.No),
            AbstentionVotes = meeting.Attendees.Count(a => a.Vote == VoteOption.Abstention),
            SecondYesVotes = meeting.Attendees.Count(a => a.SecondVote == VoteOption.Yes),
            SecondNoVotes = meeting.Attendees.Count(a => a.SecondVote == VoteOption.No),
            SecondAbstentionVotes = meeting.Attendees.Count(a => a.SecondVote == VoteOption.Abstention),
            ReadyForFirstVoteCount = meeting.Attendees.Count(a => a.ReadyForFirstVote),
            ReadyForSecondVoteCount = meeting.Attendees.Count(a => a.ReadyForSecondVote),
            CurrentSpeaker = currentSpeaker,
            Attendees = includeAttendees ? meeting.Attendees.Select(a => new AttendeeResponseDto
            {
                Id = a.Id,
                Name = a.Name,
                SeatNumber = a.SeatNumber,
                IsRegistered = a.IsRegistered,
                RequestedToSpeak = a.RequestedToSpeak,
                Vote = a.Vote,
                SecondVote = a.SecondVote,
                IsSpeaking = a.IsSpeaking,
                InterventionAccepted = a.InterventionAccepted,
                InterventionStartTime = a.InterventionStartTime,
                InterventionAcceptDeadline = a.InterventionAcceptDeadline,
                ReadyForFirstVote = a.ReadyForFirstVote,
                ReadyForSecondVote = a.ReadyForSecondVote,
                MeetingId = a.MeetingId
            }).ToList() : null
        };

        return dto;
    }
}
