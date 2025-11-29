import React, { useState, useEffect } from "react";
import ReactDOM from "react-dom";
import apiWrapper from "../../api/apiWrapper";
import "../home/HomePage.css";

function NewsWidget() {
  const [articles, setArticles] = useState([]);
  const [seasonWeekNumber, setSeasonWeekNumber] = useState(null);
  const [loading, setLoading] = useState(true);
  const [selectedArticle, setSelectedArticle] = useState(null);
  const [loadingArticle, setLoadingArticle] = useState(false);

  useEffect(() => {
    async function fetchArticles() {
      setLoading(true);
      try {
        const response = await apiWrapper.Articles.getArticles();
        setArticles(response.data?.articles || []);
        setSeasonWeekNumber(response.data?.seasonWeekNumber || null);
      } catch (error) {
        console.error("Failed to fetch articles:", error);
        setArticles([]);
      } finally {
        setLoading(false);
      }
    }
    fetchArticles();
  }, []);

  useEffect(() => {
    if (selectedArticle || loadingArticle) {
      document.body.style.overflow = 'hidden';
    } else {
      document.body.style.overflow = 'unset';
    }
    return () => {
      document.body.style.overflow = 'unset';
    };
  }, [selectedArticle, loadingArticle]);

  const handleArticleClick = async (articleId) => {
    setLoadingArticle(true);
    try {
      const response = await apiWrapper.Articles.getArticleById(articleId);
      console.log("Article response:", response.data);
      setSelectedArticle(response.data.article);
    } catch (error) {
      console.error("Failed to fetch article:", error);
      setSelectedArticle(null);
    } finally {
      setLoadingArticle(false);
    }
  };

  const handleCloseDialog = () => {
    setSelectedArticle(null);
  };

  if (loading) {
    return (
      <div className="news-card">
        <h2>Latest News{seasonWeekNumber ? ` - Week ${seasonWeekNumber}` : ''}</h2>
        <p style={{ color: "#ffc107", textAlign: "center" }}>Loading articles...</p>
      </div>
    );
  }

  return (
    <div className="news-card">
      <h2>Latest News{seasonWeekNumber ? ` - Week ${seasonWeekNumber}` : ''}</h2>
      {articles.length === 0 ? (
        <p>No articles available at this time.</p>
      ) : (
        <div style={{ 
          display: "grid", 
          gridTemplateColumns: "1fr 1fr", 
          gap: "16px",
          listStyle: "none", 
          padding: 0 
        }}>
          {articles.map((article) => (
            <div key={article.contestId} style={{ marginBottom: "12px" }}>
              <button
                onClick={() => handleArticleClick(article.articleId)}
                style={{ 
                  color: "#ffc107", 
                  textDecoration: "none", 
                  cursor: "pointer",
                  background: "none",
                  border: "none",
                  padding: 0,
                  font: "inherit",
                  textAlign: "left",
                  display: "flex",
                  alignItems: "center",
                  gap: "8px"
                }}
              >
                {article.imageUrls && article.imageUrls.length > 0 ? (
                  <div style={{ display: "flex", gap: "4px" }}>
                    {article.imageUrls.map((url, index) => (
                      <img 
                        key={index}
                        src={url} 
                        alt=""
                        style={{ 
                          width: "20px", 
                          height: "20px", 
                          objectFit: "contain" 
                        }}
                        onError={(e) => {
                          e.target.style.display = 'none';
                        }}
                      />
                    ))}
                  </div>
                ) : (
                  <strong>🏈</strong>
                )}
                <span>{article.title}</span>
              </button>
            </div>
          ))}
        </div>
      )}

      {/* Article Dialog */}
      {selectedArticle && ReactDOM.createPortal(
        <div
          className="article-dialog-backdrop"
          style={{
            position: "fixed",
            top: "0",
            left: "0",
            right: "0",
            bottom: "0",
            backgroundColor: "rgba(0, 0, 0, 0.7)",
            zIndex: "9999",
          }}
          onClick={handleCloseDialog}
        >
          <div
            className="article-dialog-content"
            style={{
              backgroundColor: "#1e1e1e",
              color: "#fff",
              padding: "24px",
              borderRadius: "8px",
              width: "50vw",
              height: "75vh",
              overflow: "auto",
              position: "absolute",
              top: "12.5vh",
              left: "25vw",
              margin: "0",
              transform: "none",
              transition: "none",
            }}
            onClick={(e) => e.stopPropagation()}
          >
            <button
              onClick={handleCloseDialog}
              style={{
                position: "absolute",
                top: "12px",
                right: "12px",
                background: "none",
                border: "none",
                color: "#ffc107",
                fontSize: "24px",
                cursor: "pointer",
              }}
            >
              ×
            </button>
            <h2 style={{ color: "#ffc107", marginTop: 0 }}>{selectedArticle.title}</h2>
            {selectedArticle.url && (
              <p>
                <a
                  href={selectedArticle.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  style={{ color: "#ffc107" }}
                >
                  View Original Article
                </a>
              </p>
            )}
            <div style={{ whiteSpace: "pre-wrap", lineHeight: "1.6" }}>
              {selectedArticle.content}
            </div>
          </div>
        </div>,
        document.body
      )}

      {/* Loading Dialog */}
      {loadingArticle && (
        <div
          style={{
            position: "fixed",
            top: 0,
            left: 0,
            right: 0,
            bottom: 0,
            backgroundColor: "rgba(0, 0, 0, 0.7)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            zIndex: 1000,
          }}
        >
          <div style={{ color: "#ffc107", fontSize: "18px" }}>
            Loading article...
          </div>
        </div>
      )}
    </div>
  );
}

export default NewsWidget;
