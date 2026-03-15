using MyApp.Application.DTOs;

namespace MyApp.Application.Interfaces;

public interface IWordService
{
    RandomWordResponse GetRandomWord(RandomWordRequest request);
}
