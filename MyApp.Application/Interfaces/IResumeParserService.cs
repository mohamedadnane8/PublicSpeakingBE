namespace MyApp.Application.Interfaces;

public interface IResumeParserService
{
    Task<string> ExtractTextFromPdfAsync(Stream fileStream);
    Task<string> ExtractTextFromDocxAsync(Stream fileStream);
}
