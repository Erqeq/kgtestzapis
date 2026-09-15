using System.Globalization;
using System.Text.RegularExpressions;
using KyrgyzTestBot.Applicants;

namespace KyrgyzTestBot.Conversation;

/// <summary>Проверка и нормализация ответов пользователя. Возвращают null, если ответ не подходит</summary>
public static partial class InputParsers
{
    public const string Any = "Любая";
    public const string Russian = "Русский";
    public const string Kyrgyz = "Кыргызча";

    private static readonly string[] DateFormats = ["dd.MM.yyyy", "yyyy-MM-dd"];
    private static readonly char[] DateSeparators = [' ', ','];

    /// <summary>Минимум два слова, так же проверяет форма мини-аппа</summary>
    public static string? FullName(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 2 ? string.Join(' ', words) : null;
    }

    public static string? Inn(string text) => InnRegex().IsMatch(text) ? text : null;

    /// <summary>Приводит номер к формату, который отправляет мини-апп: 0 и 9 цифр</summary>
    public static string? Phone(string text)
    {
        var match = PhoneRegex().Match(NonDigitRegex().Replace(text, ""));
        return match.Success ? "0" + match.Groups[1].Value : null;
    }

    /// <summary>Код языка, который отправляет мини-апп: ru или kg</summary>
    public static string? Language(string text) => text.ToLowerInvariant() switch
    {
        "русский" or "ru" => "ru",
        "кыргызча" or "kg" => "kg",
        _ => null,
    };

    /// <summary>Пустой список означает любую дату</summary>
    public static IReadOnlyList<DateOnly>? Dates(string text)
    {
        if (text.Equals(Any, StringComparison.OrdinalIgnoreCase)) return [];

        var dates = new List<DateOnly>();
        foreach (var part in text.Split(DateSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!DateOnly.TryParseExact(part, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                return null;
            dates.Add(date);
        }

        return dates.Count > 0 ? dates : null;
    }

    public static IReadOnlyList<Shift>? Shifts(string text) => text switch
    {
        ShiftExtensions.MorningTime => [Shift.Morning],
        ShiftExtensions.AfternoonTime => [Shift.Afternoon],
        _ when text.Equals(Any, StringComparison.OrdinalIgnoreCase) => [Shift.Morning, Shift.Afternoon],
        _ => null,
    };

    [GeneratedRegex(@"^\d{14}$")]
    private static partial Regex InnRegex();

    [GeneratedRegex(@"^(?:996|0)?(\d{9})$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitRegex();
}
