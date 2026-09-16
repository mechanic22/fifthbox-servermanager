namespace FifthBox.ServerManager.Shared.Exceptions;

public class ValidationException(IReadOnlyDictionary<string, string[]> errors) : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public ValidationException(string member, string error)
        : this(new Dictionary<string, string[]> { [member] = [error] })
    {
    }
}
