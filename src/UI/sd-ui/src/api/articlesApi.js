import apiClient from "./apiClient";

const ArticlesApi = {
  getArticles: () => apiClient.get("/ui/articles"),
  getArticleById: (id) => apiClient.get(`/ui/articles/${id}`),
};

export default ArticlesApi;
