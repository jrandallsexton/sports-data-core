using MediatR;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using System;
using System.Threading.Tasks;

namespace SportsData.Core.Common
{
    [Route("api/[controller]")]
    [ApiController]
    public abstract class ApiControllerBase : ControllerBase
    {
        private ISender _mediator = null!;

        protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

        public async Task<ActionResult<TResponse>> Send<TRequest, TResponse>(TRequest request)
            where TRequest : IRequest<Result<TResponse>>
        {
            if (request is null)
                return BadRequest("Request cannot be null.");

            var result = await Mediator.Send(request);

            return result switch
            {
                Success<TResponse> success => MapSuccess(success),
                Failure<TResponse> failure => MapFailure(failure),
                _ => StatusCode(500, "Unexpected result type")
            };
        }

        protected ActionResult MapFailure<T>(Failure<T>? result)
        {
            if (result is null)
                return StatusCode(500, "Unknown failure result");

            return result.Status switch
            {
                ResultStatus.BadRequest => new BadRequestObjectResult(result),
                ResultStatus.Validation => new BadRequestObjectResult(result),
                ResultStatus.Forbid => new ForbidResult(),
                ResultStatus.NotFound => new NotFoundObjectResult(result),
                ResultStatus.Success => new OkObjectResult(result),
                ResultStatus.Unauthorized => new UnauthorizedObjectResult(result),
                ResultStatus.Accepted or ResultStatus.Created or _ =>
                    throw new ArgumentOutOfRangeException(nameof(result.Status), $"Unhandled status: {result.Status}")
            };
        }

        protected ActionResult MapSuccess<T>(Success<T>? result)
        {
            if (result is null)
                return StatusCode(500, "Unknown success result");

            return result.Status switch
            {
                ResultStatus.Accepted => new AcceptedResult(),
                ResultStatus.Created => new OkObjectResult(result),
                ResultStatus.Success => new OkObjectResult(result),
                ResultStatus.BadRequest or
                ResultStatus.Forbid or
                ResultStatus.NotFound or
                ResultStatus.Unauthorized or
                ResultStatus.Validation or _ =>
                    throw new ArgumentOutOfRangeException(nameof(result.Status), $"Unexpected success status: {result.Status}")
            };
        }
    }
}
