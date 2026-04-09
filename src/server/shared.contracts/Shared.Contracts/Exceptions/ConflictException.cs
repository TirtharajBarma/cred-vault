using Microsoft.AspNetCore.Http;

namespace Shared.Contracts.Exceptions;

public class ConflictException : BaseException
{
    public ConflictException(string message, string? errorCode = "Conflict", string? details = null)
        : base(message, errorCode, StatusCodes.Status409Conflict, details) { }
}
