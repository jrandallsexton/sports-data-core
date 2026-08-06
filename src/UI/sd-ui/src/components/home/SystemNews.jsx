import { useState } from "react";
import "./SystemNews.css";

function SystemNews() {
  const [visible, setVisible] = useState(() => {
    return localStorage.getItem("systemNewsDismissed") !== "true";
  });

  const handleDismiss = () => {
    localStorage.setItem("systemNewsDismissed", "true");
    setVisible(false);
  };

  if (!visible) return null;

  return (
    <div className="card system-news-card">
      <button className="dismiss-button" onClick={handleDismiss}>
        ×
      </button>

      <h2>The Huddle</h2>
      <p>
        Welcome to <strong>sportDeets</strong>. We trash talk here. It's fun.
        Get over it.
      </p>

      <h2>✅ What Works</h2>
      <ul>
        <li>Create and join leagues</li>
        <li>Submit your picks (Straight Up or ATS)</li>
        <li>
          AI-powered matchup previews{" "}
          <em>(hint: look between the pick buttons)</em>
        </li>
        <li>The Locker Room: group-based smack talk threads</li>
        <li>
          Picks can be submitted individually instead of all at once{" "}
          <em>(ahem, you know who you are, someWebsite!)</em>
        </li>
        <li>App is mostly mobile-friendly. If you find something broken, please report it!</li>
      </ul>

      <h2>⚠️ What’s Rough</h2>
      <ul>
        <li>
          The ability to assign confidence points won't be around until Week 2.
          Maybe.
        </li>
        <li>
          The first week’s scoring will be run manually — don’t panic when your
          picks aren’t scored the second the clock hits zero.
        </li>
        <li>No email or SMS reminders (yet)</li>
        <li>
          AI previews are still evolving. They’ll get better as I source more
          historical data — and as this season unfolds.
        </li>
        <li>
          AI picks are included in every group unless people complain about it.
        </li>
        <li>You might break something — that’s just part of the deal.</li>
      </ul>

      <h2>🚧 What’s Coming</h2>
      <ul>
        <li>Importing picks from other groups</li>
        <li>Auto-importing picks from other groups (in case you forget)</li>
        <li>Weekly recaps and reminders</li>
        <li>In-App notifications for games in your league(s)</li>
        <li>Badges.  A trophy like you never knew you wanted. <em>(you can preview them under Settings)</em></li>
        <li>Game Center view for live updates</li>
        <li>No mobile app yet (2026 maybe?)</li>
        <li>Additional sports (NFL, MLB, NBA, etc.) once this foundation is solid</li>
        <li>More features once people start complaining</li>
      </ul>

      <h2>💬 FAQ</h2>
      <p>
        <strong>Q:</strong> Is this production-ready?
        <br />
        <strong>A:</strong> Hell no. But it works. Mostly.
      </p>

      <p>
        <strong>Q:</strong> What if I lose?
        <br />
        <strong>A:</strong> Don’t blame the AI. That’s a skill issue.
      </p>

      <p>
        <strong>Q:</strong> Where do I send bugs?
        <br />
        <strong>A:</strong> Email{" "}
        <a href="mailto:help@sportdeets.com">help@sportdeets.com</a> — or just
        text me like a normal person.
      </p>

      <p style={{ marginTop: "1rem" }}>
        <strong>P.S.</strong> This is still in dev — expect weird stuff. I’ll
        fix it. Maybe.
      </p>

      <p style={{ marginTop: "1rem" }}>
        <strong>P.S.S</strong> ( you can dismiss this and never see it again -
        look for the "x" button )
      </p>

      <p>-- randy 27 Aug 2025</p>
    </div>
  );
}

export default SystemNews;
