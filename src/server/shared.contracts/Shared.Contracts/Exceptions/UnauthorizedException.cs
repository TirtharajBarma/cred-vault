using Microsoft.AspNetCore.Http;

namespace Shared.Contracts.Exceptions;

public class UnauthorizedException : BaseException
{
    public UnauthorizedException(string message = "Unauthorized access.", string? errorCode = "Unauthorized")
        : base(message, errorCode, StatusCodes.Status401Unauthorized) { }
}
