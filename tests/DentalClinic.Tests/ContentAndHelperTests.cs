using System.Text;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DentalClinic.Tests;

/// <summary>Provjerava sam početni sadržaj, da neko ne može slučajno pokvariti sajt izmjenom seeda.</summary>
public class SeedContentTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();

    private static string WwwRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "DentalClinic", "wwwroot"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("wwwroot nije pronađen"), "src", "DentalClinic", "wwwroot");
    }

    [Fact]
    public async Task Every_service_lasts_a_multiple_of_the_slot_size_and_has_all_texts()
    {
        var services = await _t.Db.Services.AsNoTracking().ToListAsync();

        Assert.True(services.Count >= 10);
        Assert.All(services, s =>
        {
            Assert.Equal(0, s.DurationMinutes % ClinicRules.SlotMinutes);
            Assert.True(s.PriceFrom > 0, s.Slug);
            Assert.NotEmpty(s.StepList);
            Assert.NotEmpty(s.AftercareList);
            Assert.False(string.IsNullOrWhiteSpace(s.Description), s.Slug);
            Assert.False(string.IsNullOrWhiteSpace(s.SuitableFor), s.Slug);
        });
        Assert.Equal(services.Count, services.Select(s => s.Slug).Distinct().Count());
    }

    [Fact]
    public async Task Every_active_service_can_be_done_by_at_least_one_doctor()
    {
        var services = await _t.Db.Services.AsNoTracking().ToListAsync();
        var offered = await _t.Db.DoctorServices.Select(x => x.ServiceId).Distinct().ToListAsync();

        Assert.All(services, s => Assert.Contains(s.Id, offered));
    }

    [Fact]
    public async Task Every_service_can_actually_be_booked_some_day_in_the_next_two_weeks()
    {
        foreach (var s in await _t.Db.Services.AsNoTracking().ToListAsync())
        {
            var days = await _t.Service.GetOpenDaysAsync(s, null, TestDb.Wed, 14);
            Assert.True(days.Values.Any(n => n > 0), $"{s.Slug} se ne može zakazati nijedan dan: provjeri smjene doktora");
        }
    }

    [Fact]
    public async Task Doctor_shifts_are_on_the_half_hour_grid_and_never_overlap()
    {
        var shifts = await _t.Db.Shifts.AsNoTracking().ToListAsync();

        Assert.NotEmpty(shifts);
        Assert.All(shifts, s =>
        {
            Assert.True(s.End > s.Start);
            Assert.Equal(0, s.Start.Minutes % ClinicRules.SlotMinutes);
            Assert.Equal(0, s.End.Minutes % ClinicRules.SlotMinutes);
        });
        foreach (var g in shifts.GroupBy(s => (s.DoctorId, s.Day)))
        {
            var ordered = g.OrderBy(s => s.Start).ToList();
            for (var i = 1; i < ordered.Count; i++)
                Assert.True(ordered[i].Start >= ordered[i - 1].End, "preklapanje smjena");
        }
    }

    [Fact]
    public async Task Every_doctor_works_at_least_one_day()
    {
        var doctors = await _t.Db.Doctors.Include(d => d.Shifts).AsNoTracking().ToListAsync();

        Assert.Equal(4, doctors.Count);
        Assert.All(doctors, d => Assert.NotEmpty(d.Shifts));
    }

    [Fact]
    public async Task All_images_referenced_by_the_content_exist_on_disk()
    {
        var root = Path.Combine(WwwRoot(), "img");
        var names = (await _t.Db.Services.Select(s => s.ImageName).ToListAsync())
            .Concat(await _t.Db.Doctors.Select(d => d.PhotoName).ToListAsync())
            .Concat(await _t.Db.Articles.Select(a => a.ImageName).ToListAsync())
            .Concat(["hero", "clinic-bright", "clinic-chair", "waiting-room", "team-work", "xray", "consult"]);

        Assert.All(names.Distinct(), n => Assert.True(File.Exists(Path.Combine(root, n + ".jpg")), $"nedostaje slika {n}.jpg"));
    }

    [Fact]
    public async Task Articles_have_unique_slugs_and_formatted_bodies()
    {
        var articles = await _t.Db.Articles.AsNoTracking().ToListAsync();

        Assert.True(articles.Count >= 6);
        Assert.Equal(articles.Count, articles.Select(a => a.Slug).Distinct().Count());
        Assert.All(articles, a => Assert.Contains("<p>", ArticleFormatter.ToHtml(a.Body).Value));
    }

    [Fact]
    public void Initialize_is_idempotent_and_does_not_duplicate_content()
    {
        ClinicDb.Initialize(_t.Db);
        ClinicDb.Initialize(_t.Db);

        Assert.Equal(11, _t.Db.Services.Count());
        Assert.Equal(4, _t.Db.Doctors.Count());
    }

    [Fact]
    public void Demo_data_fills_the_database_once_and_never_overlaps_appointments()
    {
        DemoData.Seed(_t.Db, TestDb.Wed);
        var count = _t.Db.Appointments.Count();
        DemoData.Seed(_t.Db, TestDb.Wed);

        Assert.True(count > 200);
        Assert.Equal(count, _t.Db.Appointments.Count());              // drugi poziv ne dodaje ništa
        var active = _t.Db.Appointments.AsNoTracking()
            .Where(a => a.Status != AppointmentStatus.Cancelled).AsEnumerable().ToList();
        foreach (var g in active.GroupBy(a => (a.DoctorId, a.Start.Date)))
        {
            var ordered = g.OrderBy(a => a.Start).ToList();
            for (var i = 1; i < ordered.Count; i++)
                Assert.True(ordered[i].Start >= ordered[i - 1].End, "demo termini se preklapaju");
        }
    }
}

