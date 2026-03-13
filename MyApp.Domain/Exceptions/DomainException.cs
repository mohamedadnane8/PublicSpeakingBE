namespace MyApp.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class UserNotFoundException : DomainException
{
    public UserNotFoundException(string email) 
        : base($"User with email '{email}' was not found")
    {
    }
}

public class InvalidGoogleTokenException : DomainException
{
    public InvalidGoogleTokenException(string message) 
        : base($"Invalid Google token: {message}")
    {
    }
}
