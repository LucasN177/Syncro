namespace Syncro.Core.Interfaces.Response;

public interface IResponse
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public DateTime Date { get; set; }
}

public interface IResponse<T> : IResponse
{
    public T? Data { get; init; }
}