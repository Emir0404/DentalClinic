using DentalClinic.Data;
using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Services;

public record BookingRequest(
    int ServiceId, int? DoctorId, DateTime Date, TimeSpan Time,
    string PatientName, string Phone, string Email, string? Notes, bool FirstVisit);

public record BookingOutcome(Appointment? Appointment, string? Error)
{
    public bool Succeeded => Appointment is not null;
}

/// <summary>
/// Sve što se tiče termina: koji su slobodni, zakazivanje, otkazivanje i promjena statusa.
/// Pravila su ovdje (a ne u kontrolerima) da ih koriste i javni sajt i admin panel, i da ih testovi pokrivaju.
/// </summary>
public class AppointmentService
{
    // Provjera "je li termin slobodan" i upis moraju biti nedjeljivi, inače dva pacijenta mogu istovremeno
    // zauzeti isti termin. (Za više servera bi ovo bio unique indeks ili transakcija u bazi.)
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private readonly ClinicDb _db;
    private readonly TimeProvider _clock;

    public AppointmentService(ClinicDb db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public DateTime Now => _clock.GetLocalNow().DateTime;
    public DateTime Today => Now.Date;

    // ---------- slobodni termini ----------

    /// <summary>Slobodni termini za jedan dan. Ako doktor nije zadan, traži se među svima koji rade tu uslugu.</summary>
    public async Task<List<SlotOption>> GetSlotsAsync(Service service, int? doctorId, DateTime date)
    {
        var snapshot = await LoadAsync(service, doctorId, date.Date, date.Date);
        return ComputeSlots(snapshot, service, date.Date);
    }

    /// <summary>Za svaki dan od <paramref name="from"/> broj slobodnih termina (za označavanje dana u kalendaru).</summary>
    public async Task<Dictionary<DateTime, int>> GetOpenDaysAsync(Service service, int? doctorId, DateTime from, int days)
    {
        var first = from.Date;
        var last = first.AddDays(days - 1);
        var snapshot = await LoadAsync(service, doctorId, first, last);
        return Enumerable.Range(0, days).Select(i => first.AddDays(i))
            .ToDictionary(d => d, d => ComputeSlots(snapshot, service, d).Count);
    }

    private sealed record Snapshot(List<Doctor> Doctors, List<TimeOff> TimeOffs, List<Appointment> Appointments);

    /// <summary>Učitava sve potrebno za period odjednom (3 upita), da kalendar od 60 dana ne pravi stotine upita.</summary>
    private async Task<Snapshot> LoadAsync(Service service, int? doctorId, DateTime from, DateTime to)
    {
        var doctors = await _db.Doctors.AsNoTracking()
            .Include(d => d.Shifts)
            .Where(d => d.IsActive && d.Services.Any(s => s.ServiceId == service.Id) && (doctorId == null || d.Id == doctorId))
            .OrderBy(d => d.SortOrder).ToListAsync();
        var ids = doctors.Select(d => d.Id).ToList();
        var end = to.AddDays(1);

        var timeOffs = await _db.TimeOffs.AsNoTracking()
            .Where(t => t.Date >= from && t.Date <= to && (t.DoctorId == null || ids.Contains(t.DoctorId.Value))).ToListAsync();
        var appointments = await _db.Appointments.AsNoTracking()
            .Where(a => ids.Contains(a.DoctorId) && a.Start >= from && a.Start < end
                        && (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed))
            .ToListAsync();
        return new Snapshot(doctors, timeOffs, appointments);
    }

    private List<SlotOption> ComputeSlots(Snapshot s, Service service, DateTime date)
    {
        var d = date.Date;
        var now = Now;
        if (d < now.Date || d > now.Date.AddDays(ClinicRules.MaxDaysAhead)) return [];
        if (s.TimeOffs.Any(t => t.Date == d && t.DoctorId is null)) return [];           // cijela ordinacija zatvorena

        var earliest = now.AddHours(ClinicRules.LeadTimeHours);
        var perDoctor = new List<(Doctor Doctor, List<TimeSpan> Starts, int Load)>();
        foreach (var doctor in s.Doctors)
        {
            if (s.TimeOffs.Any(t => t.Date == d && t.DoctorId == doctor.Id)) continue;
            var busy = s.Appointments.Where(a => a.DoctorId == doctor.Id && a.Start.Date == d)
                .Select(a => (a.Start, a.End)).ToList();
            var starts = SlotFinder.FreeStarts(d, service.DurationMinutes, doctor.Shifts, busy, earliest);
            perDoctor.Add((doctor, starts, busy.Count));
        }

        // za svako vrijeme jedna opcija; ako je slobodno više doktora, uzimamo onog s najmanje termina taj dan
        return perDoctor.SelectMany(x => x.Starts.Select(t => (Time: t, x.Doctor, x.Load)))
            .GroupBy(x => x.Time)
            .Select(g => g.OrderBy(x => x.Load).ThenBy(x => x.Doctor.SortOrder).First())
            .OrderBy(x => x.Time)
            .Select(x => new SlotOption(x.Time, x.Doctor))
            .ToList();
    }

    // ---------- zakazivanje ----------

    public async Task<BookingOutcome> BookAsync(BookingRequest request)
    {
        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServiceId && s.IsActive);
        if (service is null) return new(null, "Odabrana usluga nije dostupna.");

        var name = request.PatientName.Trim();
        var phone = request.Phone.Trim();
        var email = request.Email.Trim();
        if (name.Length < 2) return new(null, "Unesite ime i prezime.");
        if (phone.Count(char.IsDigit) < 6) return new(null, "Unesite ispravan broj telefona.");
        if (!email.Contains('@') || email.Length < 5) return new(null, "Unesite ispravnu email adresu.");

        await WriteLock.WaitAsync();
        try
        {
            if (await HasTooManyActiveAsync(email, phone))
                return new(null, $"Na ovu email adresu ili broj telefona već je zakazano {ClinicRules.MaxActiveBookingsPerContact} termina. "
                                 + "Za dodatne termine nas pozovite ili otkažite neki od postojećih.");

            // Provjera iz pravih podataka u trenutku upisa (ne vjerujemo onome što je browser pokazao prije minut).
            var slots = await GetSlotsAsync(service, request.DoctorId, request.Date);
            var chosen = slots.FirstOrDefault(s => s.Time == request.Time);
            if (chosen is null)
                return new(null, "Ovaj termin više nije slobodan. Odaberite drugo vrijeme.");

            var appointment = new Appointment
            {
                ServiceId = service.Id,
                DoctorId = chosen.Doctor.Id,
                Start = request.Date.Date + request.Time,
                DurationMinutes = service.DurationMinutes,
                PatientName = name,
                Phone = phone,
                Email = email,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                FirstVisit = request.FirstVisit,
                Status = AppointmentStatus.Scheduled,
                CreatedAt = _clock.GetUtcNow().UtcDateTime,
            };
            do { appointment.Reference = NewReference(); }
            while (await _db.Appointments.AnyAsync(a => a.Reference == appointment.Reference));

            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync();
            // Namjerno ne postavljamo appointment.Doctor/.Service: ti objekti su učitani bez praćenja,
            // pa bi ih EF pokušao ponovo "prikačiti" i pao pri idućem upisu u istom kontekstu.
            return new(appointment, null);
        }
        finally { WriteLock.Release(); }
    }

