namespace KyrgyzTestBot.Applicants;

/// <summary>Заявка на запись: данные для регистрации и пожелания по дате и смене</summary>
public sealed record Applicant(
    long TelegramId,
    string FullName,
    string Inn,
    string Phone,
    string Language,
    int CityId,
    string CityName,
    IReadOnlyList<DateOnly> Dates,
    IReadOnlyList<Shift> Shifts,
    DateTimeOffset CreatedAt,
    bool RejectionNotified = false)
{
    public string Describe() =>
        $"""
        ФИО: {FullName}
        ИНН: {Inn}
        Телефон: {Phone}
        Язык: {(Language == "kg" ? "кыргызский" : "русский")}
        Город: {CityName}
        Даты: {(Dates.Count == 0 ? "любая" : string.Join(", ", Dates.Select(d => d.ToString("dd.MM.yyyy"))))}
        Смена: {string.Join(", ", Shifts.Select(s => s.ToTime()))}
        """;
}
