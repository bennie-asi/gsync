namespace GSYNC.Core.Models;

public sealed class ConnectionResult
{
    private ConnectionResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public string? ErrorMessage { get; }

    public static ConnectionResult Success() => new(true, null);

    public static ConnectionResult Failure(string errorMessage) => new(false, errorMessage);
}
