// src/components/system/ErrorPage.jsx
import "./ErrorPage.css";

export default function ErrorPage({ message = "Something went wrong." }) {
  return (
    <div className="error-page app-error">
      <h1>🏈 Fumble!</h1>
      <p>{message}</p>
      <p className="error-sub">We dropped the ball. Try again in a bit.</p>
    </div>
  );
}
