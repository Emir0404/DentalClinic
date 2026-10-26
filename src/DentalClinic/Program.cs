using System.Text.Encodings.Web;
using System.Text.Unicode;
using DentalClinic.Data;
using DentalClinic.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(o =>
    // svaki POST traži antiforgery token, ne oslanjamo se na to da se atribut ne zaboravi na nekoj akciji
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddClinicRateLimiting(builder.Configuration);

// u kontejneru bi se ključevi za kolačiće izgubili pri svakom restartu i admin bi bio odjavljen
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath)).SetApplicationName("StudioOsmijeh");
builder.Services.AddDbContext<ClinicDb>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Clinic") ?? "Data Source=clinic.db"));

// bez ovoga Razor ispisuje č, ć, š, ž, đ kao &#x10D; itd. (radi, ali je HTML nečitljiv)
builder.Services.Configure<WebEncoderOptions>(o =>
    o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.BasicLatin, UnicodeRanges.Latin1Supplement, UnicodeRanges.LatinExtendedA, UnicodeRanges.GeneralPunctuation));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddSingleton<PhotoStorage>();
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));
builder.Services.AddSingleton<AdminAuth>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/admin/login";
        o.AccessDeniedPath = "/admin/login";
        o.Cookie.Name = "osmijeh.admin";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClinicDb>();
    ClinicDb.Initialize(db);
    if (app.Configuration.GetValue<bool>("SeedDemoData"))
        DemoData.Seed(db, scope.ServiceProvider.GetRequiredService<TimeProvider>().GetLocalNow().Date);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (app.Environment.IsDevelopment() ? app.Configuration.GetValue("HttpsRedirection", false) : app.Configuration.GetValue("HttpsRedirection", true))
    app.UseHttpsRedirection();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    // Nijedan inline skript: sve ponašanje je u /js/*.js. Stilovi smiju biti inline (ima ih mnogo u stranicama),
    // a fontovi dolaze s Google Fonts.
    ctx.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self'; object-src 'none'; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    await next();
});

app.UseStatusCodePagesWithReExecute("/Home/NotFoundPage", "?code={0}");
app.UseStaticFiles(new StaticFileOptions
{
    // slike i stilovi se rijetko mijenjaju: browser ih može čuvati sedam dana (asp-append-version rješava osvježavanje)
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public,max-age=604800",
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("service", "usluge/{slug}", new { controller = "Services", action = "Details" });
app.MapControllerRoute("services", "usluge", new { controller = "Services", action = "Index" });
app.MapControllerRoute("team", "tim", new { controller = "Team", action = "Index" });
app.MapControllerRoute("blog-post", "savjeti/{slug}", new { controller = "Blog", action = "Details" });
app.MapControllerRoute("blog", "savjeti", new { controller = "Blog", action = "Index" });
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

app.Run();

// potrebno da integracijski testovi mogu pokrenuti aplikaciju (WebApplicationFactory<Program>)
public partial class Program { }
