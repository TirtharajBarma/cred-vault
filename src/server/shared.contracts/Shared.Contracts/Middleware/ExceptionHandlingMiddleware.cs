using Shared.Contracts.Exceptions;
using Shared.Contracts.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Shared.Contracts.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);           // goes to controller
        }
        catch (BaseException ex)
        {
            await HandleBaseExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            await HandleGenericExceptionAsync(context, ex);
        }
    }

    private async Task HandleBaseExceptionAsync(HttpContext context, BaseException ex)
    {
        _logger.LogWarning("Base exception: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);

        context.Response.StatusCode = ex.StatusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Title = ex.Message,
            Detail = ex.Details,
            Status = ex.StatusCode,
            Instance = context.Request.Path,
            Extensions = new Dictionary<string, object?>
            {
                ["errorCode"] = ex.ErrorCode
            }
        };

        if (ex is ValidationException validationEx && validationEx.Errors.Any())
        {
            problem.Extensions["errors"] = validationEx.Errors;
        }

        var response = new ApiResponse<object>
        {
            Success = false,
            Message = ex.Message,
            ErrorCode = ex.ErrorCode,
            Data = problem,
            TraceId = context.TraceIdentifier    //  ← ASP.NET auto-generates this
        };

        await context.Response.WriteAsJsonAsync(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private async Task HandleGenericExceptionAsync(HttpContext context, Exception ex)
    {
        _logger.LogError(ex, "Unhandled exception while processing request {Path}", context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred while processing your request.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = context.Request.Path
        };

        var response = new ApiResponse<ProblemDetails>
        {
            Success = false,
            Message = "Internal server error",
            ErrorCode = "InternalError",
            Data = problem,
            TraceId = context.TraceIdentifier
        };

        await context.Response.WriteAsJsonAsync(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
