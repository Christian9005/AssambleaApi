using AssambleaApi.Models;
using Microsoft.AspNetCore.SignalR;

namespace AssambleaApi.Hubs;

public class MeetingHub : Hub
{
    public Task JoinMeeting(int meetingId) => Groups.AddToGroupAsync(Context.ConnectionId, meetingId.ToString());
    public Task LeaveMeeting(int meetingId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, meetingId.ToString());

    public async Task BroadcastMeetingStatusToGroup(int meetingId, object status)
    {
        await Clients.Group(meetingId.ToString()).SendAsync("MeetingStatusUpdated", status);
    }
    public async Task NotifyInterventionStarted(int meetingId, Attendee attendee)
    {
        await Clients.Group(meetingId.ToString()).SendAsync("InterventionStarted", new
        {
            AttendeeId = attendee.Id,
            Name = attendee.Name,
            SeatNumber = attendee.SeatNumber,
            StartTime = attendee.InterventionStartTime,
            DurationMinutes = 5
        });
    }

    public async Task NotifyInterventionEnded(int meetingId, int attendeeId)
    {
        await Clients.Group(meetingId.ToString()).SendAsync("InterventionEnded", attendeeId);
    }

    public async Task NotifyInterventionCancelled(int meetingId, int attendeeId)
    {
        await Clients.Group(meetingId.ToString()).SendAsync("InterventionCancelled", attendeeId);
    }

}
