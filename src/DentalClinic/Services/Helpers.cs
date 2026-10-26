using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using DentalClinic.Models;
using Microsoft.AspNetCore.Html;

namespace DentalClinic.Services;

/// <summary>Podaci o ordinaciji koji se ponavljaju na više stranica. (Izmišljeni, za demo.)</summary>
public static class ClinicInfo
{
    public const string Name = "Dental Studio Osmijeh";
    public const string Phone = "+387 33 000 111";
    public const string PhoneLink = "+38733000111";
    public const string Email = "info@studio-osmijeh.ba";
    public const string Address = "Zmaja od Bosne 12, 71000 Sarajevo";
    public const string HoursWeek = "Pon – Pet: 08:00 – 19:00";
    public const string HoursSaturday = "Subota: 09:00 – 13:00";
    public const string HoursSunday = "Nedjelja: zatvoreno";
    public static readonly CultureInfo Culture = new("bs-Latn-BA");
}

/// <summary>
/// Pretvara jednostavan tekst članka u sigurni HTML: pasusi, podnaslovi ("## ") i liste ("- ").
/// Sav tekst se enkodira, pa ni članak koji neko jednom bude mogao uređivati ne može ubaciti skriptu.
/// </summary>
public static class ArticleFormatter
{
    public static HtmlString ToHtml(string body)
    {
        var sb = new StringBuilder();
        var enc = HtmlEncoder.Default;
        var blocks = body.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            // blok može početi podnaslovom pa nastaviti listom ili pasusom
            var i = 0;
            while (i < lines.Length)
            {
                if (lines[i].StartsWith("## "))
                {
                    sb.Append("<h2>").Append(enc.Encode(lines[i][3..])).Append("</h2>");
                    i++;
                }
                else if (lines[i].StartsWith("- "))
                {
                    sb.Append("<ul>");
                    while (i < lines.Length && lines[i].StartsWith("- "))
                        sb.Append("<li>").Append(enc.Encode(lines[i++][2..])).Append("</li>");
                    sb.Append("</ul>");
                }
                else
                {
                    var para = new List<string>();
                    while (i < lines.Length && !lines[i].StartsWith("## ") && !lines[i].StartsWith("- "))
                        para.Add(lines[i++]);
                    sb.Append("<p>").Append(enc.Encode(string.Join(" ", para))).Append("</p>");
                }
            }
        }
        return new HtmlString(sb.ToString());
    }
}

public static class CalendarFile
{
    public static string Build(Appointment a, DateTime? nowUtc = null)
    {
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Dental Studio Osmijeh//BS\r\nCALSCALE:GREGORIAN\r\nBEGIN:VEVENT\r\n");
        sb.Append($"UID:{a.Reference}@studio-osmijeh.ba\r\n");
        sb.Append($"DTSTAMP:{nowUtc ?? DateTime.UtcNow:yyyyMMdd'T'HHmmss'Z'}\r\n");
        sb.Append($"DTSTART:{a.Start:yyyyMMdd'T'HHmmss}\r\nDTEND:{a.End:yyyyMMdd'T'HHmmss}\r\n");
        sb.Append("SUMMARY:").Append(Text($"Zubar: {a.Service.Name}")).Append("\r\n");
        sb.Append("DESCRIPTION:").Append(Text($"{a.Doctor.Name}. Šifra termina: {a.Reference}. Ako ne možete doći, otkažite barem 24 sata ranije.")).Append("\r\n");
        sb.Append("LOCATION:").Append(Text($"{ClinicInfo.Name}, {ClinicInfo.Address}")).Append("\r\n");
        sb.Append("BEGIN:VALARM\r\nTRIGGER:-PT2H\r\nACTION:DISPLAY\r\nDESCRIPTION:Termin kod zubara za 2 sata\r\nEND:VALARM\r\n");
        sb.Append("END:VEVENT\r\nEND:VCALENDAR\r\n");
        return sb.ToString();
    }

    private static string Text(string s) =>
        s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");
}

/// <summary>CSV koji se bez problema otvara u Excelu (UTF-8 BOM, zaštita od formula).</summary>
public static class CsvExporter
{
    private static readonly string[] Header =
        ["Šifra", "Datum", "Vrijeme", "Trajanje (min)", "Usluga", "Doktor", "Pacijent", "Telefon", "Email", "Prvi put", "Status", "Napomena pacijenta", "Napomena osoblja"];

    public static string StatusText(AppointmentStatus s) => s switch
    {
        AppointmentStatus.Scheduled => "Zakazano",
        AppointmentStatus.Confirmed => "Potvrđeno",
        AppointmentStatus.Completed => "Završeno",
        AppointmentStatus.NoShow => "Nije došao/la",
        _ => "Otkazano",
    };

    public static byte[] Build(IEnumerable<Appointment> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Header.Select(Escape)));
        foreach (var a in rows)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                a.Reference, a.Start.ToString("yyyy-MM-dd"), a.Start.ToString("HH:mm"), a.DurationMinutes.ToString(CultureInfo.InvariantCulture),
                a.Service.Name, a.Doctor.Name, a.PatientName, a.Phone, a.Email, a.FirstVisit ? "Da" : "Ne",
                StatusText(a.Status), a.Notes ?? "", a.StaffNote ?? "",
            }.Select(Escape)));
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    /// <summary>Polja koja počinju sa = + - @ dobijaju prefiks ', da ih Excel ne izvrši kao formulu (CSV injection).</summary>
    public static string Escape(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' && !IsPhoneNumber(value))
            value = "'" + value;
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    private static bool IsPhoneNumber(string v) =>
        v[0] == '+' && v.Skip(1).All(c => char.IsDigit(c) || c is ' ' or '-' or '/' or '(' or ')');
}
