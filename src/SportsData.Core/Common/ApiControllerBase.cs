using MediatR;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

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

            return result is Success<TResponse> ?
                Ok(result) :
                BadRequest(result);
        }
    }
}
