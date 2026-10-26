using System.Text.RegularExpressions;
using DentalClinic.Data;
using DentalClinic.Models;
using DentalClinic.Services;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Tests;

public class SlotAvailabilityTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Any_doctor_shows_the_union_of_everyone_who_does_the_service_without_duplicates()
    {
        // četvrtak: Lejla 09-12/13-17, Mia 13-19, Adnan 08-12/13-16 rade pregled
        var times = await _t.TimesAsync("pregled", null, TestDb.Thu);

        Assert.Contains("08:00", times);                          // samo Adnan
        Assert.Contains("18:30", times);                          // samo Mia
        Assert.Equal(times.Distinct().Count(), times.Count);      // 09:00 radi više doktora, ali se vidi jednom
        Assert.Equal(times.OrderBy(x => x).ToList(), times);      // poredano
        Assert.DoesNotContain("12:00", times);                    // niko ne radi u 12:00 (pauza)
        Assert.DoesNotContain("12:30", times);
    }

    [Fact]
    public async Task Specific_doctor_only_shows_that_doctors_hours()
    {
        var times = await _t.TimesAsync("pregled", TestDb.Mia, TestDb.Thu);

        Assert.Equal("13:00", times.First());
        Assert.Equal("18:30", times.Last());
        Assert.Equal(12, times.Count);
    }

    [Fact]
    public async Task Doctor_on_a_day_off_has_no_slots() =>
        Assert.Empty(await _t.TimesAsync("pregled", TestDb.Tarik, TestDb.Thu));     // Tarik ne radi četvrtkom

    [Fact]
    public async Task Doctor_who_does_not_offer_the_service_has_no_slots() =>
        Assert.Empty(await _t.TimesAsync("plombe", TestDb.Mia, TestDb.Thu));        // Mia je ortodont

    [Fact]
    public async Task Sunday_has_no_slots_for_anyone() =>
        Assert.Empty(await _t.TimesAsync("pregled", null, TestDb.Sun));

    [Fact]
    public async Task Saturday_only_has_the_orthodontist()
    {
        Assert.NotEmpty(await _t.TimesAsync("pregled", null, TestDb.Sat));
        Assert.Empty(await _t.TimesAsync("plombe", null, TestDb.Sat));
    }

    [Fact]
    public async Task Same_day_slots_respect_the_minimum_notice_of_two_hours()
    {
        // sada je srijeda 10:00, najranije 12:00. Tarik radi srijedom 10-13 i 14-18.
        var times = await _t.TimesAsync("pregled", TestDb.Tarik, TestDb.Wed);

        Assert.DoesNotContain("10:00", times);
        Assert.DoesNotContain("11:30", times);
        Assert.Equal("12:00", times.First());
    }

    [Fact]
    public async Task Past_days_and_days_beyond_sixty_are_not_available()
    {
        Assert.Empty(await _t.TimesAsync("pregled", null, TestDb.Wed.AddDays(-1)));
        Assert.Empty(await _t.TimesAsync("pregled", null, TestDb.Wed.AddDays(ClinicRules.MaxDaysAhead + 3)));
    }

    [Fact]
    public async Task Whole_clinic_closed_day_removes_all_slots()
    {
        _t.Db.TimeOffs.Add(new TimeOff { Date = TestDb.Thu, Reason = "Praznik" });
        await _t.Db.SaveChangesAsync();

        Assert.Empty(await _t.TimesAsync("pregled", null, TestDb.Thu));
    }

    [Fact]
    public async Task One_doctors_day_off_removes_only_that_doctor()
    {
        _t.Db.TimeOffs.Add(new TimeOff { Date = TestDb.Thu, DoctorId = _t.Doc(TestDb.Adnan).Id, Reason = "Odmor" });
        await _t.Db.SaveChangesAsync();

        var times = await _t.TimesAsync("pregled", null, TestDb.Thu);

        Assert.DoesNotContain("08:00", times);           // Adnan je jedini radio u 08:00
        Assert.Contains("09:00", times);                 // Lejla i dalje radi
    }

    [Fact]
    public async Task Open_days_counts_free_slots_per_day_in_one_call()
    {
        var days = await _t.Service.GetOpenDaysAsync(_t.Svc("pregled"), null, TestDb.Wed, 7);

        Assert.Equal(7, days.Count);
        Assert.Equal(0, days[TestDb.Sun]);
        Assert.True(days[TestDb.Thu] > 10);
        Assert.True(days[TestDb.Sat] > 0);
    }

    [Fact]
    public async Task Opening_a_booked_slot_changes_the_count_for_that_day()
    {
        var before = (await _t.Service.GetOpenDaysAsync(_t.Svc("pregled"), TestDb.Mia is var m ? _t.Doc(m).Id : null, TestDb.Thu, 1))[TestDb.Thu];
        await _t.MustBookAsync("pregled", TestDb.Mia, TestDb.Thu, "14:00");
        var after = (await _t.Service.GetOpenDaysAsync(_t.Svc("pregled"), _t.Doc(TestDb.Mia).Id, TestDb.Thu, 1))[TestDb.Thu];

        Assert.Equal(before - 1, after);
    }
}

