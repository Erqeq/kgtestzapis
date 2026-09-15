namespace KyrgyzTestBot.KyrgyzTest;

/// <summary>День в расписании</summary>
public sealed record ScheduleDay(City City, DateOnly Date, ShiftValues Available, ShiftValues ScheduleIdsDetailed);
