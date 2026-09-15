using KyrgyzTestBot.Applicants;

namespace KyrgyzTestBot.Registration;

public sealed record Seat(DateOnly Date, Shift Shift, long ScheduleId);
