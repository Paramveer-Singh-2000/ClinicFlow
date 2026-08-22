namespace ClinicFlow.API.Models
{
    public class Appointment
    {
        public int Id { get; set; }
        public int DentistId { get; set; }
        public Dentist Dentist { get; set; } = null!;
        public int PatientId { get; set; }
        public Patient Patient { get; set; } = null!;
        public AppointmentType Type { get; set; }
        public DateTime StartUtc { get; set; }
        public TimeSpan Duration => Type.Duration();
        public DateTime EndUtc => StartUtc + Duration;
        public bool IsCancelled { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
