namespace MyApp.Application.DTOs;

public record ResumeContentDto(
    string FileName,
    string ContentType,
    int PageCount,
    int QuestionsGenerated,
    string DetectedLanguage,
    string? DetectedField
);

public record InterviewQuestionDto(
    Guid Id,
    string Question,
    string Category,
    string Difficulty,
    int ThinkingSeconds,
    int AnsweringSeconds
);

public record GeneratedQuestionDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("q")] string Question,
    [property: System.Text.Json.Serialization.JsonPropertyName("c")] string Category,
    [property: System.Text.Json.Serialization.JsonPropertyName("d")] string Difficulty,
    [property: System.Text.Json.Serialization.JsonPropertyName("ts")] int ThinkingSeconds,
    [property: System.Text.Json.Serialization.JsonPropertyName("as")] int AnsweringSeconds
);

public record DeepSeekResponseDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("questions")] List<GeneratedQuestionDto> Questions,
    [property: System.Text.Json.Serialization.JsonPropertyName("language")] string Language,
    [property: System.Text.Json.Serialization.JsonPropertyName("field")] string Field
);

public record BehavioralQuestionDto(
    string Question,
    string Difficulty,
    int ThinkingSeconds,
    int AnsweringSeconds
);
