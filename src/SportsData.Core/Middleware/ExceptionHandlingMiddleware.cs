using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using SportsData.Core.Exceptions;

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly RequestDelegate _next;

        public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger, RequestDelegate next)
        {
            _logger = logger;
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (InvalidCommandException ex)
            {
                _logger.LogError("Invalid command: {@ex}", ex);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(ex.Message, Encoding.UTF8);
            }
            catch (ResourceNotFoundException ex)
            {
                _logger.LogError("Resource not found: {@ex}", ex);
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync(ex.Message, Encoding.UTF8);
            }
            catch (ValidationException ex)
            {
                _logger.LogError("Validation failed: {@ex}", ex);
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(ex.Message, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError("Unhandled exception: {@ex}", ex);
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("An internal server error has occurred, please try again later.", Encoding.UTF8);
            }
        }
    }
}
