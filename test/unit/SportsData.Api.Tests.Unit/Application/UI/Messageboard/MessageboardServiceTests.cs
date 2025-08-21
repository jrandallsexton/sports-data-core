using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Messageboard;
using SportsData.Api.Infrastructure.Data.Entities; // MessageThread, MessagePost, MessageReaction, ReactionType

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Messageboard
{
    public class MessageboardServiceTests : ApiTestBase<MessageboardService>
    {
        private readonly Guid _groupId = Guid.NewGuid();
        private readonly Guid _userA = Guid.NewGuid();
        private readonly Guid _userB = Guid.NewGuid();

        private MessageboardService CreateSut() => Mocker.CreateInstance<MessageboardService>();

        // ---------- Helpers ----------

        // Add membership for a user -> group
        private async Task AddMembershipAsync(Guid userId, Guid groupId)
        {
            DataContext.Add(new PickemGroupMember
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PickemGroupId = groupId,
                CreatedBy = userId,
                CreatedUtc = DateTime.UtcNow
            });
            await DataContext.SaveChangesAsync();
        }

        // Same as AddThreadWithRootAsync but allows specifying groupId
        private async Task<MessageThread> AddThreadWithRootForGroupAsync(
            Guid groupId, string title, DateTime createdUtc)
        {
            var thread = new MessageThread
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                Title = title,
                CreatedBy = _userA,
                CreatedUtc = createdUtc,
                LastActivityAt = createdUtc,
                PostCount = 0
            };

            var root = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                ParentId = null,
                Depth = 0,
                Path = "0001",
                Content = $"{title} root",
                CreatedBy = _userA,
                CreatedUtc = createdUtc,
                ReplyCount = 0,
                LikeCount = 0,
                DislikeCount = 0
            };

            thread.Posts.Add(root);
            thread.PostCount = 1;

            DataContext.Add(thread);
            await DataContext.SaveChangesAsync();

            return thread;
        }


        private async Task<(MessageThread thread, MessagePost root)> AddThreadWithRootAsync(
            DateTime createdUtc,
            string? title = "Thread A",
            string content = "Root content")
        {
            var thread = new MessageThread
            {
                Id = Guid.NewGuid(),
                GroupId = _groupId,
                Title = title,
                CreatedBy = _userA,
                CreatedUtc = createdUtc,
                LastActivityAt = createdUtc,
                PostCount = 0
            };

            var root = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                ParentId = null,
                Depth = 0,
                Path = "0001",
                Content = content,
                CreatedBy = _userA,
                CreatedUtc = createdUtc,
                ReplyCount = 0,
                LikeCount = 0,
                DislikeCount = 0
            };

            thread.Posts.Add(root);
            thread.PostCount = 1;

            DataContext.Add(thread);
            await DataContext.SaveChangesAsync();

            return (thread, root);
        }

        private async Task<MessagePost> AddReplyAsync(
            MessageThread thread, MessagePost parent, DateTime createdUtc, string content = "reply")
        {
            // naïve sibling index for tests
            var siblings = await DataContext.Set<MessagePost>()
                .Where(p => p.ThreadId == thread.Id && p.ParentId == parent.Id)
                .CountAsync();

            var seg = (siblings + 1).ToString("0000");
            var reply = new MessagePost
            {
                Id = Guid.NewGuid(),
                ThreadId = thread.Id,
                ParentId = parent.Id,
                Depth = parent.Depth + 1,
                Path = $"{parent.Path}.{seg}",
                Content = content,
                CreatedBy = _userB,
                CreatedUtc = createdUtc,
                ReplyCount = 0,
                LikeCount = 0,
                DislikeCount = 0
            };

            parent.ReplyCount += 1;
            thread.PostCount += 1;
            thread.LastActivityAt = createdUtc;

            DataContext.Add(reply);
            DataContext.Update(parent);
            DataContext.Update(thread);
            await DataContext.SaveChangesAsync();

            return reply;
        }

        // ---------- Tests ----------

        [Fact]
        public async Task GetThreadsByUserGroupsAsync_Should_Return_TopN_PerGroup_Ordered_By_LastActivity()
        {
            // Arrange
            var g1 = Guid.NewGuid();
            var g2 = Guid.NewGuid();
            var g3 = Guid.NewGuid(); // user not a member

            await AddMembershipAsync(_userA, g1);
            await AddMembershipAsync(_userA, g2);
            await AddMembershipAsync(_userB, g3); // someone else

            // g1: three threads
            await AddThreadWithRootForGroupAsync(g1, "g1-oldest", new DateTime(2025, 8, 10, 10, 0, 0, DateTimeKind.Utc));
            await AddThreadWithRootForGroupAsync(g1, "g1-middle", new DateTime(2025, 8, 11, 10, 0, 0, DateTimeKind.Utc));
            await AddThreadWithRootForGroupAsync(g1, "g1-newest", new DateTime(2025, 8, 12, 10, 0, 0, DateTimeKind.Utc));

            // g2: two threads
            await AddThreadWithRootForGroupAsync(g2, "g2-older", new DateTime(2025, 8, 9, 10, 0, 0, DateTimeKind.Utc));
            await AddThreadWithRootForGroupAsync(g2, "g2-newer", new DateTime(2025, 8, 13, 10, 0, 0, DateTimeKind.Utc));

            // g3: ignored (not a member)
            await AddThreadWithRootForGroupAsync(g3, "g3-irrelevant", new DateTime(2025, 8, 14, 10, 0, 0, DateTimeKind.Utc));

            var sut = CreateSut();

            // Act
            const int perGroupLimit = 2;
            var result = await sut.GetThreadsByUserGroupsAsync(_userA, perGroupLimit, CancellationToken.None);

            // Assert: only g1 & g2 keys present
            result.Keys.Should().BeEquivalentTo(new[] { g1, g2 });

            // g1 top 2 (newest first)
            result[g1].Items.Should().HaveCount(2);
            result[g1].Items.Select(x => x.Title).Should().ContainInOrder("g1-newest", "g1-middle");
            result[g1].Items.Should().BeInDescendingOrder(x => x.LastActivityAt);
            result[g1].NextCursor.Should().NotBeNull(); // had 3, limited to 2

            // g2 all 2 (newest first)
            result[g2].Items.Should().HaveCount(2);
            result[g2].Items.Select(x => x.Title).Should().ContainInOrder("g2-newer", "g2-older");
            result[g2].Items.Should().BeInDescendingOrder(x => x.LastActivityAt);
            result[g2].NextCursor.Should().BeNull(); // exactly 2, no next
        }


        [Fact]
        public async Task GetThreadsAsync_Should_Return_MostRecentActivity_First_With_PagingCursor()
        {
            // Arrange
            var t1 = await AddThreadWithRootAsync(new DateTime(2025, 8, 18, 12, 0, 0, DateTimeKind.Utc), "T1");
            var t2 = await AddThreadWithRootAsync(new DateTime(2025, 8, 19, 12, 0, 0, DateTimeKind.Utc), "T2");
            var t3 = await AddThreadWithRootAsync(new DateTime(2025, 8, 20, 12, 0, 0, DateTimeKind.Utc), "T3");
            var sut = CreateSut();

            // Act (page 1, limit 2)
            var page1 = await sut.GetThreadsAsync(_groupId, new PageRequest(Limit: 2), CancellationToken.None);

            // Assert
            page1.Items.Select(x => x.Title).Should().ContainInOrder("T3", "T2");
            page1.NextCursor.Should().NotBeNull();

            // Act (page 2 using cursor)
            var page2 = await sut.GetThreadsAsync(_groupId, new PageRequest(Limit: 2, Cursor: page1.NextCursor), CancellationToken.None);

            // Assert
            page2.Items.Select(x => x.Title).Should().ContainSingle().Which.Should().Be("T1");
            page2.NextCursor.Should().BeNull();
        }

        [Fact]
        public async Task CreateThreadAsync_Should_Create_Thread_And_RootPost_And_Initialize_Counters()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var created = await sut.CreateThreadAsync(_groupId, _userA, "Game Talk", "Who’s ready?", CancellationToken.None);

            // Assert
            var thread = await DataContext.Set<MessageThread>().Include(t => t.Posts).SingleAsync(t => t.Id == created.Id);
            thread.GroupId.Should().Be(_groupId);
            thread.PostCount.Should().Be(1);
            thread.LastActivityAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            var root = thread.Posts.Single();
            root.Depth.Should().Be(0);
            root.ParentId.Should().BeNull();
            root.Path.Should().Be("0001");
            root.Content.Should().Be("Who’s ready?");
        }

        [Fact]
        public async Task GetRepliesAsync_Should_Return_TopLevel_Replies_In_Chrono_Order_With_Cursor()
        {
            // Arrange
            var (thread, root) = await AddThreadWithRootAsync(new DateTime(2025, 8, 18, 12, 0, 0, DateTimeKind.Utc));
            var r1 = await AddReplyAsync(thread, root, new DateTime(2025, 8, 18, 12, 5, 0, DateTimeKind.Utc), "r1");
            var r2 = await AddReplyAsync(thread, root, new DateTime(2025, 8, 18, 12, 6, 0, DateTimeKind.Utc), "r2");
            var r3 = await AddReplyAsync(thread, root, new DateTime(2025, 8, 18, 12, 7, 0, DateTimeKind.Utc), "r3");
            var sut = CreateSut();

            // Act page1 (limit 2)
            var page1 = await sut.GetRepliesAsync(thread.Id, root.Id, new PageRequest(Limit: 2), CancellationToken.None);

            // Assert
            page1.Items.Select(x => x.Content).Should().ContainInOrder("r1", "r2");
            page1.NextCursor.Should().NotBeNull();

            // Act page2
            var page2 = await sut.GetRepliesAsync(thread.Id, root.Id, new PageRequest(Limit: 2, Cursor: page1.NextCursor), CancellationToken.None);

            // Assert
            page2.Items.Select(x => x.Content).Should().ContainSingle().Which.Should().Be("r3");
            page2.NextCursor.Should().BeNull();
        }

        [Fact]
        public async Task CreateReplyAsync_Should_Set_Depth_Path_Update_Counters_And_Bump_Thread_Activity()
        {
            // Arrange
            var (thread, root) = await AddThreadWithRootAsync(new DateTime(2025, 8, 18, 12, 0, 0, DateTimeKind.Utc));
            var sut = CreateSut();

            // Act
            var reply = await sut.CreateReplyAsync(thread.Id, root.Id, _userB, "hi!", CancellationToken.None);

            // Assert
            reply.Depth.Should().Be(1);
            reply.ParentId.Should().Be(root.Id);
            reply.Path.Should().StartWith("0001.");
            reply.Content.Should().Be("hi!");

            var refreshedParent = await DataContext.Set<MessagePost>().SingleAsync(p => p.Id == root.Id);
            refreshedParent.ReplyCount.Should().Be(1);

            var refreshedThread = await DataContext.Set<MessageThread>().SingleAsync(t => t.Id == thread.Id);
            refreshedThread.PostCount.Should().Be(2);
            refreshedThread.LastActivityAt.Should().BeAfter(thread.CreatedUtc);
        }

        [Fact]
        public async Task ToggleReactionAsync_FirstSet_Should_Add_Reaction_And_Increment_Count()
        {
            // Arrange
            var (thread, root) = await AddThreadWithRootAsync(DateTime.UtcNow);
            var sut = CreateSut();

            // Act
            var result = await sut.ToggleReactionAsync(root.Id, _userA, ReactionType.Like, CancellationToken.None);

            // Assert
            result.Should().Be(ReactionType.Like);
            var post = await DataContext.Set<MessagePost>().SingleAsync(p => p.Id == root.Id);
            post.LikeCount.Should().Be(1);
            post.DislikeCount.Should().Be(0);

            var rx = await DataContext.Set<MessageReaction>().SingleAsync(r => r.PostId == root.Id && r.UserId == _userA);
            rx.Type.Should().Be(ReactionType.Like);
        }

        [Fact]
        public async Task ToggleReactionAsync_SameType_Should_Remove_Reaction_And_Decrement_Count()
        {
            // Arrange
            var (thread, root) = await AddThreadWithRootAsync(DateTime.UtcNow);
            DataContext.Add(new MessageReaction
            {
                Id = Guid.NewGuid(),
                PostId = root.Id,
                UserId = _userA,
                Type = ReactionType.Like,
                CreatedBy = _userA,
                CreatedUtc = DateTime.UtcNow
            });
            root.LikeCount = 1;
            DataContext.Update(root);
            await DataContext.SaveChangesAsync();

            var sut = CreateSut();

            // Act (toggle off)
            var result = await sut.ToggleReactionAsync(root.Id, _userA, ReactionType.Like, CancellationToken.None);

            // Assert
            result.Should().Be(ReactionType.Like); // service returns requested type; null on remove is also acceptable if you prefer
            var post = await DataContext.Set<MessagePost>().SingleAsync(p => p.Id == root.Id);
            post.LikeCount.Should().Be(0);

            var rxExists = await DataContext.Set<MessageReaction>()
                .AnyAsync(r => r.PostId == root.Id && r.UserId == _userA);
            rxExists.Should().BeFalse();
        }

        [Fact]
        public async Task ToggleReactionAsync_FlipType_Should_Switch_Reaction_And_Adjust_Counters()
        {
            // Arrange
            var (thread, root) = await AddThreadWithRootAsync(DateTime.UtcNow);
            DataContext.Add(new MessageReaction
            {
                Id = Guid.NewGuid(),
                PostId = root.Id,
                UserId = _userA,
                Type = ReactionType.Dislike,
                CreatedBy = _userA,
                CreatedUtc = DateTime.UtcNow
            });
            root.DislikeCount = 1;
            DataContext.Update(root);
            await DataContext.SaveChangesAsync();

            var sut = CreateSut();

            // Act (flip to Like)
            var result = await sut.ToggleReactionAsync(root.Id, _userA, ReactionType.Like, CancellationToken.None);

            // Assert
            result.Should().Be(ReactionType.Like);
            var post = await DataContext.Set<MessagePost>().SingleAsync(p => p.Id == root.Id);
            post.LikeCount.Should().Be(1);
            post.DislikeCount.Should().Be(0);

            var rx = await DataContext.Set<MessageReaction>().SingleAsync(r => r.PostId == root.Id && r.UserId == _userA);
            rx.Type.Should().Be(ReactionType.Like);
        }
    }
}
