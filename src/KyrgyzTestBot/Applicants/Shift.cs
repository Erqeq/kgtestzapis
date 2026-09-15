namespace KyrgyzTestBot.Applicants;

public enum Shift
{
    Morning,
    Afternoon,
}

public static class ShiftExtensions
{
    public const string MorningTime = "09:00";
    public const string AfternoonTime = "13:00";

    public static string ToTime(this Shift shift) => shift == Shift.Morning ? MorningTime : AfternoonTime;
}
