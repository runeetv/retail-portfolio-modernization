namespace FulfillmentAPI.Application;

/// <summary>Result type for FulfillmentAPI application services, carrying an error code
/// so the API layer can map to an appropriate HTTP status.</summary>
public sealed class ApiResult<T>
{
    public bool Success { get; private init; }
    public string? Error { get; private init; }
    public ApiErrorKind ErrorKind { get; private init; }
    public T? Value { get; private init; }

    public static ApiResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static ApiResult<T> Fail(string error, ApiErrorKind kind = ApiErrorKind.Validation)
        => new() { Success = false, Error = error, ErrorKind = kind };
}

public enum ApiErrorKind { Validation, NotFound, Conflict }
