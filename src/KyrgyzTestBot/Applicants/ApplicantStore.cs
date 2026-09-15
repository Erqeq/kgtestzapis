using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace KyrgyzTestBot.Applicants;

/// <summary>
/// Заявки в JSON-файле. На сотни заявок этого хватает, если людей станет больше, переносить в SQLite/Postgres
/// </summary>
public sealed class ApplicantStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Lock _lock = new();
    private readonly string _filePath;
    private readonly Dictionary<long, Applicant> _applicants;

    public ApplicantStore(IOptions<BotOptions> options)
    {
        Directory.CreateDirectory(options.Value.DataDirectory);
        _filePath = Path.Combine(options.Value.DataDirectory, "applicants.json");
        _applicants = File.Exists(_filePath)
            ? JsonSerializer.Deserialize<List<Applicant>>(File.ReadAllText(_filePath), JsonOptions)!.ToDictionary(a => a.TelegramId)
            : new Dictionary<long, Applicant>();
    }

    public Applicant? Find(long telegramId)
    {
        lock (_lock) return _applicants.GetValueOrDefault(telegramId);
    }

    /// <summary>Очередь на запись: кто раньше подал заявку, того раньше записываем</summary>
    public IReadOnlyList<Applicant> GetQueue()
    {
        lock (_lock) return _applicants.Values.OrderBy(a => a.CreatedAt).ToList();
    }

    public void Upsert(Applicant applicant)
    {
        lock (_lock)
        {
            _applicants[applicant.TelegramId] = applicant;
            Flush();
        }
    }

    /// <summary>Меняет заявку, только если она ещё есть, человек мог успеть её отменить</summary>
    public void Update(long telegramId, Func<Applicant, Applicant> change)
    {
        lock (_lock)
        {
            if (!_applicants.TryGetValue(telegramId, out var applicant)) return;
            _applicants[telegramId] = change(applicant);
            Flush();
        }
    }

    public void Remove(long telegramId)
    {
        lock (_lock)
        {
            if (_applicants.Remove(telegramId)) Flush();
        }
    }

    private void Flush()
    {
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(_applicants.Values, JsonOptions));
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
