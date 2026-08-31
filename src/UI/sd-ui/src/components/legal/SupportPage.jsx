import "./LegalPages.css";
import { Link } from "react-router-dom";

// Public support page. This URL is submitted as the Support URL in App Store
// Connect and the Google Play Console, and reviewers do visit it — a support
// URL that 404s, or that renders with no way to reach a human, is a rejection.
//
// Before this page existed the Support URL had to point at /privacy, which
// technically satisfied the requirement (it carries a contact address) but made
// support look like an afterthought. Keep a reachable contact address on this
// page; that is the part being checked.
//
// Deliberately reachable without auth: people needing help are frequently the
// people who cannot sign in.
function SupportPage() {
  return (
    <div className="legal-page legal-page--policy">
      <h2>sportDeets Support</h2>
      <p className="legal-page__meta">
        Help for the sportDeets iOS app, Android app, and sportdeets.com.
      </p>

      <section>
        <h3>Contact Us</h3>
        <p>
          Email{" "}
          <a href="mailto:help@sportdeets.com">help@sportdeets.com</a> and
          we&apos;ll get back to you. We read every message.
        </p>
        <p>
          It helps to include the device you&apos;re using, the name of the
          league involved, and what you expected to happen.
        </p>
      </section>

      <section>
        <h3>Common Questions</h3>
        <ul>
          <li>
            <strong>How do I join a league?</strong> Ask the league&apos;s
            commissioner for an invitation, or browse public leagues from the
            Leagues tab. Invitations expire, so if yours no longer works, ask
            for a new one.
          </li>
          <li>
            <strong>Why can&apos;t I change a pick?</strong> Picks lock when a
            game starts. Anything still scheduled can be changed.
          </li>
          <li>
            <strong>My scores look wrong.</strong> Scores update live during
            games and settle shortly after a game goes final. If something still
            looks wrong an hour after the final whistle, email us with the
            matchup and we&apos;ll investigate.
          </li>
          <li>
            <strong>I&apos;d rather not see betting lines.</strong> You can turn
            that content off. Open Settings and disable the spread and odds
            display; every surface in the app respects it.
          </li>
          <li>
            <strong>Is sportDeets free?</strong> Yes. There are no entry fees,
            no wagering, and no real money anywhere in the app.
          </li>
        </ul>
      </section>

      <section>
        <h3>Account and Data</h3>
        <ul>
          <li>
            <Link to="/account-deletion">Delete your account</Link> — how to
            remove your account and what happens to your data.
          </li>
          <li>
            <Link to="/privacy">Privacy Policy</Link> — what we collect and why.
            Privacy-specific questions can go to{" "}
            <a href="mailto:privacy@sportdeets.com">privacy@sportdeets.com</a>.
          </li>
          <li>
            <Link to="/terms">Terms of Service</Link> — the rules of the road.
          </li>
        </ul>
      </section>
    </div>
  );
}

export default SupportPage;
