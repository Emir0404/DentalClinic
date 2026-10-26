using System.Net;
using System.Text.RegularExpressions;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DentalClinic.Tests;

public class LimitedClinicFactory : ClinicFactory
{
    protected override Dictionary<string, string?> ExtraSettings => new()
    {
        ["RateLimit:SubmitPer10Minutes"] = "3",
        ["RateLimit:LookupPer10Minutes"] = "3",
        ["RateLimit:LoginPerMinute"] = "3",
        ["RateLimit:ReadPerMinute"] = "5",
    };
}

public class NoAdminClinicFactory : ClinicFactory
{
    protected override Dictionary<string, string?> ExtraSettings => new() { ["Admin:Username"] = "", ["Admin:Password"] = "" };
}

/// <summary>A patient's own pages open with a long random token. The short reference (DS-K7M2PQ) opens nothing.</summary>
public class AccessTokenTests : IClassFixture<ClinicFactory>
{
    private readonly ClinicFactory _f;
    public AccessTokenTests(ClinicFactory f) => _f = f;

    private async Task<(HttpClient Client, string Token, string Reference)> BookAsync(int daysAhead, string? email = null)
    {
        var c = _f.NewClient();
        var date = Browser.Tuesday(daysAhead);
        var token = await Browser.BookAsync(c, "pregled", "dr-tarik-begic", date, await Browser.FirstFreeTimeAsync(c, "pregled", "dr-tarik-begic", date), email);
        return (c, token, await Browser.ReferenceOfAsync(c, token));
    }

    [Fact]
    public async Task The_confirmation_address_holds_a_long_random_token_that_differs_per_booking()
    {
        var (_, first, _) = await BookAsync(8);
        var (_, second, _) = await BookAsync(8);

        Assert.NotEqual(first, second);
        Assert.All(new[] { first, second }, t => Assert.Matches("^[A-Za-z0-9_-]{22,}$", t));
    }

    [Fact]
    public async Task The_short_reference_does_not_open_the_confirmation_calendar_or_cancel_pages()
    {
        var (c, token, reference) = await BookAsync(15);

        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/zakazivanje/potvrda/{reference}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/zakazivanje/kalendar/{reference}")).StatusCode);
        var cancel = await Browser.PostFormAsync(c, $"/zakazivanje/otkazi/{reference}", $"/zakazivanje/potvrda/{token}", new());
        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);

        // ...and the appointment is untouched.
        Assert.Contains("Otkaži termin", await c.GetStringAsync($"/zakazivanje/potvrda/{token}"));
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("DS-AAAAAA")]
    [InlineData("1")]
    public async Task A_guessed_token_gives_404_everywhere(string guess)
    {
        var c = _f.NewClient();

        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/zakazivanje/potvrda/{guess}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/zakazivanje/kalendar/{guess}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Browser.PostFormAsync(c, $"/zakazivanje/otkazi/{guess}", "/zakazivanje/moj-termin", new())).StatusCode);
    }

    [Fact]
    public async Task Cancelling_by_token_works_and_the_calendar_file_is_named_after_the_reference()
    {
        var (c, token, reference) = await BookAsync(22);

        var ics = await c.GetAsync($"/zakazivanje/kalendar/{token}");
        Assert.Contains(reference, ics.Content.Headers.ContentDisposition!.FileName);

        var cancel = await Browser.PostFormAsync(c, $"/zakazivanje/otkazi/{token}", $"/zakazivanje/potvrda/{token}", new());
        Assert.Equal($"/zakazivanje/potvrda/{token}", cancel.Headers.Location!.OriginalString);
        Assert.Contains("Termin je otkazan", await c.GetStringAsync($"/zakazivanje/potvrda/{token}"));
    }

    [Fact]
    public async Task Lookup_still_needs_the_matching_email_and_then_hands_out_the_token()
    {
        var (c, token, reference) = await BookAsync(29, "pronadji-me@primjer.ba");

        var wrong = await Browser.PostFormAsync(c, "/zakazivanje/moj-termin", "/zakazivanje/moj-termin", new() { ["reference"] = reference, ["email"] = "tudji@primjer.ba" });
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);

        var right = await Browser.PostFormAsync(c, "/zakazivanje/moj-termin", "/zakazivanje/moj-termin", new() { ["reference"] = reference, ["email"] = "pronadji-me@primjer.ba" });
        Assert.Equal($"/zakazivanje/potvrda/{token}", right.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task The_admin_still_finds_appointments_by_the_short_reference()
    {
        var (_, _, reference) = await BookAsync(36, "admin-trazi@primjer.ba");
        var admin = _f.NewClient();
        await Browser.LoginAsync(admin);

        Assert.Contains(reference, await admin.GetStringAsync($"/admin/termini?q={reference}"));
    }

    [Fact]
    public void References_come_from_the_cryptographic_generator_in_the_expected_format()
    {
        var refs = Enumerable.Range(0, 2000).Select(_ => AppointmentService.NewReference()).ToList();

        Assert.All(refs, r => Assert.Matches("^DS-[A-HJKMNP-Z2-9]{6}$", r));
        Assert.True(refs.Distinct().Count() > 1990);   // 31^6 possibilities: repeats in 2000 draws would be a red flag
    }
}

