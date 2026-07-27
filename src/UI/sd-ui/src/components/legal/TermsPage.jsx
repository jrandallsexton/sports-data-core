import "./LegalPages.css";
import { Link } from "react-router-dom";

// Real Terms of Service (replaced the pre-launch placeholder 2026-07-27).
// Drafted to match actual product behavior — free-to-play, no wagering,
// anonymized-retention account deletion (see DeleteAccountCommandHandler),
// commissioner-run leagues with user-generated names/descriptions. Keep the
// no-gambling section aligned with reality: it is load-bearing for app-store
// review (Play's real-money-gambling policy) as well as legally.
function TermsPage() {
  return (
    <div className="legal-page legal-page--policy">
      <h2>Terms of Service</h2>
      <p className="legal-page__meta">Effective date: July 27, 2026</p>

      <section>
        <h3>1. Agreement</h3>
        <p>
          These Terms of Service (&ldquo;Terms&rdquo;) are an agreement
          between you and sportDeets (&ldquo;sportDeets,&rdquo;
          &ldquo;we,&rdquo; &ldquo;us,&rdquo; or &ldquo;our&rdquo;),
          the operator of the sportDeets sports pick&apos;em platform,
          delivered through our mobile applications and our website at
          sportdeets.com (together, the &ldquo;Service&rdquo;). By
          creating an account or using the Service, you agree to these
          Terms and to our{" "}
          <Link to="/privacy">Privacy Policy</Link>. If you do not
          agree, do not use the Service.
        </p>
      </section>

      <section>
        <h3>2. Eligibility</h3>
        <p>
          You must be at least 13 years old to use the Service. If you
          are under the age of majority where you live, you may use the
          Service only with the consent of a parent or legal guardian.
          By using the Service, you represent that you meet these
          requirements.
        </p>
      </section>

      <section>
        <h3>3. Your Account</h3>
        <ul>
          <li>
            You are responsible for your account and for everything
            that happens under it. Keep your sign-in credentials
            secure.
          </li>
          <li>
            Provide accurate information when you create your account,
            and keep it current.
          </li>
          <li>
            Accounts are personal: one account per person, and your
            account may not be sold, transferred, or shared.
          </li>
          <li>
            You may delete your account at any time — see{" "}
            <Link to="/account-deletion">Delete Your Account</Link> for
            exactly what is removed and what is retained.
          </li>
        </ul>
      </section>

      <section>
        <h3>4. Free to Play — No Gambling</h3>
        <p>
          sportDeets is a free-to-play game of skill and entertainment.
          For clarity:
        </p>
        <ul>
          <li>
            The Service involves <strong>no wagering</strong>: no entry
            fees, no buy-ins, and no deposits. We do not accept,
            process, or hold money or anything of monetary value from
            users in connection with picks or leagues.
          </li>
          <li>
            The Service awards <strong>no monetary prizes</strong>.
            Standings, streaks, and bragging rights have no cash value
            and cannot be redeemed for anything.
          </li>
          <li>
            Point spreads, totals, odds, and similar figures displayed
            in the Service are shown for{" "}
            <strong>entertainment and informational purposes only</strong>.
            They are not betting advice, an invitation to gamble, or an
            offer to accept wagers. sportDeets is not a sportsbook,
            casino, or gambling operator of any kind.
          </li>
          <li>
            Any arrangement between league members outside the Service
            (for example, a private side arrangement among friends) is
            solely between those members. We do not facilitate,
            process, endorse, or enforce any such arrangement, and you
            are responsible for complying with the laws that apply to
            you.
          </li>
        </ul>
      </section>

      <section>
        <h3>5. Leagues and Your Content</h3>
        <p>
          Leagues are created and managed by users
          (&ldquo;commissioners&rdquo;). League names, descriptions,
          display names, and anything else you submit to the Service
          are &ldquo;Your Content.&rdquo;
        </p>
        <ul>
          <li>
            You own Your Content. You grant us a non-exclusive,
            worldwide, royalty-free license to host, store, display,
            and distribute it within the Service so the product can
            function (for example, showing your league name and picks
            to your league&apos;s members, or showing a public
            league&apos;s name to users browsing public leagues).
          </li>
          <li>
            Your Content must not be unlawful, harassing, hateful,
            defamatory, sexually explicit, or infringing on
            anyone&apos;s rights. Keep league names and trash talk in
            the spirit of the game.
          </li>
          <li>
            We may remove content or suspend accounts that violate
            these rules, at our discretion. To report objectionable
            content, email{" "}
            <a href="mailto:support@sportdeets.com">
              support@sportdeets.com
            </a>
            .
          </li>
        </ul>
      </section>

      <section>
        <h3>6. Acceptable Use</h3>
        <p>You agree not to:</p>
        <ul>
          <li>
            Access the Service by any automated means (bots, scrapers,
            bulk downloads) or attempt to extract our data at scale.
          </li>
          <li>
            Interfere with or disrupt the Service, probe or circumvent
            its security, or access accounts or data that are not
            yours.
          </li>
          <li>
            Impersonate any person, misrepresent your affiliation, or
            create accounts by automated means.
          </li>
          <li>
            Use the Service in violation of any applicable law or
            regulation.
          </li>
        </ul>
      </section>

      <section>
        <h3>7. Sports Data, Scores, and Results</h3>
        <p>
          Game schedules, scores, statistics, spreads, and related data
          are sourced from third-party providers and are provided
          &ldquo;as is.&rdquo; They may contain errors, omissions, or
          delays. We may correct game results, pick scoring, and
          standings after their initial posting when source data
          changes or errors are found; our good-faith determination of
          pick scoring and league standings is final.
        </p>
      </section>

      <section>
        <h3>8. Intellectual Property</h3>
        <p>
          The Service — including the sportDeets name, logo, design,
          and software — is our property and is protected by
          intellectual-property laws. We grant you a limited,
          non-exclusive, non-transferable license to use the Service
          for personal, non-commercial purposes.
        </p>
        <p>
          Team names, league names, and other sports identifiers
          displayed in the Service are the property of their respective
          owners and are used for identification purposes only. Their
          appearance does not imply any affiliation with, or
          endorsement by, those owners.
        </p>
      </section>

      <section>
        <h3>9. Suspension and Termination</h3>
        <p>
          We may suspend or terminate your access to the Service if you
          violate these Terms, create risk or legal exposure for us or
          other users, or if we discontinue the Service. Where
          practical, we will notify you. You may stop using the Service
          — and delete your account — at any time. Sections of these
          Terms that by their nature should survive termination
          (including Sections 5, 8, 10, 11, and 12) survive.
        </p>
      </section>

      <section>
        <h3>10. Disclaimers</h3>
        <p>
          The Service is provided &ldquo;as is&rdquo; and &ldquo;as
          available,&rdquo; without warranties of any kind, express or
          implied, including merchantability, fitness for a particular
          purpose, and non-infringement. We do not warrant that the
          Service will be uninterrupted, error-free, or that data
          (including scores and results) will be accurate or timely.
          Features may change, break, or be discontinued.
        </p>
      </section>

      <section>
        <h3>11. Limitation of Liability</h3>
        <p>
          To the maximum extent permitted by law, sportDeets will not
          be liable for any indirect, incidental, special,
          consequential, or punitive damages, or for lost profits,
          data, or goodwill, arising out of or related to your use of
          the Service. To the maximum extent permitted by law, our
          total aggregate liability for all claims relating to the
          Service will not exceed one hundred U.S. dollars (US $100).
          Some jurisdictions do not allow certain limitations, so parts
          of this section may not apply to you.
        </p>
      </section>

      <section>
        <h3>12. Indemnification</h3>
        <p>
          You agree to indemnify and hold sportDeets harmless from
          claims, damages, and expenses (including reasonable
          attorneys&apos; fees) arising from Your Content, your use of
          the Service, or your violation of these Terms or of
          applicable law.
        </p>
      </section>

      <section>
        <h3>13. Changes to the Service or These Terms</h3>
        <p>
          We may modify the Service or these Terms from time to time.
          When we change these Terms, we will update the effective date
          above and, for material changes, notify you through the app
          or by email. Your continued use of the Service after the
          effective date constitutes acceptance of the updated Terms.
        </p>
      </section>

      <section>
        <h3>14. Governing Law and Disputes</h3>
        <p>
          These Terms are governed by the laws of the State of Florida,
          without regard to its conflict-of-laws rules. Before filing
          any claim, you agree to first contact us at{" "}
          <a href="mailto:support@sportdeets.com">
            support@sportdeets.com
          </a>{" "}
          and attempt to resolve the dispute informally for at least 30
          days. Any dispute that cannot be resolved informally will be
          brought exclusively in the state or federal courts located in
          Florida, and you consent to their jurisdiction.
        </p>
      </section>

      <section>
        <h3>15. Contact</h3>
        <p>
          Questions about these Terms:{" "}
          <a href="mailto:support@sportdeets.com">
            support@sportdeets.com
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

export default TermsPage;
