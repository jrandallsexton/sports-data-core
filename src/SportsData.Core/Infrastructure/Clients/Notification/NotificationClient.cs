#nullable enable

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.Notification
{
    /// <summary>
    /// Typed client for the Notification service — the ONLY sanctioned way
    /// for another service to call it (no ad-hoc HttpClient use). Current
    /// surface is the SmackBot Lab admin family; the X-Api-Key header that
    /// Notification's <c>[ApiKeyAuth]</c> endpoints require is stamped at
    /// registration from <c>CommonConfig:NotificationClientConfig:SecretKey</c>,
    /// so the key never appears at call sites (or in a browser).
    /// </summary>
    public interface IProvideNotifications
    {
        Task<Result<List<SmackPreviewResultDto>>> PreviewSmack(
            SmackPreviewRequestDto request, CancellationToken cancellationToken = default);

        Task<Result<List<SmackPhraseDto>>> GetSmackPhrases(CancellationToken cancellationToken = default);

        Task<Result<SmackPhraseDto>> CreateSmackPhrase(
            SmackPhraseUpsertDto request, CancellationToken cancellationToken = default);

        Task<Result<SmackPhraseDto>> UpdateSmackPhrase(
            Guid phraseId, SmackPhraseUpsertDto request, CancellationToken cancellationToken = default);

        Task<Result<bool>> RateSmackPreview(
            SmackRatingRequestDto request, CancellationToken cancellationToken = default);
    }

    public class NotificationClient : ClientBase, IProvideNotifications
    {
        private readonly ILogger<NotificationClient> _logger;

        public NotificationClient(
            ILogger<NotificationClient> logger,
            HttpClient httpClient)
            : base(httpClient)
        {
            _logger = logger;
        }

        public async Task<Result<List<SmackPreviewResultDto>>> PreviewSmack(
            SmackPreviewRequestDto request, CancellationToken cancellationToken = default)
        {
            return await PostAsync<List<SmackPreviewResultDto>, List<SmackPreviewResultDto>, SmackPreviewRequestDto>(
                "admin/smack/preview",
                request,
                previews => previews,
                [],
                "SmackPreview",
                cancellationToken: cancellationToken);
        }

        public async Task<Result<List<SmackPhraseDto>>> GetSmackPhrases(
            CancellationToken cancellationToken = default)
        {
            return await GetAsync<List<SmackPhraseDto>, List<SmackPhraseDto>>(
                "admin/smack/phrases",
                phrases => phrases,
                [],
                "SmackPhrases",
                cancellationToken: cancellationToken);
        }

        public async Task<Result<SmackPhraseDto>> CreateSmackPhrase(
            SmackPhraseUpsertDto request, CancellationToken cancellationToken = default)
        {
            return await PostAsync<SmackPhraseDto, SmackPhraseDto, SmackPhraseUpsertDto>(
                "admin/smack/phrases",
                request,
                phrase => phrase,
                default!,
                "SmackPhrase",
                cancellationToken: cancellationToken);
        }

        public async Task<Result<SmackPhraseDto>> UpdateSmackPhrase(
            Guid phraseId, SmackPhraseUpsertDto request, CancellationToken cancellationToken = default)
        {
            return await PutAsync<SmackPhraseDto, SmackPhraseDto, SmackPhraseUpsertDto>(
                $"admin/smack/phrases/{phraseId}",
                request,
                phrase => phrase,
                default!,
                "SmackPhrase",
                cancellationToken: cancellationToken);
        }

        public async Task<Result<bool>> RateSmackPreview(
            SmackRatingRequestDto request, CancellationToken cancellationToken = default)
        {
            return await PostWithResultAsync(
                "admin/smack/ratings",
                request,
                "SmackRating",
                cancellationToken);
        }
    }
}
