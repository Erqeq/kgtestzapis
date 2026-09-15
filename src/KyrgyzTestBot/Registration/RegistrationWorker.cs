using KyrgyzTestBot.Applicants;
using KyrgyzTestBot.KyrgyzTest;
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
    private static readonly TimeSpan KyrgyzstanUtcOffset = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Проверяю места раз в {Min}–{Max}", options.Value.MinCheckInterval, options.Value.MaxCheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextDelay();
            logger.LogInformation("Следующая проверка мест в {Time:HH:mm:ss} (UTC+6)", DateTime.UtcNow + KyrgyzstanUtcOffset + delay);
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
        var queue = store.GetQueue();
        if (queue.Count == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow + KyrgyzstanUtcOffset);
        var schedule = new List<ScheduleDay>();
        foreach (var cityId in queue.Select(a => a.CityId).Distinct())
            schedule.AddRange(await api.GetScheduleAsync(cityId, today, ct));

        var picker = new SeatPicker(schedule);
        foreach (var applicant in queue)
        {
            if (picker.TryTake(applicant) is { } seat)
                await RegisterAsync(applicant, seat, ct);
        }
    }

    private async Task RegisterAsync(Applicant applicant, Seat seat, CancellationToken ct)
    {
        var when = $"{seat.Date:dd.MM.yyyy} в {seat.Shift.ToTime()}";
        var result = await api.RegisterAsync(applicant, seat.ScheduleId, ct);
        logger.LogInformation("Запись {TelegramId} на {When}: {Outcome}", applicant.TelegramId, when, result.Outcome);

        switch (result.Outcome)
        {
            case RegistrationOutcome.Registered:
                store.Remove(applicant.TelegramId);
                await NotifyAsync(applicant, $"✅ Записал на {when}. Проверь запись в @kyrgyztest_support_bot. Твои данные удалены", ct);
                break;

            case RegistrationOutcome.AlreadyRegistered:
                logger.LogInformation("Ответ сервера для {TelegramId}: {Detail}", applicant.TelegramId, result.Detail);
                store.Remove(applicant.TelegramId);
                await NotifyAsync(applicant, $"У тебя уже есть активная запись в Кыргызтест, новую не делаю. Заявку и данные удалил\n\n{result.Detail}", ct);
                break;

            case RegistrationOutcome.Rejected:
                logger.LogWarning("Сервер отказал {TelegramId}: {Detail}", applicant.TelegramId, result.Detail);
                if (applicant.RejectionNotified) break;

                store.Update(applicant.TelegramId, a => a with { RejectionNotified = true });
                await NotifyAsync(applicant, $"Нашёл место на {when}, но сервер отказал:\n\n{result.Detail}\n\nПродолжаю искать. Если ошибка в данных, используй /cancel и /start", ct);
                break;
        }
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
