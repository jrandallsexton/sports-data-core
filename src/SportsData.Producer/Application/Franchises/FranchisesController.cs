using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Producer.Application.Franchises.Queries;

namespace SportsData.Producer.Application.Franchises
{
    [Route("api/franchises")]
    public class FranchisesController : ApiControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetFranchises()
        {
            return Ok(await Mediator.Send(new GetFranchises.Query()));
        }

        [HttpGet("{Id}")]
        public async Task<IActionResult> GetFranchise(string id)
        {
            return Ok(await Mediator.Send(new GetFranchiseById.Query(Guid.Parse(id))));
        }
    }
}
