using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using System.ComponentModel.DataAnnotations;

namespace SportsData.Api.Application.UI.Messageboard
{
    [ApiController]
    [Route("api/messageboard")] // controller-level base; action routes are explicit below
    public class MessageboardController : ApiControllerBase
    {
        private readonly ILogger<MessageboardController> _logger;
        private readonly IMessageboardService _messageboardService;

        public MessageboardController(
            ILogger<MessageboardController> logger,
            IMessageboardService messageboardService)
        {
            _logger = logger;
            _messageboardService = messageboardService;
        }

        // --- Request models ---

        public sealed record PageQuery(
            [Range(1, 100)] int Limit = 20,
            string? Cursor = null);

        public sealed record CreateThreadRequest(string? Title, [Required] string Content);

        public sealed record CreatePostRequest(Guid? ParentId, [Required] string Content);

        public sealed record ToggleReactionRequest(ReactionType? Type);

        // GET /api/messageboard/my/threads-by-group?perGroupLimit=5
        [HttpGet("my/threads-by-group")]
        public async Task<ActionResult<IDictionary<Guid, PageResult<MessageThread>>>> GetMyThreadsByGroup(
            [FromQuery] int perGroupLimit = 5,
            CancellationToken ct = default)
        {
            var userId = HttpContext.GetCurrentUserId();
            var result = await _messageboardService.GetThreadsByUserGroupsAsync(userId, perGroupLimit, ct);
            return Ok(result); // { [groupId]: { items: [MessageThread...], nextCursor: "..." }, ... }
        }

        // --- Threads ---

        // GET /api/messageboard/groups/{groupId}/threads?limit=20&cursor=...
        [HttpGet("groups/{groupId:guid}/threads")]
        [Authorize]
        public async Task<ActionResult<PageResult<MessageThread>>> GetThreads(
            [FromRoute] Guid groupId,
            [FromQuery] PageQuery q,
            CancellationToken ct)
        {
            var result = await _messageboardService.GetThreadsAsync(
                groupId,
                new PageRequest(q.Limit, q.Cursor),
                ct);

            return Ok(result);
        }

        // POST /api/messageboard/groups/{groupId}/threads
        [HttpPost("groups/{groupId:guid}/threads")]
        [Authorize]
        public async Task<ActionResult<MessageThread>> CreateThread(
            [FromRoute] Guid groupId,
            [FromBody] CreateThreadRequest body,
            CancellationToken ct)
        {
            // Replace this with your current user id accessor
            var userId = HttpContext.GetCurrentUserId(); // from ApiControllerBase or HttpContext

            var created = await _messageboardService.CreateThreadAsync(
                groupId,
                userId,
                body.Title,
                body.Content,
                ct);

            // If you add a GET-by-id later, swap to CreatedAtAction
            return Ok(created);
        }

        // --- Posts / Replies ---

        // GET /api/messageboard/threads/{threadId}/posts?parentId={guid/null}&limit=20&cursor=...
        [HttpGet("threads/{threadId:guid}/posts")]
        [Authorize]
        public async Task<ActionResult<PageResult<MessagePost>>> GetReplies(
            [FromRoute] Guid threadId,
            [FromQuery] Guid? parentId,
            [FromQuery] PageQuery q,
            CancellationToken ct)
        {
            var result = await _messageboardService.GetRepliesAsync(
                threadId,
                parentId,
                new PageRequest(q.Limit, q.Cursor),
                ct);

            return Ok(result);
        }

        // POST /api/messageboard/threads/{threadId}/posts
        [HttpPost("threads/{threadId:guid}/posts")]
        [Authorize]
        public async Task<ActionResult<MessagePost>> CreateReply(
            [FromRoute] Guid threadId,
            [FromBody] CreatePostRequest body,
            CancellationToken ct)
        {
            var userId = HttpContext.GetCurrentUserId();

            var created = await _messageboardService.CreateReplyAsync(
                threadId,
                body.ParentId,
                userId,
                body.Content,
                ct);

            return Ok(new
            {
                created.Id,
                created.ThreadId,
                created.ParentId,
                created.Depth,
                created.Path,
                created.Content,
                created.CreatedUtc,
                created.CreatedBy,
                created.ReplyCount,
                created.LikeCount,
                created.DislikeCount,
                created.EditedAt,
                created.IsDeleted
            });
        }

        // --- Reactions ---

        // PUT /api/messageboard/posts/{postId}/reaction
        // body: { "type": "Like" }  // or null to clear via DELETE
        [HttpPut("posts/{postId:guid}/reaction")]
        [Authorize]
        public async Task<ActionResult<ReactionType?>> PutReaction(
            [FromRoute] Guid postId,
            [FromBody] ToggleReactionRequest body,
            CancellationToken ct)
        {
            var userId = HttpContext.GetCurrentUserId();

            var result = await _messageboardService.ToggleReactionAsync(
                postId,
                userId,
                body.Type,
                ct);

            return Ok(result);
        }

        // DELETE /api/messageboard/posts/{postId}/reaction
        [HttpDelete("posts/{postId:guid}/reaction")]
        [Authorize]
        public async Task<IActionResult> DeleteReaction(
            [FromRoute] Guid postId,
            CancellationToken ct)
        {
            var userId = HttpContext.GetCurrentUserId();

            await _messageboardService.ToggleReactionAsync(
                postId,
                userId,
                null, // clear reaction
                ct);

            return NoContent();
        }
    }
}
