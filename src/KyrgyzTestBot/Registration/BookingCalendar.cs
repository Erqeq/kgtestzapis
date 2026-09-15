namespace KyrgyzTestBot.Registration;

/// <summary>Даты записи по времени Кыргызстана</summary>
public static class BookingCalendar
{
    public static readonly TimeSpan UtcOffset = TimeSpan.FromHours(6);

    /// <summary>Самая ранняя дата, на которую записываем: завтра. На сегодня не записываем, смена могла уже начаться</summary>
    public static DateOnly FirstDate(DateTime utcNow) => DateOnly.FromDateTime(utcNow + UtcOffset).AddDays(1);
}
