using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace KyrgyzTestBot.Conversation;

/// <summary>Принимает личные сообщения боту через long polling и отвечает по сценарию <see cref="ApplicationDialog"/></summary>
public sealed class BotWorker(ITelegramBotClient bot, ApplicationDialog dialog, ILogger<BotWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var offset = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var updates = await bot.GetUpdates(offset, timeout: 50, allowedUpdates: [UpdateType.Message], cancellationToken: stoppingToken);
                foreach (var update in updates)
                {
                    offset = update.Id + 1;
                    if (update.Message is { Text: { } text, Chat.Type: ChatType.Private } message)
                        await HandleMessageAsync(message.Chat.Id, text.Trim(), stoppingToken);
                }
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Telegram getUpdates: {Error}", e.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleMessageAsync(long chatId, string text, CancellationToken ct)
    {
        try
        {
            var reply = await dialog.HandleAsync(chatId, text, ct);
            await bot.SendMessage(chatId, reply.Text, replyMarkup: ToKeyboard(reply.Buttons), cancellationToken: ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogError(e, "Не удалось обработать сообщение от {ChatId}", chatId);
            await TrySendErrorAsync(chatId, ct);
        }
    }

    private async Task TrySendErrorAsync(long chatId, CancellationToken ct)
    {
        try
        {
            await bot.SendMessage(chatId, "Что-то пошло не так, возможно сервис Кыргызтеста сейчас не отвечает. Попробуй ещё раз чуть позже", cancellationToken: ct);
        }
        catch (Exception e) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Не смог написать {ChatId}: {Error}", chatId, e.Message);
        }
    }

    private static ReplyMarkup ToKeyboard(IReadOnlyList<string>? buttons) =>
        buttons is null
            ? new ReplyKeyboardRemove()
            : new ReplyKeyboardMarkup(buttons.Chunk(2).Select(row => row.Select(text => new KeyboardButton(text))))
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true,
            };
}
