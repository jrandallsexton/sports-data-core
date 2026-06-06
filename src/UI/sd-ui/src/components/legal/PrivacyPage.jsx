import "./LegalPages.css";
import { Link } from "react-router-dom";

function PrivacyPage() {
  return (
    <div className="legal-page legal-page--policy">
      <h2>Privacy Policy</h2>
      <p className="legal-page__meta">
        Effective date: June 4, 2026
      </p>

      <section>
        <h3>Overview</h3>
        <p>
          sportDeets (&ldquo;sportDeets,&rdquo; &ldquo;we,&rdquo;
          &ldquo;us,&rdquo; or &ldquo;our&rdquo;) provides a sports
          pick&apos;em platform delivered through mobile applications and
          our website at sportdeets.com. This Privacy Policy describes
          what information we collect, how we use it, who we share it
          with, and the choices you have. It applies to your use of
          sportDeets through the iOS app, Android app, and web product.
        </p>
        <p>
          We do not sell your personal information.
        </p>
      </section>

      <section>
        <h3>Information We Collect</h3>
        <p>We collect the following categories of information:</p>

        <h4>Account information</h4>
        <p>
          When you create an account, we collect your email address and
          a display name. Depending on how you sign in, we also receive
          a unique user identifier from your authentication provider:
        </p>
        <ul>
          <li>
            <strong>Email and password:</strong> handled by Google
            Firebase Authentication. We do not see or store your
            password.
          </li>
          <li>
            <strong>Sign in with Apple:</strong> we receive your name
            (if you choose to share it) and a private relay email or
            your real email (your choice), via Apple&apos;s OAuth flow.
          </li>
          <li>
            <strong>Sign in with Google:</strong> we receive your name,
            email address, and profile picture URL via Google&apos;s
            OAuth flow.
          </li>
        </ul>

        <h4>Product usage data</h4>
        <p>
          To run the pick&apos;em features, we store information you
          create and actions you take in the app, including:
        </p>
        <ul>
          <li>Leagues you create or join, and your role in each.</li>
          <li>Picks you submit for each game or contest.</li>
          <li>Comments, messages, or other content you post in leagues.</li>
          <li>Preferences such as theme, text size, and favorite teams.</li>
        </ul>

        <h4>Device and diagnostic data</h4>
        <p>
          We collect a limited set of device and diagnostic data to keep
          the app working and to investigate crashes:
        </p>
        <ul>
          <li>
            <strong>Push notification tokens:</strong> when you enable
            notifications, your device generates a token we store so we
            can send you reminders and league updates. The token is
            issued by Apple Push Notification service or Firebase Cloud
            Messaging.
          </li>
          <li>
            <strong>Crash reports and error logs:</strong> if the app
            crashes or encounters an error, we collect a stack trace,
            device model, operating system version, and a sequence of
            recent in-app actions (&ldquo;breadcrumbs&rdquo;). Crash
            reports may include your user identifier and email so we
            can correlate the report with your account, but we
            deliberately exclude notification content, pick contents,
            and other product data from log payloads.
          </li>
          <li>
            <strong>Server logs:</strong> our backend records standard
            request metadata, including IP address, user agent string,
            timestamp, and the API endpoint accessed. These logs are
            used for security, debugging, and abuse prevention.
          </li>
        </ul>

        <h4>Information we do not collect</h4>
        <ul>
          <li>
            We do not collect precise geolocation, contacts,
            microphone, camera, calendar, or health data.
          </li>
          <li>
            We do not use third-party advertising SDKs, and we do not
            collect advertising identifiers (IDFA, GAID).
          </li>
          <li>We do not currently process payment information.</li>
        </ul>
      </section>

      <section>
        <h3>How We Use Information</h3>
        <p>We use the information we collect to:</p>
        <ul>
          <li>Create and authenticate your account.</li>
          <li>
            Provide the core product: leagues, picks, contests,
            leaderboards, and reminders.
          </li>
          <li>
            Send push notifications you have opted in to (game
            reminders, league activity, contest results).
          </li>
          <li>
            Diagnose crashes, fix bugs, and improve product
            reliability.
          </li>
          <li>
            Detect, investigate, and prevent fraud, abuse, and
            violations of our Terms of Service.
          </li>
          <li>
            Comply with legal obligations and respond to lawful
            requests.
          </li>
        </ul>
      </section>

      <section>
        <h3>Third-Party Services</h3>
        <p>
          sportDeets relies on a small number of third-party services
          to deliver the product. Each receives only the data needed
          for its function:
        </p>
        <ul>
          <li>
            <strong>Google (Firebase Authentication, Firebase Cloud
            Messaging, Google Sign-In):</strong> account
            authentication and push notification delivery. Governed
            by{" "}
            <a
              href="https://policies.google.com/privacy"
              target="_blank"
              rel="noopener noreferrer"
            >
              Google&apos;s Privacy Policy
            </a>
            .
          </li>
          <li>
            <strong>Apple (Sign in with Apple, Apple Push Notification
            service):</strong> account authentication and push
            notification delivery on iOS. Governed by{" "}
            <a
              href="https://www.apple.com/legal/privacy/"
              target="_blank"
              rel="noopener noreferrer"
            >
              Apple&apos;s Privacy Policy
            </a>
            .
          </li>
          <li>
            <strong>Functional Software, Inc. (Sentry):</strong> crash
            reporting and error monitoring. Receives crash reports
            including the diagnostic data described above. Governed
            by{" "}
            <a
              href="https://sentry.io/privacy/"
              target="_blank"
              rel="noopener noreferrer"
            >
              Sentry&apos;s Privacy Policy
            </a>
            .
          </li>
        </ul>
        <p>
          We do not sell personal information to data brokers,
          advertisers, or any other third party.
        </p>
      </section>

      <section>
        <h3>Data Retention and Deletion</h3>
        <p>
          We retain account information and product usage data for as
          long as your account is active. Server logs and crash reports
          are retained for up to 90 days, then deleted or anonymized.
        </p>
        <p>
          You can delete your sportDeets account at any time from the
          Profile screen in the mobile app. Account deletion removes
          your account record, your picks, your league memberships,
          and your push notification tokens within 30 days. We may
          retain a limited record of the deletion request (without
          personal data) for fraud prevention and legal compliance.
        </p>
        <p>
          To request deletion if you cannot access the app, email{" "}
          <a href="mailto:privacy@sportdeets.com">
            privacy@sportdeets.com
          </a>{" "}
          from the email address associated with your account.
        </p>
      </section>

      <section>
        <h3>Children&apos;s Privacy</h3>
        <p>
          sportDeets is not directed to children under 13, and we do
          not knowingly collect personal information from anyone under
          13. If you believe a child under 13 has provided us with
          personal information, please contact us at{" "}
          <a href="mailto:privacy@sportdeets.com">
            privacy@sportdeets.com
          </a>{" "}
          and we will take steps to delete it.
        </p>
      </section>

      <section>
        <h3>Security</h3>
        <p>
          We use industry-standard measures to protect your information,
          including HTTPS for all network traffic, encrypted storage of
          credentials by Firebase Authentication, and access controls
          on our backend systems. No method of transmission or storage
          is 100% secure, and we cannot guarantee absolute security.
        </p>
      </section>

      <section>
        <h3>Your Rights</h3>
        <p>
          Depending on where you live, you may have specific rights
          regarding your personal information:
        </p>
        <ul>
          <li>
            <strong>Access and portability:</strong> request a copy of
            the personal data we hold about you.
          </li>
          <li>
            <strong>Correction:</strong> request that we correct
            inaccurate information.
          </li>
          <li>
            <strong>Deletion:</strong> request that we delete your
            personal information (see &ldquo;Data Retention and
            Deletion&rdquo; above).
          </li>
          <li>
            <strong>Objection and restriction:</strong> object to or
            restrict certain processing of your personal information.
          </li>
        </ul>
        <p>
          Residents of the European Economic Area, the United Kingdom,
          and Switzerland have rights under the General Data Protection
          Regulation (GDPR). Residents of California have rights under
          the California Consumer Privacy Act (CCPA), including the
          right to know what personal information we collect and the
          right to request deletion. sportDeets does not sell personal
          information as defined by the CCPA.
        </p>
        <p>
          To exercise any of these rights, email{" "}
          <a href="mailto:privacy@sportdeets.com">
            privacy@sportdeets.com
          </a>
          . We will respond within the timeframe required by
          applicable law.
        </p>
      </section>

      <section>
        <h3>International Users</h3>
        <p>
          sportDeets is operated from the United States. If you access
          the service from outside the United States, you understand
          that your information will be transferred to, stored, and
          processed in the United States, where data protection laws
          may differ from those in your country.
        </p>
      </section>

      <section>
        <h3>Changes to This Policy</h3>
        <p>
          We may update this Privacy Policy from time to time. When we
          do, we will update the &ldquo;Effective date&rdquo; at the
          top of this page and, for material changes, notify you
          through the app or by email. Your continued use of
          sportDeets after the effective date constitutes acceptance
          of the updated policy.
        </p>
      </section>

      <section>
        <h3>Contact</h3>
        <p>
          If you have questions about this Privacy Policy or our
          handling of your personal information, please contact us at{" "}
          <a href="mailto:privacy@sportdeets.com">
            privacy@sportdeets.com
          </a>
          .
        </p>
      </section>

      <div className="back-home-link">
        <Link to="/">← Back to Home</Link>
      </div>
    </div>
  );
}

export default PrivacyPage;
