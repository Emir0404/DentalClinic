using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Controllers;

/// <summary>Podaci koje pacijent šalje formularom za zakazivanje.</summary>
public class BookingForm
{
    [Required(ErrorMessage = "Odaberite uslugu.")] public string? Service { get; set; }
    /// <summary>"any" ili slug doktora.</summary>
    public string? Doctor { get; set; } = "any";
    [Required(ErrorMessage = "Odaberite datum.")] public DateTime? Date { get; set; }
    [Required(ErrorMessage = "Odaberite vrijeme.")] public string? Time { get; set; }
    [Required(ErrorMessage = "Unesite ime i prezime."), MaxLength(100)] public string? PatientName { get; set; }
    [Required(ErrorMessage = "Unesite broj telefona."), MaxLength(40)] public string? Phone { get; set; }
    [Required(ErrorMessage = "Unesite email adresu."), EmailAddress(ErrorMessage = "Email adresa nije ispravna."), MaxLength(150)] public string? Email { get; set; }
    [MaxLength(600)] public string? Notes { get; set; }
    public bool FirstVisit { get; set; } = true;
    public bool Consent { get; set; }
}

public class BookingViewModel
{
    public List<Service> Services { get; set; } = [];
    public BookingForm Form { get; set; } = new();
    public string? Error { get; set; }
}

[Route("zakazivanje")]
public class BookingController : Controller
{
    private readonly ClinicDb _db;
    private readonly AppointmentService _appointments;

    public BookingController(ClinicDb db, AppointmentService appointments)
    {
        _db = db;
        _appointments = appointments;
    }

    private async Task<List<Service>> ActiveServicesAsync() =>
        await _db.Services.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ToListAsync();

    [HttpGet("")]
    public async Task<IActionResult> Index(string? usluga)
    {
        var services = await ActiveServicesAsync();
        return View(new BookingViewModel
        {
            Services = services,
            Form = new BookingForm { Service = services.Any(s => s.Slug == usluga) ? usluga : null },
        });
    }

    [HttpPost(""), ValidateAntiForgeryToken, EnableRateLimiting(RateLimitPolicies.Submit)]
    public async Task<IActionResult> Index(BookingForm form)
    {
        var services = await ActiveServicesAsync();
        var vm = new BookingViewModel { Services = services, Form = form };

        if (!form.Consent) ModelState.AddModelError(nameof(form.Consent), "Za zakazivanje je potrebna saglasnost za obradu podataka.");
        if (!ModelState.IsValid) return View(vm);

        var service = services.FirstOrDefault(s => s.Slug == form.Service);
        if (service is null || !TimeSpan.TryParseExact(form.Time, @"hh\:mm", CultureInfo.InvariantCulture, out var time))
        {
            vm.Error = "Odabrani podaci nisu ispravni. Pokušajte ponovo.";
            return View(vm);
        }

        int? doctorId = null;
        if (!string.IsNullOrEmpty(form.Doctor) && form.Doctor != "any")
        {
            doctorId = (await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Slug == form.Doctor))?.Id;
            if (doctorId is null) { vm.Error = "Odabrani doktor ne postoji."; return View(vm); }
        }