/// <summary>One person (same e-mail or same phone) cannot reserve the whole calendar.</summary>
public class ContactLimitTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();

    private static readonly string[] Times = ["08:00", "08:30", "09:00", "09:30", "10:00"];

    private Task<BookingOutcome> Book(int slot, string email, string phone) =>
        _t.BookAsync("pregled", TestDb.Adnan, TestDb.Thu, Times[slot], email: email, phone: phone);

    [Fact]
    public async Task The_same_email_may_hold_three_upcoming_appointments_but_not_four()
    {
        for (var i = 0; i < ClinicRules.MaxActiveBookingsPerContact; i++)
            Assert.True((await Book(i, "ista@primjer.ba", $"+3876100000{i}")).Succeeded);

        var fourth = await Book(3, "ista@primjer.ba", "+38761000009");

        Assert.False(fourth.Succeeded);
        Assert.Contains("već je zakazano 3 termina", fourth.Error);
        Assert.Equal(3, await _t.Db.Appointments.CountAsync());
    }

    [Fact]
    public async Task Email_comparison_ignores_letter_case()
    {
        for (var i = 0; i < 3; i++) await Book(i, "Ista@Primjer.BA", $"+3876200000{i}");

        Assert.False((await Book(3, "ista@primjer.ba", "+38762000009")).Succeeded);
    }

    [Fact]
    public async Task The_same_phone_with_different_emails_is_also_limited_whatever_the_formatting()
    {
        await Book(0, "a@primjer.ba", "+387 61 111-222");
        await Book(1, "b@primjer.ba", "+38761111222");
        await Book(2, "c@primjer.ba", "+387(61)111 222");

        var fourth = await Book(3, "d@primjer.ba", "+38761 111 222");

        Assert.False(fourth.Succeeded);
    }

    [Fact]
    public async Task Other_people_are_not_affected()
    {
        for (var i = 0; i < 3; i++) await Book(i, "zauzet@primjer.ba", $"+3876300000{i}");

        Assert.True((await Book(3, "slobodan@primjer.ba", "+38763999999")).Succeeded);
    }

    [Fact]
    public async Task Cancelled_appointments_free_the_place()
    {
        var first = (await Book(0, "otkazi@primjer.ba", "+38764000000")).Appointment!;
        await Book(1, "otkazi@primjer.ba", "+38764000001");
        await Book(2, "otkazi@primjer.ba", "+38764000002");
        Assert.False((await Book(3, "otkazi@primjer.ba", "+38764000003")).Succeeded);

        await _t.Service.CancelAsync(first.Reference, byStaff: true);

        Assert.True((await Book(3, "otkazi@primjer.ba", "+38764000003")).Succeeded);
    }

    [Fact]
    public async Task Appointments_in_the_past_do_not_count()
    {
        // three earlier appointments, written directly because the site refuses to book in the past
        var past = _t.Clock.Now.AddDays(-7);
        for (var i = 0; i < 3; i++)
            _t.Db.Appointments.Add(new Appointment
            {
                Reference = $"DS-PAST{i}2", ServiceId = _t.Svc("pregled").Id, DoctorId = _t.Doc(TestDb.Adnan).Id,
                Start = past.AddHours(i), DurationMinutes = 30, PatientName = "Stari Pacijent",
                Phone = "+38765000000", Email = "stari@primjer.ba", CreatedAt = past,
            });
        await _t.Db.SaveChangesAsync();

        Assert.True((await Book(0, "stari@primjer.ba", "+38765000000")).Succeeded);
    }
}

