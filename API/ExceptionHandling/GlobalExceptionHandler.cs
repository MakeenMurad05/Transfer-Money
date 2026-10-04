using FluentValidation; 
using Application.Common;
using Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace API.ExceptionHandling ;

public class GlobalExceptionHandler(IProblemDetailsService _problemDetailsService, ILogger<GlobalExceptionHandler> _logger) : IExceptionHandler
{


     async ValueTask<bool> IExceptionHandler.TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        
        var problemDetails = exception switch
        {
            
            ValidationException validationException => CreateValidationProblem(validationException) ,

            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not Found",
                Detail = exception.Message
            },

            ConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = exception.Message

            },

            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Server Error",
                Detail = "An unexpected error occurred."
            }

        };


        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
                    _logger.LogError(exception, "Unhandled exception");
                else
                    _logger.LogWarning("Request failed with {StatusCode}: {Message}",
                        problemDetails.Status, exception.Message);

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });



    }

     private static ProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more validation errors occurred."
        };

        problemDetails.Extensions["errors"] = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return problemDetails;
    }

}