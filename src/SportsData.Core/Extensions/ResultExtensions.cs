using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;

namespace SportsData.Core.Extensions
{
    public static class ResultExtensions
    {
        public static ActionResult<T> ToActionResult<T>(this Result<T> result)
        {
            if (result.IsSuccess)
                return new OkObjectResult(result.Value);

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