public class RateLimitTests
{
    private static Dictionary<string, string> EmptyBookingForm() => new() { ["Service"] = "", ["Consent"] = "false" };

    [Fact]
    public async Task Booking_and_contact_share_one_limit_then_answer_429_with_retry_after()
    {
        using var factory = new LimitedClinicFactory();
        var c = factory.NewClient();

        var statuses = new List<HttpStatusCode>();
        statuses.Add((await Browser.PostFormAsync(c, "/zakazivanje", "/zakazivanje", EmptyBookingForm())).StatusCode);
        statuses.Add((await Browser.PostFormAsync(c, "/kontakt", "/kontakt", new() { ["consent"] = "false" })).StatusCode);
        statuses.Add((await Browser.PostFormAsync(c, "/zakazivanje", "/zakazivanje", EmptyBookingForm())).StatusCode);
        var blocked = await Browser.PostFormAsync(c, "/kontakt", "/kontakt", new() { ["consent"] = "false" });

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.OK, s));   // forms shown again with validation messages
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.Contains("Retry-After"));
        Assert.Contains("Previše pokušaja", await blocked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Finding_and_cancelling_appointments_is_limited_so_codes_cannot_be_tried_in_bulk()
    {
        using var factory = new LimitedClinicFactory();
        var c = factory.NewClient();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await Browser.PostFormAsync(c, "/zakazivanje/moj-termin", "/zakazivanje/moj-termin",
                new() { ["reference"] = $"DS-GUESS{i}", ["email"] = "x@y.ba" })).StatusCode);

        var next = await Browser.PostFormAsync(c, "/zakazivanje/moj-termin", "/zakazivanje/moj-termin", new() { ["reference"] = "DS-GUESS9", ["email"] = "x@y.ba" });
        var cancel = await Browser.PostFormAsync(c, "/zakazivanje/otkazi/nesto", "/zakazivanje/moj-termin", new());

        Assert.Equal(HttpStatusCode.TooManyRequests, next.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, cancel.StatusCode);   // cancel shares the limit
    }

    [Fact]
    public async Task Admin_password_guessing_is_stopped_even_for_the_correct_password_afterwards()
    {
        using var factory = new LimitedClinicFactory();
        var c = factory.NewClient();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await Browser.PostFormAsync(c, "/admin/login", "/admin/login",
                new() { ["username"] = ClinicFactory.AdminUser, ["password"] = $"pogresno-{i}" })).StatusCode);

        var correct = await Browser.PostFormAsync(c, "/admin/login", "/admin/login",
            new() { ["username"] = ClinicFactory.AdminUser, ["password"] = ClinicFactory.AdminPass });

        Assert.Equal(HttpStatusCode.TooManyRequests, correct.StatusCode);
    }

    [Fact]
    public async Task The_json_the_booking_form_loads_is_limited_but_normal_pages_are_not()
    {
        using var factory = new LimitedClinicFactory();
        var c = factory.NewClient();

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/zakazivanje/doktori?usluga=pregled")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await c.GetAsync("/zakazivanje/doktori?usluga=pregled")).StatusCode);

        for (var i = 0; i < 30; i++)
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/")).StatusCode);
    }
}

public class HardeningTests : IClassFixture<ClinicFactory>
{
    private readonly ClinicFactory _f;
    public HardeningTests(ClinicFactory f) => _f = f;

    [Fact]
    public async Task The_content_security_policy_forbids_inline_scripts_and_foreign_origins()
    {
        var r = await _f.NewClient().GetAsync("/");

        var csp = r.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self';", csp);               // no 'unsafe-inline' for scripts
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("object-src 'none'", csp);
        Assert.Contains("form-action 'self'", csp);
        Assert.DoesNotMatch(@"script-src[^;]*unsafe", csp);
    }

