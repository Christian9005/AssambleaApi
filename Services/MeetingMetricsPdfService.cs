using AssambleaApi.Models;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using System.IO;
using System.Linq;

public class DejaVuFontResolver : IFontResolver
{
    private static readonly byte[] DejaVuSansData = File.ReadAllBytes("Fonts/DejaVuSans.ttf");

    public string DefaultFontName => throw new NotImplementedException();

    public byte[] GetFont(string faceName)
    {
        return DejaVuSansData;
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Always return the same font for any style
        return new FontResolverInfo("DejaVuSans#");
    }
}

public class MeetingMetricsPdfService
{
    static MeetingMetricsPdfService()
    {
        GlobalFontSettings.FontResolver = new DejaVuFontResolver();
    }

    public byte[] GenerateMetricsPdf(Meeting meeting)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        var font = new XFont("DejaVuSans#", 12, XFontStyle.Regular);

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