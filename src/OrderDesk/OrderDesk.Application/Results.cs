namespace OrderDesk.Application;

/// <summary>Simple operation result used by OrderDesk application services.</summary>
public sealed class OperationResult
{
    public bool Success { get; private init; }
    public string? Error { get; private init; }

    public static OperationResult Ok() => new() { Success = true };
    public static OperationResult Fail(string error) => new() { Success = false, Error = error };
}

public sealed class OperationResult<T>
{
    public bool Success { get; private init; }
    public string? Error { get; private init; }
    public T? Value { get; private init; }

    public static OperationResult<T> Ok(T value) => new() { Success = true, Value = value };
    public static OperationResult<T> Fail(string error) => new() { Success = false, Error = error };
}
