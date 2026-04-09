using Microsoft.AspNetCore.Http;

namespace Shared.Contracts.Exceptions;

public class ForbiddenException : BaseException
{
    public ForbiddenException(string message = "Access forbidden.", string? errorCode = "Forbidden")
        : base(message, errorCode, StatusCodes.Status403Forbidden) { }
}