        var result = await _appointments.BookAsync(new BookingRequest(
            service.Id, doctorId, form.Date!.Value, time, form.PatientName!, form.Phone!, form.Email!, form.Notes, form.FirstVisit));
        if (!result.Succeeded)
        {
            vm.Error = result.Error;
            return View(vm);
        }
        return RedirectToAction(nameof(Confirmation), new { token = result.Appointment!.AccessToken });
    }

    // ---------- JSON za formular (poziva ga booking.js) ----------

    [HttpGet("doktori")]
    [EnableRateLimiting(RateLimitPolicies.Read)]
    public async Task<IActionResult> Doctors(string usluga)
    {
        var doctors = await _db.Doctors.AsNoTracking()
            .Where(d => d.IsActive && d.Services.Any(s => s.Service.Slug == usluga)).OrderBy(d => d.SortOrder).ToListAsync();
        return Json(doctors.Select(d => new { slug = d.Slug, name = d.Name, title = d.Title, photo = d.PhotoUrl }));
    }

    /// <summary>Za idućih N dana: koliko slobodnih termina (0 = nema), da kalendar može označiti dane.</summary>
    [HttpGet("dani")]
    [EnableRateLimiting(RateLimitPolicies.Read)]
    public async Task<IActionResult> Days(string usluga, string? doktor, DateTime? od, int broj = 21)
    {
        var (service, doctorId, error) = await ResolveAsync(usluga, doktor);
        if (error is not null) return error;
        var from = (od ?? _appointments.Today).Date;
        if (from < _appointments.Today) from = _appointments.Today;
        var days = await _appointments.GetOpenDaysAsync(service!, doctorId, from, Math.Clamp(broj, 1, 42));
        return Json(days.Select(d => new { date = d.Key.ToString("yyyy-MM-dd"), free = d.Value }));
    }

    [HttpGet("termini")]
    [EnableRateLimiting(RateLimitPolicies.Read)]
    public async Task<IActionResult> Slots(string usluga, string? doktor, DateTime datum)
    {
        var (service, doctorId, error) = await ResolveAsync(usluga, doktor);
        if (error is not null) return error;
        var slots = await _appointments.GetSlotsAsync(service!, doctorId, datum);
        return Json(slots.Select(s => new { time = s.Time.ToString(@"hh\:mm"), doctor = s.Doctor.Name }));
    }

    private async Task<(Service? Service, int? DoctorId, IActionResult? Error)> ResolveAsync(string usluga, string? doktor)
    {
        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Slug == usluga && s.IsActive);
        if (service is null) return (null, null, NotFound());
        if (string.IsNullOrEmpty(doktor) || doktor == "any") return (service, null, null);
        var doctor = await _db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.Slug == doktor);
        return doctor is null ? (null, null, NotFound()) : (service, doctor.Id, null);
    }

    // ---------- potvrda, pregled, otkazivanje ----------
    // Pacijentove stranice se otvaraju preko dugog nasumičnog tokena (AccessToken), ne preko kratke šifre.
    // Šifru (DS-XXXXXX) pacijent dobija da je kaže ordinaciji; za vlastiti pregled treba i email (Lookup).

    private Task<Appointment?> FindAsync(string token) =>
        _db.Appointments.Include(a => a.Service).Include(a => a.Doctor).FirstOrDefaultAsync(a => a.AccessToken == token);

    [HttpGet("potvrda/{token}")]
    public async Task<IActionResult> Confirmation(string token)
    {
        var a = await FindAsync(token);
        if (a is null) return NotFound();
        ViewBag.CanCancel = _appointments.CanCancel(a);
        return View(a);
    }

    [HttpGet("moj-termin")]
    public IActionResult Lookup() => View();

    [HttpPost("moj-termin"), ValidateAntiForgeryToken, EnableRateLimiting(RateLimitPolicies.Lookup)]
    public async Task<IActionResult> Lookup(string? reference, string? email)
    {
        var r = (reference ?? "").Trim().ToUpperInvariant();
        var e = (email ?? "").Trim().ToLowerInvariant();
        var a = await _db.Appointments.AsNoTracking().FirstOrDefaultAsync(x => x.Reference == r && x.Email.ToLower() == e);
        if (a is null)
        {
            ViewBag.Error = "Termin nije pronađen. Provjerite šifru i email adresu.";
            return View();
        }
        return RedirectToAction(nameof(Confirmation), new { token = a.AccessToken });
    }

    [HttpPost("otkazi/{token}"), ValidateAntiForgeryToken, EnableRateLimiting(RateLimitPolicies.Lookup)]
    public async Task<IActionResult> Cancel(string token)
    {
        var a = await _db.Appointments.AsNoTracking().FirstOrDefaultAsync(x => x.AccessToken == token);
        if (a is null) return NotFound();
        if (!await _appointments.CancelAsync(a.Reference))
            TempData["Error"] = "Termin nije moguće otkazati putem sajta (već je otkazan ili je do termina manje od 24 sata). Pozovite nas na " + ClinicInfo.Phone + ".";
        else
            TempData["Ok"] = "Termin je otkazan.";
        return RedirectToAction(nameof(Confirmation), new { token });
    }

    [HttpGet("kalendar/{token}")]
    public async Task<IActionResult> Calendar(string token)
    {
        var a = await FindAsync(token);
        if (a is null || !a.IsActive) return NotFound();
        return File(Encoding.UTF8.GetBytes(CalendarFile.Build(a)), "text/calendar; charset=utf-8", $"termin-{a.Reference}.ics");
    }
}
