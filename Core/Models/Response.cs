using Syncro.Core.Interfaces.Response;

namespace Syncro.Core.Models;

public class Response : IResponse
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public DateTime Date { get; set; }

    public static IResponse Success()
        => new Response()
        {
            IsSuccess = true,
            ErrorMessage = null,
            Exception = null,
            Date = DateTime.Now
            
        };
    
    public static IResponse Failure(string? errorMessage, Exception? exception)
        => new Response()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Exception = exception,
            Date = DateTime.Now
        };
    
}