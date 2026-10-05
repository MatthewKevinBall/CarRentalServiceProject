namespace CarRental.Domain;

/// <summary>
/// Either a value (success) or every reason it couldn't be produced (failure).
/// Used for business-rule failures, which are expected outcomes; programming bugs still throw.
/// </summary>
public sealed class Result<TValue, TError>
{
    private readonly TValue? _value;

    private Result(TValue? value, IReadOnlyList<TError> errors)
    {
        _value = value;
        Errors = errors;
    }

    public static Result<TValue, TError> Success(TValue value) => new(value, []);

    public static Result<TValue, TError> Failure(IEnumerable<TError> errors)
    {
        // Copy once: the caller's sequence may be a lazy LINQ query that would re-run on every enumeration.
        IReadOnlyList<TError> errorList = [.. errors];

        if (errorList.Count == 0)
        {
            throw new ArgumentException("A failure must have at least one error.", nameof(errors));
        }

        return new(default, errorList);
    }

    public bool IsSuccess => Errors.Count == 0;

    public IReadOnlyList<TError> Errors { get; }

    /// <summary>The value of a successful result. Throws for a failure, so callers can't carry on with a meaningless default.</summary>
    public TValue Value => IsSuccess
        // `!` is safe: a success is only ever created by Success(value), which received a real TValue.
        // The compiler can't see that link between IsSuccess and _value, so we state it.
        ? _value!
        : throw new InvalidOperationException("A failed result has no value. Check IsSuccess first.");
}
