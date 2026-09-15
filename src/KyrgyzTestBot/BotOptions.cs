namespace KyrgyzTestBot;

public sealed class BotOptions
{
    public const string SectionName = "Bot";

    /// <summary>Токен бота от @BotFather</summary>
    public string Token { get; init; } = "";

    /// <summary>Пауза между проверками мест выбирается случайно в диапазоне [Min; Max]</summary>
    public TimeSpan MinCheckInterval { get; init; } = TimeSpan.FromMinutes(3);

    public TimeSpan MaxCheckInterval { get; init; } = TimeSpan.FromMinutes(7);

    /// <summary>Папка, где лежит applicants.json</summary>
    public string DataDirectory { get; init; } = "data";
}
