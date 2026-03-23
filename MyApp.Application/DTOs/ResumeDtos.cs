namespace MyApp.Application.DTOs;

public record ResumeContentDto(
    string FileName,
    string ContentType,
    int PageCount,
    int QuestionsGenerated
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
