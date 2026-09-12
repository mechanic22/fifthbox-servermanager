namespace FifthBox.ServerManager.Shared.Exceptions;

/// <summary>
/// Input didn't validate. Carries per-field errors so the Host can return a 400 with a
/// validation ProblemDetails body.
/// </summary>
public class ValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public ValidationException(string member, string error)
        : this(new Dictionary<string, string[]> { [member] = [error] })
    {
    }
}
