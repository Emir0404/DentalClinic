using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models;

/// <summary>Jedna usluga ordinacije (pregled, plomba, implantat...). Trajanje određuje koliko termina zauzima u kalendaru.</summary>
public class Service
{
    public int Id { get; set; }
    [MaxLength(80)] public string Slug { get; set; } = "";
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(60)] public string Category { get; set; } = "";
    /// <summary>Jedna rečenica za kartice i listu.</summary>
    [MaxLength(300)] public string Summary { get; set; } = "";
    /// <summary>Šta je to, jednostavnim jezikom (odvojeni pasusi praznim redom).</summary>
    public string Description { get; set; } = "";
    /// <summary>Koraci zahvata, jedan po redu u tekstu.</summary>
    public string Steps { get; set; } = "";
    public string Aftercare { get; set; } = "";
    public string SuitableFor { get; set; } = "";
    /// <summary>Trajanje u minutama, uvijek višekratnik od 30 (veličina jednog termina).</summary>
    public int DurationMinutes { get; set; } = 30;
    public decimal PriceFrom { get; set; }
    public decimal? PriceTo { get; set; }
    /// <summary>Kratko o bolu/ugodnosti, npr. "Bezbolno" ili "Lokalna anestezija".</summary>
    [MaxLength(80)] public string Comfort { get; set; } = "";
    [MaxLength(60)] public string ImageName { get; set; } = "clinic-bright";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public string ImageUrl => $"/img/{ImageName}.jpg";
    public IEnumerable<string> StepList => Lines(Steps);
    public IEnumerable<string> AftercareList => Lines(Aftercare);
    public string PriceText => PriceTo is { } to && to > PriceFrom ? $"{PriceFrom:0} – {to:0} KM" : $"od {PriceFrom:0} KM";
    public string DurationText => DurationMinutes >= 60
        ? (DurationMinutes % 60 == 0 ? $"{DurationMinutes / 60} h" : $"{DurationMinutes / 60} h {DurationMinutes % 60} min")
        : $"{DurationMinutes} min";

    private static IEnumerable<string> Lines(string s) =>
        s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class Doctor
{
    public int Id { get; set; }
    [MaxLength(80)] public string Slug { get; set; } = "";
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(120)] public string Title { get; set; } = "";
    public string Bio { get; set; } = "";
    [MaxLength(60)] public string PhotoName { get; set; } = "";
    public int YearsExperience { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    /// <summary>
    /// Fotografije iz početnog sadržaja su zapisane bez nastavka ("doctor-lejla"), a one koje je admin otpremio s nastavkom
    /// ("doctor-ana-3f9a1c.jpg", u img/uploads). Ako fotografije nema, prikazuje se neutralna silueta.
    /// </summary>
    public string PhotoUrl =>
        string.IsNullOrEmpty(PhotoName) ? "/img/doctor-placeholder.svg"
        : PhotoName.Contains('.') ? $"/img/uploads/{PhotoName}"
        : $"/img/{PhotoName}.jpg";
    public List<DoctorService> Services { get; set; } = [];
    public List<DoctorShift> Shifts { get; set; } = [];
}

/// <summary>Koji doktor radi koju uslugu (veza "više-na-više").</summary>
public class DoctorService
{
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
}

/// <summary>Jedan radni blok doktora u sedmici, npr. utorak 09:00–12:00. Pauza je samo razmak između dva bloka.</summary>
public class DoctorShift
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;
    public DayOfWeek Day { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
}

public enum AppointmentStatus
{
    Scheduled = 0,   // zakazano (pacijent je zakazao)
    Confirmed = 1,   // potvrđeno (ordinacija potvrdila)
    Completed = 2,   // završeno
    NoShow = 3,      // pacijent nije došao
    Cancelled = 4,   // otkazano
}

public class Appointment
{
    public int Id { get; set; }
    [MaxLength(12)] public string Reference { get; set; } = "";

    /// <summary>Unguessable key for the patient's own pages (confirmation, calendar, cancel). The short Reference is only for people.</summary>
    [MaxLength(64)] public string AccessToken { get; set; } = DentalClinic.Services.Tokens.New();
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public int DoctorId { get; set; }
    public Doctor Doctor { get; set; } = null!;

    /// <summary>Početak termina (lokalno vrijeme ordinacije).</summary>
    public DateTime Start { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime End => Start.AddMinutes(DurationMinutes);

    [Required(ErrorMessage = "Unesite ime i prezime."), MaxLength(100)]
    public string PatientName { get; set; } = "";
    [Required(ErrorMessage = "Unesite broj telefona."), MaxLength(40)]
    public string Phone { get; set; } = "";
    [Required(ErrorMessage = "Unesite email adresu."), EmailAddress(ErrorMessage = "Email adresa nije ispravna."), MaxLength(150)]
    public string Email { get; set; } = "";
    [MaxLength(600)] public string? Notes { get; set; }
    public bool FirstVisit { get; set; } = true;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    [MaxLength(500)] public string? StaffNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    public bool IsActive => Status is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed;
}

/// <summary>Neradni dan: za cijelu ordinaciju (praznik) ili za jednog doktora (godišnji, kongres).</summary>
public class TimeOff
{
    public int Id { get; set; }
    /// <summary>Null = zatvorena cijela ordinacija.</summary>
    public int? DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public DateTime Date { get; set; }
    [MaxLength(120)] public string Reason { get; set; } = "";
}

public class ContactMessage
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Unesite ime."), MaxLength(100)] public string Name { get; set; } = "";
    [Required(ErrorMessage = "Unesite email adresu."), EmailAddress(ErrorMessage = "Email adresa nije ispravna."), MaxLength(150)] public string Email { get; set; } = "";
    [MaxLength(40)] public string? Phone { get; set; }
    [Required(ErrorMessage = "Napišite poruku."), MaxLength(2000)] public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool Handled { get; set; }
}

public class Article
{
    public int Id { get; set; }
    [MaxLength(100)] public string Slug { get; set; } = "";
    [MaxLength(160)] public string Title { get; set; } = "";
    [MaxLength(60)] public string Category { get; set; } = "";
    [MaxLength(320)] public string Excerpt { get; set; } = "";
    /// <summary>Jednostavan format: pasusi odvojeni praznim redom, "## " je podnaslov, "- " je stavka liste.</summary>
    public string Body { get; set; } = "";
    [MaxLength(60)] public string ImageName { get; set; } = "";
    public int ReadMinutes { get; set; }
    public DateTime PublishedAt { get; set; }

    public string ImageUrl => $"/img/{ImageName}.jpg";
}