    /// <summary>Šifra bez zbunjujućih znakova (0/O, 1/I/L), npr. DS-K7M2PQ.</summary>
    public static string NewReference()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        // kriptografski generator, ne Random: šifre se ne smiju moći predvidjeti iz prethodnih
        return "DS-" + new string(Enumerable.Range(0, 6).Select(_ => alphabet[System.Security.Cryptography.RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
    }

    /// <summary>Da li ista email adresa ili isti telefon već ima najviše dozvoljenih budućih termina.</summary>
    private async Task<bool> HasTooManyActiveAsync(string email, string phone)
    {
        var now = Now;
        var upcoming = await _db.Appointments.AsNoTracking()
            .Where(a => (a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed) && a.Start >= now)
            .Select(a => new { a.Email, a.Phone }).ToListAsync();
        var digits = DigitsOf(phone);
        var count = upcoming.Count(a => string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase)
                                        || (digits.Length >= 6 && DigitsOf(a.Phone) == digits));
        return count >= ClinicRules.MaxActiveBookingsPerContact;
    }

    // "+387 61 111-222" i "061111222" su različito napisani, ali slični brojevi se porede po ciframa
    private static string DigitsOf(string phone) => new(phone.Where(char.IsDigit).ToArray());

    // ---------- otkazivanje i statusi ----------

    /// <summary>Pacijent može otkazati do 24 sata prije termina. Osoblje bilo kad.</summary>
    public bool CanCancel(Appointment a, bool byStaff = false) =>
        a.IsActive && (byStaff || a.Start - Now >= TimeSpan.FromHours(ClinicRules.CancelNoticeHours));

    public async Task<bool> CancelAsync(string reference, bool byStaff = false)
    {
        var a = await _db.Appointments.FirstOrDefaultAsync(x => x.Reference == reference);
        if (a is null || !CanCancel(a, byStaff)) return false;
        a.Status = AppointmentStatus.Cancelled;
        a.CancelledAt = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Promjena statusa iz admin panela. Vraća poruku greške ili null ako je sve u redu.</summary>
    public async Task<string?> SetStatusAsync(int id, AppointmentStatus status)
    {
        await WriteLock.WaitAsync();
        try
        {
            var a = await _db.Appointments.Include(x => x.Service).FirstOrDefaultAsync(x => x.Id == id);
            if (a is null) return "Termin ne postoji.";
            if (a.Status == status) return null;

            switch (status)
            {
                case AppointmentStatus.Cancelled:
                    if (!a.IsActive) return "Samo aktivan termin se može otkazati.";
                    a.CancelledAt = _clock.GetUtcNow().UtcDateTime;
                    break;

                case AppointmentStatus.Confirmed:
                case AppointmentStatus.Scheduled:
                    if (a.Status == AppointmentStatus.Cancelled || a.Status == AppointmentStatus.NoShow || a.Status == AppointmentStatus.Completed)
                    {
                        // vraćanje u aktivno stanje: termin mora još uvijek biti slobodan i radni
                        var error = await CanReactivateAsync(a);
                        if (error is not null) return error;
                        a.CancelledAt = null;
                    }
                    break;

                case AppointmentStatus.Completed:
                case AppointmentStatus.NoShow:
                    if (a.Status == AppointmentStatus.Cancelled) return "Otkazan termin ne može biti završen. Prvo ga vratite.";
                    if (a.Start > Now) return "Termin se još nije dogodio.";
                    break;
            }
            a.Status = status;
            await _db.SaveChangesAsync();
            return null;
        }
        finally { WriteLock.Release(); }
    }

    private async Task<string?> CanReactivateAsync(Appointment a)
    {
        var day = a.Start.Date;
        if (await _db.TimeOffs.AnyAsync(t => t.Date == day && (t.DoctorId == null || t.DoctorId == a.DoctorId)))
            return "Taj dan ordinacija ili doktor ne radi, termin nije moguće vratiti.";
        // preklapanje računamo u memoriji: SQLite ne zna sabirati datum i trajanje iz kolone
        var sameDay = await _db.Appointments.AsNoTracking()
            .Where(x => x.Id != a.Id && x.DoctorId == a.DoctorId && x.Start >= day && x.Start < day.AddDays(1)
                        && (x.Status == AppointmentStatus.Scheduled || x.Status == AppointmentStatus.Confirmed))
            .ToListAsync();
        var overlaps = sameDay.Any(x => x.Start < a.End && x.End > a.Start);
        return overlaps ? "Termin je u međuvremenu zauzet, nije ga moguće vratiti." : null;
    }
}
