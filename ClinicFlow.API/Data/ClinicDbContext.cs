using ClinicFlow.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Reflection.Emit;

namespace ClinicFlow.API.Data
{
    public class ClinicDbContext: DbContext
    {
        public ClinicDbContext(DbContextOptions<ClinicDbContext> options) : base(options) { }

        public DbSet<Dentist> Dentists => Set<Dentist>();
        public DbSet<Patient> Patients => Set<Patient>();
        public DbSet<Appointment> Appointments => Set<Appointment>();
        public DbSet<WorkingHours> WorkingHours => Set<WorkingHours>();

        private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
            new(v => v.ToUniversalTime(),
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Dentist>(e =>
            {
                e.HasKey(d => d.Id);
                e.Property(d => d.Name).IsRequired().HasMaxLength(100);
                e.Property(d => d.Specialty).IsRequired().HasMaxLength(100);
            });

            modelBuilder.Entity<Patient>(e =>
            {
                e.HasKey(p => p.Id);
                e.Property(p => p.Name).IsRequired().HasMaxLength(100);
                e.Property(p => p.PhoneNumber).IsRequired().HasMaxLength(20);
                e.HasIndex(p => p.PhoneNumber).IsUnique();
            });

            modelBuilder.Entity<WorkingHours>(e =>
            {
                e.HasKey(w => w.Id);

                e.HasOne(w => w.Dentist)
                 .WithMany(d => d.WorkingHours)
                 .HasForeignKey(w => w.DentistId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(w => new { w.DentistId, w.DayOfWeek }).IsUnique();
            });

            modelBuilder.Entity<Appointment>(e =>
            {
                e.HasKey(a => a.Id);

                e.Property(a => a.Type).HasConversion<int>();
                e.Property(a => a.StartUtc).HasConversion(UtcConverter);
                e.Property(a => a.CreatedUtc).HasConversion(UtcConverter);

                e.Ignore(a => a.Duration);
                e.Ignore(a => a.EndUtc);

                e.HasOne(a => a.Dentist)
                 .WithMany(d => d.Appointments)
                 .HasForeignKey(a => a.DentistId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(a => a.Patient)
                 .WithMany(p => p.Appointments)
                 .HasForeignKey(a => a.PatientId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(a => new { a.DentistId, a.StartUtc })
                 .IsUnique()
                 .HasFilter("[IsCancelled] = 0");

                e.HasIndex(a => new { a.DentistId, a.IsCancelled, a.StartUtc });
            });
        }
    }
}
