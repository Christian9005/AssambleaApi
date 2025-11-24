namespace AssambleaApi.Models.Dto;

public class MeetingUpdateDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public MeetingStatus Status { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    
    // Estadísticas agregadas en lugar de lista completa de attendees
    public int TotalAttendees { get; set; }
    public int RegisteredCount { get; set; }
    public int PendingInterventions { get; set; }
    public int YesVotes { get; set; }
    public int NoVotes { get; set; }
    public int AbstentionVotes { get; set; }
    public int SecondYesVotes { get; set; }
    public int SecondNoVotes { get; set; }
    public int SecondAbstentionVotes { get; set; }
    public int ReadyForFirstVoteCount { get; set; }
    public int ReadyForSecondVoteCount { get; set; }
    
    // Solo el speaker actual si existe
    public AttendeeResponseDto? CurrentSpeaker { get; set; }
    
    // Lista ligera solo cuando es necesario (usa endpoints específicos para lista completa)
    public List<AttendeeResponseDto>? Attendees { get; set; }
}
