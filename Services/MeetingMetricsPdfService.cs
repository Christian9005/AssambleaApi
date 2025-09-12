using AssambleaApi.Models;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using System.IO;
using System.Linq;

public class MeetingMetricsPdfService
{
    public byte[] GenerateMetricsPdf(Meeting meeting)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 12, XFontStyle.Regular);

        double y = 40;

        gfx.DrawString($"Meeting Metrics - ID: {meeting.Id}", font, XBrushes.Black, new XRect(40, y, page.Width, page.Height), XStringFormats.TopLeft);
        y += 25;
        gfx.DrawString($"Start: {meeting.StartTime}, End: {meeting.EndTime}", font, XBrushes.Black, new XRect(40, y, page.Width, page.Height), XStringFormats.TopLeft);
        y += 25;

        gfx.DrawString("Participants:", font, XBrushes.Black, new XRect(40, y, page.Width, page.Height), XStringFormats.TopLeft);
        y += 20;
        foreach (var attendee in meeting.Attendees.Where(a => a.IsRegistered))
        {
            gfx.DrawString($"- {attendee.Name} (Seat {attendee.SeatNumber})", font, XBrushes.Black, new XRect(60, y, page.Width, page.Height), XStringFormats.TopLeft);
            y += 18;
        }

        y += 10;
        gfx.DrawString("Speakers:", font, XBrushes.Black, new XRect(40, y, page.Width, page.Height), XStringFormats.TopLeft);
        y += 20;
        foreach (var attendee in meeting.Attendees.Where(a => a.RequestedToSpeak))
        {
            gfx.DrawString($"- {attendee.Name}", font, XBrushes.Black, new XRect(60, y, page.Width, page.Height), XStringFormats.TopLeft);
            y += 18;
        }

        y += 10;
        gfx.DrawString("First Voting Results:", font, XBrushes.Black, new XRect(40, y, page.Width, page.Height), XStringFormats.TopLeft);
        y += 20;
        var voteGroups = meeting.Attendees.Where(a => a.Vote.HasValue).GroupBy(a => a.Vote);
        foreach (var group in voteGroups)
        {
            gfx.DrawString($"{group.Key}: {group.Count()}", font, XBrushes.Black, new XRect(60, y, page.Width, page.Height), XStringFormats.TopLeft);
            y += 18;
        }

        using var ms = new MemoryStream();
        document.Save(ms, false);
        return ms.ToArray();
    }
}