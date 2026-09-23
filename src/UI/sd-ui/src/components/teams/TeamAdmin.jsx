import { useState } from "react";
import apiWrapper from "../../api/apiWrapper";
import "./TeamAdmin.css";

/**
 * Admin-only tab on the team card. Operator actions for ONE franchise season,
 * exposed on the team page so a broken team is repaired where it is noticed.
 *
 * Enrichment = the single-team twin of the weekly FranchiseSeasonEnrichmentJob:
 * record enrichment (W/L from finalized games), a season-statistics refresh
 * from ESPN, and metrics (football only). The API returns a correlation id;
 * every leg logs under it in Seq.
 */
export default function TeamAdmin({ slug, seasonYear, sport, league }) {
  const [state, setState] = useState({ status: "idle", result: null, error: null });

  const enrich = async () => {
    setState({ status: "pending", result: null, error: null });
    try {
      const response = await apiWrapper.FranchiseAdmin.enrichFranchiseSeason(sport, league, slug, seasonYear);
      setState({ status: "done", result: response.data, error: null });
    } catch (err) {
      const status = err?.response?.status;
      // ToActionResult returns { errors: [ValidationFailure, ...] } (objects
      // with errorMessage); ASP.NET model binding returns { errors: { field:
      // [string, ...] } }. Handle both, never render "[object Object]".
      const errors = err?.response?.data?.errors;
      const messages = errors
        ? (Array.isArray(errors) ? errors : Object.values(errors).flat())
            .map((e) => (typeof e === "string" ? e : e?.errorMessage ?? JSON.stringify(e)))
        : [];
      const detail = messages.length
        ? messages.join("; ")
        : err?.response?.data?.title || err?.message || "Request failed";
      setState({
        status: "error",
        result: null,
        error: status ? `${status}: ${detail}` : detail,
      });
    }
  };

  return (
    <div className="team-admin">
      <section className="team-admin-section">
        <h3 className="team-admin-title">Enrich franchise season</h3>
        <p className="team-admin-help">
          Re-derives this team&apos;s {seasonYear} record from finalized games, requests a
          fresh season-statistics pull from ESPN, and regenerates metrics (football only).
          Runs in the background on the Producer; the correlation id is the Seq handle.
        </p>
        <button
          type="button"
          className="team-admin-button"
          onClick={enrich}
          disabled={state.status === "pending"}
        >
          {state.status === "pending" ? "Requesting…" : `Enrich ${seasonYear} season`}
        </button>

        {state.status === "done" && state.result && (
          <div className="team-admin-result" role="status">
            <div>Accepted. Enrichment is running.</div>
            <dl className="team-admin-kv">
              <dt>Correlation id</dt>
              <dd><code>{state.result.correlationId}</code></dd>
              <dt>Franchise season</dt>
              <dd><code>{state.result.franchiseSeasonId}</code></dd>
            </dl>
          </div>
        )}

        {state.status === "error" && (
          <div className="team-admin-error" role="alert">
            Enrichment request failed: {state.error}
          </div>
        )}
      </section>
    </div>
  );
}
