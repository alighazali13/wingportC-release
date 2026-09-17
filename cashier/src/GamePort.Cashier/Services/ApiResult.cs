namespace GamePort.Cashier.Services;

public class ApiResult<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public bool IsUnauthorized { get; init; }

    public static ApiResult<T> Success(T data) => new() { IsSuccess = true, Data = data };

    public static ApiResult<T> Failure(string error, bool unauthorized = false)
        => new() { IsSuccess = false, Error = error, IsUnauthorized = unauthorized };
}
