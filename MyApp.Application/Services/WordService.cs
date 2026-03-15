using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public class WordService : IWordService
{
    private readonly ILogger<WordService> _logger;
    private readonly IReadOnlyDictionary<SessionLanguage, IReadOnlyDictionary<SessionDifficulty, string[]>> _wordBank;

    public WordService(IConfiguration configuration, ILogger<WordService> logger)
    {
        _logger = logger;
        _wordBank = LoadWordBank(configuration) ?? throw new InvalidOperationException("Failed to load word bank from configuration and no fallback is available.");
    }

    public RandomWordResponse GetRandomWord(RandomWordRequest request)
    {
        var language = ParseEnum(request.Language, SessionLanguage.En);
        var difficulty = ParseEnum(request.Difficulty, SessionDifficulty.Medium);

        var words = GetWordPool(language, difficulty);
        var excludedWords = BuildExclusionSet(request.ExcludedWords);

        var availableWords = words.Where(word => !excludedWords.Contains(word)).ToArray();
        var pool = availableWords.Length > 0 ? availableWords : words;
        var chosenWord = pool[Random.Shared.Next(pool.Length)];

        return new RandomWordResponse
        {
            Word = chosenWord,
            Language = language.ToString().ToUpperInvariant(),
            Difficulty = difficulty.ToString().ToUpperInvariant()
        };
    }

    private string[] GetWordPool(SessionLanguage language, SessionDifficulty difficulty)
    {
        if (_wordBank.TryGetValue(language, out var byDifficulty) &&
            byDifficulty.TryGetValue(difficulty, out var words) &&
            words.Length > 0)
        {
            return words;
        }

        if (_wordBank.TryGetValue(language, out byDifficulty) &&
            byDifficulty.TryGetValue(SessionDifficulty.Medium, out words) &&
            words.Length > 0)
        {
            return words;
        }

        if (_wordBank.TryGetValue(SessionLanguage.En, out var englishWords) &&
            englishWords.TryGetValue(SessionDifficulty.Medium, out words) &&
            words.Length > 0)
        {
            return words;
        }
        throw new InvalidOperationException("Failed to retrieve word pool for the specified language and difficulty.");

    }

    private IReadOnlyDictionary<SessionLanguage, IReadOnlyDictionary<SessionDifficulty, string[]>>? LoadWordBank(IConfiguration configuration)
    {
        foreach (var candidatePath in GetWordBankCandidatePaths(configuration))
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(candidatePath);
                var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string[]>>>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (raw == null || raw.Count == 0)
                {
                    _logger.LogWarning("Words JSON file is empty: {Path}", candidatePath);
                    continue;
                }

                var mapped = MapWordBank(raw);
                if (mapped.Count == 0)
                {
                    _logger.LogWarning("Words JSON did not contain valid language/difficulty entries: {Path}", candidatePath);
                    continue;
                }

                _logger.LogInformation("Loaded words from JSON: {Path}", candidatePath);
                return mapped;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load words JSON from {Path}", candidatePath);
            }
        }

        _logger.LogWarning("No valid words JSON found. Falling back to in-code minimal word bank.");
        return null;
    }

    private static IEnumerable<string> GetWordBankCandidatePaths(IConfiguration configuration)
    {
        var configuredPath = configuration["Words:FilePath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(configuredPath);
        }

        yield return Path.Combine(AppContext.BaseDirectory, "Data", "words.json");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "Data", "words.json");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "MyApp.API", "Data", "words.json");
    }

    private static IReadOnlyDictionary<SessionLanguage, IReadOnlyDictionary<SessionDifficulty, string[]>> MapWordBank(
        Dictionary<string, Dictionary<string, string[]>> raw)
    {
        var mapped = new Dictionary<SessionLanguage, IReadOnlyDictionary<SessionDifficulty, string[]>>();

        foreach (var languagePair in raw)
        {
            if (!TryParseEnum(languagePair.Key, out SessionLanguage language))
            {
                continue;
            }

            var byDifficulty = new Dictionary<SessionDifficulty, string[]>();
            foreach (var difficultyPair in languagePair.Value)
            {
                if (!TryParseEnum(difficultyPair.Key, out SessionDifficulty difficulty))
                {
                    continue;
                }

                var words = difficultyPair.Value
                    .Where(word => !string.IsNullOrWhiteSpace(word))
                    .Select(word => word.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (words.Length > 0)
                {
                    byDifficulty[difficulty] = words;
                }
            }

            if (byDifficulty.Count > 0)
            {
                mapped[language] = byDifficulty;
            }
        }

        return mapped;
    }

    private static bool TryParseEnum<TEnum>(string value, out TEnum parsed) where TEnum : struct
    {
        if (Enum.TryParse(value, ignoreCase: true, out parsed))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            var normalized = char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
            return Enum.TryParse(normalized, ignoreCase: true, out parsed);
        }

        return false;
    }

    private static HashSet<string> BuildExclusionSet(IReadOnlyCollection<string>? excludedWords)
    {
        if (excludedWords == null || excludedWords.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return excludedWords
            .Select(word => word.Trim())
            .Where(word => word.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        return TryParseEnum<TEnum>(value, out var parsed) ? parsed : fallback;
    }
}
