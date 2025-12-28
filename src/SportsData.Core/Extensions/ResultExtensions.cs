using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;

namespace SportsData.Core.Extensions
{
    public static class ResultExtensions
    {
        public static ActionResult<T> ToActionResult<T>(this Result<T> result)
        {
            if (result.IsSuccess)
            {
                // ✅ Handle Success<T> with different ResultStatus values
                return result.Status switch
                {
                    ResultStatus.Accepted => new AcceptedResult(location: null, value: result.Value), // 202 Accepted
                    _ => new OkObjectResult(result.Value) // 200 OK (default for Success)
                };
            }

            if (result is Failure<T> failure)
            {
                return result.Status switch
                {
                    ResultStatus.Validation => new BadRequestObjectResult(new { failure.Errors }),
                    ResultStatus.NotFound => new NotFoundObjectResult(new { failure.Errors }),
                    ResultStatus.Unauthorized => new UnauthorizedObjectResult(new { failure.Errors }),
                    ResultStatus.Forbid => new ForbidResult(),
                    _ => new ObjectResult(new { failure.Errors }) { StatusCode = 500 }
                };
            }

            return new StatusCodeResult(500);
        }
    }
}
