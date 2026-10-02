using ClinicFlow.API.Models;
using ClinicFlow.API.Scheduling;
using FluentAssertions;

namespace ClinicFlow.Tests;

public class SlotCalculatorTests
{
    private static readonly DateOnly Monday = new(2026, 3, 2);

    private static WorkingHours NineToFiveWithLunch() => new()
    {
        DayOfWeek = DayOfWeek.Monday,
        StartTime = new TimeOnly(9, 0),
        EndTime = new TimeOnly(17, 0),
        BreakStart = new TimeOnly(12, 0),
        BreakEnd = new TimeOnly(13, 0)
    };

    private static WorkingHours TenToFourNoBreak() => new()
    {
        DayOfWeek = DayOfWeek.Monday,
        StartTime = new TimeOnly(10, 0),
        EndTime = new TimeOnly(16, 0)
    };

    private static Appointment At(int hour, int minute, AppointmentType type) => new()
    {
        DentistId = 1,
        PatientId = 1,
        Type = type,
        StartUtc = new DateTime(2026, 3, 2, hour, minute, 0, DateTimeKind.Utc)
    };

    private static TimeOnly[] Times(IEnumerable<DateTime> slots) =>
        slots.Select(s => TimeOnly.FromDateTime(s)).ToArray();

    [Fact]
    public void EmptyDay_ReturnsSlotsAcrossWholeMorningAndAfternoon()
    {
        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), Array.Empty<Appointment>(), AppointmentType.Cleaning);

        Times(slots).Should().StartWith(new[] { new TimeOnly(9, 0), new TimeOnly(9, 30) });
        Times(slots).Should().Contain(new TimeOnly(16, 30));
    }

    [Fact]
    public void LastSlotEndsExactlyAtClosingTime()
    {
        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), Array.Empty<Appointment>(), AppointmentType.Cleaning);

        Times(slots).Last().Should().Be(new TimeOnly(16, 30)); // 16:30 + 30m = 17:00
    }

    [Fact]
    public void AppointmentThatWouldRunPastClosingIsNotOffered()
    {
        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), Array.Empty<Appointment>(), AppointmentType.RootCanal);

        Times(slots).Last().Should().Be(new TimeOnly(15, 30)); // 15:30 + 90m = 17:00
    }

    [Fact]
    public void NoSlotsOverlapTheLunchBreak()
    {
        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), Array.Empty<Appointment>(), AppointmentType.Filling);

        Times(slots).Should().NotContain(new TimeOnly(11, 30)); // would run to 12:30
        Times(slots).Should().Contain(new TimeOnly(11, 0));     // ends exactly at 12:00
        Times(slots).Should().Contain(new TimeOnly(13, 0));
    }

    [Fact]
    public void ExistingAppointmentBlocksItsOwnSlot()
    {
        var booked = new[] { At(10, 0, AppointmentType.Cleaning) };

        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), booked, AppointmentType.Cleaning);

        Times(slots).Should().NotContain(new TimeOnly(10, 0));
        Times(slots).Should().Contain(new TimeOnly(10, 30));
    }

    [Fact]
    public void SlotEndingExactlyWhenAppointmentStartsIsStillAvailable()
    {
        var booked = new[] { At(10, 0, AppointmentType.Cleaning) };

        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), booked, AppointmentType.Cleaning);

        Times(slots).Should().Contain(new TimeOnly(9, 30)); // 09:30–10:00, touching but not overlapping
    }

    [Fact]
    public void LongAppointmentDoesNotFitInShortGap()
    {
        // Gap is 11:30–12:00, only 30 minutes.
        var booked = new[] { At(10, 30, AppointmentType.Filling) }; // 10:30–11:30

        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), booked, AppointmentType.Filling);

        Times(slots).Should().NotContain(new TimeOnly(11, 30));
    }

    [Fact]
    public void ShortAppointmentDoesFitInShortGap()
    {
        var booked = new[] { At(10, 30, AppointmentType.Filling) }; // 10:30–11:30

        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), booked, AppointmentType.Cleaning);

        Times(slots).Should().Contain(new TimeOnly(11, 30));
    }

    [Fact]
    public void CancelledAppointmentsDoNotBlockSlots()
    {
        var cancelled = At(10, 0, AppointmentType.Cleaning);
        cancelled.IsCancelled = true;

        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), new[] { cancelled }, AppointmentType.Cleaning);

        Times(slots).Should().Contain(new TimeOnly(10, 0));
    }

    [Fact]
    public void AppointmentOnAnotherDayIsIgnored()
    {
        var otherDay = new Appointment
        {
            DentistId = 1,
            PatientId = 1,
            Type = AppointmentType.Cleaning,
            StartUtc = new DateTime(2026, 3, 3, 10, 0, 0, DateTimeKind.Utc) // Tuesday
        };

        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), new[] { otherDay }, AppointmentType.Cleaning);

        Times(slots).Should().Contain(new TimeOnly(10, 0));
    }

    [Fact]
    public void NullWorkingHours_MeansDentistDoesNotWorkThatDay()
    {
        var slots = SlotCalculator.GetAvailableSlots(
            Monday, null, Array.Empty<Appointment>(), AppointmentType.Cleaning);

        slots.Should().BeEmpty();
    }

    [Fact]
    public void DayWithNoBreak_HasContinuousSlots()
    {
        var slots = SlotCalculator.GetAvailableSlots(
            Monday, TenToFourNoBreak(), Array.Empty<Appointment>(), AppointmentType.Cleaning);

        Times(slots).Should().Contain(new TimeOnly(12, 0));
        Times(slots).First().Should().Be(new TimeOnly(10, 0));
        Times(slots).Last().Should().Be(new TimeOnly(15, 30));
    }

    [Fact]
    public void FullyBookedDay_ReturnsNoSlots()
    {
        var booked = Enumerable.Range(0, 16)
            .Select(i => At(9 + i / 2, i % 2 == 0 ? 0 : 30, AppointmentType.Cleaning))
            .Where(a => a.StartUtc.Hour is not 12) // lunch is already blocked
            .ToArray();

        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), booked, AppointmentType.Cleaning);

        slots.Should().BeEmpty();
    }

    [Fact]
    public void AllReturnedSlotsAreUtc()
    {
        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), Array.Empty<Appointment>(), AppointmentType.Cleaning);

        slots.Should().OnlyContain(s => s.Kind == DateTimeKind.Utc);
    }

    [Fact]
    public void SlotsAreReturnedInChronologicalOrder()
    {
        var slots = SlotCalculator.GetAvailableSlots(
            Monday, NineToFiveWithLunch(), Array.Empty<Appointment>(), AppointmentType.Cleaning);

        slots.Should().BeInAscendingOrder();
    }
}