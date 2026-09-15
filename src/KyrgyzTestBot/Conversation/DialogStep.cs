namespace KyrgyzTestBot.Conversation;

/// <summary>Шаги анкеты в порядке, в котором бот задаёт вопросы</summary>
public enum DialogStep
{
    FullName,
    Inn,
    Phone,
    Language,
    City,
    Dates,
    Shifts,
    Confirm,
}
