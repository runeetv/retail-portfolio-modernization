namespace CustomerPortal.Application;

/// <summary>Operation result used by CustomerPortal application services.</summary>
public sealed class ServiceResult
{
    public bool Success { get; private init; }
    public string? Error { get; private init; }
    public static ServiceResult Ok() => new() { Success = true };
    public static ServiceResult Fail(string error) => new() { Success = false, Error = error };
}

public sealed class ServiceResult<T>
{
    public bool Success { get; private init; }
    public string? Error { get; private init; }
    public T? Value { get; private init; }
    public static ServiceResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static ServiceResult<T> Fail(string error) => new() { Success = false, Error = error };
}