public class BookingTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Valid_booking_is_saved_with_doctor_duration_reference_and_status()
    {
        var a = await _t.MustBookAsync("lijecenje-kanala", TestDb.Lejla, TestDb.Thu, "13:00");

        var saved = await _t.Db.Appointments.AsNoTracking().SingleAsync();
        Assert.Equal(a.Id, saved.Id);
        Assert.Equal(new DateTime(2026, 10, 15, 13, 0, 0), saved.Start);
        Assert.Equal(90, saved.DurationMinutes);
        Assert.Equal(new DateTime(2026, 10, 15, 14, 30, 0), saved.End);
        Assert.Equal(AppointmentStatus.Scheduled, saved.Status);
        Assert.Equal(_t.Doc(TestDb.Lejla).Id, saved.DoctorId);
        Assert.Matches(new Regex("^DS-[A-HJKMNP-Z2-9]{6}$"), saved.Reference);   // bez 0/O i 1/I/L
    }

    [Fact]
    public async Task Booking_removes_the_slot_from_the_available_list()
    {
        await _t.MustBookAsync("pregled", TestDb.Tarik, TestDb.Fri, "10:00");

        Assert.DoesNotContain("10:00", await _t.TimesAsync("pregled", TestDb.Tarik, TestDb.Fri));
    }

    [Fact]
    public async Task The_same_doctor_cannot_be_booked_twice_at_the_same_time()
    {
        await _t.MustBookAsync("pregled", TestDb.Tarik, TestDb.Fri, "10:00");

        var second = await _t.BookAsync("pregled", TestDb.Tarik, TestDb.Fri, "10:00", name: "Drugi Pacijent");

        Assert.False(second.Succeeded);
        Assert.Contains("više nije slobodan", second.Error);
        Assert.Equal(1, await _t.Db.Appointments.CountAsync());
    }

    [Fact]
    public async Task Longer_appointment_blocks_the_whole_time_it_lasts()
    {
        await _t.MustBookAsync("implantati", TestDb.Tarik, TestDb.Fri, "10:00");      // 60 min: 10:00-11:00

        Assert.False((await _t.BookAsync("pregled", TestDb.Tarik, TestDb.Fri, "10:30")).Succeeded);   // upada u sredinu
        Assert.True((await _t.BookAsync("pregled", TestDb.Tarik, TestDb.Fri, "11:00")).Succeeded);    // tačno poslije
    }

    [Fact]
    public async Task A_booking_that_would_run_into_the_lunch_break_is_refused()
    {
        // Lejla četvrtkom radi do 12:00. Kanal traje 90 min, pa 11:00 nije moguće
        var r = await _t.BookAsync("lijecenje-kanala", TestDb.Lejla, TestDb.Thu, "11:00");
        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task First_free_doctor_option_spreads_patients_over_the_available_doctors()
    {
        // četvrtak 09:30 pregled: slobodni su Lejla i Adnan
        var first = await _t.MustBookAsync("pregled", null, TestDb.Thu, "09:30");
        var second = await _t.MustBookAsync("pregled", null, TestDb.Thu, "09:30");

        Assert.NotEqual(first.DoctorId, second.DoctorId);
        var third = await _t.BookAsync("pregled", null, TestDb.Thu, "09:30");
        Assert.False(third.Succeeded);                                                // više nema slobodnog doktora
    }

    [Fact]
    public async Task First_free_doctor_prefers_the_one_with_fewer_appointments_that_day()
    {
        await _t.MustBookAsync("pregled", TestDb.Lejla, TestDb.Thu, "09:00");
        await _t.MustBookAsync("pregled", TestDb.Lejla, TestDb.Thu, "10:00");

        var a = await _t.MustBookAsync("pregled", null, TestDb.Thu, "11:00");         // Lejla ima 2, Adnan 0

        Assert.Equal(_t.Doc(TestDb.Adnan).Id, a.DoctorId);
    }

    [Fact]
    public async Task Cancelled_appointment_frees_the_slot_for_somebody_else()
    {
        var a = await _t.MustBookAsync("pregled", TestDb.Tarik, TestDb.Fri, "10:00");
        Assert.True(await _t.Service.CancelAsync(a.Reference));

        Assert.True((await _t.BookAsync("pregled", TestDb.Tarik, TestDb.Fri, "10:00")).Succeeded);
    }

    [Theory]
    [InlineData("A", "+38761123456", "a@b.ba", "ime")]
    [InlineData("Ana Anić", "123", "a@b.ba", "telefon")]
    [InlineData("Ana Anić", "+38761123456", "nije-email", "email")]
    public async Task Obviously_wrong_patient_data_is_refused(string name, string phone, string email, string what)
    {
        var r = await _t.BookAsync("pregled", TestDb.Tarik, TestDb.Fri, "10:00", name, phone, email);

        Assert.False(r.Succeeded, what);
        Assert.Empty(_t.Db.Appointments);
    }

    [Fact]
    public async Task Inactive_service_cannot_be_booked()
    {
        var s = await _t.Db.Services.SingleAsync(x => x.Slug == "pregled");
        s.IsActive = false;
        await _t.Db.SaveChangesAsync();

        var r = await _t.Service.BookAsync(new BookingRequest(s.Id, null, TestDb.Thu, TimeSpan.FromHours(9), "Ana Anić", "+38761123456", "a@b.ba", null, true));

        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task Inactive_doctor_gets_no_new_appointments()
    {
        var d = await _t.Db.Doctors.SingleAsync(x => x.Slug == TestDb.Tarik);
        d.IsActive = false;
        await _t.Db.SaveChangesAsync();

        Assert.Empty(await _t.TimesAsync("pregled", TestDb.Tarik, TestDb.Fri));
        Assert.False((await _t.BookAsync("pregled", TestDb.Tarik, TestDb.Fri, "10:00")).Succeeded);
    }

    [Fact]
    public async Task Patient_text_is_trimmed_and_blank_notes_are_stored_as_null()
    {
        var r = await _t.Service.BookAsync(new BookingRequest(_t.Svc("pregled").Id, _t.Doc(TestDb.Tarik).Id, TestDb.Fri,
            TimeSpan.FromHours(10), "  Ana Anić  ", " +387 61 123 456 ", " ana@primjer.ba ", "   ", false));

        Assert.True(r.Succeeded);
        Assert.Equal("Ana Anić", r.Appointment!.PatientName);
        Assert.Equal("ana@primjer.ba", r.Appointment.Email);
        Assert.Null(r.Appointment.Notes);
        Assert.False(r.Appointment.FirstVisit);
    }

    [Fact]
    public async Task Simultaneous_requests_for_the_same_slot_produce_exactly_one_appointment()
    {
        var serviceId = _t.Svc("pregled").Id;
        var doctorId = _t.Doc(TestDb.Tarik).Id;

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async i =>
        {
            using var other = _t.NewContext();
            var svc = new AppointmentService(other, _t.Clock);
            return await svc.BookAsync(new BookingRequest(serviceId, doctorId, TestDb.Fri, TimeSpan.FromHours(10),
                $"Pacijent {i}", "+38761123456", "p@primjer.ba", null, true));
        }));

        Assert.Equal(1, results.Count(r => r.Succeeded));
        Assert.Equal(1, await _t.Db.Appointments.CountAsync());
    }

    [Fact]
    public async Task References_are_unique()
    {
        var refs = new HashSet<string>();
        for (var i = 0; i < 5; i++)
        {
            var a = await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Thu, TimeSpan.FromMinutes(8 * 60 + i * 30).ToString(@"hh\:mm"));
            refs.Add(a.Reference);
        }
        Assert.Equal(5, refs.Count);
    }
}

