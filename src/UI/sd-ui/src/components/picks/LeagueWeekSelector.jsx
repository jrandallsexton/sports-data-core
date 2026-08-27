import "./LeagueWeekSelector.css";
import LeagueSelector from "../shared/LeagueSelector";

// Week numbers repeat across season phases, so a phase-qualified entry
// labels non-regular weeks explicitly; regular season keeps the plain
// "Week N" label users expect.
const PHASE_LABEL = {
  preseason: "Preseason",
  postseason: "Postseason",
  offseason: "Offseason",
};

const weekLabel = (week, phase) =>
  PHASE_LABEL[phase] ? `${PHASE_LABEL[phase]} Week ${week}` : `Week ${week}`;

function LeagueWeekSelector({
  leagues = [],
  selectedLeagueId,
  setSelectedLeagueId,
  selectedWeek,
  selectedPhase, // only meaningful with weekDetails
  setSelectedWeek,
  seasonWeeks = [],
  // Phase-qualified entries [{ week, phase }] — when provided they
  // replace seasonWeeks as the option source and setSelectedWeek is
  // called as (week, phase). Legacy callers (GameMap) keep passing the
  // int list and are unaffected.
  weekDetails = null,
  allowAll = false, // New prop to enable "All" option
}) {
  const phased = Array.isArray(weekDetails) && weekDetails.length > 0;
  const hasWeeks = phased
    ? true
    : Array.isArray(seasonWeeks) && seasonWeeks.length > 0;

  // Custom-window leagues (e.g. a one-week pool) only ever have a single
  // entry — a dropdown there implies a selection to make when there isn't
  // one. Render the value as static text instead so it stays informational.
  // allowAll is the exception (admin view): even with one week, "All
  // Weeks" is a meaningful alternative, so keep the dropdown.
  const optionCount = phased ? weekDetails.length : seasonWeeks.length;
  const isSingleWeek = !allowAll && hasWeeks && optionCount === 1;

  // Composite option value — "phase:week" — because the number alone
  // under-identifies a week when phases collide.
  const detailValue = (d) => `${d.phase}:${d.week}`;
  const selectedDetailValue =
    phased && selectedWeek != null
      ? detailValue({ phase: selectedPhase ?? "regular", week: selectedWeek })
      : "";

  return (
    <div className="league-week-selector">
      {/* League Select */}
      <div className="selector-block">
        <LeagueSelector
          leagues={leagues}
          selectedLeagueId={selectedLeagueId}
          setSelectedLeagueId={setSelectedLeagueId}
          allowAll={allowAll}
        />
      </div>

      {/* Week Select */}
      <div className="selector-block">
        {isSingleWeek ? (
          <>
            {/* No <label htmlFor> here — <label> must point at a labelable
                form control (input/select/textarea/etc.), not at a <span>.
                Use a styled span for the visual "Week:" prefix instead. */}
            <span className="week-label">Week:</span>
            <span className="week-static">
              {phased
                ? weekLabel(weekDetails[0].week, weekDetails[0].phase)
                : seasonWeeks[0]}
            </span>
          </>
        ) : (
          <>
            <label htmlFor="weekSelect">Week:</label>
            <select
              id="weekSelect"
              value={phased ? selectedDetailValue : selectedWeek ?? ""}
              onChange={(e) => {
                if (!e.target.value) {
                  setSelectedWeek(null);
                  return;
                }
                if (phased) {
                  const [phase, week] = e.target.value.split(":");
                  setSelectedWeek(Number(week), phase);
                } else {
                  setSelectedWeek(Number(e.target.value));
                }
              }}
              disabled={!hasWeeks}
            >
              {allowAll && <option value="">All Weeks</option>}
              {phased
                ? weekDetails.map((d) => (
                    <option key={detailValue(d)} value={detailValue(d)}>
                      {weekLabel(d.week, d.phase)}
                    </option>
                  ))
                : hasWeeks &&
                  seasonWeeks.map((week) => (
                    <option key={week} value={week}>
                      Week {week}
                    </option>
                  ))}
            </select>
          </>
        )}
      </div>
    </div>
  );
}

export default LeagueWeekSelector;
