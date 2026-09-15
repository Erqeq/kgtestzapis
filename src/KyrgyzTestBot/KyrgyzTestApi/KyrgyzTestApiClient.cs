using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using KyrgyzTestBot.Applicants;

namespace KyrgyzTestBot.KyrgyzTestApi;

/// <summary>
/// Клиент API, с которым работает мини-апп @kyrgyztest_support_bot (kg-test-front.vercel.app). Авторизации у API нет
/// </summary>
public sealed partial class KyrgyzTestApiClient(IHttpClientFactory httpClientFactory, ILogger<KyrgyzTestApiClient> logger)
{
    public const string HttpClientName = "KyrgyzTest";

    private const string FrontUrl = "https://kg-test-front.vercel.app";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private string? _baseUrl;

    public async Task<IReadOnlyList<City>> GetCitiesAsync(CancellationToken ct) =>
        await GetJsonAsync<List<City>>("/application/cities/", ct);

    public async Task<IReadOnlyList<ScheduleDay>> GetScheduleAsync(int cityId, DateOnly from, CancellationToken ct) =>
        await GetJsonAsync<List<ScheduleDay>>($"/application/schedules/?city={cityId}&date__gte={from:yyyy-MM-dd}", ct);

    /// <summary>Тот же запрос, что отправляет кнопка «Записаться» в мини-аппе</summary>
    public Task<RegistrationResult> RegisterAsync(Applicant applicant, long scheduleId, CancellationToken ct) =>
        SendAsync(async (http, baseUrl) =>
        {
            var request = new RegisterRequest(
                new RegisterUser(applicant.FullName, applicant.Phone, applicant.Inn, applicant.Language),
                scheduleId,
                applicant.TelegramId);

            using var response = await http.PostAsJsonAsync($"{baseUrl}/application/registrations/register_with_user/", request, JsonOptions, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var detail = body.Length > 300 ? body[..300] : body;

            if (response.IsSuccessStatusCode)
                return new RegistrationResult(RegistrationOutcome.Registered, detail);

            return response.StatusCode switch
            {
                HttpStatusCode.Conflict => new RegistrationResult(RegistrationOutcome.AlreadyRegistered, detail),
                HttpStatusCode.BadRequest => new RegistrationResult(RegistrationOutcome.Rejected, detail),
                _ => throw new HttpRequestException($"Регистрация: HTTP {(int)response.StatusCode} {detail}", null, response.StatusCode),
            };
        }, ct);

    private Task<T> GetJsonAsync<T>(string path, CancellationToken ct) =>
        SendAsync(async (http, baseUrl) =>
            await http.GetFromJsonAsync<T>(baseUrl + path, JsonOptions, ct)
            ?? throw new InvalidOperationException($"Пустой ответ на {path}"), ct);

    private async Task<T> SendAsync<T>(Func<HttpClient, string, Task<T>> send, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient(HttpClientName);
        var baseUrl = _baseUrl ??= await ResolveBaseUrlAsync(http, ct);
        try
        {
            return await send(http, baseUrl);
        }
        catch when (!ct.IsCancellationRequested)
        {
            _baseUrl = null;
            throw;
        }
    }

    private async Task<string> ResolveBaseUrlAsync(HttpClient http, CancellationToken ct)
    {
        var html = await http.GetStringAsync(FrontUrl, ct);
        var bundlePath = BundlePathRegex().Match(html);
        if (!bundlePath.Success)
            throw new InvalidOperationException("Не нашёл JS-бандл на странице мини-аппа");

        var bundle = await http.GetStringAsync(FrontUrl + bundlePath.Groups[1].Value, ct);
        var baseUrl = BaseUrlRegex().Match(bundle);
        if (!baseUrl.Success)
            throw new InvalidOperationException("Не нашёл BASE_URL в бандле мини-аппа");

        logger.LogInformation("Адрес API Кыргызтеста: {BaseUrl}", baseUrl.Groups[1].Value);
        return baseUrl.Groups[1].Value;
    }

    [GeneratedRegex(@"src=""(/assets/index-[^""]+\.js)""")]
    private static partial Regex BundlePathRegex();

    [GeneratedRegex(@"BASE_URL:""([^""]+)""")]
    private static partial Regex BaseUrlRegex();

    private sealed record RegisterRequest(RegisterUser User, long Schedule, long TelegramId);

    private sealed record RegisterUser(string FullName, string Phone, string Inn, string Language);
}
