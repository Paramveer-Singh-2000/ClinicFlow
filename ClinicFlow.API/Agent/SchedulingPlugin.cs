using ClinicFlow.API.Models;
using ClinicFlow.API.Scheduling;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace ClinicFlow.API.Agent
{
    public class SchedulingPlugin
    {
        private readonly SchedulingService _scheduling;

        public SchedulingPlugin(SchedulingService scheduling) => _scheduling = scheduling;

        [KernelFunction("get_today")]
        [Description("Gets today's date. Call this FIRST whenever the patient uses a relative date " +
                     "like 'tomorrow', 'next Tuesday', or 'this week', so you can work out the real date.")]
        public string GetToday()
        {
            var now = DateTime.UtcNow;
            return $"Today is {now:dddd, d MMMM yyyy}. The current time is {now:HH:mm} UTC.";
        }

        [KernelFunction("list_dentists")]
        [Description("Lists the clinic's dentists, their specialties, and which days they work. " +
                     "Use when the patient asks who they can see, or to pick a dentist for them.")]
        public async Task<string> ListDentistsAsync(CancellationToken ct = default)
        {
            var dentists = await _scheduling.GetDentistsAsync(ct);

            if (dentists.Count == 0)
                return "No dentists are configured.";

            var lines = dentists.Select(d =>
                $"ID {d.Id}: {d.Name} ({d.Specialty}), works {string.Join(", ", d.WorkingDays)}");

            return string.Join("\n", lines);
        }

        [KernelFunction("get_available_slots")]
        [Description("Finds open appointment times for a given date and treatment type. " +
                 "Always call this before offering any time to a patient — never guess availability. " +
                 "Returns a list of times with the dentist ID you must use to book.")]
        public async Task<string> GetAvailableSlotsAsync(
        [Description("The date, in yyyy-MM-dd format. Resolve relative dates with get_today first.")]
        string date,
        [Description("One of: Cleaning, Checkup, Filling, RootCanal, Extraction.")]
        string appointmentType,
        [Description("Optional dentist ID to restrict the search. Omit to search all dentists.")]
        int? dentistId = null,
        CancellationToken ct = default)
        {
            if (!DateOnly.TryParse(date, out var parsedDate))
                return $"'{date}' is not a valid date. Use yyyy-MM-dd format.";

            if (!Enum.TryParse<AppointmentType>(appointmentType, ignoreCase: true, out var type))
                return $"'{appointmentType}' is not a treatment we offer. " +
                       "Valid types: Cleaning, Checkup, Filling, RootCanal, Extraction.";

            if (parsedDate < DateOnly.FromDateTime(DateTime.UtcNow))
                return $"{parsedDate:dddd d MMMM} is in the past. Please ask for a future date.";

            var slots = await _scheduling.GetAvailableSlotsAsync(parsedDate, type, dentistId, ct);

            if (slots.Count == 0)
                return $"No {type} slots are available on {parsedDate:dddd d MMMM}. " +
                       "Suggest the patient tries another day.";

            var grouped = slots
                .GroupBy(s => (s.DentistId, s.DentistName))
                .Select(g => $"{g.Key.DentistName} (dentist ID {g.Key.DentistId}): " +
                             string.Join(", ", g.Select(s => s.StartUtc.ToString("HH:mm"))));

            return $"Available {type} slots on {parsedDate:dddd d MMMM} " +
                   $"(duration {type.Duration().TotalMinutes:0} minutes):\n" +
                   string.Join("\n", grouped);
        }

        [KernelFunction("book_appointment")]
        [Description("Books an appointment. Only call this after the patient has confirmed a specific " +
                "time from get_available_slots AND you have their full name and phone number. " +
                "Never invent a name or phone number — ask the patient for them.")]
        public async Task<string> BookAppointmentAsync(
       [Description("The dentist ID, taken from get_available_slots results.")]
        int dentistId,
       [Description("The patient's full name, as they gave it.")]
        string patientName,
       [Description("The patient's phone number.")]
        string phoneNumber,
       [Description("One of: Cleaning, Checkup, Filling, RootCanal, Extraction.")]
        string appointmentType,
       [Description("Appointment start in yyyy-MM-ddTHH:mm:ss format, exactly as offered.")]
        string startTime,
       CancellationToken ct = default)
        {
            if (!Enum.TryParse<AppointmentType>(appointmentType, ignoreCase: true, out var type))
                return $"'{appointmentType}' is not a treatment we offer.";

            if (!DateTime.TryParse(startTime, out var parsedStart))
                return $"'{startTime}' is not a valid time. Use yyyy-MM-ddTHH:mm:ss.";

            if (string.IsNullOrWhiteSpace(patientName))
                return "I need the patient's name before booking.";

            if (string.IsNullOrWhiteSpace(phoneNumber))
                return "I need the patient's phone number before booking.";

            var result = await _scheduling.BookAsync(
                dentistId, patientName.Trim(), phoneNumber,
                type, DateTime.SpecifyKind(parsedStart, DateTimeKind.Utc), ct);

            return result.Message;
        }
        [KernelFunction("find_appointments")]
        [Description("Looks up a patient's upcoming appointments by phone number. " +
                 "Use before cancelling, so you know the appointment ID.")]
        public async Task<string> FindAppointmentsAsync(
        [Description("The patient's phone number.")]
        string phoneNumber,
        CancellationToken ct = default)
        {
            var found = await _scheduling.FindByPhoneAsync(phoneNumber, ct);

            if (found.Count == 0)
                return "No upcoming appointments are booked under that phone number.";

            var lines = found.Select(a =>
                $"Appointment ID {a.Id}: {a.Type} with {a.DentistName} on " +
                $"{a.StartUtc:dddd d MMMM} at {a.StartUtc:HH:mm} for {a.PatientName}");

            return string.Join("\n", lines);
        }

        [KernelFunction("cancel_appointment")]
        [Description("Cancels an appointment. Requires both the appointment ID and the phone number " +
                 "it was booked under — the phone number must match or the cancellation is refused. " +
                 "Confirm with the patient before calling this.")]
        public async Task<string> CancelAppointmentAsync(
        [Description("The appointment ID, from find_appointments.")]
        int appointmentId,
        [Description("The phone number the appointment was booked under.")]
        string phoneNumber,
        CancellationToken ct = default)
        {
            var result = await _scheduling.CancelAsync(appointmentId, phoneNumber, ct);
            return result.Message;
        }

    }

}
