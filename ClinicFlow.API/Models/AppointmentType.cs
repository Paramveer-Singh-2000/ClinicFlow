namespace ClinicFlow.API.Models
{
    public enum AppointmentType
    {
        Cleaning = 1,
        Checkup = 2,
        Filling = 3,
        RootCanal = 4,
        Extraction = 5
    }

    public static class AppointmentTypeExtensions
    {
        public static TimeSpan Duration(this AppointmentType type) => type switch
        {
            AppointmentType.Cleaning => TimeSpan.FromMinutes(30),
            AppointmentType.Checkup => TimeSpan.FromMinutes(30),
            AppointmentType.Filling => TimeSpan.FromMinutes(60),
            AppointmentType.RootCanal => TimeSpan.FromMinutes(90),
            AppointmentType.Extraction => TimeSpan.FromMinutes(60),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown appointment type.")
        };
    }
}
