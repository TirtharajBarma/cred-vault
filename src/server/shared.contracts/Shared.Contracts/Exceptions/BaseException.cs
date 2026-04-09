using Microsoft.AspNetCore.Http;

namespace Shared.Contracts.Exceptions;

public abstract class BaseException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }
    public string? Details { get; }

    protected BaseException(string message, string? errorCode = "Error", int statusCode = StatusCodes.Status500InternalServerError, string? details = null)
        : base(message)
    {
        ErrorCode = errorCode ?? "Error";
        StatusCode = statusCode;
        Details = details;
    }
}
