import { useMemo, useState } from "react";
import { FaThumbsUp, FaThumbsDown, FaReply } from "react-icons/fa";

function PostCard({ post, onReply, onReact, parentId = null, isReadOnly = false }) {
  const [showReplyForm, setShowReplyForm] = useState(false);
  const [replyContent, setReplyContent] = useState("");
  const [popLike, setPopLike] = useState(false);
  const [popDislike, setPopDislike] = useState(false);
  const [reactPending, setReactPending] = useState(false);

  const author = post.author ?? "User";
  const likes = Number.isFinite(post.likes) ? post.likes : 0;
  const dislikes = Number.isFinite(post.dislikes) ? post.dislikes : 0;
  const userReaction = post.userReaction ?? null;
  const isOptimistic = Boolean(post._optimistic);

  const formattedTs = useMemo(() => {
    try {
      return new Date(post.timestamp).toLocaleString();
    } catch {
      return "";
    }
  }, [post.timestamp]);

  function handleReplySubmit() {
    const text = replyContent.trim();
    if (!text) return;
    onReply?.(post.id, text);
    setReplyContent("");
    setShowReplyForm(false);
  }

  async function handleReaction(reactionType) {
    if (!onReact || reactPending) return;
    setReactPending(true);
    try {
      onReact(post.id, reactionType);
      // Trigger pop animation
      if (reactionType === "like") {
        setPopLike(true);
        setTimeout(() => setPopLike(false), 300);
      } else {
        setPopDislike(true);
        setTimeout(() => setPopDislike(false), 300);
      }
    } finally {
      // small delay to avoid rapid re-click thrash
      setTimeout(() => setReactPending(false), 150);
    }
  }

  return (
    <div
      className={`post-card ${isOptimistic ? "optimistic" : ""}`}
      style={{ marginLeft: parentId ? "20px" : "0" }}
      aria-busy={isOptimistic ? "true" : "false"}
    >
      <div className="post-header">
        <strong>{author}</strong>
        {formattedTs && <> • {formattedTs}</>}
      </div>

      <div className="post-content">{post.content}</div>

      <div className="post-actions">
        {/* Reply */}
        <button
          className="reply-icon-button"
          onClick={() => setShowReplyForm((s) => !s)}
          aria-label={showReplyForm ? "Cancel reply" : "Reply"}
          disabled={isReadOnly}
          title={isReadOnly ? "Read-only mode" : ""}
        >
          <FaReply />
        </button>
        <button
          className="reply-text-button"
          onClick={() => setShowReplyForm((s) => !s)}
          disabled={isReadOnly}
        >
          {showReplyForm ? "Cancel" : "Reply"}
        </button>

        {/* Like */}
        <button
          className={`reaction-button ${userReaction === "like" ? "liked" : ""}`}
          onClick={() => handleReaction("like")}
          disabled={reactPending || isReadOnly}
          aria-pressed={userReaction === "like"}
          aria-label="Like"
          title={isReadOnly ? "Read-only mode" : ""}
        >
          <FaThumbsUp style={{ marginRight: 4 }} />
          <span className={`reaction-count ${popLike ? "pop" : ""}`}>{likes}</span>
        </button>

        {/* Dislike */}
        <button
          className={`reaction-button ${userReaction === "dislike" ? "disliked" : ""}`}
          onClick={() => handleReaction("dislike")}
          disabled={reactPending || isReadOnly}
          aria-pressed={userReaction === "dislike"}
          aria-label="Dislike"
          title={isReadOnly ? "Read-only mode" : ""}
        >
          <FaThumbsDown style={{ marginRight: 4 }} />
          <span className={`reaction-count ${popDislike ? "pop" : ""}`}>{dislikes}</span>
        </button>
      </div>

      {showReplyForm && !isReadOnly && (
        <div className="reply-form">
          <textarea
            placeholder="Write your reply..."
            value={replyContent}
            onChange={(e) => setReplyContent(e.target.value)}
            onKeyDown={(e) => {
              if ((e.metaKey || e.ctrlKey) && e.key === "Enter") {
                handleReplySubmit();
              }
            }}
          />
          <button onClick={handleReplySubmit} disabled={!replyContent.trim()}>
            Submit Reply
          </button>
        </div>
      )}

      {/* Recursive rendering of replies */}
      <div className="replies">
        {post.replies?.map((reply) => (
          <PostCard
            key={reply.id}
            post={reply}
            onReply={onReply}
            onReact={onReact}
            parentId={post.id}
            isReadOnly={isReadOnly}
          />
        ))}
      </div>
    </div>
  );
}

export default PostCard;
