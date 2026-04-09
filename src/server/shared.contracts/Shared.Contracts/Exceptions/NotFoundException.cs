using Microsoft.AspNetCore.Http;

namespace Shared.Contracts.Exceptions;

public class NotFoundException : BaseException
{
    public NotFoundException(string message, string? errorCode = "NotFound", string? details = null)
        : base(message, errorCode, StatusCodes.Status404NotFound, details) { }

    public NotFoundException(string resourceName, object key, string? errorCode = "NotFound")
        : base($"{resourceName} with id '{key}' not found.", errorCode, StatusCodes.Status404NotFound) { }
}