public class ArticleFormatterTests
{
    private static string Html(string body) => ArticleFormatter.ToHtml(body).Value!;

    [Fact]
    public void Blank_line_separated_text_becomes_paragraphs() =>
        Assert.Equal("<p>Prvi pasus.</p><p>Drugi pasus.</p>", Html("Prvi pasus.\n\nDrugi pasus."));

    [Fact]
    public void Lines_inside_one_paragraph_are_joined() =>
        Assert.Equal("<p>jedan dva</p>", Html("jedan\ndva"));

    [Fact]
    public void Double_hash_makes_a_heading() =>
        Assert.Equal("<h2>Naslov</h2><p>Tekst.</p>", Html("## Naslov\nTekst."));

    [Fact]
    public void Dash_lines_make_a_list() =>
        Assert.Equal("<ul><li>a</li><li>b</li></ul>", Html("- a\n- b"));

    [Fact]
    public void Heading_followed_by_a_list_in_the_same_block_works() =>
        Assert.Equal("<h2>Plan</h2><ul><li>jedan</li><li>dva</li></ul>", Html("## Plan\n- jedan\n- dva"));

    [Fact]
    public void Html_in_the_text_is_escaped_so_scripts_cannot_run()
    {
        var html = Html("<script>alert(1)</script>\n\n## <b>x</b>\n\n- <img src=x onerror=alert(1)>");

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<b>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Windows_line_endings_are_handled() =>
        Assert.Equal("<p>a</p><p>b</p>", Html("a\r\n\r\nb"));

    [Fact]
    public void Empty_text_gives_empty_html() => Assert.Equal("", Html(""));
}

public class CsvExporterTests
{
    private static Appointment Sample() => new()
    {
        Reference = "DS-ABC234", Start = new DateTime(2026, 10, 20, 9, 30, 0), DurationMinutes = 60,
        Service = new Service { Name = "Plombe, estetske" }, Doctor = new Doctor { Name = "Dr. Test" },
        PatientName = "Ana Anić", Phone = "+387 61 123 456", Email = "ana@primjer.ba", FirstVisit = true,
    };

