using KyrgyzTestBot.Applicants;
using KyrgyzTestBot.KyrgyzTestApi;
using Microsoft.Extensions.Options;
using Telegram.Bot;

namespace KyrgyzTestBot.Registration;

/// <summary>Периодически проверяет расписание и записывает людей из очереди на свободные места</summary>
public sealed class RegistrationWorker(
    ApplicantStore store,
    KyrgyzTestApiClient api,
    ITelegramBotClient bot,
    IOptions<BotOptions> options,
    ILogger<RegistrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Проверяю места раз в {Min}–{Max}", options.Value.MinCheckInterval, options.Value.MaxCheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextDelay();
            logger.LogInformation("Следующая проверка мест в {Time:HH:mm:ss} (UTC+6)", DateTime.UtcNow + BookingCalendar.UtcOffset + delay);
            await Task.Delay(delay, stoppingToken);

            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Проверка мест не удалась: {Error}", e.Message);
            }
        }
    }

    private TimeSpan NextDelay()
    {
        var min = options.Value.MinCheckInterval;
        var max = options.Value.MaxCheckInterval;
        return min + (max - min) * Random.Shared.NextDouble();
    }

    private async Task CheckAsync(CancellationToken ct)
    {
        var firstDate = BookingCalendar.FirstDate(DateTime.UtcNow);
        var queue = new List<Applicant>();
        foreach (var applicant in store.GetQueue())
        {
            if (applicant.Dates.Count > 0 && applicant.Dates.Max() < firstDate)
                await ExpireAsync(applicant, ct);
            else
                queue.Add(applicant);
        }

        if (queue.Count == 0) return;

        var schedule = new List<ScheduleDay>();
        foreach (var cityId in queue.Select(a => a.CityId).Distinct())
            schedule.AddRange(await api.GetScheduleAsync(cityId, firstDate, ct));

        var picker = new SeatPicker(schedule, firstDate);
        foreach (var applicant in queue)
        {
            if (picker.TryTake(applicant) is not { } seat) continue;
            if (!await TryRegisterAsync(applicant, seat, ct))
                picker.Release(seat);
        }
    }

    private async Task ExpireAsync(Applicant applicant, CancellationToken ct)
    {
        logger.LogInformation("Все даты заявки {TelegramId} прошли, удаляю", applicant.TelegramId);
        store.Remove(applicant.TelegramId);
        await NotifyAsync(applicant, "Все выбранные даты прошли, а места так и не появились. Заявку и данные удалил\n/start: подать заново", ct);
    }

    /// <returns>true, если человек записан на это место</returns>
    private async Task<bool> TryRegisterAsync(Applicant applicant, Seat seat, CancellationToken ct)
    {
        var when = $"{seat.Date:dd.MM.yyyy} в {seat.Shift.ToTime()}";
        RegistrationResult result;
        try
        {
            result = await api.RegisterAsync(applicant, seat.ScheduleId, ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Запись {TelegramId} на {When} не удалась: {Error}", applicant.TelegramId, when, e.Message);
            return false;
        }

        logger.LogInformation("Запись {TelegramId} на {When}: {Outcome}", applicant.TelegramId, when, result.Outcome);

        switch (result.Outcome)
        {
            case RegistrationOutcome.Registered:
                store.Remove(applicant.TelegramId);
                await NotifyAsync(applicant, $"✅ Записал на {when}. Проверь запись в @kyrgyztest_support_bot. Твои данные удалены", ct);
                return true;

            case RegistrationOutcome.AlreadyRegistered:
                logger.LogInformation("Ответ сервера для {TelegramId}: {Detail}", applicant.TelegramId, result.Detail);
                store.Remove(applicant.TelegramId);
                await NotifyAsync(applicant,
                    $"Кыргызтест ответил, что у тебя уже есть активная запись, новую не делаю. Проверь её в @kyrgyztest_support_bot: " +
                    $"если в прошлый раз сервер не успел ответить, это может быть моя запись. Заявку и данные удалил\n\n{result.Detail}", ct);
                break;

            case RegistrationOutcome.Rejected:
                logger.LogWarning("Сервер отказал {TelegramId}: {Detail}", applicant.TelegramId, result.Detail);
                if (applicant.RejectionNotified) break;

                store.Update(applicant.TelegramId, a => a with { RejectionNotified = true });
                await NotifyAsync(applicant, $"Нашёл место на {when}, но сервер отказал:\n\n{result.Detail}\n\nПродолжаю искать. Если ошибка в данных, используй /cancel и /start", ct);
                break;
        }

        return false;
    }

    private async Task NotifyAsync(Applicant applicant, string text, CancellationToken ct)
    {
        try
        {
            await bot.SendMessage(applicant.TelegramId, text, cancellationToken: ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Не смог написать {TelegramId}: {Error}", applicant.TelegramId, e.Message);
        }
    }
}
