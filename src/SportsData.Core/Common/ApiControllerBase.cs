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
        private ISender _mediator;

        protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();

        public async Task<ActionResult<TResponse>> Send<TRequest, TResponse>(TRequest request)
        {
            var result = await Mediator.Send(request);

            return result is Success<TResponse> success ?
                MapSuccess(success) :
                MapFailure(result as Failure<TResponse>);
        }

        protected ActionResult MapFailure<T>(Failure<T> result)
        {
            switch (result.Status)
            {
                case ResultStatus.BadRequest:
                case ResultStatus.Validation:
                    return new BadRequestObjectResult(result);
                case ResultStatus.Forbid:
                    return new ForbidResult();
                case ResultStatus.NotFound:
                    return new NotFoundObjectResult(result);
                case ResultStatus.Success:
                    return new OkObjectResult(result);
                case ResultStatus.Unauthorized:
                    return new UnauthorizedObjectResult(result);
                case ResultStatus.Accepted:
                case ResultStatus.Created:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected ActionResult MapSuccess<T>(Success<T> result)
        {
            switch (result.Status)
            {
                case ResultStatus.Accepted:
                    return new AcceptedResult();
                case ResultStatus.Created:
                    return new OkObjectResult(result);
                case ResultStatus.Success:
                    return new OkObjectResult(result);
                case ResultStatus.BadRequest:
                case ResultStatus.Forbid:
                case ResultStatus.NotFound:
                case ResultStatus.Unauthorized:
                case ResultStatus.Validation:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}