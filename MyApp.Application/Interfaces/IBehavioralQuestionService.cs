using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface IBehavioralQuestionService
{
    BehavioralQuestionDto? GetRandomQuestion(string? language, string? difficulty);
}
