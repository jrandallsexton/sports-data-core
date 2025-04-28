import { useState } from "react";
import PostCard from "./PostCard";
import "./MessageBoardPage.css";
import postsData from "../../data/posts"; // Clean naming: imported mock posts

function MessageBoardPage() {
  const [posts, setPosts] = useState(postsData); // Avoid "postsState" unless you need multiple versions
  const [newPostContent, setNewPostContent] = useState("");

  function handleNewPost() {
    if (!newPostContent.trim()) return;

    const newPost = {
      id: Date.now(),
      author: "Current User",
      timestamp: new Date().toISOString(),
      content: newPostContent,
      likes: 0,
      dislikes: 0,
      userReaction: null,
      replies: [],
    };

    setPosts((prevPosts) => [newPost, ...prevPosts]);
    setNewPostContent("");
  }

  function handleReply(parentId, replyContent) {
    const newReply = {
      id: Date.now(),
      author: "Current User",
      timestamp: new Date().toISOString(),
      content: replyContent,
      likes: 0,
      dislikes: 0,
      userReaction: null,
      replies: [],
    };

    function addReplyRecursive(items) {
      return items.map((item) => {
        if (item.id === parentId) {
          return {
            ...item,
            replies: [...item.replies, newReply],
          };
        }
        if (item.replies.length > 0) {
          return {
            ...item,
            replies: addReplyRecursive(item.replies),
          };
        }
        return item;
      });
    }

    setPosts((prevPosts) => addReplyRecursive(prevPosts));
  }

  function handleReaction(postId, reactionType) {
    function updateRecursive(items) {
      return items.map((item) => {
        if (item.id === postId) {
          let updatedLikes = item.likes;
          let updatedDislikes = item.dislikes;
          let newReaction = reactionType;

          // Toggle off if already selected
          if (item.userReaction === reactionType) {
            newReaction = null;
            if (reactionType === "like") updatedLikes -= 1;
            if (reactionType === "dislike") updatedDislikes -= 1;
          } else {
            if (item.userReaction === "like") updatedLikes -= 1;
            if (item.userReaction === "dislike") updatedDislikes -= 1;

            if (reactionType === "like") updatedLikes += 1;
            if (reactionType === "dislike") updatedDislikes += 1;
          }

          return {
            ...item,
            likes: updatedLikes,
            dislikes: updatedDislikes,
            userReaction: newReaction,
          };
        }

        if (item.replies.length > 0) {
          return {
            ...item,
            replies: updateRecursive(item.replies),
          };
        }

        return item;
      });
    }

    setPosts((prevPosts) => updateRecursive(prevPosts));
  }

  //   function handleReaction(postId, reactionType) {
  //     function updateRecursive(items) {
  //       return items.map((item) => {
  //         if (item.id === postId) {
  //           return { ...item }; // Already handled inside PostCard
  //         }
  //         if (item.replies.length > 0) {
  //           return {
  //             ...item,
  //             replies: updateRecursive(item.replies),
  //           };
  //         }
  //         return item;
  //       });
  //     }

  //     setPosts((prevPosts) => updateRecursive(prevPosts));
  //   }

  return (
    <div className="message-board">
      <h2>Group Message Board</h2>

      {/* New Post Form */}
      <div className="new-post-form">
        <textarea
          placeholder="Start a new conversation..."
          value={newPostContent}
          onChange={(e) => setNewPostContent(e.target.value)}
        />
        <button onClick={handleNewPost}>Post</button>
      </div>

      {/* Post List */}
      <div className="post-list">
        {posts.map((post) => (
          <PostCard
            key={post.id}
            post={post}
            onReply={handleReply}
            onReact={handleReaction}
          />
        ))}
      </div>
    </div>
  );
}

export default MessageBoardPage;
