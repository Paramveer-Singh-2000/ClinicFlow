using ClinicFlow.API.Models;
using Microsoft.EntityFrameworkCore;

namespace ClinicFlow.API.Data
{
    public class SeedData
    {
        public static async Task InitialiseAsync(ClinicDbContext db)
        {
            await db.Database.MigrateAsync();

            if (await db.Dentists.AnyAsync())
                return;

            var weekdays = new[]
            {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday
        };

            // Full-time general dentist: 9-5 with a lunch break.
            var chen = new Dentist
            {
                Name = "Dr. Emily Chen",
                Specialty = "General Dentistry",
                WorkingHours = weekdays.Select(day => new WorkingHours
                {
                    DayOfWeek = day,
                    StartTime = new TimeOnly(9, 0),
                    EndTime = new TimeOnly(17, 0),
                    BreakStart = new TimeOnly(12, 0),
                    BreakEnd = new TimeOnly(13, 0)
                }).ToList()
            };

            // Part-time specialist: three days, no break. Exercises the
            // "dentist doesn't work that day" and "no break" paths.
            var patel = new Dentist
            {
                Name = "Dr. Raj Patel",
                Specialty = "Endodontics",
                WorkingHours = new List<WorkingHours>
            {
                new() { DayOfWeek = DayOfWeek.Tuesday,  StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(16, 0) },
                new() { DayOfWeek = DayOfWeek.Wednesday, StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(16, 0) },
                new() { DayOfWeek = DayOfWeek.Thursday,  StartTime = new TimeOnly(10, 0), EndTime = new TimeOnly(16, 0) }
            }
            };

            db.Dentists.AddRange(chen, patel);

            var patients = new[]
            {
            new Patient { Name = "Sarah Miller",  PhoneNumber = "8055550142" },
            new Patient { Name = "James Okafor",  PhoneNumber = "8055550198" },
            new Patient { Name = "Maria Delgado", PhoneNumber = "8055550176" }
        };

            db.Patients.AddRange(patients);
            await db.SaveChangesAsync();

            // A few appointments on the next working day so the calendar is not empty.
            var nextMonday = NextWeekday(DateTime.UtcNow.Date, DayOfWeek.Monday);

            db.Appointments.AddRange(
                new Appointment
                {
                    DentistId = chen.Id,
                    PatientId = patients[0].Id,
                    Type = AppointmentType.Cleaning,
                    StartUtc = nextMonday.AddHours(9),          // 09:00–09:30
                    CreatedUtc = DateTime.UtcNow
                },
                new Appointment
                {
                    DentistId = chen.Id,
                    PatientId = patients[1].Id,
                    Type = AppointmentType.Filling,
                    StartUtc = nextMonday.AddHours(10).AddMinutes(30), // 10:30–11:30
                    CreatedUtc = DateTime.UtcNow
                },
                new Appointment
                {
                    DentistId = chen.Id,
                    PatientId = patients[2].Id,
                    Type = AppointmentType.Checkup,
                    StartUtc = nextMonday.AddHours(14),         // 14:00–14:30
                    CreatedUtc = DateTime.UtcNow
                });

            await db.SaveChangesAsync();
        }
        private static DateTime NextWeekday(DateTime from, DayOfWeek target)
        {
            var daysAhead = ((int)target - (int)from.DayOfWeek + 7) % 7;
            if (daysAhead == 0) daysAhead = 7;
            return DateTime.SpecifyKind(from.AddDays(daysAhead), DateTimeKind.Utc);
        }
    }
}
