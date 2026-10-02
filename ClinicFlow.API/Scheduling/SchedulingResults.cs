using ClinicFlow.API.Models;

namespace ClinicFlow.Api.Scheduling
{
    public class SchedulingResults
    {
        public record BookingResult(bool Success, string Message, int? AppointmentId = null)
        {
            public static BookingResult Ok(int id, string message) => new(true, message, id);
            public static BookingResult Fail(string message) => new(false, message);
        }

        public record AppointmentSummary(
            int Id,
            string DentistName,
            string PatientName,
            AppointmentType Type,
            DateTime StartUtc,
            DateTime EndUtc,
            bool IsCancelled);
    }
}
