#nullable enable

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Infrastructure.Data;

namespace SportsData.Notification.Application.Smack.Queries.GetSmackPhrases;

public interface IGetSmackPhrasesQueryHandler
{
    Task<Result<List<SmackPhraseDto>>> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>Full catalog, inactive rows included — the Lab shows both.</summary>
public class GetSmackPhrasesQueryHandler : IGetSmackPhrasesQueryHandler
{
    private readonly AppDataContext _dataContext;

    public GetSmackPhrasesQueryHandler(AppDataContext dataContext)
    {
        _dataContext = dataContext;
    }

    public async Task<Result<List<SmackPhraseDto>>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var phrases = await _dataContext.SmackPhrases
            .AsNoTracking()
            .OrderBy(p => p.Situation).ThenBy(p => p.CreatedUtc)
            .Select(p => new SmackPhraseDto(
                p.Id, p.Voice.ToString(), p.Situation.ToString(),
                p.Sport.HasValue ? p.Sport.Value.ToString() : null,
                p.Text, p.IsActive, p.RequiresGamblingContent, p.Weight, p.Description,
                p.RowVersion))
            .ToListAsync(cancellationToken);

        return new Success<List<SmackPhraseDto>>(phrases);
    }
}
