using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.Application.DTOs;
using MyApp.Application.Interfaces;

namespace MyApp.API.Controllers;

[ApiController]
[Route("api/words")]
[AllowAnonymous]
public class WordsController : ControllerBase
{
    private readonly IWordService _wordService;

    public WordsController(IWordService wordService)
    {
        _wordService = wordService;
    }

    [HttpGet("random")]
    [ProducesResponseType(typeof(RandomWordResponse), StatusCodes.Status200OK)]
    public ActionResult<RandomWordResponse> GetRandomWord(
        [FromQuery] string? language,
        [FromQuery] string? difficulty,
        [FromQuery(Name = "exclude")] string[]? exclude)
    {
        var response = _wordService.GetRandomWord(new RandomWordRequest
        {
            Language = language,
            Difficulty = difficulty,
            ExcludedWords = exclude
        });

        return Ok(response);
    }
}
