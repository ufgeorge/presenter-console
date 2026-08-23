using System.Net;
using PresenterConsole.Contracts;

namespace PresenterConsole.Desktop;

public sealed class AudienceQuestionStore
{
    private readonly List<AudienceQuestion> questions = [];
    private readonly Dictionary<string, DateTimeOffset> lastByAddress = [];

    public IReadOnlyList<AudienceQuestion> Questions
    {
        get
        {
            lock (questions)
            {
                return questions.OrderBy(question => question.CreatedAt).ToArray();
            }
        }
    }

    public bool TryAdd(
        string? text,
        IPAddress address,
        DateTimeOffset now,
        out AudienceQuestion? question,
        out string error,
        out bool rateLimited)
    {
        question = null;
        error = string.Empty;
        rateLimited = false;
        var normalized = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 200)
        {
            error = "問題不可為空白，且最多 200 字";
            return false;
        }

        var addressKey = address.ToString();
        lock (lastByAddress)
        {
            if (lastByAddress.TryGetValue(addressKey, out var last)
                && now - last < TimeSpan.FromSeconds(10))
            {
                error = "請稍候 10 秒再提問";
                rateLimited = true;
                return false;
            }

            lastByAddress[addressKey] = now;
        }

        question = new AudienceQuestion(
            Guid.NewGuid().ToString(), normalized!, now.UtcDateTime);
        lock (questions)
        {
            questions.Add(question);
        }

        return true;
    }

    public bool Remove(string questionId)
    {
        lock (questions)
        {
            return questions.RemoveAll(question => question.Id == questionId) > 0;
        }
    }
}