    [Fact]
    public void No_page_contains_inline_event_handlers_or_inline_scripts_which_the_policy_would_block()
    {
        var views = FindViewsFolder();
        var offenders = new List<string>();
        foreach (var file in Directory.EnumerateFiles(views, "*.cshtml", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (Regex.IsMatch(text, @"\son(click|submit|change|input|load|error|focus|blur|keyup|keydown|mouse\w+)\s*=", RegexOptions.IgnoreCase))
                offenders.Add($"{Path.GetRelativePath(views, file)}: inline event handler");
            if (Regex.IsMatch(text, @"<script(?![^>]*\bsrc=)(?![^>]*ld\+json)[^>]*>", RegexOptions.IgnoreCase))
                offenders.Add($"{Path.GetRelativePath(views, file)}: inline <script>");
            if (text.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
                offenders.Add($"{Path.GetRelativePath(views, file)}: javascript: link");
        }
        Assert.Empty(offenders);
    }

    [Theory]
    [InlineData("/zakazivanje", "/zakazivanje")]
    [InlineData("/zakazivanje/moj-termin", "/zakazivanje/moj-termin")]
    [InlineData("/kontakt", "/kontakt")]
    [InlineData("/admin/login", "/admin/login")]
    public async Task A_post_without_an_antiforgery_token_is_refused(string page, string post)
    {
        var c = _f.NewClient();
        await c.GetStringAsync(page);   // gets the antiforgery cookie but we deliberately do not send the form token

        var r = await c.PostAsync(post, new FormUrlEncodedContent([new("x", "y")]));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task The_site_script_files_are_served_and_the_behaviour_attributes_are_in_the_pages()
    {
        var c = _f.NewClient();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/js/ui.js")).StatusCode);

        var date = Browser.Tuesday(8);
        var token = await Browser.BookAsync(c, "pregled", "dr-tarik-begic", date, await Browser.FirstFreeTimeAsync(c, "pregled", "dr-tarik-begic", date));
        var page = await c.GetStringAsync($"/zakazivanje/potvrda/{token}");

        Assert.Contains("data-print", page);
        Assert.Contains("data-confirm=\"Sigurno želite otkazati termin?\"", page);
        Assert.Contains("/js/ui.js", page);
    }

    private static string FindViewsFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "DentalClinic", "Views"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "DentalClinic", "Views");
    }
}

public class AdminDisabledTests : IClassFixture<NoAdminClinicFactory>
{
    private readonly NoAdminClinicFactory _f;
    public AdminDisabledTests(NoAdminClinicFactory f) => _f = f;

