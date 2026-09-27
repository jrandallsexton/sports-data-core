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
 *
 * Sourcing = the single-team twin of the bulk franchise-season sourcing:
 * re-requests the team's TeamSeason document from ESPN with the full child
 * cascade (schedule, record, statistics, roster), for a season that was
 * incompletely sourced.
 */
export default function TeamAdmin({ slug, seasonYear, sport, league }) {
  const [sourceState, setSourceState] = useState({ status: "idle", result: null, error: null });
  const [state, setState] = useState({ status: "idle", result: null, error: null });

  const source = async () => {
    setSourceState({ status: "pending", result: null, error: null });
    try {
      const response = await apiWrapper.FranchiseAdmin.sourceFranchiseSeason(sport, league, slug, seasonYear);
      setSourceState({ status: "done", result: response.data, error: null });
    } catch (err) {
      // Same error shapes as enrich below: ValidationFailure objects from
      // ToActionResult, or ASP.NET model-binding { field: [string] }.
      const status = err?.response?.status;
      const errors = err?.response?.data?.errors;
      const messages = errors
        ? (Array.isArray(errors) ? errors : Object.values(errors).flat())
            .map((e) => (typeof e === "string" ? e : e?.errorMessage ?? JSON.stringify(e)))
        : [];
      const detail = messages.length
        ? messages.join("; ")
        : err?.response?.data?.title || err?.message || "Request failed";
      setSourceState({
        status: "error",
        result: null,
        error: status ? `${status}: ${detail}` : detail,
      });
    }
  };

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
        <h3 className="team-admin-title">Source franchise season</h3>
        <p className="team-admin-help">
          Re-requests this team&apos;s {seasonYear} season from ESPN with everything under it:
          schedule (games), record, statistics, roster. Use it when the season looks incompletely
          sourced, e.g. games missing from the schedule. Documents arrive asynchronously; give it a
          few minutes, reload the page, then run Enrich below to recompute the record from the new games.
          Each press re-fetches the whole season from ESPN, so check Seq before pressing again.
        </p>
        <button
          type="button"
          className="team-admin-button"
          onClick={source}
          disabled={sourceState.status === "pending"}
        >
          {sourceState.status === "pending" ? "Requesting…" : `Source ${seasonYear} season`}
        </button>

        {sourceState.status === "done" && sourceState.result && (
          <div className="team-admin-result" role="status">
            <div>Accepted. Sourcing is running.</div>
            <dl className="team-admin-kv">
              <dt>Correlation id</dt>
              <dd><code>{sourceState.result.correlationId}</code></dd>
              <dt>Franchise season</dt>
              <dd><code>{sourceState.result.franchiseSeasonId}</code></dd>
            </dl>
          </div>
        )}

        {sourceState.status === "error" && (
          <div className="team-admin-error" role="alert">
            Sourcing request failed: {sourceState.error}
          </div>
        )}
      </section>

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
