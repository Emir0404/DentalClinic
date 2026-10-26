using DentalClinic.Models;
using DentalClinic.Services;

namespace DentalClinic.Tests;

public class SlotFinderTests
{
    private static readonly DateTime Thursday = new(2026, 10, 15);
    private static readonly DateTime LongAgo = new(2020, 1, 1);

    private static DoctorShift Shift(DayOfWeek day, int fromH, int toH, int fromM = 0, int toM = 0) =>
        new() { Day = day, Start = new TimeSpan(fromH, fromM, 0), End = new TimeSpan(toH, toM, 0) };

    private static List<string> Starts(int minutes, DoctorShift[] shifts, (DateTime, DateTime)[]? busy = null, DateTime? earliest = null) =>
        SlotFinder.FreeStarts(Thursday, minutes, shifts, busy ?? [], earliest ?? LongAgo).Select(t => t.ToString(@"hh\:mm")).ToList();

    [Fact]
    public void Half_hour_service_gets_a_start_every_half_hour_of_the_shift() =>
        Assert.Equal(["09:00", "09:30", "10:00", "10:30", "11:00", "11:30"], Starts(30, [Shift(DayOfWeek.Thursday, 9, 12)]));

    [Fact]
    public void Longer_service_must_fit_completely_before_the_shift_ends() =>
        // 60 min u bloku 09-12: posljednji početak je 11:00 (završava tačno u 12:00)
        Assert.Equal(["09:00", "09:30", "10:00", "10:30", "11:00"], Starts(60, [Shift(DayOfWeek.Thursday, 9, 12)]));

    [Fact]
    public void Service_never_runs_through_the_lunch_break_between_two_shifts()
    {
        var shifts = new[] { Shift(DayOfWeek.Thursday, 9, 12), Shift(DayOfWeek.Thursday, 13, 17) };

        var starts = Starts(90, shifts);

        Assert.Equal("10:30", starts.Last(s => string.CompareOrdinal(s, "12:00") < 0));   // zadnji prije pauze: 10:30 do 12:00
        Assert.DoesNotContain("11:00", starts);                                            // 11:00 + 90 min bi prešlo u pauzu
        Assert.DoesNotContain("12:00", starts);
        Assert.DoesNotContain("12:30", starts);
        Assert.Contains("13:00", starts);
        Assert.Equal("15:30", starts.Last());                                              // 15:30 + 90 = 17:00
    }

    [Fact]
    public void Shift_shorter_than_the_service_gives_no_slots() =>
        Assert.Empty(Starts(90, [Shift(DayOfWeek.Thursday, 9, 10)]));         // blok od 60 min, a usluga traje 90

    [Fact]
    public void Day_without_a_shift_has_no_slots() =>
        Assert.Empty(Starts(30, [Shift(DayOfWeek.Friday, 9, 17)]));

    [Fact]
    public void Existing_appointment_blocks_every_start_that_would_overlap_it()
    {
        var busy = new[] { (Thursday.AddHours(10), Thursday.AddHours(11)) };   // zauzeto 10:00 do 11:00

        Assert.Equal(["09:00", "09:30", "11:00", "11:30"], Starts(30, [Shift(DayOfWeek.Thursday, 9, 12)], busy));
        // usluga od 60 min: 09:30 bi trajalo do 10:30 i sudarilo bi se, pa ostaje samo 09:00 i 11:00
        Assert.Equal(["09:00", "11:00"], Starts(60, [Shift(DayOfWeek.Thursday, 9, 12)], busy));
    }

    [Fact]
    public void Back_to_back_appointments_are_allowed()
    {
        var busy = new[] { (Thursday.AddHours(9), Thursday.AddHours(10)) };

        Assert.Contains("10:00", Starts(30, [Shift(DayOfWeek.Thursday, 9, 12)], busy));      // počinje tačno kad prethodni završi
    }

    [Fact]
    public void Starts_before_the_earliest_allowed_time_are_removed()
    {
        var starts = Starts(30, [Shift(DayOfWeek.Thursday, 9, 12)], earliest: Thursday.AddHours(10).AddMinutes(15));

        Assert.Equal(["10:30", "11:00", "11:30"], starts);
    }

    [Fact]
    public void Earliest_time_exactly_at_a_slot_keeps_that_slot()
    {
        var starts = Starts(30, [Shift(DayOfWeek.Thursday, 9, 12)], earliest: Thursday.AddHours(11));

        Assert.Equal(["11:00", "11:30"], starts);
    }
}
