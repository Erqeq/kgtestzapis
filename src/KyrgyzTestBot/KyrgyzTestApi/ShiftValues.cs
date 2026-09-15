using KyrgyzTestBot.Applicants;

namespace KyrgyzTestBot.KyrgyzTestApi;

/// <summary>Пара значений «утренняя смена / дневная смена» в ответах API</summary>
public sealed record ShiftValues(long Morning, long Afternoon)
{
    public long this[Shift shift] => shift == Shift.Morning ? Morning : Afternoon;
}
