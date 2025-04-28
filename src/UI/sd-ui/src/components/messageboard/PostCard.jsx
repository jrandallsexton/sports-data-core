import { useState } from "react";
import { FaThumbsUp, FaThumbsDown, FaReply } from "react-icons/fa";

function PostCard({ post, onReply, onReact, parentId = null }) {
  const [showReplyForm, setShowReplyForm] = useState(false);
  const [replyContent, setReplyContent] = useState("");
  const [popLike, setPopLike] = useState(false);
  const [popDislike, setPopDislike] = useState(false);

  function handleReplySubmit() {
    if (replyContent.trim() === "") return;

    onReply(post.id, replyContent);
    setReplyContent("");
    setShowReplyForm(false);
  }

  function handleReaction(reactionType) {
    if (onReact) {
      onReact(post.id, reactionType);

      // Trigger pop animation
      if (reactionType === "like") {
        setPopLike(true);
        setTimeout(() => setPopLike(false), 300);
      } else {
        setPopDislike(true);
        setTimeout(() => setPopDislike(false), 300);
      }
    }
  }

  return (
    <div className="post-card" style={{ marginLeft: parentId ? "20px" : "0" }}>
      <div className="post-header">
        <strong>{post.author}</strong> • {new Date(post.timestamp).toLocaleString()}
      </div>
      <div className="post-content">{post.content}</div>

      <div className="post-actions">
        {/* Reply */}
        <button
          className="reply-icon-button"
          onClick={() => setShowReplyForm(!showReplyForm)}
        >
          <FaReply />
        </button>
        <button
          className="reply-text-button"
          onClick={() => setShowReplyForm(!showReplyForm)}
        >
          {showReplyForm ? "Cancel" : "Reply"}
        </button>

        {/* Like */}
        <button
          className={`reaction-button ${post.userReaction === "like" ? "liked" : ""}`}
          onClick={() => handleReaction("like")}
        >
          <FaThumbsUp style={{ marginRight: "4px" }} />
          <span className={`reaction-count ${popLike ? "pop" : ""}`}>{post.likes}</span>
        </button>

        {/* Dislike */}
        <button
          className={`reaction-button ${post.userReaction === "dislike" ? "disliked" : ""}`}
          onClick={() => handleReaction("dislike")}
        >
          <FaThumbsDown style={{ marginRight: "4px" }} />
          <span className={`reaction-count ${popDislike ? "pop" : ""}`}>{post.dislikes}</span>
        </button>
      </div>

      {showReplyForm && (
        <div className="reply-form">
          <textarea
            placeholder="Write your reply..."
            value={replyContent}
            onChange={(e) => setReplyContent(e.target.value)}
          />
          <button onClick={handleReplySubmit}>Submit Reply</button>
        </div>
      )}

      {/* Recursive rendering of replies */}
      <div className="replies">
        {post.replies &&
          post.replies.map((reply) => (
            <PostCard
              key={reply.id}
              post={reply}
              onReply={onReply}
              onReact={onReact}
              parentId={post.id}
            />
          ))}
      </div>
    </div>
  );
}

export default PostCard;
