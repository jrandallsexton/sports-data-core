import { useEffect, useRef, useState } from "react";
import PostCard from "./PostCard";
import "./MessageBoardPage.css";
import MessageboardApi from "../../api/messageboardApi";
import { useParams } from "react-router-dom";

function MessageBoardPage() {
  const { groupId } = useParams(); // may be undefined on "All Groups"
  const [posts, setPosts] = useState([]);
  const [newPostContent, setNewPostContent] = useState("");
  const [loading, setLoading] = useState(false);
  const [cursor, setCursor] = useState(null);
  const [hasMore, setHasMore] = useState(true);
  const postThreadMapRef = useRef(new Map()); // postId -> threadId

  // --- helpers to map BE → FE shape (minimal) ---
  const mapPost = (p) => ({
    id: p.id,
    threadId: p.threadId,
    author: p.createdByUserId?.slice(0, 8) || "User",
    timestamp: p.createdUtc,
    content: p.content,
    likes: p.likeCount || 0,
    dislikes: p.dislikeCount || 0,
    userReaction: p.userReaction ?? null,
    replies: [],
  });

  // --- initial & paged load of threads for a specific group ---
  async function fetchThreadsPage(limit = 10) {
    if (!groupId || loading || !hasMore) return; // guard when no groupId
    setLoading(true);
    try {
      const { data } = await MessageboardApi.getThreads(groupId, {
        limit,
        cursor,
      });
      const { items, nextCursor } = data;

      // For each thread, fetch the OP (parentId == null returns the root)
      const opsWithReplies = await Promise.all(
        items.map((t) => fetchOpWithReplies(t))
      );
      setPosts((prev) => [...prev, ...opsWithReplies.filter(Boolean)]);
      setCursor(nextCursor || null);
      setHasMore(Boolean(nextCursor));
    } finally {
      setLoading(false);
    }
  }

  // --- home feed: threads grouped by user's groups (flattened for now) ---
  async function fetchHomeFeed(perGroupLimit = 5) {
    setLoading(true);
    try {
      const { data } = await MessageboardApi.getMyThreadsByGroup({
        perGroupLimit,
      });
      // data: { [groupId]: { items: [threads], nextCursor } }
      const allThreads = Object.values(data).flatMap(
        (page) => page.items || []
      );

      // fetch each thread’s OP
      const opsWithReplies = await Promise.all(
        allThreads.map((t) => fetchOpWithReplies(t))
      );
      setPosts(opsWithReplies.filter(Boolean));
      // home feed doesn’t paginate (yet); you can add per-group cursors later
      setCursor(null);
      setHasMore(false);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    // reset when groupId changes
    setPosts([]);
    setCursor(null);
    setHasMore(true);

    if (groupId) {
      fetchThreadsPage();
    } else {
      fetchHomeFeed();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [groupId]);

  // --- create a new thread (OP post) ---
  async function handleNewPost() {
    const content = newPostContent.trim();
    if (!content || !groupId) return; // only allow posting when scoped to a group

    // optimistic OP card
    const tempId = `temp-${Date.now()}`;
    const optimistic = {
      id: tempId,
      threadId: null,
      author: "You",
      timestamp: new Date().toISOString(),
      content,
      likes: 0,
      dislikes: 0,
      userReaction: null,
      replies: [],
      _optimistic: true,
    };
    setPosts((prev) => [optimistic, ...prev]);
    setNewPostContent("");

    try {
      const { data: thread } = await MessageboardApi.createThread(groupId, {
        title: null,
        content,
      });
      // fetch the OP (root) back to get real ids
      const { data: page } = await MessageboardApi.getReplies(thread.id, {
        parentId: null,
        limit: 1,
      });
      const op = page.items?.[0];
      if (op) {
        const mapped = mapPost(op);
        postThreadMapRef.current.set(mapped.id, thread.id);
        setPosts((prev) => [mapped, ...prev.filter((p) => p.id !== tempId)]);
      } else {
        setPosts((prev) => prev.filter((p) => p.id !== tempId));
      }
    } catch {
      setPosts((prev) => prev.filter((p) => p.id !== tempId));
    }
  }

  // --- create a reply under any post ---
  async function handleReply(parentId, replyContent) {
    const content = replyContent.trim();
    if (!content) return;

    const threadId = postThreadMapRef.current.get(parentId);
    if (!threadId) return;

    // optimistic
    const tempId = `temp-r-${Date.now()}`;
    const newReply = {
      id: tempId,
      threadId,
      author: "You",
      timestamp: new Date().toISOString(),
      content,
      likes: 0,
      dislikes: 0,
      userReaction: null,
      replies: [],
      _optimistic: true,
    };

    function addReplyRecursive(items) {
      return items.map((item) => {
        if (item.id === parentId) {
          return { ...item, replies: [...(item.replies || []), newReply] };
        }
        if (item.replies?.length) {
          return { ...item, replies: addReplyRecursive(item.replies) };
        }
        return item;
      });
    }
    setPosts((prev) => addReplyRecursive(prev));

    try {
      const { data: created } = await MessageboardApi.createReply(threadId, {
        parentId,
        content,
      });
      const mapped = mapPost(created);
      postThreadMapRef.current.set(mapped.id, threadId);

      // swap temp with real
      function swapRecursive(items) {
        return items.map((item) => {
          if (item.id === parentId) {
            return {
              ...item,
              replies: item.replies.map((r) => (r.id === tempId ? mapped : r)),
            };
          }
          if (item.replies?.length) {
            return { ...item, replies: swapRecursive(item.replies) };
          }
          return item;
        });
      }
      setPosts((prev) => swapRecursive(prev));
    } catch {
      // rollback remove temp
      function removeTemp(items) {
        return items.map((item) => {
          if (item.id === parentId) {
            return {
              ...item,
              replies: item.replies.filter((r) => r.id !== tempId),
            };
          }
          if (item.replies?.length) {
            return { ...item, replies: removeTemp(item.replies) };
          }
          return item;
        });
      }
      setPosts((prev) => removeTemp(prev));
    }
  }

  // --- like/dislike reaction (optimistic) ---
  async function handleReaction(postId, reactionType) {
    const toEnum = (s) =>
      s === "like" ? "Like" : s === "dislike" ? "Dislike" : null;

    function updateRecursive(items) {
      return items.map((item) => {
        if (item.id === postId) {
          let { likes, dislikes, userReaction } = item;
          const next = userReaction === reactionType ? null : reactionType;

          if (userReaction === "like") likes = Math.max(0, likes - 1);
          if (userReaction === "dislike") dislikes = Math.max(0, dislikes - 1);
          if (next === "like") likes += 1;
          if (next === "dislike") dislikes += 1;

          return { ...item, likes, dislikes, userReaction: next };
        }
        if (item.replies?.length)
          return { ...item, replies: updateRecursive(item.replies) };
        return item;
      });
    }

    const prev = posts;
    setPosts(updateRecursive(posts));

    try {
      const findPrev = (items) => {
        for (const it of items) {
          if (it.id === postId) return it.userReaction;
          if (it.replies?.length) {
            const r = findPrev(it.replies);
            if (r !== undefined) return r;
          }
        }
        return undefined;
      };
      const prevReaction = findPrev(prev);
      if (prevReaction === reactionType) {
        await MessageboardApi.deleteReaction(postId);
      } else {
        await MessageboardApi.putReaction(postId, toEnum(reactionType));
      }
    } catch {
      setPosts(prev);
    }
  }

  async function fetchOpWithReplies(thread, replyLimit = 10) {
    // OP (root)
    const opResp = await MessageboardApi.getReplies(thread.id, {
      parentId: null,
      limit: 1,
    });
    const op = opResp.data.items?.[0];
    if (!op) return null;

    const mappedOp = mapPost(op);
    postThreadMapRef.current.set(mappedOp.id, thread.id);

    // Top-level replies under the OP
    const repliesResp = await MessageboardApi.getReplies(thread.id, {
      parentId: op.id,
      limit: replyLimit,
    });
    const replies = (repliesResp.data.items || []).map(mapPost);

    mappedOp.replies = replies;
    return mappedOp;
  }

  return (
    <div className="message-board">
      <h2>{groupId ? "Group Message Board" : "All Groups"}</h2>

      {/* New Post Form (only when scoped to a group) */}
      <div className="new-post-form">
        <textarea
          placeholder={
            groupId ? "Start a new conversation..." : "Select a group to post"
          }
          value={newPostContent}
          onChange={(e) => setNewPostContent(e.target.value)}
          disabled={!groupId}
        />
        <button
          onClick={handleNewPost}
          disabled={!groupId || !newPostContent.trim()}
        >
          Post
        </button>
      </div>

      {/* Post List (each item is an OP for a thread) */}
      <div className="post-list">
        {posts.map((post) => (
          <PostCard
            key={post.id}
            post={post}
            onReply={handleReply}
            onReact={handleReaction}
          />
        ))}

        {groupId && (
          <div className="pager">
            {hasMore && (
              <button onClick={() => fetchThreadsPage()} disabled={loading}>
                {loading ? "Loading..." : "Load more threads"}
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

export default MessageBoardPage;
