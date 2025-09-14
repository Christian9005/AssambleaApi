namespace AssambleaApi.Models;

public class Meeting
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public MeetingStatus Status { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public ICollection<Attendee> Attendees { get; set; } = new List<Attendee>();
}
