using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Messageboard
{
    public sealed record PageRequest(int Limit = 20, string? Cursor = null); // Cursor = createdAt ticks or guid string
    public sealed record PageResult<T>(IReadOnlyList<T> Items, string? NextCursor);

    public class MessageboardService : IMessageboardService
    {
        private readonly ILogger<MessageboardService> _logger;
        private readonly AppDataContext _dataContext;

        public MessageboardService(
            ILogger<MessageboardService> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        // --- Threads ---
        public async Task<IDictionary<Guid, PageResult<MessageThread>>> GetThreadsByUserGroupsAsync(
            Guid userId, int perGroupLimit, CancellationToken ct = default)
        {
            var groupIds = await _dataContext.Set<PickemGroupMember>()
                .Where(m => m.UserId == userId)
                .Select(m => m.PickemGroupId)
                .Distinct()
                .ToListAsync(ct);

            var result = new Dictionary<Guid, PageResult<MessageThread>>();

            foreach (var gid in groupIds)
            {
                var q = _dataContext.Set<MessageThread>()
                    .AsNoTracking()
                    .Include(t => t.User)
                    .Where(t => t.GroupId == gid)
                    .OrderByDescending(t => t.LastActivityAt);

                var items = await q.Take(perGroupLimit + 1).ToListAsync(ct);
                string? next = null;
                if (items.Count > perGroupLimit)
                {
                    var last = items[perGroupLimit - 1];
                    next = last.LastActivityAt.Ticks.ToString();
                    items = items.Take(perGroupLimit).ToList();
                }

                result[gid] = new PageResult<MessageThread>(items, next);
            }

            return result;
        }

        // List threads under a group, newest activity first
        public async Task<PageResult<MessageThread>> GetThreadsAsync(
            Guid groupId, PageRequest page, CancellationToken ct = default)
        {
            // Cursor = LastActivityAt ticks (string)
            long? cursorTicks = long.TryParse(page.Cursor, out var t) ? t : (long?)null;

            var q = _dataContext.Set<MessageThread>()
                .AsNoTracking()
                .Include(t => t.User)
                .Where(t0 => t0.GroupId == groupId);

            if (cursorTicks is not null)
                q = q.Where(t0 => t0.LastActivityAt.Ticks < cursorTicks.Value);

            q = q.OrderByDescending(t0 => t0.LastActivityAt);

            var items = await q.Take(page.Limit + 1).ToListAsync(ct);
            string? next = null;

            if (items.Count > page.Limit)
            {
                var last = items[^2];               // second-to-last (since we took +1)
                next = last.LastActivityAt.Ticks.ToString();
                items.RemoveAt(items.Count - 1);
            }

            return new PageResult<MessageThread>(items, next);
        }

        // Create a new thread with a root post (OP)
        public async Task<MessageThread> CreateThreadAsync(
            Guid groupId, Guid userId, string? title, string content, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content is required.", nameof(content));

            var utcNow = DateTime.UtcNow;

            var thread = new MessageThread
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                CreatedBy = userId,
                CreatedUtc = utcNow,
                LastActivityAt = utcNow,
                Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
                PostCount = 0
            };

            // Root post (Depth 0, no ParentId)
            var rootPost = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                ParentId = null,
                Depth = 0,
                Path = "0001", // first segment
                Content = content.Trim(),
                CreatedBy = userId,
                CreatedUtc = utcNow,
                ReplyCount = 0,
                LikeCount = 0,
                DislikeCount = 0
            };

            thread.Posts.Add(rootPost);
            thread.PostCount = 1;

            _dataContext.Add(thread);
            await _dataContext.SaveChangesAsync(ct);

            return thread;
        }

        // --- Posts (replies) ---

        // List direct children of a parent (or OP’s direct children when parentId == root.Id)
        public async Task<PageResult<MessagePost>> GetRepliesAsync(
            Guid threadId, Guid? parentId, PageRequest page, CancellationToken ct = default)
        {
            // Cursor = CreatedAt ticks
            long? cursorTicks = long.TryParse(page.Cursor, out var t) ? t : (long?)null;

            var q = _dataContext.Set<MessagePost>()
                .Include(p => p.User)
                .AsNoTracking()
                .Where(p => p.ThreadId == threadId && p.ParentId == parentId);

            if (cursorTicks is not null)
                q = q.Where(p => p.CreatedUtc.Ticks > cursorTicks.Value); // forward paging (old->new)

            q = q.OrderBy(p => p.CreatedUtc); // oldest first for conversational flow

            var items = await q.Take(page.Limit + 1).ToListAsync(ct);
            string? next = null;

            if (items.Count > page.Limit)
            {
                var last = items[page.Limit - 1];
                next = last.CreatedUtc.Ticks.ToString();
                items = items.Take(page.Limit).ToList();
            }

            _logger.LogInformation("Replies returned: {Count}", items.Count);

            return new PageResult<MessagePost>(items, next);
        }

        // Add a reply to any post (or to the root)
        public async Task<MessagePost> CreateReplyAsync(
            Guid threadId, Guid? parentPostId, Guid userId, string content, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content is required.", nameof(content));

            var utcNow = DateTime.UtcNow;

            // Load thread & (optional) parent
            var thread = await _dataContext.Set<MessageThread>()
                .SingleOrDefaultAsync(t => t.Id == threadId, ct)
                ?? throw new InvalidOperationException("Thread not found.");

            MessagePost? parent = null;
            if (parentPostId is not null)
            {
                parent = await _dataContext.Set<MessagePost>()
                    .SingleOrDefaultAsync(p => p.Id == parentPostId.Value && p.ThreadId == threadId, ct)
                    ?? throw new InvalidOperationException("Parent post not found.");
            }

            // Compute Path + Depth (materialized path)
            var parentPath = parent?.Path ?? "0001"; // root is "0001"
            var depth = (parent?.Depth ?? 0) + 1;

            // Determine next sibling index under the same parent
            var siblingCount = await _dataContext.Set<MessagePost>()
                .Where(p => p.ThreadId == threadId && p.ParentId == parentPostId)
                .CountAsync(ct);

            var segment = ToFixedBase36(siblingCount + 1, 4); // "0001", "0002", ...
            var path = parent is null ? parentPath : $"{parentPath}.{segment}";

            var reply = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                ParentId = parentPostId,
                Depth = depth,
                Path = path,
                Content = content.Trim(),
                CreatedBy = userId,
                CreatedUtc = utcNow,
                ReplyCount = 0,
                LikeCount = 0,
                DislikeCount = 0
            };

            _dataContext.Add(reply);

            // Denorm counters + thread bump
            if (parent is not null)
            {
                parent.ReplyCount += 1;
                _dataContext.Update(parent);
            }

            thread.PostCount += 1;
            thread.LastActivityAt = utcNow;
            _dataContext.Update(thread);

            await _dataContext.SaveChangesAsync(ct);
            return reply;
        }

        // --- Reactions (toggle/upsert) ---

        // Type == null => remove (toggle off)
        public async Task<ReactionType?> ToggleReactionAsync(
            Guid postId, Guid userId, ReactionType? type, CancellationToken ct = default)
        {
            var post = await _dataContext.Set<MessagePost>()
                           .SingleOrDefaultAsync(p => p.Id == postId, ct)
                       ?? throw new InvalidOperationException("Post not found.");

            var existing = await _dataContext.Set<MessageReaction>()
                .SingleOrDefaultAsync(r => r.PostId == postId && r.UserId == userId, ct);

            // Clear reaction (explicit remove)
            if (type is null)
            {
                if (existing is not null)
                {
                    AdjustCounts(post, existing.Type, decrement: true);
                    _dataContext.Remove(existing);
                    _dataContext.Update(post);                 // <-- ensure counts persist
                    await _dataContext.SaveChangesAsync(ct);
                }
                return null;
            }

            // Upsert / toggle / flip
            if (existing is null)
            {
                _dataContext.Add(new MessageReaction
                {
                    Id = Guid.NewGuid(),
                    PostId = postId,
                    UserId = userId,
                    Type = type.Value,
                    CreatedBy = userId,
                    CreatedUtc = DateTime.UtcNow
                });
                AdjustCounts(post, type.Value, decrement: false);
            }
            else if (existing.Type == type.Value)
            {
                // Toggle off
                AdjustCounts(post, existing.Type, decrement: true);
                _dataContext.Remove(existing);
            }
            else
            {
                // Flip
                AdjustCounts(post, existing.Type, decrement: true);
                existing.Type = type.Value;
                existing.ModifiedBy = userId;
                existing.ModifiedUtc = DateTime.UtcNow;
                _dataContext.Update(existing);
                AdjustCounts(post, type.Value, decrement: false);
            }

            _dataContext.Update(post);
            await _dataContext.SaveChangesAsync(ct);

            return type.Value;
        }

        // --- Helpers ---

        private static void AdjustCounts(MessagePost post, ReactionType type, bool decrement)
        {
            int delta = decrement ? -1 : 1;
            switch (type)
            {
                case ReactionType.Like: post.LikeCount += delta; break;
                case ReactionType.Dislike: post.DislikeCount += delta; break;
                case ReactionType.Laugh: post.LikeCount += delta; break;   // or track each separately if you later add columns
                case ReactionType.Sad: post.DislikeCount += delta; break;   // keep simple for now
                case ReactionType.Angry: post.DislikeCount += delta; break;
                case ReactionType.Surprise: post.LikeCount += delta; break;
            }
        }

        private static string ToFixedBase36(int value, int width)
        {
            const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var stack = new Stack<char>();
            int v = Math.Max(0, value);
            do { stack.Push(alphabet[v % 36]); v /= 36; } while (v > 0);
            var s = new string(stack.ToArray());
            return s.PadLeft(width, '0');
        }
    }
}
