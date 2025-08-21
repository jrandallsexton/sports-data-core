import apiClient from "./apiClient";

const MessageboardApi = {
  // --- Home feed: threads grouped by user's groups ---
  getMyThreadsByGroup: ({ perGroupLimit = 5 } = {}) =>
    apiClient.get(`/api/messageboard/my/threads-by-group`, {
      params: { perGroupLimit },
    }),

  // --- Threads ---
  getThreads: (groupId, { limit = 20, cursor = null } = {}) =>
    apiClient.get(`/api/messageboard/groups/${groupId}/threads`, {
      params: { limit, cursor },
    }),

  createThread: (groupId, { title, content }) =>
    apiClient.post(`/api/messageboard/groups/${groupId}/threads`, {
      title,
      content,
    }),

  // --- Posts / Replies ---
  getReplies: (threadId, { parentId = null, limit = 20, cursor = null } = {}) =>
    apiClient.get(`/api/messageboard/threads/${threadId}/posts`, {
      params: { parentId, limit, cursor },
    }),

  createReply: (threadId, { parentId = null, content }) =>
    apiClient.post(`/api/messageboard/threads/${threadId}/posts`, {
      parentId,
      content,
    }),

  // --- Reactions ---
  putReaction: (postId, type) =>
    apiClient.put(`/api/messageboard/posts/${postId}/reaction`, { type }),

  deleteReaction: (postId) =>
    apiClient.delete(`/api/messageboard/posts/${postId}/reaction`),
};

export default MessageboardApi;
