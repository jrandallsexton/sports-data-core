import "./LegalPages.css";
import { Link } from "react-router-dom";

// Public account-deletion page. This URL is submitted in the Google Play
// Console's Data Safety section, which requires a web resource where users —
// including users who have uninstalled the app — can request account deletion
// without reinstalling. Reviewers spot-check the link, so deletion must be the
// page's unmistakable purpose.
//
// The described behavior mirrors DeleteAccountCommandHandler exactly (login
// hard-deleted, PII anonymized in place, game history retained without
// identity, devices/preferences purged via UserDeleted). If that handler's
// semantics change, update this page and the Privacy Policy's "Data Retention
// and Deletion" section together.
function AccountDeletionPage() {
  return (
    <div className="legal-page legal-page--policy">
      <h2>Delete Your sportDeets Account</h2>
      <p className="legal-page__meta">
        Applies to accounts created in the sportDeets iOS app, Android app,
        or at sportdeets.com.
      </p>

      <section>
        <h3>How to Delete Your Account</h3>
        <p>You can delete your account yourself from either product:</p>
        <ul>
          <li>
            <strong>On the web:</strong> sign in and open{" "}
            <Link to="/app/settings">Settings</Link>, then choose{" "}
            <strong>Delete Account</strong>. If you&apos;re not signed in,
            you&apos;ll be asked to sign in first.
          </li>
          <li>
            <strong>In the mobile app:</strong> open the{" "}
            <strong>Profile</strong> tab and choose{" "}
            <strong>Delete Account</strong>.
          </li>
        </ul>
        <p>
          Deletion is permanent and cannot be undone. You&apos;ll be asked to
          confirm before anything is deleted.
        </p>
      </section>

      <section>
        <h3>What Is Deleted</h3>
        <ul>
          <li>
            <strong>Your login</strong> is removed immediately. You will no
            longer be able to sign in, and the account cannot be recovered.
          </li>
          <li>
            <strong>Your personal information</strong> — email address,
            username, and display name — is permanently removed from your
            account record.
          </li>
          <li>
            <strong>Your devices and notification settings</strong> — push
            notification tokens, notification preferences, and any scheduled
            reminders — are removed promptly after deletion: typically within
            minutes, and no later than 24 hours.
          </li>
        </ul>
      </section>

      <section>
        <h3>What Is Retained</h3>
        <p>
          Picks and results you submitted in leagues you played in are
          retained so that other members&apos; leagues, standings, and
          history remain intact — but they are no longer connected to you.
          After deletion they appear under the anonymous label
          &ldquo;Deleted user&rdquo; with no email, name, or other
          identifying information attached.
        </p>
        <p>
          We may also retain a limited record of the deletion request
          (without personal data) for fraud prevention and legal compliance,
          and server logs are retained for up to 90 days as described in our{" "}
          <Link to="/privacy">Privacy Policy</Link>.
        </p>
      </section>

      <section>
        <h3>If You Can&apos;t Access the App or Website</h3>
        <p>
          You can request deletion without reinstalling the app: email{" "}
          <a href="mailto:privacy@sportdeets.com">privacy@sportdeets.com</a>{" "}
          from the email address associated with your account and we will
          process the deletion for you. Requests are typically handled
          much sooner, and we complete verified requests within 30 days
          at the latest.
        </p>
      </section>

      <div className="back-home-link">
        <Link to="/">← Back to Home</Link>
      </div>
    </div>
  );
}

export default AccountDeletionPage;
