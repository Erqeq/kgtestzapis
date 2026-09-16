using KyrgyzTestBot.Applicants;
using KyrgyzTestBot.KyrgyzTestApi;

namespace KyrgyzTestBot.Registration;

/// <summary>
/// Раздаёт свободные места из одного снимка расписания и не выдаёт одно место двоим, пока расписание не перечитано
/// </summary>
public sealed class SeatPicker(IEnumerable<ScheduleDay> schedule, DateOnly firstDate)
{
    // Даты отсеиваем сами: date__gte в запросе расписания API игнорирует и всё равно отдаёт сегодняшний день
    private readonly List<ScheduleDay> _days = schedule.Where(d => d.Date >= firstDate).OrderBy(d => d.Date).ToList();
    private readonly Dictionary<long, long> _takenBySchedule = new();

    /// <summary>Самая ранняя подходящая дата, смены в порядке приоритета заявки</summary>
    public Seat? TryTake(Applicant applicant)
    {
        var days = _days.Where(d =>
            d.City.Id == applicant.CityId &&
            (applicant.Dates.Count == 0 || applicant.Dates.Contains(d.Date)));

        foreach (var day in days)
        {
            foreach (var shift in applicant.Shifts)
            {
                var scheduleId = day.ScheduleIdsDetailed[shift];
                var taken = _takenBySchedule.GetValueOrDefault(scheduleId);
                if (day.Available[shift] <= taken) continue;

                _takenBySchedule[scheduleId] = taken + 1;
                return new Seat(day.Date, shift, scheduleId);
            }
        }

        return null;
    }

    /// <summary>Возвращает место, на которое записать не вышло, чтобы его попробовал следующий в очереди</summary>
    public void Release(Seat seat) => _takenBySchedule[seat.ScheduleId]--;
}
