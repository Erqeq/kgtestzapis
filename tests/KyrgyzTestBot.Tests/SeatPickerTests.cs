using KyrgyzTestBot.Applicants;
using KyrgyzTestBot.KyrgyzTestApi;
using KyrgyzTestBot.Registration;

namespace KyrgyzTestBot.Tests;

public class SeatPickerTests
{
    private static readonly City Bishkek = new(1, "Бишкек", true);
    private static readonly City Manas = new(4, "Манас (Жалал-Абад)", true);

    [Fact]
    public void TakesEarliestDateAndShiftsInPriorityOrder()
    {
        var picker = new SeatPicker([Day(Bishkek, 22, morningFree: 5, afternoonFree: 5, morningId: 20), Day(Bishkek, 21, morningFree: 0, afternoonFree: 3, morningId: 10)]);

        var seat = picker.TryTake(CreateApplicant(1, Bishkek));

        Assert.Equal(new Seat(new DateOnly(2026, 9, 21), Shift.Afternoon, 11), seat);
    }

    [Fact]
    public void DoesNotGiveTheSameSeatTwice()
    {
        var picker = new SeatPicker([Day(Bishkek, 21, morningFree: 1, afternoonFree: 0, morningId: 10)]);

        Assert.NotNull(picker.TryTake(CreateApplicant(1, Bishkek)));
        Assert.Null(picker.TryTake(CreateApplicant(2, Bishkek)));
    }

    [Fact]
    public void ReleasedSeatGoesToNextApplicant()
    {
        var picker = new SeatPicker([Day(Bishkek, 21, morningFree: 1, afternoonFree: 0, morningId: 10)]);

        var seat = picker.TryTake(CreateApplicant(1, Bishkek))!;
        picker.Release(seat);

        Assert.Equal(seat, picker.TryTake(CreateApplicant(2, Bishkek)));
    }

    [Theory]
    [InlineData("2026-09-15T17:59:00Z", "2026-09-16")]
    [InlineData("2026-09-15T18:00:00Z", "2026-09-17")]
    public void FirstBookableDateIsTomorrowInKyrgyzstan(string utcNow, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), BookingCalendar.FirstDate(DateTime.Parse(utcNow).ToUniversalTime()));

    [Fact]
    public void RespectsCityDatesAndShifts()
    {
        var picker = new SeatPicker([Day(Bishkek, 21, 5, 5, morningId: 10), Day(Manas, 22, 5, 5, morningId: 20)]);

        Assert.Null(picker.TryTake(CreateApplicant(1, Manas, dates: [new DateOnly(2026, 9, 21)])));
        Assert.Equal(21, picker.TryTake(CreateApplicant(2, Manas, shifts: [Shift.Afternoon]))?.ScheduleId);
    }

    private static ScheduleDay Day(City city, int day, long morningFree, long afternoonFree, long morningId) =>
        new(city, new DateOnly(2026, 9, day), new ShiftValues(morningFree, afternoonFree), new ShiftValues(morningId, morningId + 1));

    private static Applicant CreateApplicant(long telegramId, City city, DateOnly[]? dates = null, Shift[]? shifts = null) =>
        new(telegramId, "Тест Тестов", "12345678901234", "0700123456", "ru", city.Id, city.Name,
            dates ?? Array.Empty<DateOnly>(),
            shifts ?? new[] { Shift.Morning, Shift.Afternoon },
            DateTimeOffset.UtcNow);
}
