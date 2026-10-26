using System.Net;
using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace DentalClinic.Tests;

/// <summary>Pokreće cijelu aplikaciju u memoriji, s privremenom bazom i poznatom admin lozinkom.</summary>
public class ClinicFactory : WebApplicationFactory<Program>
{
    public const string AdminUser = "tester";
    public const string AdminPass = "test-lozinka-123";
    private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"clinic-web-{Guid.NewGuid():N}.db");
    /// <summary>Otpremljene fotografije idu u temp folder, ne u pravi wwwroot projekta.</summary>
    public string UploadsDir { get; } = Path.Combine(Path.GetTempPath(), $"clinic-uploads-{Guid.NewGuid():N}");

    /// <summary>Settings a special factory can change (e.g. very low rate limits). Applied after the defaults.</summary>
    protected virtual Dictionary<string, string?> ExtraSettings => [];

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Clinic"] = $"Data Source={_dbFile}",
                ["SeedDemoData"] = "false",
                ["UploadsPath"] = UploadsDir,
                ["Admin:Username"] = AdminUser,
                ["Admin:Password"] = AdminPass,
                // the many test visitors all come from the same address, so the normal limits must not apply to them
                ["RateLimit:SubmitPer10Minutes"] = "100000",
                ["RateLimit:LookupPer10Minutes"] = "100000",
                ["RateLimit:LoginPerMinute"] = "100000",
                ["RateLimit:ReadPerMinute"] = "100000",
            };
            foreach (var (key, value) in ExtraSettings) settings[key] = value;
            cfg.AddInMemoryCollection(settings);
        });

    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbFile); } catch (IOException) { /* nije bitno */ }
        try { if (Directory.Exists(UploadsDir)) Directory.Delete(UploadsDir, true); } catch (IOException) { /* nije bitno */ }
    }
}

/// <summary>Pomoćne metode da testovi liče na ponašanje stvarnog posjetioca.</summary>
public static class Browser
{
    private static readonly Regex Token = new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

    public static async Task<string> TokenFromAsync(HttpClient c, string url)
    {
        var html = await c.GetStringAsync(url);
        var m = Token.Match(html);
        Assert.True(m.Success, $"Na {url} nema antiforgery tokena.");
        return m.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> PostFormAsync(HttpClient c, string url, string tokenPage, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = await TokenFromAsync(c, tokenPage);
        return await c.PostAsync(url, new FormUrlEncodedContent(fields));
    }

    public static async Task LoginAsync(HttpClient c)
    {
        var r = await PostFormAsync(c, "/admin/login", "/admin/login", new() { ["username"] = ClinicFactory.AdminUser, ["password"] = ClinicFactory.AdminPass });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
    }

    /// <summary>Prvi utorak najmanje <paramref name="daysAhead"/> dana unaprijed (utorkom rade sva četiri doktora osim Mie).</summary>
    public static DateTime Tuesday(int daysAhead)
    {
        var d = DateTime.Today.AddDays(daysAhead);
        while (d.DayOfWeek != DayOfWeek.Tuesday) d = d.AddDays(1);
        return d;
    }

    private static int _contactCounter;

    /// <summary>Without an explicit e-mail each form gets its own e-mail and phone (one contact may only hold a few upcoming appointments).</summary>
    public static Dictionary<string, string> BookingForm(string service, string doctor, DateTime date, string time, string? email = null, string? phone = null)
    {
        var n = Interlocked.Increment(ref _contactCounter);
        return new()
        {
            ["Service"] = service, ["Doctor"] = doctor, ["Date"] = date.ToString("yyyy-MM-dd"), ["Time"] = time,
            ["PatientName"] = "Ana Test", ["Phone"] = phone ?? $"+3876{n:D7}", ["Email"] = email ?? $"pacijent{n}@primjer.ba", ["Notes"] = "",
            ["FirstVisit"] = "true", ["Consent"] = "true",
        };
    }

    public static async Task<JsonElement> JsonAsync(HttpClient c, string url) =>
        JsonDocument.Parse(await c.GetStringAsync(url)).RootElement;

    public static async Task<string> FirstFreeTimeAsync(HttpClient c, string service, string doctor, DateTime date)
    {
        var slots = await JsonAsync(c, $"/zakazivanje/termini?usluga={service}&doktor={doctor}&datum={date:yyyy-MM-dd}");
        Assert.True(slots.GetArrayLength() > 0, $"nema slobodnih termina {date:yyyy-MM-dd}");
        return slots[0].GetProperty("time").GetString()!;
    }

    /// <summary>Zakaže termin kroz formular i vrati pristupni token iz adrese potvrde (ne kratku šifru).</summary>
    public static async Task<string> BookAsync(HttpClient c, string service, string doctor, DateTime date, string time, string? email = null)
    {
        var r = await PostFormAsync(c, "/zakazivanje", "/zakazivanje", BookingForm(service, doctor, date, time, email));
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var token = r.Headers.Location!.OriginalString.Split('/').Last();
        Assert.True(token.Length >= 20, $"Adresa potvrde treba sadržavati dugi token, a ne šifru: {token}");
        Assert.DoesNotContain("DS-", token);
        return token;
    }

    /// <summary>Kratka šifra (DS-XXXXXX) koju pacijent vidi na stranici potvrde.</summary>
    public static async Task<string> ReferenceOfAsync(HttpClient c, string token)
    {
        var page = await c.GetStringAsync($"/zakazivanje/potvrda/{token}");
        var m = Regex.Match(page, @"DS-[A-HJKMNP-Z2-9]{6}");
        Assert.True(m.Success, "Na potvrdi nema šifre termina.");
        return m.Value;
    }
}

public class PublicSiteTests : IClassFixture<ClinicFactory>
{
    private readonly ClinicFactory _f;
    public PublicSiteTests(ClinicFactory f) => _f = f;

