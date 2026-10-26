using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Tests;

/// <summary>Sat kojem testovi sami zadaju "sada", da rezultati ne zavise od datuma pokretanja.</summary>
public class FixedClock : TimeProvider
{
    public DateTime Now { get; set; }
    public FixedClock(DateTime localNow) => Now = localNow;
    public override DateTimeOffset GetUtcNow() => new(Now, TimeSpan.Zero);
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>
/// Prava SQLite baza u memoriji s pravim početnim sadržajem (usluge, doktori, radno vrijeme).
/// Tako testovi istovremeno provjeravaju i sam seed.
///
/// "Danas" je srijeda, 14. oktobar 2026, 10:00. Radno vrijeme iz seeda:
///   Lejla: pon, uto, čet 09-12 i 13-17; pet 09-14
///   Tarik: uto, sri, pet 10-13 i 14-18
///   Mia:   pon, sri, čet 13-19; sub 09-13
///   Adnan: pon-pet 08-12 i 13-16
/// </summary>
public sealed class TestDb : IDisposable
{
    // Datoteka u temp folderu (a ne :memory:) da više konekcija može istovremeno raditi, kao u pravoj aplikaciji.
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"clinic-test-{Guid.NewGuid():N}.db");
    public ClinicDb Db { get; }
    public FixedClock Clock { get; } = new(new DateTime(2026, 10, 14, 10, 0, 0));
    public AppointmentService Service { get; }

    public static readonly DateTime Wed = new(2026, 10, 14);
    public static readonly DateTime Thu = new(2026, 10, 15);
    public static readonly DateTime Fri = new(2026, 10, 16);
    public static readonly DateTime Sat = new(2026, 10, 17);
    public static readonly DateTime Sun = new(2026, 10, 18);
    public static readonly DateTime Mon = new(2026, 10, 19);

    public TestDb()
    {
        Db = NewContext();
        ClinicDb.Initialize(Db);
        Service = new AppointmentService(Db, Clock);
    }

    public ClinicDb NewContext() => new(new DbContextOptionsBuilder<ClinicDb>().UseSqlite($"Data Source={_file}").Options);

    public Service Svc(string slug) => Db.Services.AsNoTracking().Single(s => s.Slug == slug);
    public Doctor Doc(string slug) => Db.Doctors.AsNoTracking().Single(d => d.Slug == slug);

    public const string Lejla = "dr-lejla-kovacevic";
    public const string Tarik = "dr-tarik-begic";
    public const string Mia = "dr-mia-delic";
    public const string Adnan = "dr-adnan-softic";

    private static int _contactCounter;

    /// <summary>
    /// Every booking gets its own e-mail and phone unless the test chooses them: one contact may only have a few
    /// upcoming appointments, so tests that book many slots must not look like the same person.
    /// </summary>
    public Task<BookingOutcome> BookAsync(string service, string? doctor, DateTime date, string time, string name = "Test Pacijent",
        string? phone = null, string? email = null)
    {
        var n = Interlocked.Increment(ref _contactCounter);
        phone ??= $"+3876{n:D7}";
        email ??= $"pacijent{n}@primjer.ba";
        var request = new BookingRequest(Svc(service).Id, doctor is null ? null : Doc(doctor).Id, date,
            TimeSpan.Parse(time), name, phone, email, null, true);
        return Service.BookAsync(request);
    }

    public async Task<Appointment> MustBookAsync(string service, string? doctor, DateTime date, string time)
    {
        var r = await BookAsync(service, doctor, date, time);
        Assert.True(r.Succeeded, r.Error);
        return r.Appointment!;
    }

    public async Task<List<string>> TimesAsync(string service, string? doctor, DateTime date) =>
        (await Service.GetSlotsAsync(Svc(service), doctor is null ? null : Doc(doctor).Id, date))
            .Select(s => s.Time.ToString(@"hh\:mm")).ToList();

    public void Dispose()
    {
        Db.Dispose();
        SqliteConnection.ClearAllPools();
        try { File.Delete(_file); } catch (IOException) { /* nije bitno ako ostane u temp folderu */ }
    }
}
