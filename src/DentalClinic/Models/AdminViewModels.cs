using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Models;

public class AppointmentFilter
{
    public string? Q { get; set; }
    /// <summary>all | active | scheduled | confirmed | completed | noshow | cancelled</summary>
    public string Status { get; set; } = "all";
    public int? DoctorId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;

    public IQueryable<Appointment> Apply(IQueryable<Appointment> q)
    {
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var t = Q.Trim().ToLower();
            q = q.Where(a => a.Reference.ToLower().Contains(t) || a.PatientName.ToLower().Contains(t)
                          || a.Email.ToLower().Contains(t) || a.Phone.Contains(t));
        }
        q = Status switch
        {
            "active" => q.Where(a => a.Status == AppointmentStatus.Scheduled || a.Status == AppointmentStatus.Confirmed),
            "scheduled" => q.Where(a => a.Status == AppointmentStatus.Scheduled),
            "confirmed" => q.Where(a => a.Status == AppointmentStatus.Confirmed),
            "completed" => q.Where(a => a.Status == AppointmentStatus.Completed),
            "noshow" => q.Where(a => a.Status == AppointmentStatus.NoShow),
            "cancelled" => q.Where(a => a.Status == AppointmentStatus.Cancelled),
            _ => q,
        };
        if (DoctorId is { } d) q = q.Where(a => a.DoctorId == d);
        if (From is { } f) q = q.Where(a => a.Start >= f.Date);
        if (To is { } t2) q = q.Where(a => a.Start < t2.Date.AddDays(1));
        return q;
    }
}

public class DashboardViewModel
{
    public List<Appointment> Today { get; set; } = [];
    public int TomorrowCount { get; set; }
    public int WeekCount { get; set; }
    public int UnhandledMessages { get; set; }
    public double NoShowRate { get; set; }
    public int Completed30Days { get; set; }
    public List<(DateTime Day, int Count)> NextDays { get; set; } = [];
    public List<ContactMessage> RecentMessages { get; set; } = [];
}

public class AppointmentListViewModel
{
    public required AppointmentFilter Filter { get; init; }
    public List<Appointment> Items { get; set; } = [];
    public List<Doctor> Doctors { get; set; } = [];
    public int Total { get; set; }
    public int PageSize { get; set; }
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}

public class ScheduleViewModel
{
    public DateTime Date { get; set; }
    public List<Doctor> Doctors { get; set; } = [];
    public List<Appointment> Appointments { get; set; } = [];
    public List<TimeOff> TimeOffs { get; set; } = [];
}

public class ServiceEditModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Naziv je obavezan."), MaxLength(120)] public string Name { get; set; } = "";
    [Required(ErrorMessage = "Unesite kategoriju."), MaxLength(60)] public string Category { get; set; } = "";
    [Required(ErrorMessage = "Unesite kratak opis."), MaxLength(300)] public string Summary { get; set; } = "";
    [MaxLength(60)] public string ImageName { get; set; } = "clinic-bright";
    public List<int> DoctorIds { get; set; } = [];
    public string Description { get; set; } = "";
    public string Steps { get; set; } = "";
    public string Aftercare { get; set; } = "";
    public string SuitableFor { get; set; } = "";
    [Range(30, 240, ErrorMessage = "Trajanje mora biti između 30 i 240 minuta.")] public int DurationMinutes { get; set; } = 30;
    [Range(0, 100000, ErrorMessage = "Cijena nije ispravna.")] public decimal PriceFrom { get; set; }
    [Range(0, 100000, ErrorMessage = "Cijena nije ispravna.")] public decimal? PriceTo { get; set; }
    [MaxLength(80)] public string Comfort { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class DoctorEditModel
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Ime je obavezno."), MaxLength(100)] public string Name { get; set; } = "";
    [Required, MaxLength(120)] public string Title { get; set; } = "";
    public string Bio { get; set; } = "";
    [Range(0, 60)] public int YearsExperience { get; set; }
    public bool IsActive { get; set; } = true;
    public List<int> ServiceIds { get; set; } = [];
}
