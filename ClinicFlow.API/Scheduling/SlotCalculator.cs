using ClinicFlow.API.Models;

namespace ClinicFlow.API.Scheduling
{
        public static class SlotCalculator
        {
            public static readonly TimeSpan SlotInterval = TimeSpan.FromMinutes(30);

            public static IReadOnlyList<DateTime> GetAvailableSlots(
                DateOnly date,
                WorkingHours? workingHours,
                IEnumerable<Appointment> existing,
                AppointmentType type)
            {
                if (workingHours is null)
                    return Array.Empty<DateTime>();

                var duration = type.Duration();

                var dayStart = ToUtc(date, workingHours.StartTime);
                var dayEnd = ToUtc(date, workingHours.EndTime);

                var blocks = existing
                    .Where(a => !a.IsCancelled)
                    .Where(a => DateOnly.FromDateTime(a.StartUtc) == date)
                    .Select(a => (Start: a.StartUtc, End: a.EndUtc))
                    .ToList();

                if (workingHours.BreakStart is { } breakStart && workingHours.BreakEnd is { } breakEnd)
                    blocks.Add((ToUtc(date, breakStart), ToUtc(date, breakEnd)));

                var slots = new List<DateTime>();

                for (var start = dayStart; start + duration <= dayEnd; start += SlotInterval)
                {
                    var end = start + duration;

                    var conflicts = blocks.Any(b => start < b.End && end > b.Start);

                    if (!conflicts)
                        slots.Add(start);
                }

                return slots;
            }

            private static DateTime ToUtc(DateOnly date, TimeOnly time) =>
                DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Utc);
        }
 }
