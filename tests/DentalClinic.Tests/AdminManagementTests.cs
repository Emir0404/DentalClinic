using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Tests;

public class SluggerTests
{
    [Theory]
    [InlineData("Dr. Đorđe Šarić", "dr-djordje-saric")]
    [InlineData("Čišćenje kamenca", "ciscenje-kamenca")]
    [InlineData("  Ortodoncija: konzultacija!  ", "ortodoncija-konzultacija")]
    [InlineData("Žaklina Ćosić", "zaklina-cosic")]
    [InlineData("Dr. José Müller", "dr-jose-muller")]
    [InlineData("???", "stavka")]
    [InlineData("", "stavka")]
    public void Names_become_safe_addresses(string input, string expected) =>
        Assert.Equal(expected, Slugger.Make(input));

    [Fact]
    public void Unique_adds_a_number_until_the_address_is_free()
    {
        var taken = new HashSet<string> { "dr-ana", "dr-ana-2" };

        Assert.Equal("dr-ana-3", Slugger.Unique("Dr. Ana", taken.Contains));
        Assert.Equal("dr-nova", Slugger.Unique("Dr. Nova", taken.Contains));
    }
}

public class PhotoStorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"photo-test-{Guid.NewGuid():N}");
    private readonly PhotoStorage _storage;

    public PhotoStorageTests()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["UploadsPath"] = _dir }).Build();
        _storage = new PhotoStorage(new FakeEnv(), config);
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    private sealed class FakeEnv : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "t";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Test";
    }

    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0, 1, 1, 1, 2, 3, 4];
    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3];
    public static readonly byte[] Webp = [(byte)'R', (byte)'I', (byte)'F', (byte)'F', 1, 2, 3, 4, (byte)'W', (byte)'E', (byte)'B', (byte)'P', 9, 9];

    private static IFormFile File(byte[] bytes, string fileName = "slika.jpg") =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "photo", fileName);

    [Theory]
    [InlineData(nameof(Jpeg), ".jpg")]
    [InlineData(nameof(Png), ".png")]
    [InlineData(nameof(Webp), ".webp")]
    public async Task Real_images_are_saved_with_the_extension_matching_their_content(string which, string ext)
    {
        var bytes = (byte[])typeof(PhotoStorageTests).GetField(which)!.GetValue(null)!;

        var r = await _storage.SaveAsync(File(bytes, "ime-koje-lazira.exe"), "dr-ana");

        Assert.True(r.Succeeded);
        Assert.EndsWith(ext, r.FileName);
        Assert.StartsWith("dr-ana-", r.FileName);
        Assert.Equal(bytes, await System.IO.File.ReadAllBytesAsync(Path.Combine(_dir, r.FileName!)));    // fajl je sačuvan cijeli
    }

    [Fact]
    public async Task Non_images_are_refused_even_when_named_like_images()
    {
        var r = await _storage.SaveAsync(File("MZ ovo je program, ne slika"u8.ToArray(), "slika.jpg"), "dr-ana");

        Assert.False(r.Succeeded);
        Assert.Contains("JPG, PNG i WEBP", r.Error);
        Assert.False(Directory.Exists(_dir) && Directory.GetFiles(_dir).Length > 0);
    }

    [Fact]
    public async Task Empty_and_oversized_files_are_refused()
    {
        Assert.False((await _storage.SaveAsync(File([]), "x")).Succeeded);
        var big = new byte[PhotoStorage.MaxBytes + 1];
        Jpeg.CopyTo(big, 0);
        Assert.Contains("prevelika", (await _storage.SaveAsync(File(big), "x")).Error);
    }

    [Fact]
    public async Task Stored_file_name_never_contains_user_supplied_path_parts()
    {
        var r = await _storage.SaveAsync(File(Jpeg, "../../evil.jpg"), "dr-ana");

        Assert.DoesNotContain("..", r.FileName);
        Assert.DoesNotContain("/", r.FileName);
        Assert.Equal(Path.GetFullPath(_dir), Path.GetDirectoryName(Path.GetFullPath(Path.Combine(_dir, r.FileName!))));
    }

    [Fact]
    public async Task Very_long_prefix_is_shortened_but_names_stay_unique()
    {
        var a = await _storage.SaveAsync(File(Jpeg), new string('a', 200));
        var b = await _storage.SaveAsync(File(Jpeg), new string('a', 200));

        Assert.NotEqual(a.FileName, b.FileName);
        Assert.True(a.FileName!.Length < 70);
    }

    [Fact]
    public async Task Delete_removes_uploaded_photos_but_ignores_seed_images_and_path_tricks()
    {
        var r = await _storage.SaveAsync(File(Jpeg), "dr-ana");
        var seedLike = Path.Combine(_dir, "doctor-lejla");          // bez nastavka = slika iz početnog sadržaja
        Directory.CreateDirectory(_dir);
        await System.IO.File.WriteAllTextAsync(seedLike, "x");

        _storage.Delete("doctor-lejla");
        _storage.Delete("../neki-drugi.jpg");
        _storage.Delete(null);
        Assert.True(System.IO.File.Exists(seedLike));

        _storage.Delete(r.FileName);
        Assert.False(System.IO.File.Exists(Path.Combine(_dir, r.FileName!)));
    }
}

