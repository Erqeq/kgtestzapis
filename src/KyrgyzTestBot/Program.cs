using System.Text;
using KyrgyzTestBot;
using KyrgyzTestBot.Applicants;
using KyrgyzTestBot.Conversation;
using KyrgyzTestBot.KyrgyzTestApi;
using KyrgyzTestBot.Registration;
using Microsoft.Extensions.Options;
using Telegram.Bot;

Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(BotOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Token), "Не задан Bot:Token (user-secrets или переменная окружения Bot__Token)")
    .Validate(o => o.MinCheckInterval > TimeSpan.Zero && o.MinCheckInterval <= o.MaxCheckInterval,
        "Bot:MinCheckInterval должен быть больше нуля и не больше Bot:MaxCheckInterval")
    .ValidateOnStart();

builder.Services.AddHttpClient(KyrgyzTestApiClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton<KyrgyzTestApiClient>();
builder.Services.AddSingleton<ITelegramBotClient>(sp =>
    new TelegramBotClient(sp.GetRequiredService<IOptions<BotOptions>>().Value.Token));

builder.Services.AddSingleton<ApplicantStore>();
builder.Services.AddSingleton<ApplicationDialog>();

builder.Services.AddHostedService<TelegramPollingWorker>();
builder.Services.AddHostedService<RegistrationWorker>();

builder.Build().Run();
