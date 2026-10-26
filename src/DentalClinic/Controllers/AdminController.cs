using System.Security.Claims;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Controllers;

[Authorize]
[Route("admin")]
public class AdminController : Controller
{
    private const int PageSize = 15;

    private readonly ClinicDb _db;
    private readonly AppointmentService _appointments;
    private readonly AdminAuth _auth;
    private readonly PhotoStorage _photos;
    private readonly IWebHostEnvironment _env;

    public AdminController(ClinicDb db, AppointmentService appointments, AdminAuth auth, PhotoStorage photos, IWebHostEnvironment env)
    {
        _db = db;
        _appointments = appointments;
        _auth = auth;
        _photos = photos;
        _env = env;
    }

    private IQueryable<Appointment> AppointmentsWithDetails() =>
        _db.Appointments.AsNoTracking().Include(a => a.Service).Include(a => a.Doctor);

    // ---------- prijava ----------

    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction(nameof(Dashboard));
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [AllowAnonymous, HttpPost("login"), ValidateAntiForgeryToken, Microsoft.AspNetCore.RateLimiting.EnableRateLimiting(RateLimitPolicies.Login)]
    public async Task<IActionResult> Login(string? username, string? password, string? returnUrl = null)
    {
        if (!_auth.Validate(username, password))
        {
            // ista poruka i kad je admin isključen: ne odajemo kako je server podešen
            ViewBag.Error = "Pogrešno korisničko ime ili lozinka.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, username!), new Claim(ClaimTypes.Role, "Admin")],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));
        // samo lokalne adrese, inače bi neko mogao poslati link koji nakon prijave vodi na tuđi sajt
        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction(nameof(Dashboard));
    }

    [HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    // ---------- pregled ----------

    [HttpGet("")]
    public async Task<IActionResult> Dashboard()
    {
        var today = _appointments.Today;
        var weekEnd = today.AddDays(7);
        var since = today.AddDays(-30);

        var upcoming = await _db.Appointments.AsNoTracking()
            .Where(a => a.Start >= today && a.Start < weekEnd
                        && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed))
            .Select(a => a.Start).ToListAsync();
        var recent = await _db.Appointments.AsNoTracking().Where(a => a.Start >= since && a.Start < today)
            .Select(a => a.Status).ToListAsync();
        var done = recent.Count(s => s == AppointmentStatus.Completed);
        var noShow = recent.Count(s => s == AppointmentStatus.NoShow);

        var vm = new DashboardViewModel
        {
            Today = await AppointmentsWithDetails()
                .Where(a => a.Start >= today && a.Start < today.AddDays(1) && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.Start).ToListAsync(),
            TomorrowCount = upcoming.Count(s => s.Date == today.AddDays(1)),
            WeekCount = upcoming.Count,
            UnhandledMessages = await _db.Messages.CountAsync(m => !m.Handled),
            Completed30Days = done,
            NoShowRate = done + noShow == 0 ? 0 : (double)noShow / (done + noShow),
            NextDays = Enumerable.Range(0, 7).Select(i => (today.AddDays(i), upcoming.Count(s => s.Date == today.AddDays(i)))).ToList(),
            RecentMessages = (await _db.Messages.AsNoTracking().Where(m => !m.Handled).ToListAsync())
                .OrderByDescending(m => m.CreatedAt).Take(3).ToList(),
        };
        return View(vm);
    }

    // ---------- termini ----------

    [HttpGet("termini")]
    public async Task<IActionResult> Appointments([FromQuery] AppointmentFilter filter)
    {
        var query = filter.Apply(AppointmentsWithDetails());
        var total = await query.CountAsync();
        var page = Math.Max(1, filter.Page);
        var items = await query.OrderByDescending(a => a.Start).ThenBy(a => a.Id)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        filter.Page = page;
        return View(new AppointmentListViewModel
        {
            Filter = filter, Items = items, Total = total, PageSize = PageSize,
            Doctors = await _db.Doctors.AsNoTracking().OrderBy(d => d.SortOrder).ToListAsync(),
        });
    }

    [HttpGet("termini/{id:int}")]
    public async Task<IActionResult> Appointment(int id)
    {
        var a = await AppointmentsWithDetails().FirstOrDefaultAsync(x => x.Id == id);
        if (a is null) return NotFound();
        return View(a);
    }

    [HttpPost("termini/{id:int}/status"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, AppointmentStatus status)
    {
        var error = await _appointments.SetStatusAsync(id, status);
        if (error is null) TempData["Ok"] = "Status je promijenjen.";
        else TempData["Error"] = error;
        return RedirectToAction(nameof(Appointment), new { id });
    }

    [HttpPost("termini/{id:int}/biljeska"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Note(int id, string? note)
    {
        var a = await _db.Appointments.FindAsync(id);
        if (a is null) return NotFound();
        note = note?.Trim();
        a.StaffNote = string.IsNullOrEmpty(note) ? null : note[..Math.Min(note.Length, 500)];
        await _db.SaveChangesAsync();
        TempData["Ok"] = "Bilješka je sačuvana.";
        return RedirectToAction(nameof(Appointment), new { id });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] AppointmentFilter filter)
    {
        var rows = await filter.Apply(AppointmentsWithDetails()).OrderBy(a => a.Start).ToListAsync();
        return File(CsvExporter.Build(rows), "text/csv; charset=utf-8", $"termini-{_appointments.Today:yyyy-MM-dd}.csv");
    }

    // ---------- raspored po danima ----------

    [HttpGet("raspored")]
    public async Task<IActionResult> Schedule(DateTime? date)
    {
        var day = (date ?? _appointments.Today).Date;
        var doctors = await _db.Doctors.AsNoTracking().Include(d => d.Shifts).Where(d => d.IsActive).OrderBy(d => d.SortOrder).ToListAsync();
        return View(new ScheduleViewModel
        {
            Date = day,
            Doctors = doctors,
            Appointments = await AppointmentsWithDetails()
                .Where(a => a.Start >= day && a.Start < day.AddDays(1) && a.Status != AppointmentStatus.Cancelled)
                .OrderBy(a => a.Start).ToListAsync(),
            TimeOffs = await _db.TimeOffs.AsNoTracking().Where(t => t.Date == day).ToListAsync(),
        });
    }

    // ---------- usluge ----------

    private async Task FillServiceViewBagAsync(string? slug, bool isNew)
    {
        ViewBag.Slug = slug;
        ViewBag.IsNew = isNew;
        ViewBag.AllDoctors = await _db.Doctors.AsNoTracking().OrderBy(d => d.SortOrder).ToListAsync();
        ViewBag.Categories = await _db.Services.AsNoTracking().Select(s => s.Category).Distinct().OrderBy(c => c).ToListAsync();
        // slike koje se mogu odabrati: samo one iz foldera img (bez portreta doktora)
        var img = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), "img");
        ViewBag.Images = Directory.Exists(img)
            ? Directory.GetFiles(img, "*.jpg").Select(f => Path.GetFileNameWithoutExtension(f)).Where(n => !n.StartsWith("doctor-")).OrderBy(n => n).ToList()
            : new List<string>();
    }

    [HttpGet("usluge")]
    public async Task<IActionResult> Services()
    {
        ViewBag.Upcoming = await UpcomingCountsAsync(a => a.ServiceId);
        return View(await _db.Services.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync());
    }

    [HttpGet("usluge/nova")]
    public async Task<IActionResult> NewService()
    {
        await FillServiceViewBagAsync(null, isNew: true);
        return View("EditService", new ServiceEditModel { Category = "Opća stomatologija", DurationMinutes = 30 });
    }

    [HttpPost("usluge/nova"), ValidateAntiForgeryToken]
    public async Task<IActionResult> NewService(ServiceEditModel m)
    {
        await FillServiceViewBagAsync(null, isNew: true);
        await ValidateServiceAsync(m);
        if (!ModelState.IsValid) return View("EditService", m);

        var service = new Service
        {
            Slug = Slugger.Unique(m.Name, slug => _db.Services.Any(x => x.Slug == slug)),
            SortOrder = (await _db.Services.MaxAsync(s => (int?)s.SortOrder) ?? 0) + 1,
        };
        ApplyService(service, m);
        _db.Services.Add(service);
        await _db.SaveChangesAsync();
        await SetServiceDoctorsAsync(service.Id, m.DoctorIds);
        TempData["Ok"] = m.DoctorIds.Count == 0
            ? "Usluga je dodana, ali je još nijedan doktor ne radi. Dok je ne dodijelite doktoru, ne može se zakazati."
            : "Usluga je dodana i vidljiva je na sajtu.";
        return RedirectToAction(nameof(Services));
    }

    [HttpGet("usluge/{id:int}")]
    public async Task<IActionResult> EditService(int id)
    {
        var s = await _db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        await FillServiceViewBagAsync(s.Slug, isNew: false);
        ViewBag.AppointmentCount = await _db.Appointments.CountAsync(a => a.ServiceId == id);
        return View(new ServiceEditModel
        {
            Id = s.Id, Name = s.Name, Category = s.Category, Summary = s.Summary, ImageName = s.ImageName, Description = s.Description,
            Steps = s.Steps, Aftercare = s.Aftercare, SuitableFor = s.SuitableFor, DurationMinutes = s.DurationMinutes, PriceFrom = s.PriceFrom,
            PriceTo = s.PriceTo, Comfort = s.Comfort, IsActive = s.IsActive,
            DoctorIds = await _db.DoctorServices.Where(x => x.ServiceId == id).Select(x => x.DoctorId).ToListAsync(),
        });
    }

    [HttpPost("usluge/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditService(int id, ServiceEditModel m)
    {
        var s = await _db.Services.FindAsync(id);
        if (s is null) return NotFound();
        m.Id = id;
        await FillServiceViewBagAsync(s.Slug, isNew: false);
        ViewBag.AppointmentCount = await _db.Appointments.CountAsync(a => a.ServiceId == id);
        await ValidateServiceAsync(m);
        if (!ModelState.IsValid) return View(m);

        var wasActive = s.IsActive;
        ApplyService(s, m);
        await _db.SaveChangesAsync();
        await SetServiceDoctorsAsync(id, m.DoctorIds);

        var now = _appointments.Now;
        var upcoming = wasActive && !m.IsActive
            ? await _db.Appointments.CountAsync(a => a.ServiceId == id && a.Start >= now
                && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed))
            : 0;
        TempData["Ok"] = upcoming > 0
            ? $"Usluga je sačuvana i sakrivena sa sajta. Pažnja: za nju postoji {upcoming} budućih termina koji ostaju na rasporedu."
            : "Usluga je sačuvana. Postojeći termini zadržavaju staro trajanje.";
        return RedirectToAction(nameof(Services));
    }

    [HttpPost("usluge/{id:int}/obrisi"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteService(int id)
    {
        var s = await _db.Services.FindAsync(id);
        if (s is null) return NotFound();
        var count = await _db.Appointments.CountAsync(a => a.ServiceId == id);
        if (count > 0)
        {
            TempData["Error"] = $"Uslugu nije moguće obrisati jer postoji {count} termina vezanih za nju (historija ostaje sačuvana). Umjesto brisanja je deaktivirajte.";
            return RedirectToAction(nameof(EditService), new { id });
        }
        _db.DoctorServices.RemoveRange(_db.DoctorServices.Where(x => x.ServiceId == id));
        _db.Services.Remove(s);
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"Usluga „{s.Name}“ je obrisana.";
        return RedirectToAction(nameof(Services));
    }

    private async Task ValidateServiceAsync(ServiceEditModel m)
    {
        if (m.DurationMinutes % ClinicRules.SlotMinutes != 0)
            ModelState.AddModelError(nameof(m.DurationMinutes), $"Trajanje mora biti višekratnik od {ClinicRules.SlotMinutes} minuta.");
        if (m.PriceTo is { } to && to < m.PriceFrom)
            ModelState.AddModelError(nameof(m.PriceTo), "Gornja cijena ne može biti manja od donje.");
        if (m.PriceFrom <= 0)
            ModelState.AddModelError(nameof(m.PriceFrom), "Unesite cijenu veću od nule.");
        // slika mora biti jedna od ponuđenih: ime iz forme se nikad ne koristi kao putanja bez provjere
        if (!((List<string>)ViewBag.Images).Contains(m.ImageName ?? ""))
            ModelState.AddModelError(nameof(m.ImageName), "Odaberite jednu od ponuđenih slika.");
        var name = (m.Name ?? "").Trim();
        if (name.Length > 0 && await _db.Services.AnyAsync(s => s.Id != m.Id && s.Name.ToLower() == name.ToLower()))
            ModelState.AddModelError(nameof(m.Name), "Usluga s tim nazivom već postoji.");
    }

    private static void ApplyService(Service s, ServiceEditModel m)
    {
        s.Name = m.Name.Trim(); s.Category = m.Category.Trim(); s.Summary = m.Summary.Trim(); s.ImageName = m.ImageName;
        s.Description = m.Description ?? ""; s.Steps = m.Steps ?? ""; s.Aftercare = m.Aftercare ?? ""; s.SuitableFor = m.SuitableFor ?? "";
        s.DurationMinutes = m.DurationMinutes; s.PriceFrom = m.PriceFrom; s.PriceTo = m.PriceTo; s.Comfort = m.Comfort ?? ""; s.IsActive = m.IsActive;
    }

    private async Task SetServiceDoctorsAsync(int serviceId, List<int> doctorIds)
    {
        var current = await _db.DoctorServices.Where(x => x.ServiceId == serviceId).ToListAsync();
        _db.DoctorServices.RemoveRange(current.Where(x => !doctorIds.Contains(x.DoctorId)));
        var valid = await _db.Doctors.Where(d => doctorIds.Contains(d.Id)).Select(d => d.Id).ToListAsync();
        foreach (var d in valid.Where(d => current.All(x => x.DoctorId != d)))
            _db.DoctorServices.Add(new DoctorService { DoctorId = d, ServiceId = serviceId });
        await _db.SaveChangesAsync();
    }

    /// <summary>Broj aktivnih budućih termina po doktoru/usluzi (za upozorenja i prikaz u listama).</summary>
    private async Task<Dictionary<int, int>> UpcomingCountsAsync(Func<Appointment, int> key)
    {
        var now = _appointments.Now;
        var rows = await _db.Appointments.AsNoTracking()
            .Where(a => a.Start >= now && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed))
            .ToListAsync();
        return rows.GroupBy(key).ToDictionary(g => g.Key, g => g.Count());
    }

    // ---------- doktori i radno vrijeme ----------

    [HttpGet("doktori")]
    public async Task<IActionResult> Doctors()
    {
        ViewBag.Upcoming = await UpcomingCountsAsync(a => a.DoctorId);
        return View(await _db.Doctors.AsNoTracking().Include(d => d.Shifts).Include(d => d.Services).OrderBy(d => d.SortOrder).ToListAsync());
    }

    [HttpGet("doktori/novi")]
    public async Task<IActionResult> NewDoctor()
    {
        await FillDoctorViewBagAsync(new Doctor(), isNew: true);
        return View("EditDoctor", new DoctorEditModel());
    }

    [HttpPost("doktori/novi"), ValidateAntiForgeryToken, RequestSizeLimit(PhotoStorage.MaxBytes + 512 * 1024)]
    public async Task<IActionResult> NewDoctor(DoctorEditModel m, IFormFile? photo)
    {
        await FillDoctorViewBagAsync(new Doctor(), isNew: true);
        string? photoName = null;
        if (photo is { Length: > 0 })
        {
            var saved = await _photos.SaveAsync(photo, Slugger.Make(m.Name ?? "doktor"));
            if (!saved.Succeeded) ModelState.AddModelError("photo", saved.Error!);
            else photoName = saved.FileName;
        }
        if (!ModelState.IsValid)
        {
            _photos.Delete(photoName);
            return View("EditDoctor", m);
        }

        var doctor = new Doctor
        {
            Slug = Slugger.Unique(m.Name ?? "doktor", slug => _db.Doctors.Any(x => x.Slug == slug)),
            SortOrder = (await _db.Doctors.MaxAsync(d => (int?)d.SortOrder) ?? 0) + 1,
            PhotoName = photoName ?? "",
        };
        ApplyDoctor(doctor, m);
        doctor.Services = m.ServiceIds.Distinct().Select(sid => new DoctorService { ServiceId = sid }).ToList();
        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"{doctor.Name} je dodan/a. Sada dodajte radno vrijeme, jer bez smjena nije moguće zakazati termin.";
        return RedirectToAction(nameof(EditDoctor), new { id = doctor.Id });
    }

    [HttpGet("doktori/{id:int}")]
    public async Task<IActionResult> EditDoctor(int id)
    {
        var d = await _db.Doctors.AsNoTracking().Include(x => x.Services).Include(x => x.Shifts).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        await FillDoctorViewBagAsync(d, isNew: false);
        return View(new DoctorEditModel
        {
            Id = d.Id, Name = d.Name, Title = d.Title, Bio = d.Bio, YearsExperience = d.YearsExperience,
            IsActive = d.IsActive, ServiceIds = d.Services.Select(s => s.ServiceId).ToList(),
        });
    }

    private async Task FillDoctorViewBagAsync(Doctor d, bool isNew)
    {
        ViewBag.Doctor = d;
        ViewBag.IsNew = isNew;
        ViewBag.AllServices = await _db.Services.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync();
        ViewBag.Upcoming = 0;
        ViewBag.AppointmentCount = 0;
        if (!isNew)
        {
            var now = _appointments.Now;
            ViewBag.Upcoming = await _db.Appointments.CountAsync(a => a.DoctorId == d.Id && a.Start >= now
                && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed));
            ViewBag.AppointmentCount = await _db.Appointments.CountAsync(a => a.DoctorId == d.Id);
        }
    }

    private static void ApplyDoctor(Doctor d, DoctorEditModel m)
    {
        d.Name = m.Name.Trim(); d.Title = m.Title.Trim(); d.Bio = m.Bio ?? ""; d.YearsExperience = m.YearsExperience; d.IsActive = m.IsActive;
    }

    [HttpPost("doktori/{id:int}"), ValidateAntiForgeryToken, RequestSizeLimit(PhotoStorage.MaxBytes + 512 * 1024)]
    public async Task<IActionResult> EditDoctor(int id, DoctorEditModel m, IFormFile? photo, bool removePhoto = false)
    {
        var d = await _db.Doctors.Include(x => x.Services).Include(x => x.Shifts).FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        m.Id = id;
        await FillDoctorViewBagAsync(d, isNew: false);

        string? newPhoto = null;
        if (photo is { Length: > 0 })
        {
            var saved = await _photos.SaveAsync(photo, d.Slug);
            if (!saved.Succeeded) ModelState.AddModelError("photo", saved.Error!);
            else newPhoto = saved.FileName;
        }
        if (!ModelState.IsValid)
        {
            _photos.Delete(newPhoto);
            return View(m);
        }

        var wasActive = d.IsActive;
        ApplyDoctor(d, m);
        d.Services.RemoveAll(s => !m.ServiceIds.Contains(s.ServiceId));
        foreach (var sid in m.ServiceIds.Distinct().Where(sid => d.Services.All(s => s.ServiceId != sid)))
            d.Services.Add(new DoctorService { ServiceId = sid });

        var oldPhoto = d.PhotoName;
        if (newPhoto is not null) d.PhotoName = newPhoto;
        else if (removePhoto) d.PhotoName = "";
        await _db.SaveChangesAsync();
        if (d.PhotoName != oldPhoto) _photos.Delete(oldPhoto);

        var upcoming = (int)ViewBag.Upcoming;
        TempData["Ok"] = wasActive && !m.IsActive && upcoming > 0
            ? $"Doktor je deaktiviran i više ne prima nove termine. Pažnja: ima {upcoming} budućih termina, kontaktirajte pacijente i otkažite ih ili premjestite."
            : "Doktor je sačuvan.";
        return RedirectToAction(nameof(EditDoctor), new { id });
    }

    [HttpPost("doktori/{id:int}/obrisi"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDoctor(int id)
    {
        var d = await _db.Doctors.FindAsync(id);
        if (d is null) return NotFound();
        var count = await _db.Appointments.CountAsync(a => a.DoctorId == id);
        if (count > 0)
        {
            // historija termina (i ko je koga liječio) se ne briše; doktor se samo skida s rasporeda
            TempData["Error"] = $"Doktora nije moguće obrisati jer ima {count} termina u evidenciji (historija mora ostati sačuvana). Umjesto brisanja ga deaktivirajte: nestaje sa sajta i ne prima nove termine.";
            return RedirectToAction(nameof(EditDoctor), new { id });
        }
        _photos.Delete(d.PhotoName);
        _db.Doctors.Remove(d);                      // smjene, usluge i neradni dani se brišu zajedno s njim
        await _db.SaveChangesAsync();
        TempData["Ok"] = $"{d.Name} je obrisan/a.";
        return RedirectToAction(nameof(Doctors));
    }

    [HttpPost("doktori/{id:int}/smjene"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddShift(int id, DayOfWeek day, string? from, string? to)
    {
        if (!await _db.Doctors.AnyAsync(d => d.Id == id)) return NotFound();
        if (!TimeSpan.TryParse(from, out var start) || !TimeSpan.TryParse(to, out var end) || end <= start)
            TempData["Error"] = "Unesite ispravno vrijeme: kraj mora biti poslije početka.";
        else if (start.Minutes % ClinicRules.SlotMinutes != 0 || end.Minutes % ClinicRules.SlotMinutes != 0)
            TempData["Error"] = $"Vrijeme mora biti na pun sat ili pola sata (višekratnik od {ClinicRules.SlotMinutes} minuta).";
        else if ((await _db.Shifts.AsNoTracking().Where(s => s.DoctorId == id && s.Day == day).ToListAsync())   // SQLite ne zna porediti TimeSpan u upitu
                     .Any(s => s.Start < end && s.End > start))
            TempData["Error"] = "Ta smjena se preklapa s postojećom.";
        else
        {
            _db.Shifts.Add(new DoctorShift { DoctorId = id, Day = day, Start = start, End = end });
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Smjena je dodana.";
        }
        return RedirectToAction(nameof(EditDoctor), new { id });
    }

    [HttpPost("doktori/{id:int}/smjene/{shiftId:int}/ukloni"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveShift(int id, int shiftId)
    {
        var s = await _db.Shifts.FirstOrDefaultAsync(x => x.Id == shiftId && x.DoctorId == id);
        if (s is not null)
        {
            _db.Shifts.Remove(s);
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Smjena je uklonjena. Već zakazani termini ostaju.";
        }
        return RedirectToAction(nameof(EditDoctor), new { id });
    }

    // ---------- neradni dani ----------

    [HttpGet("slobodni-dani")]
    public async Task<IActionResult> TimeOffs()
    {
        ViewBag.Doctors = await _db.Doctors.AsNoTracking().OrderBy(d => d.SortOrder).ToListAsync();
        return View(await _db.TimeOffs.AsNoTracking().Include(t => t.Doctor)
            .Where(t => t.Date >= _appointments.Today).OrderBy(t => t.Date).ToListAsync());
    }

    [HttpPost("slobodni-dani"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTimeOff(DateTime? date, int? doctorId, string? reason)
    {
        reason = reason?.Trim();
        if (date is null) TempData["Error"] = "Odaberite datum.";
        else if (date.Value.Date < _appointments.Today) TempData["Error"] = "Datum je u prošlosti.";
        else if (string.IsNullOrEmpty(reason)) TempData["Error"] = "Upišite razlog.";
        else if (await _db.TimeOffs.AnyAsync(t => t.Date == date.Value.Date && t.DoctorId == doctorId)) TempData["Error"] = "Taj dan je već unesen.";
        else
        {
            var day = date.Value.Date;
            _db.TimeOffs.Add(new TimeOff { Date = day, DoctorId = doctorId, Reason = reason[..Math.Min(reason.Length, 120)] });
            await _db.SaveChangesAsync();
            var affected = await _db.Appointments.CountAsync(a => a.Start >= day && a.Start < day.AddDays(1)
                && (doctorId == null || a.DoctorId == doctorId)
                && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed));
            TempData["Ok"] = affected == 0
                ? "Dan je unesen kao neradni."
                : $"Dan je unesen, ali za taj datum već postoji {affected} aktivnih termina. Kontaktirajte pacijente i otkažite ili premjestite termine.";
        }
        return RedirectToAction(nameof(TimeOffs));
    }

    [HttpPost("slobodni-dani/{id:int}/ukloni"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTimeOff(int id)
    {
        var t = await _db.TimeOffs.FindAsync(id);
        if (t is not null)
        {
            _db.TimeOffs.Remove(t);
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Dan je ponovo radni.";
        }
        return RedirectToAction(nameof(TimeOffs));
    }

    // ---------- poruke ----------

    [HttpGet("poruke")]
    public async Task<IActionResult> Messages() =>
        View((await _db.Messages.AsNoTracking().ToListAsync()).OrderBy(m => m.Handled).ThenByDescending(m => m.CreatedAt).ToList());

    [HttpPost("poruke/{id:int}/obradjeno"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleMessage(int id)
    {
        var m = await _db.Messages.FindAsync(id);
        if (m is not null)
        {
            m.Handled = !m.Handled;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Messages));
    }
}
