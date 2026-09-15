using KyrgyzTestBot.Applicants;
using KyrgyzTestBot.KyrgyzTest;

namespace KyrgyzTestBot.Conversation;

/// <summary>Заявка в процессе заполнения</summary>
public sealed class ApplicantDraft(long telegramId)
{
    public long TelegramId { get; } = telegramId;
    public DialogStep Step { get; set; } = DialogStep.FullName;

    public string FullName { get; set; } = "";
    public string Inn { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Language { get; set; } = "";

    /// <summary>Города, которые показали кнопками на шаге выбора города</summary>
    public IReadOnlyList<City> AvailableCities { get; set; } = [];
    public City? City { get; set; }

    public IReadOnlyList<DateOnly> Dates { get; set; } = [];
    public IReadOnlyList<Shift> Shifts { get; set; } = [];

    public Applicant ToApplicant(DateTimeOffset createdAt)
    {
        var city = City ?? throw new InvalidOperationException("Город ещё не выбран");
        return new Applicant(TelegramId, FullName, Inn, Phone, Language, city.Id, city.Name, Dates, Shifts, createdAt);
    }
}
