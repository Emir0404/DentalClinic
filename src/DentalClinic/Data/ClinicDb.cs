using DentalClinic.Models;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Data;

public class ClinicDb : DbContext
{
    public ClinicDb(DbContextOptions<ClinicDb> options) : base(options) { }

    public DbSet<Service> Services => Set<Service>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorService> DoctorServices => Set<DoctorService>();
    public DbSet<DoctorShift> Shifts => Set<DoctorShift>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<TimeOff> TimeOffs => Set<TimeOff>();
    public DbSet<ContactMessage> Messages => Set<ContactMessage>();
    public DbSet<Article> Articles => Set<Article>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Service>().HasIndex(s => s.Slug).IsUnique();
        b.Entity<Service>().Property(s => s.PriceFrom).HasConversion<double>();
        b.Entity<Service>().Property(s => s.PriceTo).HasConversion<double?>();
        b.Entity<Doctor>().HasIndex(d => d.Slug).IsUnique();
        b.Entity<Article>().HasIndex(a => a.Slug).IsUnique();

        b.Entity<DoctorService>().HasKey(x => new { x.DoctorId, x.ServiceId });
        b.Entity<DoctorShift>().HasOne(s => s.Doctor).WithMany(d => d.Shifts).HasForeignKey(s => s.DoctorId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Appointment>().HasIndex(a => a.Reference).IsUnique();
        b.Entity<Appointment>().HasIndex(a => a.AccessToken).IsUnique();
        b.Entity<Appointment>().HasIndex(a => new { a.DoctorId, a.Start });
        b.Entity<Appointment>().Ignore(a => a.End).Ignore(a => a.IsActive);
        b.Entity<Appointment>().HasOne(a => a.Doctor).WithMany().HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Appointment>().HasOne(a => a.Service).WithMany().HasForeignKey(a => a.ServiceId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<TimeOff>().HasIndex(t => new { t.Date, t.DoctorId });
        b.Entity<TimeOff>().HasOne(t => t.Doctor).WithMany().HasForeignKey(t => t.DoctorId).OnDelete(DeleteBehavior.Cascade);
    }

    /// <summary>Primijeni migracije (pravi ili nadograđuje bazu) i puni je početnim sadržajem (usluge, doktori, članci).</summary>
    public static void Initialize(ClinicDb db)
    {
        db.MigrateAsync().GetAwaiter().GetResult();   // pravi bazu, nadograđuje je, ili preuzima staru koja je pravljena bez migracija
        if (!db.Services.Any()) SeedContent.AddServicesAndDoctors(db);
        if (!db.Articles.Any()) SeedContent.AddArticles(db);
    }
}
