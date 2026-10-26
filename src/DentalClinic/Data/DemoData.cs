using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Data;

/// <summary>
/// Izmišljeni pacijenti i termini da admin panel ne bude prazan pri prvom pokretanju (demo, screenshotovi).
/// Pokreće se samo ako je uključeno SeedDemoData i tek kad nema nijednog termina.
/// </summary>
public static class DemoData
{
    private static readonly string[] Names =
    [
        "Amra Hodžić", "Emir Mujić", "Lejla Begović", "Haris Kovač", "Selma Alić", "Adnan Delić", "Ivana Marić", "Damir Softić",
        "Nermina Jusić", "Kenan Tahirović", "Maja Petrović", "Armin Zukić", "Dina Husić", "Tarik Čengić", "Sara Babić",
        "Mirza Omerović", "Azra Karić", "Faruk Demirović", "Lana Ristić", "Elvedin Ćosić",
    ];

    public static void Seed(ClinicDb db, DateTime today)
    {
        if (db.Appointments.Any()) return;

        var rnd = new Random(2026);
        var doctors = db.Doctors.Include(d => d.Shifts).Include(d => d.Services).ThenInclude(s => s.Service).ToList();

        for (var offset = -21; offset <= 21; offset++)
        {
            var day = today.AddDays(offset);
            foreach (var doctor in doctors)
            {
                var offered = doctor.Services.Select(s => s.Service).Where(s => s.Slug != "hitni-slucaj" || rnd.Next(4) == 0).ToList();
                foreach (var shift in doctor.Shifts.Where(s => s.Day == day.DayOfWeek).OrderBy(s => s.Start))
                {
                    var t = shift.Start;
                    while (t < shift.End)
                    {
                        if (rnd.Next(100) < 38) { t += TimeSpan.FromMinutes(ClinicRules.SlotMinutes); continue; }   // slobodno
                        var service = offered[rnd.Next(offered.Count)];
                        var length = TimeSpan.FromMinutes(service.DurationMinutes);
                        if (t + length > shift.End) { t += TimeSpan.FromMinutes(ClinicRules.SlotMinutes); continue; }

                        var start = day + t;
                        var past = start < today.AddHours(DateTime.Now.Hour);
                        var roll = rnd.Next(100);
                        var status = past
                            ? roll < 84 ? AppointmentStatus.Completed : roll < 92 ? AppointmentStatus.NoShow : AppointmentStatus.Cancelled
                            : roll < 55 ? AppointmentStatus.Confirmed : roll < 92 ? AppointmentStatus.Scheduled : AppointmentStatus.Cancelled;
                        var name = Names[rnd.Next(Names.Length)];
                        db.Appointments.Add(new Appointment
                        {
                            Reference = AppointmentService.NewReference(),
                            Service = service, Doctor = doctor, Start = start, DurationMinutes = service.DurationMinutes,
                            PatientName = name, Phone = $"+38761{rnd.Next(100000, 999999)}",
                            Email = name.ToLowerInvariant().Replace(" ", ".").Replace("ž", "z").Replace("š", "s").Replace("ć", "c").Replace("č", "c").Replace("đ", "d") + "@primjer.ba",
                            FirstVisit = rnd.Next(100) < 30, Status = status,
                            CreatedAt = start.AddDays(-rnd.Next(2, 14)).ToUniversalTime(),
                            CancelledAt = status == AppointmentStatus.Cancelled ? start.AddDays(-1).ToUniversalTime() : null,
                            Notes = rnd.Next(100) < 12 ? "Imam osjetljive zube, molim blaži pristup." : null,
                        });
                        t += length;
                    }
                }
            }
        }

        var mia = doctors.First(d => d.Slug == "dr-mia-delic");
        db.TimeOffs.Add(new TimeOff { Date = today.AddDays(12), Reason = "Državni praznik" });
        db.TimeOffs.Add(new TimeOff { Date = today.AddDays(8), Doctor = mia, Reason = "Stručni kongres" });

        db.Messages.AddRange(
            new ContactMessage { Name = "Jasmina Kurtović", Email = "jasmina@primjer.ba", Phone = "+38761555123", CreatedAt = DateTime.UtcNow.AddHours(-5),
                Message = "Dobar dan, zanima me da li radite izbjeljivanje i koliko otprilike košta za dvije osobe? Hvala." },
            new ContactMessage { Name = "Omer Salihović", Email = "omer@primjer.ba", CreatedAt = DateTime.UtcNow.AddDays(-1),
                Message = "Treba mi potvrda o obavljenom pregledu za školu mog sina. Mogu li je preuzeti u ordinaciji?" },
            new ContactMessage { Name = "Dženana Hadžić", Email = "dzenana@primjer.ba", Phone = "+38762111222", CreatedAt = DateTime.UtcNow.AddDays(-4), Handled = true,
                Message = "Hvala na lijepom tretmanu, sve je prošlo odlično!" });

        db.SaveChanges();
    }
}
