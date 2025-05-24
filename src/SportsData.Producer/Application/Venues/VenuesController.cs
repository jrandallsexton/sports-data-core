using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Models.Canonical;
using SportsData.Producer.Infrastructure.Data;

[Route("api/venues")]
[ApiController]
public class VenuesController : ControllerBase
{
    private readonly ILogger<VenuesController> _logger;
    private readonly IDataContextFactory _contextFactory;
    private readonly IAppMode _appMode;
    private readonly IMapper _mapper;

    public VenuesController(
        ILogger<VenuesController> logger,
        IDataContextFactory contextFactory,
        IAppMode appMode,
        IMapper mapper)
    {
        _logger = logger;
        _contextFactory = contextFactory;
        _appMode = appMode;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<IActionResult> GetVenues(CancellationToken cancellationToken)
    {
        var context = _contextFactory.Resolve(_appMode.CurrentSport);
        var venues = await context.Venues
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var dtos = _mapper.Map<List<VenueCanonicalModel>>(venues);
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetVenueById(Guid id, CancellationToken cancellationToken)
    {
        var context = _contextFactory.Resolve(_appMode.CurrentSport);
        var venue = await context.Venues
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (venue == null)
            return NotFound();

        var dto = _mapper.Map<VenueCanonicalModel>(venue);
        return Ok(dto);
    }
}