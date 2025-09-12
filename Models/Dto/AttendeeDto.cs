namespace AssambleaApi.Models.Dto;

public class AttendeeDto
{
    public string Name { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public int MeetingId { get; set; }
    public string MeetingCode { get; set; } = string.Empty;
}
