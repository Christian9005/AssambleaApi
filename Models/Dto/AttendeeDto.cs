namespace AssambleaApi.Models.Dto;

// DTO para registro de asistente (request)
public class AttendeeDto
{
    public string Name { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public int MeetingId { get; set; }
    public string MeetingCode { get; set; } = string.Empty;
}

// DTO para respuesta con información completa del asistente
public class AttendeeResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public bool IsRegistered { get; set; }
    public bool RequestedToSpeak { get; set; }
    public VoteOption? Vote { get; set; }
    public VoteOption? SecondVote { get; set; }
    public bool IsSpeaking { get; set; }
    public bool InterventionAccepted { get; set; }
    public DateTimeOffset? InterventionStartTime { get; set; }
    public DateTimeOffset? InterventionAcceptDeadline { get; set; }
    public bool ReadyForFirstVote { get; set; }
    public bool ReadyForSecondVote { get; set; }
    public int MeetingId { get; set; }
}
