using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MyApp.Application.Interfaces;
using UglyToad.PdfPig;

namespace MyApp.Infrastructure.Services;

public class ResumeParserService : IResumeParserService
{
    public Task<string> ExtractTextFromPdfAsync(Stream fileStream)
    {
        using var document = PdfDocument.Open(fileStream);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return Task.FromResult(sb.ToString().Trim());
    }

    public Task<string> ExtractTextFromDocxAsync(Stream fileStream)
    {
        using var document = WordprocessingDocument.Open(fileStream, false);
        var body = document.MainDocumentPart?.Document?.Body;

        if (body == null)
            return Task.FromResult(string.Empty);

        var sb = new StringBuilder();

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            sb.AppendLine(paragraph.InnerText);
        }

        return Task.FromResult(sb.ToString().Trim());
    }
}
