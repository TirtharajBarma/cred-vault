using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace Shared.Contracts.Exceptions;

public class ValidationException : BaseException
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(string message, string? errorCode = "ValidationError", Dictionary<string, string[]>? errors = null)
        : base(message, errorCode, StatusCodes.Status400BadRequest)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public ValidationException(Dictionary<string, string[]> errors, string? errorCode = "ValidationError")
        : base("One or more validation errors occurred.", errorCode, StatusCodes.Status400BadRequest)
    {
        Errors = errors;
    }
}
