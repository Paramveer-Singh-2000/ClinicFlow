using ClinicFlow.API.Data;
using ClinicFlow.API.Models;
using Microsoft.EntityFrameworkCore;
using static ClinicFlow.Api.Scheduling.SchedulingResults;

namespace ClinicFlow.API.Scheduling
{
    public class SchedulingService
    {
        private readonly ClinicDbContext _db;

        public SchedulingService(ClinicDbContext db) => _db = db;

        public async Task<IReadOnlyList<(int DentistId, string DentistName, DateTime StartUtc)>>
            GetAvailableSlotsAsync(DateOnly date, AppointmentType type, int? dentistId = null, CancellationToken ct = default)
        {
            var dentists = await _db.Dentists
                .Include(d => d.WorkingHours)
                .Where(d => dentistId == null || d.Id == dentistId)
                .ToListAsync(ct);

            var dayStart = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var dayEnd = dayStart.AddDays(1);

            var appointments = await _db.Appointments
                .Where(a => !a.IsCancelled)
                .Where(a => a.StartUtc >= dayStart && a.StartUtc < dayEnd)
                .ToListAsync(ct);

            var results = new List<(int, string, DateTime)>();

            foreach (var dentist in dentists)
            {
                var hours = dentist.WorkingHours.FirstOrDefault(w => w.DayOfWeek == date.DayOfWeek);
                var theirs = appointments.Where(a => a.DentistId == dentist.Id);

                foreach (var slot in SlotCalculator.GetAvailableSlots(date, hours, theirs, type))
                    results.Add((dentist.Id, dentist.Name, slot));
            }

            return results.OrderBy(r => r.Item3).ThenBy(r => r.Item1).ToList();
        }

        public async Task<BookingResult> BookAsync(
            int dentistId,
            string patientName,
            string phoneNumber,
            AppointmentType type,
            DateTime startUtc,
            CancellationToken ct = default)
        {
            startUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
            var endUtc = startUtc + type.Duration();

            if (startUtc <= DateTime.UtcNow)
                return BookingResult.Fail("That time is in the past. Please choose a future time.");

            var dentist = await _db.Dentists
                .Include(d => d.WorkingHours)
                .FirstOrDefaultAsync(d => d.Id == dentistId, ct);

            if (dentist is null)
                return BookingResult.Fail("I couldn't find that dentist.");

            var date = DateOnly.FromDateTime(startUtc);
            var hours = dentist.WorkingHours.FirstOrDefault(w => w.DayOfWeek == date.DayOfWeek);

            if (hours is null)
                return BookingResult.Fail($"{dentist.Name} doesn't work on {date.DayOfWeek}s.");

            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            var sameDay = await _db.Appointments
                .Where(a => a.DentistId == dentistId && !a.IsCancelled)
                .Where(a => a.StartUtc >= date.ToDateTime(TimeOnly.MinValue)
                         && a.StartUtc < date.ToDateTime(TimeOnly.MinValue).AddDays(1))
                .ToListAsync(ct);

            var open = SlotCalculator.GetAvailableSlots(date, hours, sameDay, type);

            if (!open.Contains(startUtc))
                return BookingResult.Fail(
                    $"{startUtc:h:mm tt} on {startUtc:dddd d MMMM} isn't available for a {type} with {dentist.Name}.");

            var normalisedPhone = new string(phoneNumber.Where(char.IsDigit).ToArray());

            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.PhoneNumber == normalisedPhone, ct);

            if (patient is null)
            {
                patient = new Patient { Name = patientName, PhoneNumber = normalisedPhone };
                _db.Patients.Add(patient);
                await _db.SaveChangesAsync(ct);
            }

            var appointment = new Appointment
            {
                DentistId = dentistId,
                PatientId = patient.Id,
                Type = type,
                StartUtc = startUtc,
                CreatedUtc = DateTime.UtcNow
            };

            _db.Appointments.Add(appointment);

            try
            {
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync(ct);
                return BookingResult.Fail("That slot was just taken. Please pick another time.");
            }

            return BookingResult.Ok(
                appointment.Id,
                $"Booked: {type} with {dentist.Name} on {startUtc:dddd d MMMM} at {startUtc:h:mm tt} for {patient.Name}.");
        }

        public async Task<IReadOnlyList<AppointmentSummary>> FindByPhoneAsync(
         string phoneNumber, CancellationToken ct = default)
        {
            var normalised = new string(phoneNumber.Where(char.IsDigit).ToArray());

            var appointments = await _db.Appointments
                .Include(a => a.Dentist)
                .Include(a => a.Patient)
                .Where(a => a.Patient.PhoneNumber == normalised && !a.IsCancelled)
                .Where(a => a.StartUtc >= DateTime.UtcNow)
                .OrderBy(a => a.StartUtc)
                .ToListAsync(ct);

            return appointments
                .Select(a => new AppointmentSummary(
                    a.Id, a.Dentist.Name, a.Patient.Name, a.Type, a.StartUtc, a.EndUtc, a.IsCancelled))
                .ToList();
        }

        public async Task<BookingResult> CancelAsync(
            int appointmentId, string phoneNumber, CancellationToken ct = default)
        {
            var normalised = new string(phoneNumber.Where(char.IsDigit).ToArray());

            var appointment = await _db.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Dentist)
                .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);

            if (appointment is null)
                return BookingResult.Fail("I couldn't find that appointment.");

            if (appointment.Patient.PhoneNumber != normalised)
                return BookingResult.Fail("That appointment isn't under this phone number.");

            if (appointment.IsCancelled)
                return BookingResult.Fail("That appointment is already cancelled.");

            appointment.IsCancelled = true;
            await _db.SaveChangesAsync(ct);

            return BookingResult.Ok(
                appointment.Id,
                $"Cancelled: {appointment.Type} with {appointment.Dentist.Name} on {appointment.StartUtc:dddd d MMMM} at {appointment.StartUtc:h:mm tt}.");
        }

        public async Task<IReadOnlyList<(int Id, string Name, string Specialty, string[] WorkingDays)>>
            GetDentistsAsync(CancellationToken ct = default)
        {
            var dentists = await _db.Dentists
                .Include(d => d.WorkingHours)
                .ToListAsync(ct);

            return dentists.Select(d => (
                d.Id,
                d.Name,
                d.Specialty,
                d.WorkingHours
                    .OrderBy(w => w.DayOfWeek)
                    .Select(w => $"{w.DayOfWeek} {w.StartTime:HH\\:mm}-{w.EndTime:HH\\:mm}")
                    .ToArray()
            )).ToList();
        }
    }
}
