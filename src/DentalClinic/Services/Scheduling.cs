using DentalClinic.Models;

namespace DentalClinic.Services;

/// <summary>Pravila ordinacije na jednom mjestu. Mijenja se ovdje, a ne po kodu.</summary>
public static class ClinicRules
{
    /// <summary>Veličina jednog termina u minutama. Sve usluge traju višekratnik ovog broja.</summary>
    public const int SlotMinutes = 30;
    /// <summary>Najmanje koliko unaprijed se može zakazati (sati). Sprječava termin "za 5 minuta".</summary>
    public const int LeadTimeHours = 2;
    public const int MaxDaysAhead = 60;
    /// <summary>Pacijent može sam otkazati najkasnije toliko sati prije termina.</summary>
    public const int CancelNoticeHours = 24;
    /// <summary>Koliko budućih termina smije imati jedna email adresa ili jedan telefon. Sprječava da jedna osoba zauzme cijeli kalendar.</summary>
    public const int MaxActiveBookingsPerContact = 3;
}

public record SlotOption(TimeSpan Time, Doctor Doctor);

/// <summary>
/// Srce zakazivanja: iz radnog vremena doktora i već zauzetih termina računa koji su početci termina slobodni.
/// Čista funkcija (bez baze i bez sata), pa se lako testira.
/// </summary>
public static class SlotFinder
{
    /// <param name="date">Dan za koji tražimo termine.</param>
    /// <param name="durationMinutes">Koliko traje usluga.</param>
    /// <param name="shifts">Radni blokovi doktora (svi dani, filtriramo po datumu).</param>
    /// <param name="busy">Već zauzeti intervali tog doktora.</param>
    /// <param name="earliestStart">Najraniji dozvoljeni početak (sad + minimalno vrijeme unaprijed).</param>
    public static List<TimeSpan> FreeStarts(DateTime date, int durationMinutes, IEnumerable<DoctorShift> shifts,
        IReadOnlyCollection<(DateTime Start, DateTime End)> busy, DateTime earliestStart)
    {
        var result = new List<TimeSpan>();
        var duration = TimeSpan.FromMinutes(durationMinutes);
        var step = TimeSpan.FromMinutes(ClinicRules.SlotMinutes);

        foreach (var shift in shifts.Where(s => s.Day == date.DayOfWeek).OrderBy(s => s.Start))
        {
            // usluga mora cijela stati u radni blok: tako pauza (razmak između blokova) nikad ne bude probijena
            for (var t = shift.Start; t + duration <= shift.End; t += step)
            {
                var start = date.Date + t;
                var end = start + duration;
                if (start < earliestStart) continue;
                if (busy.Any(b => b.Start < end && b.End > start)) continue;   // preklapanje intervala
                result.Add(t);
            }
        }
        return result;
    }
}
