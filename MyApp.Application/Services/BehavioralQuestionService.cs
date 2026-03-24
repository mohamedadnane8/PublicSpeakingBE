using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;
using MyApp.Domain.Enums;

namespace MyApp.Application.Services;

public class BehavioralQuestionService : IBehavioralQuestionService
{
    private readonly ILogger<BehavioralQuestionService> _logger;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, BehavioralQuestionDto[]>> _questionBank;

    public BehavioralQuestionService(IConfiguration configuration, ILogger<BehavioralQuestionService> logger)
    {
        _logger = logger;
        _questionBank = LoadQuestionBank(configuration)
            ?? throw new InvalidOperationException("Failed to load behavioral questions.");
    }

    public BehavioralQuestionDto? GetRandomQuestion(string? language, string? difficulty)
    {
        var lang = string.IsNullOrWhiteSpace(language) ? "en" : language.Trim().ToLowerInvariant();
        var diff = string.IsNullOrWhiteSpace(difficulty) ? "Medium" : difficulty.Trim();

        // Normalize language key to title case for JSON lookup (En, Fr, Ar)
        var langKey = char.ToUpperInvariant(lang[0]) + lang[1..];

        if (!_questionBank.TryGetValue(langKey, out var byDifficulty))
        {
            // Fallback to English
            if (!_questionBank.TryGetValue("En", out byDifficulty))
                return null;
        }

        if (!byDifficulty.TryGetValue(diff, out var questions) || questions.Length == 0)
        {
            // Fallback to Medium
            if (!byDifficulty.TryGetValue("Medium", out questions) || questions.Length == 0)
                return null;
        }

        return questions[Random.Shared.Next(questions.Length)];
    }

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, BehavioralQuestionDto[]>>? LoadQuestionBank(
        IConfiguration configuration)
    {
        foreach (var path in GetCandidatePaths(configuration))
        {
            if (!File.Exists(path)) continue;

            try
            {
                var json = File.ReadAllText(path);
                var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, BehavioralQuestionEntry[]>>>(
                    json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (raw == null || raw.Count == 0) continue;

                var mapped = new Dictionary<string, IReadOnlyDictionary<string, BehavioralQuestionDto[]>>();
                foreach (var (langKey, difficulties) in raw)
                {
                    var byDiff = new Dictionary<string, BehavioralQuestionDto[]>();
                    foreach (var (diffKey, entries) in difficulties)
                    {
                        byDiff[diffKey] = entries
                            .Where(e => !string.IsNullOrWhiteSpace(e.Question))
                            .Select(e => new BehavioralQuestionDto(
                                e.Question!,
                                diffKey,
                                e.ThinkingSeconds > 0 ? e.ThinkingSeconds : 30,
                                e.AnsweringSeconds > 0 ? e.AnsweringSeconds : 60))
                            .ToArray();
                    }
                    mapped[langKey] = byDiff;
                }

                _logger.LogInformation("Loaded behavioral questions from: {Path}", path);
                return mapped;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load behavioral questions from {Path}", path);
            }
        }

        return null;
    }

    private static IEnumerable<string> GetCandidatePaths(IConfiguration configuration)
    {
        var configuredPath = configuration["BehavioralQuestions:FilePath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(configuredPath);
        }

        yield return Path.Combine(AppContext.BaseDirectory, "Data", "behavioral-questions.json");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "Data", "behavioral-questions.json");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "MyApp.API", "Data", "behavioral-questions.json");
    }

    private class BehavioralQuestionEntry
    {
        public string? Question { get; set; }
        public int ThinkingSeconds { get; set; }
        public int AnsweringSeconds { get; set; }
    }
}