    [Fact]
    public async Task With_no_admin_configured_the_login_page_does_not_reveal_that_it_is_switched_off()
    {
        var c = _f.NewClient();

        var r = await Browser.PostFormAsync(c, "/admin/login", "/admin/login", new() { ["username"] = "admin", ["password"] = "admin123" });

        var html = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Pogrešno korisničko ime ili lozinka", html);
        Assert.DoesNotContain("nije podešen", html);
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/admin")).StatusCode);
    }
}

/// <summary>Databases made before the access token existed must upgrade without losing or duplicating anything.</summary>
public class MigrationUpgradeTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"clinic-upgrade-{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_file); } catch (IOException) { }
    }

    [Fact]
    public async Task Existing_appointments_each_get_their_own_token_when_the_database_is_upgraded()
    {
        var options = new DbContextOptionsBuilder<ClinicDb>().UseSqlite($"Data Source={_file}").Options;
        await using (var old = new ClinicDb(options))
        {
            // a database as it was before the token: only the first migration applied
            var migrator = old.GetService<IMigrator>();
            var first = old.GetService<IMigrationsAssembly>().Migrations.Keys.Order().First();
            await migrator.MigrateAsync(first);

            await old.Database.ExecuteSqlRawAsync("""
                INSERT INTO Services (Id, Slug, Name, Category, Summary, Description, Steps, Aftercare, SuitableFor, DurationMinutes, PriceFrom, Comfort, ImageName, IsActive, SortOrder)
                VALUES (1, 's', 'Usluga', 'k', 's', 'd', 's', 'a', 'f', 30, 10, 'c', 'img', 1, 1);
                INSERT INTO Doctors (Id, Slug, Name, Title, Bio, PhotoName, YearsExperience, IsActive, SortOrder)
                VALUES (1, 'dr', 'Dr. Test', 'T', 'b', 'p', 5, 1, 1);
                """);
            for (var i = 1; i <= 3; i++)
            {
                var reference = $"DS-OLD{i}22";
                var start = $"2030-01-0{i} 09:00:00";
                await old.Database.ExecuteSqlAsync($"""
                    INSERT INTO Appointments (Id, Reference, ServiceId, DoctorId, Start, DurationMinutes, PatientName, Phone, Email, FirstVisit, Status, CreatedAt)
                    VALUES ({i}, {reference}, 1, 1, {start}, 30, 'Stari', '+38761000000', 'stari@primjer.ba', 1, 'Scheduled', '2029-12-01 10:00:00')
                    """);
            }
        }

        await using (var upgraded = new ClinicDb(options))
        {
            await upgraded.Database.MigrateAsync();

            var tokens = await upgraded.Appointments.AsNoTracking().Select(a => a.AccessToken).ToListAsync();
            Assert.Equal(3, tokens.Count);
            Assert.All(tokens, t => Assert.True(t.Length >= 20, "token missing after upgrade"));
            Assert.Equal(3, tokens.Distinct().Count());
        }
    }

    [Fact]
    public async Task A_database_made_before_migrations_existed_is_adopted_and_upgraded_without_losing_data()
    {
        var options = new DbContextOptionsBuilder<ClinicDb>().UseSqlite($"Data Source={_file}").Options;
        await using (var old = new ClinicDb(options))
        {
            // what the first versions produced: the tables of the first migration, but no migration history (they used EnsureCreated)
            var first = old.GetService<IMigrationsAssembly>().Migrations.Keys.Order().First();
            await old.GetService<IMigrator>().MigrateAsync(first);
            await old.Database.ExecuteSqlRawAsync("DROP TABLE __EFMigrationsHistory");
            await old.Database.ExecuteSqlRawAsync("""
                INSERT INTO Services (Id, Slug, Name, Category, Summary, Description, Steps, Aftercare, SuitableFor, DurationMinutes, PriceFrom, Comfort, ImageName, IsActive, SortOrder)
                VALUES (1, 's', 'Stara usluga', 'k', 's', 'd', 's', 'a', 'f', 30, 10, 'c', 'img', 1, 1);
                """);
        }

        await using (var upgraded = new ClinicDb(options))
        {
            await upgraded.MigrateAsync();   // without the adoption step this fails with "table already exists"

            Assert.Equal("Stara usluga", (await upgraded.Services.AsNoTracking().SingleAsync()).Name);
            Assert.Empty(await upgraded.Database.GetPendingMigrationsAsync());
            Assert.Equal(0, await upgraded.Appointments.CountAsync());   // the new AccessToken column exists
        }
    }

    [Fact]
    public async Task A_fresh_database_gets_the_full_schema_through_migrations_alone()
    {
        var options = new DbContextOptionsBuilder<ClinicDb>().UseSqlite($"Data Source={_file}").Options;
        await using var db = new ClinicDb(options);

        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.True(await db.Database.CanConnectAsync());
        Assert.Equal(0, await db.Appointments.CountAsync());
    }

    [Fact]
    public async Task The_model_and_the_migrations_agree_so_no_forgotten_migration_is_waiting()
    {
        var options = new DbContextOptionsBuilder<ClinicDb>().UseSqlite($"Data Source={_file}").Options;
        await using var db = new ClinicDb(options);

        // false = the model snapshot of the last migration equals the current entity classes
        Assert.False(db.Database.HasPendingModelChanges());
    }
}

/// <summary>The site must never show a person again: only photos that are known to contain nobody may be fetched.</summary>
public class PhotoPolicyTests
{
    private static readonly string[] ApprovedPhotosWithoutPeople =
        ["clinic-bright", "clinic-chair", "waiting-room", "implant", "braces-model", "toothbrush-bamboo", "toothbrush-red"];

    [Fact]
    public void Only_photos_of_empty_rooms_and_objects_are_downloaded_by_the_image_script()
    {
        var tsv = Path.Combine(FindRoot(), "tools", "images.tsv");

        var names = File.ReadAllLines(tsv).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Split('\t')[0]).ToList();

        Assert.Equal(ApprovedPhotosWithoutPeople.Order(), names.Order());
    }

    [Fact]
    public void Every_picture_the_pages_use_exists()
    {
        var img = Path.Combine(FindRoot(), "src", "DentalClinic", "wwwroot", "img");
        var generated = new[] { "hero", "kids", "kids-2", "team-work", "consult", "smile-1", "smile-2", "smile-3", "smile-4", "braces-girl", "aligner",
                                "aligner-case", "veneers", "xray", "exam", "tools", "cleaning", "extraction", "brushing",
                                "doctor-lejla", "doctor-tarik", "doctor-mia", "doctor-adnan" };

        Assert.All(generated.Concat(ApprovedPhotosWithoutPeople), n => Assert.True(File.Exists(Path.Combine(img, n + ".jpg")), $"{n}.jpg missing"));
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tools", "images.tsv"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
