using System;

using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;

namespace SportsData.Core.Extensions
{
    public static class ResultExtensions
    {
        /// <summary>
        /// Maps a <see cref="Result{T}"/> to an <see cref="ActionResult"/>, projecting a
        /// successful value through <paramref name="onSuccess"/> (e.g. a custom 202/201
        /// payload) while routing failures through the shared status→result mapping in
        /// the parameterless overload. Use when the success shape differs from the raw
        /// <c>result.Value</c> but failures should map identically.
        /// </summary>
        public static ActionResult ToActionResult<T>(this Result<T> result, Func<T, ActionResult> onSuccess)
        {
            if (result.IsSuccess)
                return onSuccess(result.Value);

            // Failures always populate ActionResult<T>.Result; fall back to 500 defensively.
            return result.ToActionResult().Result ?? new StatusCodeResult(500);
        }

        public static ActionResult<T> ToActionResult<T>(this Result<T> result)
        {
            if (result.IsSuccess)
            {
                // ✅ Handle Success<T> with different ResultStatus values
                return result.Status switch
                {
                    ResultStatus.Created => new CreatedResult((string?)null, result.Value), // 201 Created
                    ResultStatus.Accepted => new AcceptedResult((string?)null, result.Value), // 202 Accepted
                    _ => new OkObjectResult(result.Value) // 200 OK (default for Success)
                };
            }

            if (result is Failure<T> failure)
            {
                return result.Status switch
                {
                    ResultStatus.BadRequest => new BadRequestObjectResult(new { failure.Errors }), // 400 Bad Request
                    ResultStatus.Validation => new BadRequestObjectResult(new { failure.Errors }), // 400 Validation
                    ResultStatus.Unauthorized => new UnauthorizedObjectResult(new { failure.Errors }), // 401 Unauthorized
                    ResultStatus.Forbid => new ForbidResult(), // 403 Forbidden
                    ResultStatus.NotFound => new NotFoundObjectResult(new { failure.Errors }), // 404 Not Found
                    _ => new ObjectResult(new { failure.Errors }) { StatusCode = 500 } // 500 Internal Server Error
                };
            }

            return new StatusCodeResult(500);
        }
    }
}
