namespace ClinicFlow.API.Models
{
    public class Dentist
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public ICollection<WorkingHours> WorkingHours { get; set; } = new List<WorkingHours>();
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
