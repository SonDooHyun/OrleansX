namespace OrleansX.Abstractions.Exceptions;

/// <summary>
/// OrleansX 기본 예외
/// </summary>
public class OrleansXException : Exception
{
    public OrleansXException() { }

    public OrleansXException(string message) : base(message) { }

    public OrleansXException(string message, Exception innerException) 
        : base(message, innerException) { }
}

/// <summary>
/// Grain을 찾을 수 없을 때 발생하는 예외
/// </summary>
public class GrainNotFoundException : OrleansXException
{
    public GrainNotFoundException(string grainId) 
        : base($"Grain not found: {grainId}")
    {
        GrainId = grainId;
    }

    public string GrainId { get; }
}

/// <summary>
/// Grain 상태 충돌 예외
/// </summary>
public class GrainStateConflictException : OrleansXException
{
    public GrainStateConflictException(string message) : base(message) { }
}

/// <summary>
/// 재시도 가능한 예외
/// </summary>
public class RetryableException : OrleansXException
{
    public RetryableException(string message) : base(message) { }

    public RetryableException(string message, Exception innerException) 
        : base(message, innerException) { }
}

