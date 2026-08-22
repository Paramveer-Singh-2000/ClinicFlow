namespace ClinicFlow.API.Models
{
    public class WorkingHours
    {
        public int Id { get; set; }
        public int DentistId { get; set; }
        public Dentist Dentist { get; set; } = null!;
        public DayOfWeek DayOfWeek { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public TimeOnly? BreakStart { get; set; }
        public TimeOnly? BreakEnd { get; set; }
    }
}
