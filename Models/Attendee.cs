using System.Text.Json.Serialization;

namespace AssambleaApi.Models;

public class Attendee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public bool IsRegistered { get; set; }
    public bool RequestedToSpeak { get; set; }
    public VoteOption? Vote { get; set; }
    public VoteOption? SecondVote { get; set; }
    public int MeetingId { get; set; }
    public bool IsSpeaking { get; set; }
    public bool InterventionAccepted { get; set; }
    public DateTimeOffset? InterventionStartTime { get; set; }
    public DateTimeOffset? InterventionAcceptDeadline { get; set; }
    public bool ReadyForFirstVote { get; set; }
    public bool ReadyForSecondVote { get; set; }

    [JsonIgnore]
    public Meeting Meeting { get; set; } = null!;
}
