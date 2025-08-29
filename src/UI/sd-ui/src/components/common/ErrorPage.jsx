// src/components/system/ErrorPage.jsx
import "./ErrorPage.css";

export default function ErrorPage({ message = "Something went wrong." }) {
  return (
    <div className="error-page app-error">
      <h1>🏈 Fumble!</h1>
      <p>{message}</p>
      <p className="error-sub">
        <em>(The devs are probably watching the game instead.)</em>
      </p>
    </div>
  );
}
