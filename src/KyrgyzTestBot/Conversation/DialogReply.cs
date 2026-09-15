namespace KyrgyzTestBot.Conversation;

/// <summary>Ответ бота</summary>
public sealed record DialogReply(string Text, IReadOnlyList<string>? Buttons = null);
