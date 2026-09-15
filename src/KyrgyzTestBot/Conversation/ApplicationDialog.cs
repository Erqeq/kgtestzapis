using System.Collections.Concurrent;
using System.Diagnostics;
using KyrgyzTestBot.Applicants;
using KyrgyzTestBot.KyrgyzTestApi;
using KyrgyzTestBot.Registration;
using Microsoft.Extensions.Options;

namespace KyrgyzTestBot.Conversation;

/// <summary>
/// Сценарий заполнения заявки: по вопросу на шаг, в конце подтверждение. Черновики живут в памяти, после перезапуска незаконченную анкету придётся начать заново
/// </summary>
public sealed class ApplicationDialog(ApplicantStore store, KyrgyzTestApiClient api, IOptions<BotOptions> options)
{
    private const string Yes = "Да";
    private const string No = "Нет";

    private const string Help =
        """
        Запишу тебя на Кыргыз тест, как только появится свободное место
        /start: заполнить заявку
        /cancel: удалить заявку

        Данные нужны только для записи и удаляются сразу после неё
        """;

    private readonly ConcurrentDictionary<long, ApplicantDraft> _drafts = new();

    public async Task<DialogReply> HandleAsync(long telegramId, string text, CancellationToken ct)
    {
        switch (text)
        {
            case "/cancel":
                _drafts.TryRemove(telegramId, out _);
                store.Remove(telegramId);
                return new DialogReply("Заявка удалена. /start: подать заново");

            case "/start" when store.Find(telegramId) is { } active:
                return new DialogReply($"Заявка уже есть, ищу место:\n\n{active.Describe()}\n\n/cancel: удалить");

            case "/start":
                var draft = new ApplicantDraft(telegramId);
                _drafts[telegramId] = draft;
                var firstQuestion = await AskAsync(draft, ct);
                return firstQuestion with { Text = $"{Help}\n\n{firstQuestion.Text}" };
        }

        return _drafts.TryGetValue(telegramId, out var current)
            ? await AnswerAsync(current, text, ct)
            : new DialogReply(Help);
    }

    private async Task<DialogReply> AnswerAsync(ApplicantDraft draft, string text, CancellationToken ct)
    {
        if (draft.Step == DialogStep.Confirm)
            return Confirm(draft, text);

        var accepted = draft.Step switch
        {
            DialogStep.FullName => TrySet(InputParsers.FullName(text), value => draft.FullName = value),
            DialogStep.Inn => TrySet(InputParsers.Inn(text), value => draft.Inn = value),
            DialogStep.Phone => TrySet(InputParsers.Phone(text), value => draft.Phone = value),
            DialogStep.Language => TrySet(InputParsers.Language(text), value => draft.Language = value),
            DialogStep.City => TrySet(
                draft.AvailableCities.FirstOrDefault(c => c.Name.Equals(text, StringComparison.OrdinalIgnoreCase)),
                value => draft.City = value),
            DialogStep.Dates => TrySet(InputParsers.Dates(text, BookingCalendar.FirstDate(DateTime.UtcNow)), value => draft.Dates = value),
            DialogStep.Shifts => TrySet(InputParsers.Shifts(text), value => draft.Shifts = value),
            _ => throw new UnreachableException($"Неизвестный шаг {draft.Step}"),
        };

        if (!accepted)
        {
            var question = await AskAsync(draft, ct);
            return question with { Text = $"Не похоже на правильный ответ\n\n{question.Text}" };
        }

        draft.Step++;
        return await AskAsync(draft, ct);
    }

    private DialogReply Confirm(ApplicantDraft draft, string text)
    {
        _drafts.TryRemove(draft.TelegramId, out _);
        if (!text.Equals(Yes, StringComparison.OrdinalIgnoreCase))
            return new DialogReply("Не сохраняю. /start: заполнить заново");

        store.Upsert(draft.ToApplicant(DateTimeOffset.UtcNow));
        return new DialogReply(
            $"Заявка принята. Проверяю места раз в {options.Value.MinCheckInterval.TotalMinutes:0}–{options.Value.MaxCheckInterval.TotalMinutes:0} мин, напишу, как только запишу\n/cancel: отменить");
    }

    private async Task<DialogReply> AskAsync(ApplicantDraft draft, CancellationToken ct)
    {
        switch (draft.Step)
        {
            case DialogStep.FullName:
                return new DialogReply("ФИО полностью, как в паспорте:");
            case DialogStep.Inn:
                return new DialogReply("ИНН: 14 цифр");
            case DialogStep.Phone:
                return new DialogReply("Номер телефона, например 0700123456:");
            case DialogStep.Language:
                return new DialogReply("Язык:", [InputParsers.Russian, InputParsers.Kyrgyz]);
            case DialogStep.City:
                draft.AvailableCities = (await api.GetCitiesAsync(ct)).Where(c => c.IsActive).ToList();
                return new DialogReply("Город сдачи:", draft.AvailableCities.Select(c => c.Name).ToList());
            case DialogStep.Dates:
                var firstDate = BookingCalendar.FirstDate(DateTime.UtcNow);
                return new DialogReply(
                    $"Даты через пробел, не раньше {firstDate:dd.MM.yyyy}, например: {firstDate:dd.MM.yyyy} {firstDate.AddDays(1):dd.MM.yyyy}",
                    [InputParsers.Any]);
            case DialogStep.Shifts:
                return new DialogReply("Смена:", [ShiftExtensions.MorningTime, ShiftExtensions.AfternoonTime, InputParsers.Any]);
            case DialogStep.Confirm:
                return new DialogReply($"Проверь данные:\n\n{draft.ToApplicant(DateTimeOffset.UtcNow).Describe()}\n\nВсё верно?", [Yes, No]);
            default:
                throw new UnreachableException($"Неизвестный шаг {draft.Step}");
        }
    }

    private static bool TrySet<T>(T? value, Action<T> set) where T : class
    {
        if (value is null) return false;
        set(value);
        return true;
    }
}