    [Theory]
    [InlineData("/")]
    [InlineData("/usluge")]
    [InlineData("/usluge/implantati")]
    [InlineData("/usluge/hitni-slucaj")]
    [InlineData("/tim")]
    [InlineData("/cjenovnik")]
    [InlineData("/savjeti")]
    [InlineData("/savjeti?category=Djeca")]
    [InlineData("/savjeti/kako-pravilno-prati-zube")]
    [InlineData("/pitanja")]
    [InlineData("/kontakt")]
    [InlineData("/privatnost")]
    [InlineData("/zakazivanje")]
    [InlineData("/zakazivanje?usluga=plombe")]
    [InlineData("/zakazivanje/moj-termin")]
    public async Task Public_pages_load(string url) =>
        Assert.Equal(HttpStatusCode.OK, (await _f.NewClient().GetAsync(url)).StatusCode);

    [Theory]
    [InlineData("/usluge/nema-takve")]
    [InlineData("/savjeti/nema-takav")]
    [InlineData("/nesto/sto-ne-postoji")]
    public async Task Unknown_addresses_give_a_friendly_404(string url)
    {
        var r = await _f.NewClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Contains("Ovu stranicu ne možemo naći", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Home_page_has_title_description_and_structured_data_for_search_engines()
    {
        var html = await _f.NewClient().GetStringAsync("/");

        Assert.Contains("<title>", html);
        Assert.Contains("name=\"description\"", html);
        Assert.Contains("\"@type\": \"Dentist\"", html);
        Assert.Contains("č", html);                       // znakovi se ne ispisuju kao &#x10D;
        Assert.DoesNotContain("&#x10D;", html);
        Assert.DoesNotContain("&#x161;", html);
    }

    [Fact]
    public async Task Service_page_shows_steps_price_and_the_book_button()
    {
        var html = await _f.NewClient().GetStringAsync("/usluge/izbjeljivanje");

        Assert.Contains("Kako izgleda zahvat", html);
        Assert.Contains("od 350 KM", html);
        Assert.Contains("/zakazivanje?usluga=izbjeljivanje", html);
        Assert.Contains("Dr. Lejla Kovačević", html);          // samo ona radi izbjeljivanje
    }

    [Fact]
    public async Task Robots_and_sitemap_hide_admin_and_list_pages()
    {
        var c = _f.NewClient();
        var robots = await c.GetStringAsync("/robots.txt");
        var sitemap = await c.GetStringAsync("/sitemap.xml");

        Assert.Contains("Disallow: /admin", robots);
        Assert.Contains("/usluge/implantati", sitemap);
        Assert.Contains("/savjeti/kako-pravilno-prati-zube", sitemap);
        Assert.DoesNotContain("/admin", sitemap);
    }

    [Fact]
    public async Task Security_headers_are_present()
    {
        var r = await _f.NewClient().GetAsync("/");
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task Static_files_are_cached_by_the_browser()
    {
        var r = await _f.NewClient().GetAsync("/css/site.css");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("max-age=604800", r.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Contact_form_saves_the_message_and_requires_consent()
    {
        var c = _f.NewClient();
        var noConsent = await Browser.PostFormAsync(c, "/kontakt", "/kontakt", new() { ["Name"] = "Jasna", ["Email"] = "jasna@primjer.ba", ["Message"] = "Pitanje o cijeni." });
        Assert.Equal(HttpStatusCode.OK, noConsent.StatusCode);
        Assert.Contains("potrebna je saglasnost", await noConsent.Content.ReadAsStringAsync());

        var ok = await Browser.PostFormAsync(c, "/kontakt", "/kontakt", new() { ["Name"] = "Jasna", ["Email"] = "jasna@primjer.ba", ["Message"] = "Pitanje o cijeni.", ["consent"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        Assert.Contains("Poruka je poslana", await c.GetStringAsync("/kontakt"));
    }

    [Fact]
    public async Task Contact_form_rejects_empty_and_invalid_input()
    {
        var r = await Browser.PostFormAsync(_f.NewClient(), "/kontakt", "/kontakt",
            new() { ["Name"] = "", ["Email"] = "nije-email", ["Message"] = "", ["consent"] = "true" });

        var html = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Unesite ime.", html);
        Assert.Contains("Email adresa nije ispravna.", html);
        Assert.Contains("Napišite poruku.", html);
    }
}

public class BookingFlowTests : IClassFixture<ClinicFactory>
{
    private readonly ClinicFactory _f;
    public BookingFlowTests(ClinicFactory f) => _f = f;

    [Fact]
    public async Task Doctors_endpoint_lists_only_doctors_who_do_the_service()
    {
        var doctors = await Browser.JsonAsync(_f.NewClient(), "/zakazivanje/doktori?usluga=ortodoncija");

        Assert.Equal(1, doctors.GetArrayLength());
        Assert.Equal("dr-mia-delic", doctors[0].GetProperty("slug").GetString());
    }

    [Fact]
    public async Task Days_endpoint_returns_the_requested_number_of_days_with_free_counts()
    {
        var days = await Browser.JsonAsync(_f.NewClient(), $"/zakazivanje/dani?usluga=pregled&doktor=any&od={DateTime.Today:yyyy-MM-dd}&broj=14");

        Assert.Equal(14, days.GetArrayLength());
        Assert.Contains(days.EnumerateArray(), d => d.GetProperty("free").GetInt32() > 0);
        Assert.Contains(days.EnumerateArray(), d => d.GetProperty("free").GetInt32() == 0);     // nedjelja
    }

    [Fact]
    public async Task Slots_and_days_endpoints_return_404_for_unknown_service_or_doctor()
    {
        var c = _f.NewClient();
        var date = Browser.Tuesday(10);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/zakazivanje/termini?usluga=nema&doktor=any&datum={date:yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/zakazivanje/termini?usluga=pregled&doktor=nema&datum={date:yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/zakazivanje/dani?usluga=nema")).StatusCode);
    }

    [Fact]
    public async Task Full_flow_book_view_confirmation_download_calendar_and_cancel()
    {
        var c = _f.NewClient();
        var date = Browser.Tuesday(7);
        var time = await Browser.FirstFreeTimeAsync(c, "pregled", "dr-tarik-begic", date);

        var reference = await Browser.BookAsync(c, "pregled", "dr-tarik-begic", date, time);   // token

        var page = await c.GetStringAsync($"/zakazivanje/potvrda/{reference}");
        Assert.Contains(await Browser.ReferenceOfAsync(c, reference), page);
        Assert.Contains("Dr. Tarik Begić", page);
        Assert.Contains("Pregled i savjetovanje", page);
        Assert.Contains("Otkaži termin", page);

        var ics = await c.GetAsync($"/zakazivanje/kalendar/{reference}");
        Assert.Equal("text/calendar", ics.Content.Headers.ContentType!.MediaType);
        Assert.Contains("BEGIN:VCALENDAR", await ics.Content.ReadAsStringAsync());

        var cancel = await Browser.PostFormAsync(c, $"/zakazivanje/otkazi/{reference}", $"/zakazivanje/potvrda/{reference}", new());
        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        Assert.Contains("Termin je otkazan", await c.GetStringAsync($"/zakazivanje/potvrda/{reference}"));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/zakazivanje/kalendar/{reference}")).StatusCode);
    }

    [Fact]
    public async Task Booked_slot_disappears_from_the_slot_list_and_cannot_be_booked_again()
    {
        var c = _f.NewClient();
        var date = Browser.Tuesday(14);
        var time = await Browser.FirstFreeTimeAsync(c, "pregled", "dr-tarik-begic", date);
        await Browser.BookAsync(c, "pregled", "dr-tarik-begic", date, time);

        var slots = await Browser.JsonAsync(c, $"/zakazivanje/termini?usluga=pregled&doktor=dr-tarik-begic&datum={date:yyyy-MM-dd}");
        Assert.DoesNotContain(slots.EnumerateArray(), s => s.GetProperty("time").GetString() == time);

        var again = await Browser.PostFormAsync(c, "/zakazivanje", "/zakazivanje", Browser.BookingForm("pregled", "dr-tarik-begic", date, time, "drugi@primjer.ba"));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var html = await again.Content.ReadAsStringAsync();
        Assert.Contains("više nije slobodan", html);
        Assert.Contains("drugi@primjer.ba", html);              // forma zadržava unesene podatke
    }

    [Fact]
    public async Task Missing_consent_and_empty_fields_show_errors_and_nothing_is_saved()
    {
        var c = _f.NewClient();
        var date = Browser.Tuesday(21);
        var time = await Browser.FirstFreeTimeAsync(c, "pregled", "dr-tarik-begic", date);
        var form = Browser.BookingForm("pregled", "dr-tarik-begic", date, time);
        form.Remove("Consent");
        form["PatientName"] = "";
        form["Email"] = "nije-email";

        var r = await Browser.PostFormAsync(c, "/zakazivanje", "/zakazivanje", form);

        var html = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Unesite ime i prezime.", html);
        Assert.Contains("Email adresa nije ispravna.", html);
        Assert.Contains("je potrebna saglasnost", html);
        var slots = await Browser.JsonAsync(c, $"/zakazivanje/termini?usluga=pregled&doktor=dr-tarik-begic&datum={date:yyyy-MM-dd}");
        Assert.Contains(slots.EnumerateArray(), s => s.GetProperty("time").GetString() == time);       // termin je i dalje slobodan
    }

    [Fact]
    public async Task Tampered_time_outside_working_hours_is_refused()
    {
        var c = _f.NewClient();
        var r = await Browser.PostFormAsync(c, "/zakazivanje", "/zakazivanje",
            Browser.BookingForm("pregled", "dr-tarik-begic", Browser.Tuesday(28), "03:00"));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("više nije slobodan", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Booking_without_antiforgery_token_is_rejected()
    {
        var r = await _f.NewClient().PostAsync("/zakazivanje",
            new FormUrlEncodedContent(Browser.BookingForm("pregled", "any", Browser.Tuesday(10), "10:00")));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task First_free_doctor_option_works_through_the_form()
    {
        var c = _f.NewClient();
        var date = Browser.Tuesday(35);
        var time = await Browser.FirstFreeTimeAsync(c, "pregled", "any", date);

        var reference = await Browser.BookAsync(c, "pregled", "any", date, time);

        Assert.Contains("Dr. ", await c.GetStringAsync($"/zakazivanje/potvrda/{reference}"));
    }

    [Fact]
    public async Task Lookup_needs_the_reference_and_the_matching_email_in_any_letter_case()
    {
        var c = _f.NewClient();
        var date = Browser.Tuesday(42);
        var token = await Browser.BookAsync(c, "pregled", "dr-adnan-softic", date, await Browser.FirstFreeTimeAsync(c, "pregled", "dr-adnan-softic", date), "Vlasnik@Primjer.ba");
        var reference = await Browser.ReferenceOfAsync(c, token);

        var wrong = await Browser.PostFormAsync(c, "/zakazivanje/moj-termin", "/zakazivanje/moj-termin", new() { ["reference"] = reference, ["email"] = "drugi@primjer.ba" });
        Assert.Contains("Termin nije pronađen", await wrong.Content.ReadAsStringAsync());

        var right = await Browser.PostFormAsync(c, "/zakazivanje/moj-termin", "/zakazivanje/moj-termin", new() { ["reference"] = $" {reference.ToLower()} ", ["email"] = "vlasnik@primjer.ba" });
        Assert.Equal(HttpStatusCode.Redirect, right.StatusCode);
        Assert.Equal($"/zakazivanje/potvrda/{token}", right.Headers.Location!.OriginalString);   // vodi na token, ne na šifru
    }

    [Fact]
    public async Task Appointment_less_than_24_hours_away_cannot_be_cancelled_on_the_site()
    {
        // termin za 3 sata upisujemo direktno u bazu: preko sajta se ne može zakazati ni ranije od 2 sata, ni u prošlosti
        string reference;
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ClinicDb>();
            reference = "hitan-slucaj-token-0001";   // adresa termina je token, ne šifra
            db.Appointments.Add(new Appointment
            {
                Reference = "DS-HITN22", AccessToken = reference, ServiceId = 1, DoctorId = 1, Start = DateTime.Now.AddHours(3), DurationMinutes = 30,
                PatientName = "Hitan Slučaj", Phone = "+38761111222", Email = "hitno@primjer.ba", CreatedAt = DateTime.UtcNow,
            });
            db.SaveChanges();
        }
        var c = _f.NewClient();

        var r = await Browser.PostFormAsync(c, $"/zakazivanje/otkazi/{reference}", "/zakazivanje/moj-termin", new());

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var page = await c.GetStringAsync($"/zakazivanje/potvrda/{reference}");
        Assert.Contains("manje od 24 sata", page);
        Assert.DoesNotContain("Termin je otkazan", page);
        Assert.DoesNotContain("Otkaži termin", page);               // dugme se uopšte ne prikazuje
    }

    [Fact]
    public async Task Cancelling_an_unknown_appointment_gives_404()
    {
        var r = await Browser.PostFormAsync(_f.NewClient(), "/zakazivanje/otkazi/nepostojeci-token-xxxxxxxxxx", "/zakazivanje/moj-termin", new());
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }
}

public class AdminPanelTests : IClassFixture<ClinicFactory>
{
    private readonly ClinicFactory _f;
    public AdminPanelTests(ClinicFactory f) => _f = f;

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/raspored")]
    [InlineData("/admin/termini")]
    [InlineData("/admin/termini/1")]
    [InlineData("/admin/usluge")]
    [InlineData("/admin/usluge/1")]
    [InlineData("/admin/doktori")]
    [InlineData("/admin/doktori/1")]
    [InlineData("/admin/slobodni-dani")]
    [InlineData("/admin/poruke")]
    [InlineData("/admin/export")]
    public async Task Anonymous_visitors_are_sent_to_login(string url)
    {
        var r = await _f.NewClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.StartsWith("/admin/login", r.Headers.Location!.OriginalString.Replace("http://localhost", ""));
    }

    [Fact]
    public async Task Admin_actions_without_login_cannot_change_anything()
    {
        var c = _f.NewClient();
        var token = await Browser.TokenFromAsync(c, "/kontakt");
        var r = await c.PostAsync("/admin/usluge/1", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["Name"] = "Hakovano", ["Summary"] = "x", ["DurationMinutes"] = "30", ["PriceFrom"] = "1",
        }));

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);                              // preusmjeren na prijavu
        Assert.DoesNotContain("Hakovano", await c.GetStringAsync("/usluge"));
    }

    [Fact]
    public async Task Wrong_password_does_not_log_in()
    {
        var c = _f.NewClient();
        var r = await Browser.PostFormAsync(c, "/admin/login", "/admin/login", new() { ["username"] = ClinicFactory.AdminUser, ["password"] = "pogresno" });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Pogrešno korisničko ime ili lozinka", await r.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Login_opens_the_dashboard_and_logout_closes_it_again()
    {
        var c = _f.NewClient();
        await Browser.LoginAsync(c);

        var dash = await c.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.OK, dash.StatusCode);
        Assert.Contains("Zauzetost narednih 7 dana", await dash.Content.ReadAsStringAsync());

        await Browser.PostFormAsync(c, "/admin/logout", "/admin", new());
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Login_never_redirects_to_another_website()
    {
        var r = await Browser.PostFormAsync(_f.NewClient(), "/admin/login", "/admin/login", new()
        {
            ["username"] = ClinicFactory.AdminUser, ["password"] = ClinicFactory.AdminPass, ["returnUrl"] = "https://zlobnik.example/phishing",
        });

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.DoesNotContain("zlobnik", r.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Admin_pages_all_render_for_a_logged_in_user()
    {
        var c = _f.NewClient();
        await Browser.LoginAsync(c);
        foreach (var url in new[] { "/admin", "/admin/raspored", "/admin/raspored?date=2026-10-20", "/admin/termini", "/admin/usluge", "/admin/usluge/1",
                                    "/admin/doktori", "/admin/doktori/1", "/admin/slobodni-dani", "/admin/poruke" })
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/admin/termini/99999")).StatusCode);
    }

    [Fact]
    public async Task Admin_actions_without_antiforgery_token_are_rejected()
    {
        var c = _f.NewClient();
        await Browser.LoginAsync(c);

        var r = await c.PostAsync("/admin/slobodni-dani", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["date"] = Browser.Tuesday(90).ToString("yyyy-MM-dd"), ["reason"] = "CSRF",
        }));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    private async Task<(HttpClient Admin, string Token, string DetailUrl)> BookAndOpenAsync(int daysAhead, string email)
    {
        var pub = _f.NewClient();
        var date = Browser.Tuesday(daysAhead);
        var time = await Browser.FirstFreeTimeAsync(pub, "pregled", "dr-tarik-begic", date);
        var reference = await Browser.BookAsync(pub, "pregled", "dr-tarik-begic", date, time, email);   // token
        var admin = _f.NewClient();
        await Browser.LoginAsync(admin);
        var list = await admin.GetStringAsync($"/admin/termini?q={Uri.EscapeDataString(email)}");
        var id = Regex.Match(list, @"/admin/termini/(\d+)").Groups[1].Value;
        return (admin, reference, $"/admin/termini/{id}");
    }

    [Fact]
    public async Task Appointment_list_search_finds_the_appointment_and_detail_shows_contact()
    {
        var (admin, token, detail) = await BookAndOpenAsync(49, "trazi-me@primjer.ba");
        var reference = await Browser.ReferenceOfAsync(_f.NewClient(), token);

        Assert.Contains(reference, await admin.GetStringAsync("/admin/termini?q=trazi-me@primjer.ba"));
        var none = await admin.GetStringAsync("/admin/termini?q=nepostojeci-tekst-xyz");
        Assert.DoesNotContain(reference, none);
        Assert.Contains("Nema termina za zadane filtere", none);
        Assert.Contains("trazi-me@primjer.ba", await admin.GetStringAsync(detail));
    }

    [Fact]
    public async Task Staff_can_confirm_cancel_restore_and_annotate()
    {
        var (admin, reference, detail) = await BookAndOpenAsync(14, "osoblje@primjer.ba");   // reference = token
        var pub = _f.NewClient();

        await Browser.PostFormAsync(admin, $"{detail}/status", detail, new() { ["status"] = "Confirmed" });
        Assert.Contains("Potvrđeno", await admin.GetStringAsync(detail));

        await Browser.PostFormAsync(admin, $"{detail}/status", detail, new() { ["status"] = "Cancelled" });
        Assert.Contains("Vrati u zakazano", await admin.GetStringAsync(detail));
        Assert.Contains("Termin je otkazan", await pub.GetStringAsync($"/zakazivanje/potvrda/{reference}"));

        await Browser.PostFormAsync(admin, $"{detail}/status", detail, new() { ["status"] = "Scheduled" });
        Assert.Contains("Otkaži", await admin.GetStringAsync(detail));

        await Browser.PostFormAsync(admin, $"{detail}/biljeska", detail, new() { ["note"] = "Alergija na penicilin" });
        Assert.Contains("Alergija na penicilin", await admin.GetStringAsync(detail));
        Assert.DoesNotContain("Alergija na penicilin", await pub.GetStringAsync($"/zakazivanje/potvrda/{reference}"));   // pacijent ne smije vidjeti
    }

    [Fact]
    public async Task Marking_a_future_appointment_as_completed_shows_an_error_message()
    {
        var (admin, _, detail) = await BookAndOpenAsync(35, "buducnost@primjer.ba");

        await Browser.PostFormAsync(admin, $"{detail}/status", detail, new() { ["status"] = "Completed" });

        Assert.Contains("još nije dogodio", await admin.GetStringAsync(detail));
    }

    [Fact]
    public async Task Export_returns_csv_for_excel_with_filtered_rows_only()
    {
        var pub = _f.NewClient();
        var d1 = Browser.Tuesday(21);
        var d2 = Browser.Tuesday(28);
        var wanted = await Browser.BookAsync(pub, "pregled", "dr-tarik-begic", d1, await Browser.FirstFreeTimeAsync(pub, "pregled", "dr-tarik-begic", d1), "izvoz-da@primjer.ba");
        var other = await Browser.BookAsync(pub, "pregled", "dr-tarik-begic", d2, await Browser.FirstFreeTimeAsync(pub, "pregled", "dr-tarik-begic", d2), "izvoz-ne@primjer.ba");
        wanted = await Browser.ReferenceOfAsync(pub, wanted);   // the CSV shows the short reference, not the access token
        other = await Browser.ReferenceOfAsync(pub, other);
        var admin = _f.NewClient();
        await Browser.LoginAsync(admin);

        var r = await admin.GetAsync("/admin/export?q=izvoz-da@primjer.ba");

        Assert.Equal("text/csv", r.Content.Headers.ContentType!.MediaType);
        Assert.Contains("termini-", r.Content.Headers.ContentDisposition!.FileName);
        var bytes = await r.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3).ToArray());
        var csv = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains(wanted, csv);
        Assert.DoesNotContain(other, csv);
    }

    [Fact]
    public async Task Closing_a_day_removes_its_slots_and_reopening_brings_them_back()
    {
        // 53, a ne 56: Tuesday() traži PRVI utorak nakon tog broja dana (do 6 dana kasnije), a zakazivanje prima najviše
        // ClinicRules.MaxDaysAhead (60) dana unaprijed. Sa 56 test je padao nekim danima u sedmici (56 + 6 = 62 > 60).
        var day = Browser.Tuesday(53);
        var admin = _f.NewClient();
        await Browser.LoginAsync(admin);
        var pub = _f.NewClient();
        Assert.True((await Browser.JsonAsync(pub, $"/zakazivanje/termini?usluga=pregled&doktor=any&datum={day:yyyy-MM-dd}")).GetArrayLength() > 0);

        await Browser.PostFormAsync(admin, "/admin/slobodni-dani", "/admin/slobodni-dani", new() { ["date"] = day.ToString("yyyy-MM-dd"), ["reason"] = "Velika oluja" });

        Assert.Contains("Velika oluja", await admin.GetStringAsync("/admin/slobodni-dani"));
        Assert.Equal(0, (await Browser.JsonAsync(pub, $"/zakazivanje/termini?usluga=pregled&doktor=any&datum={day:yyyy-MM-dd}")).GetArrayLength());

        // id uzimamo iz reda s našim razlogom (na stranici su i drugi neradni dani iz ostalih testova)
        var id = Regex.Match(await admin.GetStringAsync("/admin/slobodni-dani"), @"Velika oluja.*?slobodni-dani/(\d+)/ukloni", RegexOptions.Singleline).Groups[1].Value;
        await Browser.PostFormAsync(admin, $"/admin/slobodni-dani/{id}/ukloni", "/admin/slobodni-dani", new());

        Assert.True((await Browser.JsonAsync(pub, $"/zakazivanje/termini?usluga=pregled&doktor=any&datum={day:yyyy-MM-dd}")).GetArrayLength() > 0);
    }

    [Fact]
    public async Task Time_off_needs_a_reason_and_cannot_be_added_twice()
    {
        var day = Browser.Tuesday(42);
        var admin = _f.NewClient();
        await Browser.LoginAsync(admin);

        await Browser.PostFormAsync(admin, "/admin/slobodni-dani", "/admin/slobodni-dani", new() { ["date"] = day.ToString("yyyy-MM-dd"), ["reason"] = "  " });
        Assert.Contains("Upišite razlog", await admin.GetStringAsync("/admin/slobodni-dani"));

        await Browser.PostFormAsync(admin, "/admin/slobodni-dani", "/admin/slobodni-dani", new() { ["date"] = day.ToString("yyyy-MM-dd"), ["reason"] = "Prvi put" });
        await Browser.PostFormAsync(admin, "/admin/slobodni-dani", "/admin/slobodni-dani", new() { ["date"] = day.ToString("yyyy-MM-dd"), ["reason"] = "Drugi put" });
        var page = await admin.GetStringAsync("/admin/slobodni-dani");
        Assert.Contains("već unesen", page);
        Assert.DoesNotContain("Drugi put", page);
    }

    [Fact]
    public async Task Editing_a_service_changes_the_public_page_and_validates_input()
    {
        var admin = _f.NewClient();
        await Browser.LoginAsync(admin);
        var form = () => new Dictionary<string, string>
        {
            ["Name"] = "Pregled i savjetovanje", ["Category"] = "Opća stomatologija", ["ImageName"] = "exam", ["Summary"] = "Novi kratki opis.", ["DurationMinutes"] = "30", ["PriceFrom"] = "35", ["PriceTo"] = "",
            ["Comfort"] = "Bezbolno", ["Description"] = "Opis.", ["Steps"] = "Korak", ["Aftercare"] = "Savjet", ["SuitableFor"] = "Svi", ["IsActive"] = "true",
        };

        var bad = form();
        bad["DurationMinutes"] = "45";
        var badResult = await Browser.PostFormAsync(admin, "/admin/usluge/1", "/admin/usluge/1", bad);
        Assert.Equal(HttpStatusCode.OK, badResult.StatusCode);
        Assert.Contains("višekratnik", await badResult.Content.ReadAsStringAsync());

        var ok = await Browser.PostFormAsync(admin, "/admin/usluge/1", "/admin/usluge/1", form());
        Assert.Equal(HttpStatusCode.Redirect, ok.StatusCode);
        var html = await _f.NewClient().GetStringAsync("/usluge/pregled");
        Assert.Contains("od 35 KM", html);
        Assert.Contains("Novi kratki opis.", html);
    }

    [Fact]
    public async Task Deactivated_service_disappears_from_the_site_and_from_booking()
    {
        var admin = _f.NewClient();
        await Browser.LoginAsync(admin);
        var r = await Browser.PostFormAsync(admin, "/admin/usluge/9", "/admin/usluge/9", new()
        {
            ["Name"] = "Dječja stomatologija", ["Category"] = "Djeca", ["ImageName"] = "kids", ["Summary"] = "x", ["DurationMinutes"] = "30", ["PriceFrom"] = "25", ["IsActive"] = "false",
        });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

        var pub = _f.NewClient();
        Assert.Equal(HttpStatusCode.NotFound, (await pub.GetAsync("/usluge/djecja-stomatologija")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pub.GetAsync("/zakazivanje/dani?usluga=djecja-stomatologija")).StatusCode);
    }

    [Fact]
    public async Task Doctor_shifts_can_be_added_removed_and_overlaps_are_refused()
    {
        var admin = _f.NewClient();
        await Browser.LoginAsync(admin);
        // Mia (id 3) ne radi utorkom
        var add = await Browser.PostFormAsync(admin, "/admin/doktori/3/smjene", "/admin/doktori/3", new() { ["day"] = "Tuesday", ["from"] = "09:00", ["to"] = "12:00" });
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
        var times = await Browser.JsonAsync(_f.NewClient(), $"/zakazivanje/termini?usluga=ortodoncija&doktor=dr-mia-delic&datum={Browser.Tuesday(49):yyyy-MM-dd}");
        Assert.True(times.GetArrayLength() > 0);

        await Browser.PostFormAsync(admin, "/admin/doktori/3/smjene", "/admin/doktori/3", new() { ["day"] = "Tuesday", ["from"] = "11:00", ["to"] = "13:00" });
        Assert.Contains("preklapa", await admin.GetStringAsync("/admin/doktori/3"));

        await Browser.PostFormAsync(admin, "/admin/doktori/3/smjene", "/admin/doktori/3", new() { ["day"] = "Tuesday", ["from"] = "09:10", ["to"] = "12:00" });
        Assert.Contains("pun sat ili pola sata", await admin.GetStringAsync("/admin/doktori/3"));

        var page = await admin.GetStringAsync("/admin/doktori/3");
        var removeUrl = Regex.Match(page, @"Utorak.*?(/admin/doktori/3/smjene/\d+/ukloni)", RegexOptions.Singleline).Groups[1].Value;
        await Browser.PostFormAsync(admin, removeUrl, "/admin/doktori/3", new());
        var after = await Browser.JsonAsync(_f.NewClient(), $"/zakazivanje/termini?usluga=ortodoncija&doktor=dr-mia-delic&datum={Browser.Tuesday(49):yyyy-MM-dd}");
        Assert.Equal(0, after.GetArrayLength());                 // Mia opet ne radi utorkom
    }

    [Fact]
    public async Task Contact_messages_show_up_for_staff_and_can_be_marked_as_handled()
    {
        var pub = _f.NewClient();
        await Browser.PostFormAsync(pub, "/kontakt", "/kontakt", new() { ["Name"] = "Poruka Test", ["Email"] = "poruka@primjer.ba", ["Message"] = "Koliko košta izbjeljivanje?", ["consent"] = "true" });
        var admin = _f.NewClient();
        await Browser.LoginAsync(admin);

        var list = await admin.GetStringAsync("/admin/poruke");
        Assert.Contains("Koliko košta izbjeljivanje?", list);
        Assert.Contains("Novo", list);

        var id = Regex.Match(list, @"poruke/(\d+)/obradjeno").Groups[1].Value;
        await Browser.PostFormAsync(admin, $"/admin/poruke/{id}/obradjeno", "/admin/poruke", new());
        Assert.Contains("Obrađeno", await admin.GetStringAsync("/admin/poruke"));
    }

    [Fact]
    public async Task Doctor_can_be_deactivated_and_stops_appearing_in_booking()
    {
        var admin = _f.NewClient();
        await Browser.LoginAsync(admin);
        var r = await Browser.PostFormAsync(admin, "/admin/doktori/4", "/admin/doktori/4", new()
        {
            ["Name"] = "Dr. Adnan Softić", ["Title"] = "Doktor stomatologije", ["YearsExperience"] = "10", ["IsActive"] = "false",
            ["ServiceIds"] = "1",
        });
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);

        var doctors = await Browser.JsonAsync(_f.NewClient(), "/zakazivanje/doktori?usluga=pregled");
        Assert.DoesNotContain(doctors.EnumerateArray(), d => d.GetProperty("slug").GetString() == "dr-adnan-softic");
    }
}