public class CancelAndStatusTests : IDisposable
{
    private readonly TestDb _t = new();
    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Patient_can_cancel_with_24_hours_notice_or_more()
    {
        var a = await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Thu, "10:00");     // sad je sri 10:00: tačno 24 h

        Assert.True(_t.Service.CanCancel(a));
        Assert.True(await _t.Service.CancelAsync(a.Reference));

        var saved = await _t.Db.Appointments.AsNoTracking().SingleAsync();
        Assert.Equal(AppointmentStatus.Cancelled, saved.Status);
        Assert.NotNull(saved.CancelledAt);
    }

    [Fact]
    public async Task Patient_cannot_cancel_with_less_than_24_hours_notice_but_staff_can()
    {
        var a = await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Thu, "09:30");     // 23,5 h unaprijed

        Assert.False(_t.Service.CanCancel(a));
        Assert.False(await _t.Service.CancelAsync(a.Reference));
        Assert.Equal(AppointmentStatus.Scheduled, (await _t.Db.Appointments.AsNoTracking().SingleAsync()).Status);

        Assert.True(await _t.Service.CancelAsync(a.Reference, byStaff: true));
    }

    [Fact]
    public async Task Cancelling_unknown_or_already_cancelled_returns_false()
    {
        var a = await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Fri, "09:00");

        Assert.False(await _t.Service.CancelAsync("DS-NEMA00"));
        Assert.True(await _t.Service.CancelAsync(a.Reference));
        Assert.False(await _t.Service.CancelAsync(a.Reference));
    }

    [Fact]
    public async Task Staff_can_confirm_and_cancel()
    {
        var a = await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Fri, "09:00");

        Assert.Null(await _t.Service.SetStatusAsync(a.Id, AppointmentStatus.Confirmed));
        Assert.Equal(AppointmentStatus.Confirmed, (await _t.Db.Appointments.AsNoTracking().SingleAsync()).Status);
        Assert.Null(await _t.Service.SetStatusAsync(a.Id, AppointmentStatus.Cancelled));
        Assert.Equal(AppointmentStatus.Cancelled, (await _t.Db.Appointments.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Future_appointment_cannot_be_marked_completed_or_no_show()
    {
        var a = await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Fri, "09:00");

        Assert.NotNull(await _t.Service.SetStatusAsync(a.Id, AppointmentStatus.Completed));
        Assert.NotNull(await _t.Service.SetStatusAsync(a.Id, AppointmentStatus.NoShow));
    }

    [Fact]
    public async Task After_the_visit_time_it_can_be_completed_or_marked_no_show()
    {
        var a = await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Thu, "10:00");
        _t.Clock.Now = new DateTime(2026, 10, 15, 11, 0, 0);

        Assert.Null(await _t.Service.SetStatusAsync(a.Id, AppointmentStatus.Completed));
        Assert.Null(await _t.Service.SetStatusAsync(a.Id, AppointmentStatus.NoShow));
    }

    [Fact]
    public async Task Cancelled_appointment_cannot_be_completed_before_it_is_restored()
    {
        var a = await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Thu, "10:00");
        await _t.Service.CancelAsync(a.Reference, byStaff: true);
        _t.Clock.Now = new DateTime(2026, 10, 15, 11, 0, 0);

        Assert.NotNull(await _t.Service.SetStatusAsync(a.Id, AppointmentStatus.Completed));
    }

    [Fact]
    public async Task Cancelled_appointment_can_be_restored_when_the_slot_is_still_free()
    {
        var a = await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Fri, "09:00");
        await _t.Service.CancelAsync(a.Reference, byStaff: true);

        Assert.Null(await _t.Service.SetStatusAsync(a.Id, AppointmentStatus.Scheduled));

        var saved = await _t.Db.Appointments.AsNoTracking().SingleAsync();
        Assert.Equal(AppointmentStatus.Scheduled, saved.Status);
        Assert.Null(saved.CancelledAt);
    }

    [Fact]
    public async Task Restoring_is_refused_when_somebody_else_took_the_slot()
    {
        var a = await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Fri, "09:00");
        await _t.Service.CancelAsync(a.Reference, byStaff: true);
        await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Fri, "09:00");

        var error = await _t.Service.SetStatusAsync(a.Id, AppointmentStatus.Scheduled);

        Assert.Contains("zauzet", error);
        Assert.Equal(AppointmentStatus.Cancelled, (await _t.Db.Appointments.AsNoTracking().SingleAsync(x => x.Id == a.Id)).Status);
    }

    [Fact]
    public async Task Restoring_is_refused_when_the_day_was_closed_in_the_meantime()
    {
        var a = await _t.MustBookAsync("pregled", TestDb.Adnan, TestDb.Fri, "09:00");
        await _t.Service.CancelAsync(a.Reference, byStaff: true);
        _t.Db.TimeOffs.Add(new TimeOff { Date = TestDb.Fri, Reason = "Oluja" });
        await _t.Db.SaveChangesAsync();

        Assert.NotNull(await _t.Service.SetStatusAsync(a.Id, AppointmentStatus.Scheduled));
    }

    [Fact]
    public async Task Unknown_appointment_gives_an_error_message() =>
        Assert.NotNull(await _t.Service.SetStatusAsync(9999, AppointmentStatus.Confirmed));
}