public class PhotoUrlTests
{
    [Theory]
    [InlineData("", "/img/doctor-placeholder.svg")]
    [InlineData("doctor-lejla", "/img/doctor-lejla.jpg")]
    [InlineData("dr-ana-1a2b3c4d.png", "/img/uploads/dr-ana-1a2b3c4d.png")]
    public void Photo_url_depends_on_where_the_photo_lives(string name, string expected) =>
        Assert.Equal(expected, new Doctor { PhotoName = name }.PhotoUrl);
}

public class AdminManagementTests : IClassFixture<ClinicFactory>
{
    private readonly ClinicFactory _f;
    public AdminManagementTests(ClinicFactory f) => _f = f;

    private async Task<HttpClient> AdminAsync()
    {
        var c = _f.NewClient();
        await Browser.LoginAsync(c);
        return c;
    }

    private static async Task<HttpResponseMessage> PostMultipartAsync(HttpClient c, string url, string tokenPage,
        Dictionary<string, string[]> fields, byte[]? file = null, string fileName = "portret.jpg")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(await Browser.TokenFromAsync(c, tokenPage)), "__RequestVerificationToken");
        foreach (var (key, values) in fields) foreach (var v in values) content.Add(new StringContent(v), key);
        if (file is not null)
        {
            var part = new ByteArrayContent(file);
            part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(part, "photo", fileName);
        }
        return await c.PostAsync(url, content);
    }

    private static Dictionary<string, string[]> DoctorForm(string name, params string[] serviceIds) => new()
    {
        ["Name"] = [name], ["Title"] = ["Doktor stomatologije, parodontolog"], ["YearsExperience"] = ["6"], ["Bio"] = ["Nova u timu."],
        ["IsActive"] = ["true", "false"], ["ServiceIds"] = serviceIds,
    };

    private static int IdFromRedirect(HttpResponseMessage r) => int.Parse(r.Headers.Location!.OriginalString.Split('/').Last());

    // ---------- doktori ----------

    [Fact]
    public async Task Admin_can_add_a_new_doctor_with_a_photo_and_working_hours_and_patients_can_book_them()
    {
        var admin = await AdminAsync();

        var add = await PostMultipartAsync(admin, "/admin/doktori/novi", "/admin/doktori/novi", DoctorForm("Dr. Nova Šarić", "1", "2"), PhotoStorageTests.Jpeg);
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
        var id = IdFromRedirect(add);

        var edit = await admin.GetStringAsync($"/admin/doktori/{id}");
        Assert.Contains("Dr. Nova Šarić", edit);
        Assert.Contains("dodan/a", edit);                                           // poruka o uspjehu s uputom za smjene
        Assert.Contains("Nema unesenih smjena", edit);

        // još bez smjena: vidi se u timu, ali se ne može zakazati
        Assert.Contains("Dr. Nova Šarić", await _f.NewClient().GetStringAsync("/tim"));
        var day = Browser.Tuesday(14);
        Assert.Equal(0, (await Browser.JsonAsync(_f.NewClient(), $"/zakazivanje/termini?usluga=pregled&doktor=dr-nova-saric&datum={day:yyyy-MM-dd}")).GetArrayLength());

        await Browser.PostFormAsync(admin, $"/admin/doktori/{id}/smjene", $"/admin/doktori/{id}", new() { ["day"] = "Tuesday", ["from"] = "09:00", ["to"] = "12:00" });
        var slots = await Browser.JsonAsync(_f.NewClient(), $"/zakazivanje/termini?usluga=pregled&doktor=dr-nova-saric&datum={day:yyyy-MM-dd}");
        Assert.Equal(6, slots.GetArrayLength());                                    // 09:00 do 11:30

        var reference = await Browser.BookAsync(_f.NewClient(), "pregled", "dr-nova-saric", day, "09:00", "nova@primjer.ba");
        Assert.Contains("Dr. Nova Šarić", await _f.NewClient().GetStringAsync($"/zakazivanje/potvrda/{reference}"));
    }

    [Fact]
    public async Task Uploaded_photo_is_saved_and_shown_for_the_doctor()
    {
        var admin = await AdminAsync();
        var add = await PostMultipartAsync(admin, "/admin/doktori/novi", "/admin/doktori/novi", DoctorForm("Dr. Foto Test", "1"), PhotoStorageTests.Png, "x.png");
        var id = IdFromRedirect(add);

        var html = await admin.GetStringAsync($"/admin/doktori/{id}");

        var url = Regex.Match(html, @"/img/uploads/(dr-foto-test-[0-9a-f]+\.png)").Groups[1].Value;
        Assert.NotEmpty(url);
        Assert.True(File.Exists(Path.Combine(_f.UploadsDir, url)));
        Assert.Contains("Ukloni trenutnu fotografiju", html);
    }

    [Fact]
    public async Task Doctor_without_a_photo_gets_the_placeholder()
    {
        var admin = await AdminAsync();
        var add = await PostMultipartAsync(admin, "/admin/doktori/novi", "/admin/doktori/novi", DoctorForm("Dr. Bez Slike", "1"));

        var html = await admin.GetStringAsync($"/admin/doktori/{IdFromRedirect(add)}");

        Assert.Contains("/img/doctor-placeholder.svg", html);
    }

    [Fact]
    public async Task Fake_images_are_refused_and_nothing_is_created()
    {
        var admin = await AdminAsync();
        var before = (await admin.GetStringAsync("/admin/doktori")).Split("Dr. Lažna Slika").Length;

        var r = await PostMultipartAsync(admin, "/admin/doktori/novi", "/admin/doktori/novi", DoctorForm("Dr. Lažna Slika", "1"), "ovo nije slika"u8.ToArray(), "slika.jpg");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Dozvoljeni su samo JPG, PNG i WEBP", await r.Content.ReadAsStringAsync());
        Assert.Equal(before, (await admin.GetStringAsync("/admin/doktori")).Split("Dr. Lažna Slika").Length);
    }

    [Fact]
    public async Task New_doctor_needs_a_name_and_title()
    {
        var admin = await AdminAsync();
        var form = DoctorForm("", "1");
        form["Title"] = [""];

        var r = await PostMultipartAsync(admin, "/admin/doktori/novi", "/admin/doktori/novi", form);

        var html = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Ime je obavezno.", html);
    }

    [Fact]
    public async Task Two_doctors_with_the_same_name_get_different_addresses()
    {
        var admin = await AdminAsync();
        await PostMultipartAsync(admin, "/admin/doktori/novi", "/admin/doktori/novi", DoctorForm("Dr. Isto Ime", "1"));
        await PostMultipartAsync(admin, "/admin/doktori/novi", "/admin/doktori/novi", DoctorForm("Dr. Isto Ime", "1"));

        var team = await _f.NewClient().GetStringAsync("/tim");

        Assert.Contains("id=\"dr-isto-ime\"", team);
        Assert.Contains("id=\"dr-isto-ime-2\"", team);
    }

    [Fact]
    public async Task Doctor_without_any_appointments_can_be_deleted_together_with_photo_and_shifts()
    {
        var admin = await AdminAsync();
        var add = await PostMultipartAsync(admin, "/admin/doktori/novi", "/admin/doktori/novi", DoctorForm("Dr. Za Brisanje", "1"), PhotoStorageTests.Jpeg);
        var id = IdFromRedirect(add);
        await Browser.PostFormAsync(admin, $"/admin/doktori/{id}/smjene", $"/admin/doktori/{id}", new() { ["day"] = "Tuesday", ["from"] = "09:00", ["to"] = "12:00" });
        var file = Regex.Match(await admin.GetStringAsync($"/admin/doktori/{id}"), @"/img/uploads/(dr-za-brisanje-[0-9a-f]+\.jpg)").Groups[1].Value;
        Assert.True(File.Exists(Path.Combine(_f.UploadsDir, file)));

        var del = await Browser.PostFormAsync(admin, $"/admin/doktori/{id}/obrisi", $"/admin/doktori/{id}", new());

        Assert.Equal(HttpStatusCode.Redirect, del.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/admin/doktori/{id}")).StatusCode);
        Assert.DoesNotContain("Dr. Za Brisanje", await _f.NewClient().GetStringAsync("/tim"));
        Assert.False(File.Exists(Path.Combine(_f.UploadsDir, file)));                  // i fotografija je obrisana
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDb>();
        Assert.False(await db.Shifts.AnyAsync(s => s.DoctorId == id));                  // smjene su otišle s njim
    }

    [Fact]
    public async Task Doctor_with_appointment_history_cannot_be_deleted_but_can_be_deactivated_with_a_warning()
    {
        var admin = await AdminAsync();
        var pub = _f.NewClient();
        var day = Browser.Tuesday(21);
        await Browser.BookAsync(pub, "pregled", "dr-tarik-begic", day, await Browser.FirstFreeTimeAsync(pub, "pregled", "dr-tarik-begic", day), "istorija@primjer.ba");

        var del = await Browser.PostFormAsync(admin, "/admin/doktori/2/obrisi", "/admin/doktori/2", new());
        Assert.Equal(HttpStatusCode.Redirect, del.StatusCode);
        var page = await admin.GetStringAsync("/admin/doktori/2");
        Assert.Contains("nije moguće obrisati", page);
        Assert.Contains("Dr. Tarik Begić", page);                                        // doktor i dalje postoji
        Assert.Contains("budućih termina", page);

        var deactivate = await Browser.PostFormAsync(admin, "/admin/doktori/2", "/admin/doktori/2", new()
        {
            ["Name"] = "Dr. Tarik Begić", ["Title"] = "Specijalista oralne hirurgije i implantologije", ["YearsExperience"] = "12", ["IsActive"] = "false", ["ServiceIds"] = "1",
        });
        Assert.Equal(HttpStatusCode.Redirect, deactivate.StatusCode);
        var after = await admin.GetStringAsync("/admin/doktori/2");
        Assert.Contains("deaktiviran", after);
        Assert.Contains("kontaktirajte pacijente", after);                                // upozorenje o postojećim terminima
        Assert.DoesNotContain("Dr. Tarik Begić", await pub.GetStringAsync("/tim"));
        Assert.Contains("istorija@primjer.ba", await admin.GetStringAsync("/admin/termini?q=istorija@primjer.ba"));   // historija je ostala
    }

    [Fact]
    public async Task Doctors_list_shows_inactive_status_and_missing_working_hours()
    {
        var admin = await AdminAsync();
        await PostMultipartAsync(admin, "/admin/doktori/novi", "/admin/doktori/novi", DoctorForm("Dr. Bez Smjena", "1"));

        var list = await admin.GetStringAsync("/admin/doktori");

        Assert.Contains("Nema radnog vremena", list);
        Assert.Contains("+ Dodaj doktora", list);
    }

    [Fact]
    public async Task Anonymous_users_cannot_reach_the_new_admin_actions()
    {
        var c = _f.NewClient();
        foreach (var url in new[] { "/admin/doktori/novi", "/admin/usluge/nova" })
            Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync(url)).StatusCode);

        var token = await Browser.TokenFromAsync(c, "/kontakt");
        foreach (var url in new[] { "/admin/doktori/1/obrisi", "/admin/usluge/1/obrisi" })
        {
            var r = await c.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
            Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
            Assert.StartsWith("/admin/login", r.Headers.Location!.OriginalString.Replace("http://localhost", ""));
        }
    }

    // ---------- usluge ----------

    private static Dictionary<string, string[]> ServiceForm(string name, string category = "Parodontologija", string price = "90", params string[] doctorIds) => new()
    {
        ["Name"] = [name], ["Category"] = [category], ["Summary"] = ["Liječenje desni."], ["ImageName"] = ["cleaning"], ["DurationMinutes"] = ["60"],
        ["PriceFrom"] = [price], ["PriceTo"] = ["120"], ["Comfort"] = ["Bezbolno"], ["Description"] = ["Opis usluge."], ["Steps"] = ["Prvi korak\nDrugi korak"],
        ["Aftercare"] = ["Savjet"], ["SuitableFor"] = ["Svi"], ["IsActive"] = ["true", "false"], ["DoctorIds"] = doctorIds,
    };

    [Fact]
    public async Task Admin_can_add_a_new_service_that_shows_up_on_the_site_and_can_be_booked()
    {
        var admin = await AdminAsync();

        var add = await PostMultipartAsync(admin, "/admin/usluge/nova", "/admin/usluge/nova", ServiceForm("Liječenje parodontoze", doctorIds: ["1"]));

        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
        var pub = _f.NewClient();
        var page = await pub.GetStringAsync("/usluge/lijecenje-parodontoze");
        Assert.Contains("Liječenje parodontoze", page);
        Assert.Contains("90 – 120 KM", page);
        Assert.Contains("Drugi korak", page);
        Assert.Contains("Parodontologija", await pub.GetStringAsync("/usluge"));
        Assert.Contains("Liječenje parodontoze", await pub.GetStringAsync("/cjenovnik"));

        var day = Browser.Tuesday(7);
        var doctors = await Browser.JsonAsync(pub, "/zakazivanje/doktori?usluga=lijecenje-parodontoze");
        Assert.Equal(1, doctors.GetArrayLength());
        Assert.Equal("dr-lejla-kovacevic", doctors[0].GetProperty("slug").GetString());
        Assert.True((await Browser.JsonAsync(pub, $"/zakazivanje/termini?usluga=lijecenje-parodontoze&doktor=any&datum={day:yyyy-MM-dd}")).GetArrayLength() > 0);
    }

    [Fact]
    public async Task New_service_validates_input_and_refuses_unknown_images_and_duplicate_names()
    {
        var admin = await AdminAsync();

        var bad = ServiceForm("Loša usluga", price: "0");
        bad["ImageName"] = ["../../etc/passwd"];
        bad["DurationMinutes"] = ["45"];
        var r = await PostMultipartAsync(admin, "/admin/usluge/nova", "/admin/usluge/nova", bad);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Odaberite jednu od ponuđenih slika", html);
        Assert.Contains("višekratnik", html);
        Assert.Contains("cijenu veću od nule", html);

        var dup = await PostMultipartAsync(admin, "/admin/usluge/nova", "/admin/usluge/nova", ServiceForm("Pregled i savjetovanje"));
        Assert.Contains("već postoji", await dup.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Service_nobody_performs_is_listed_but_the_admin_is_warned()
    {
        var admin = await AdminAsync();

        var add = await PostMultipartAsync(admin, "/admin/usluge/nova", "/admin/usluge/nova", ServiceForm("Usluga bez doktora"));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);

        Assert.Contains("nijedan doktor ne radi", await admin.GetStringAsync("/admin/usluge"));
        Assert.Equal(0, (await Browser.JsonAsync(_f.NewClient(), "/zakazivanje/doktori?usluga=usluga-bez-doktora")).GetArrayLength());
    }

    [Fact]
    public async Task Unused_service_can_be_deleted_but_one_with_appointments_cannot()
    {
        var admin = await AdminAsync();
        var add = await PostMultipartAsync(admin, "/admin/usluge/nova", "/admin/usluge/nova", ServiceForm("Privremena usluga", doctorIds: ["1"]));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
        var list = await admin.GetStringAsync("/admin/usluge");
        var id = Regex.Match(list, @"Privremena usluga.*?/admin/usluge/(\d+)", RegexOptions.Singleline).Groups[1].Value;

        var del = await Browser.PostFormAsync(admin, $"/admin/usluge/{id}/obrisi", $"/admin/usluge/{id}", new());
        Assert.Equal(HttpStatusCode.Redirect, del.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _f.NewClient().GetAsync("/usluge/privremena-usluga")).StatusCode);

        // "pregled" (id 1) ima termine iz drugih testova ili ih dobija ovdje
        var pub = _f.NewClient();
        var day = Browser.Tuesday(28);
        await Browser.BookAsync(pub, "pregled", "dr-lejla-kovacevic", day, await Browser.FirstFreeTimeAsync(pub, "pregled", "dr-lejla-kovacevic", day), "usluga-hist@primjer.ba");
        await Browser.PostFormAsync(admin, "/admin/usluge/1/obrisi", "/admin/usluge/1", new());
        var still = await admin.GetStringAsync("/admin/usluge/1");
        Assert.Contains("nije moguće obrisati", still);
        Assert.Equal(HttpStatusCode.OK, (await pub.GetAsync("/usluge/pregled")).StatusCode);
    }

    [Fact]
    public async Task Changing_which_doctors_do_a_service_takes_effect_in_booking()
    {
        var admin = await AdminAsync();
        await PostMultipartAsync(admin, "/admin/usluge/nova", "/admin/usluge/nova", ServiceForm("Usluga za doktore", doctorIds: ["1"]));
        var id = Regex.Match(await admin.GetStringAsync("/admin/usluge"), @"Usluga za doktore.*?/admin/usluge/(\d+)", RegexOptions.Singleline).Groups[1].Value;
        Assert.Equal(1, (await Browser.JsonAsync(_f.NewClient(), "/zakazivanje/doktori?usluga=usluga-za-doktore")).GetArrayLength());

        var r = await PostMultipartAsync(admin, $"/admin/usluge/{id}", $"/admin/usluge/{id}", ServiceForm("Usluga za doktore", doctorIds: ["1", "4"]));

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal(2, (await Browser.JsonAsync(_f.NewClient(), "/zakazivanje/doktori?usluga=usluga-za-doktore")).GetArrayLength());
    }

    [Fact]
    public async Task Deactivating_a_service_with_future_appointments_warns_the_admin()
    {
        var admin = await AdminAsync();
        var add = await PostMultipartAsync(admin, "/admin/usluge/nova", "/admin/usluge/nova", ServiceForm("Usluga s terminima", doctorIds: ["1"]));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
        var pub = _f.NewClient();
        var day = Browser.Tuesday(35);
        await Browser.BookAsync(pub, "usluga-s-terminima", "dr-lejla-kovacevic", day, await Browser.FirstFreeTimeAsync(pub, "usluga-s-terminima", "dr-lejla-kovacevic", day), "uterminima@primjer.ba");
        var id = Regex.Match(await admin.GetStringAsync("/admin/usluge"), @"Usluga s terminima.*?/admin/usluge/(\d+)", RegexOptions.Singleline).Groups[1].Value;

        var form = ServiceForm("Usluga s terminima", doctorIds: ["1"]);
        form["IsActive"] = ["false"];
        await PostMultipartAsync(admin, $"/admin/usluge/{id}", $"/admin/usluge/{id}", form);

        Assert.Contains("budućih termina koji ostaju", await admin.GetStringAsync("/admin/usluge"));
        Assert.Equal(HttpStatusCode.NotFound, (await pub.GetAsync("/usluge/usluga-s-terminima")).StatusCode);
    }
}