    [Theory]
    [InlineData("obično", "obično")]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("kaže \"hej\"", "\"kaže \"\"hej\"\"\"")]
    public void Escape_quotes_fields_that_need_it(string input, string expected) =>
        Assert.Equal(expected, CsvExporter.Escape(input));

    [Theory]
    [InlineData("=HYPERLINK(\"http://zlo.ba\")")]
    [InlineData("@SUM(A1:A9)")]
    [InlineData("-2+3")]
    public void Spreadsheet_formulas_typed_by_patients_are_neutralised(string malicious) =>
        Assert.StartsWith("'", CsvExporter.Escape(malicious).TrimStart('"'));

    [Fact]
    public void Real_phone_numbers_are_left_alone() =>
        Assert.Equal("+387 61 123 456", CsvExporter.Escape("+387 61 123 456"));

    [Fact]
    public void Build_writes_bom_header_and_one_row_per_appointment()
    {
        var a = Sample();
        a.Status = AppointmentStatus.NoShow;
        a.StaffNote = "Zvati poslije podne";

        var bytes = CsvExporter.Build([a]);
        var text = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
        var lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3).ToArray());
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Šifra,", lines[0]);
        Assert.Contains("DS-ABC234,2026-10-20,09:30,60,\"Plombe, estetske\",Dr. Test,Ana Anić", lines[1]);
        Assert.Contains("Nije došao/la", lines[1]);
        Assert.Contains("Zvati poslije podne", lines[1]);
    }

    [Theory]
    [InlineData(AppointmentStatus.Scheduled, "Zakazano")]
    [InlineData(AppointmentStatus.Confirmed, "Potvrđeno")]
    [InlineData(AppointmentStatus.Completed, "Završeno")]
    [InlineData(AppointmentStatus.NoShow, "Nije došao/la")]
    [InlineData(AppointmentStatus.Cancelled, "Otkazano")]
    public void Every_status_has_a_readable_name(AppointmentStatus status, string expected) =>
        Assert.Equal(expected, CsvExporter.StatusText(status));
}

public class CalendarFileTests
{
    [Fact]
    public void Event_has_correct_start_end_reminder_and_escaped_text()
    {
        var a = new Appointment
        {
            Reference = "DS-ABC234", Start = new DateTime(2026, 10, 20, 9, 30, 0), DurationMinutes = 90,
            Service = new Service { Name = "Čišćenje; poliranje, fluor" }, Doctor = new Doctor { Name = "Dr. Test" },
        };

        var ics = CalendarFile.Build(a, new DateTime(2026, 10, 14, 8, 0, 0));

        Assert.Contains("DTSTART:20261020T093000", ics);
        Assert.Contains("DTEND:20261020T110000", ics);
        Assert.Contains("DTSTAMP:20261014T080000Z", ics);
        Assert.Contains("UID:DS-ABC234@", ics);
        Assert.Contains("TRIGGER:-PT2H", ics);
        Assert.Contains("Čišćenje\\; poliranje\\, fluor", ics);
        Assert.StartsWith("BEGIN:VCALENDAR", ics);
        Assert.EndsWith("END:VCALENDAR\r\n", ics);
    }
}

public class AdminAuthTests
{
    private static AdminAuth With(string user, string pass) => new(Options.Create(new AdminOptions { Username = user, Password = pass }));

    [Fact] public void Correct_credentials_are_accepted() => Assert.True(With("admin", "tajna").Validate("admin", "tajna"));

    [Theory]
    [InlineData("admin", "pogresna")]
    [InlineData("neko", "tajna")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Wrong_credentials_are_rejected(string? user, string? pass) =>
        Assert.False(With("admin", "tajna").Validate(user, pass));

    [Fact]
    public void Admin_is_disabled_when_no_password_is_configured()
    {
        var auth = With("admin", "");
        Assert.False(auth.Enabled);
        Assert.False(auth.Validate("admin", ""));
    }
}
