using Syncro.Core.Interfaces.Response;

namespace Syncro.Core.Models;

public class Response<T> : Core.Models.Response, IResponse<T>
{
    public T? Data { get; init; }

    private Response()
    {
        IsSuccess = false;
        Date = DateTime.Now;
    }

    public static IResponse<T> Success(T data)
        => new Response<T>()
        {
            IsSuccess = true,
            Data = data,
            Date = DateTime.Now,
        };

    public static IResponse<T> Failure(Exception? exception)
        => new Response<T>()
        {
            IsSuccess = false,
            Date = DateTime.Now,
            Exception = exception
        };
    
    public new static IResponse<T> Failure(string errorMessage)
        => new Response<T>()
        {
            IsSuccess = false,
            Date = DateTime.Now,
            ErrorMessage =  errorMessage
        };
}