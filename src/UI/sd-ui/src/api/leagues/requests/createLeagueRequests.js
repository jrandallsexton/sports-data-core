// Sport-specific create-league request builders.
// Each maps FE form state to the shape expected by the corresponding BE endpoint.

const toStartOfDayIso = (dateStr) =>
  dateStr ? `${dateStr}T00:00:00Z` : null;

const toEndOfDayIso = (dateStr) =>
  dateStr ? `${dateStr}T23:59:59Z` : null;

const buildWindow = ({ durationMode, startsOn, endsOn }) => {
  if (durationMode === "dates") {
    return {
      startsOn: toStartOfDayIso(startsOn),
      endsOn: toEndOfDayIso(endsOn),
    };
  }
  // Full season or Week Range (week→date translation is a BE follow-up).
  return { startsOn: null, endsOn: null };
};

// Map the form's tiebreaker UI value to the BE enum name.
// BE enum TiebreakerType: TotalPoints | HomeAndAwayScores | EarliestSubmission.
const tiebreakerTypeFromUi = (value) => {
  switch (value) {
    case "earliest":
      return "EarliestSubmission";
    case "closest":
      return "TotalPoints";
    default:
      return "TotalPoints";
  }
};

const buildShared = ({
  leagueName,
  description,
  pickType,
  tiebreaker,
  useConfidencePoints,
  isPublic,
  dropLowWeeksCount,
  durationMode,
  startsOn,
  endsOn,
}) => ({
  name: leagueName,
  description: description?.trim() || null,
  pickType,
  tiebreakerType: tiebreakerTypeFromUi(tiebreaker),
  // TiebreakerTiePolicy currently has one valid value. Revisit when more policies land.
  tiebreakerTiePolicy: "EarliestSubmission",
  useConfidencePoints,
  isPublic,
  dropLowWeeksCount: dropLowWeeksCount || 0,
  ...buildWindow({ durationMode, startsOn, endsOn }),
});

export const buildCreateFootballNcaaLeagueRequest = (form) => ({
  ...buildShared(form),
  rankingFilter: form.rankingFilter || null,
  conferenceSlugs: form.teamFilter,
});

export const buildCreateFootballNflLeagueRequest = (form) => ({
  ...buildShared(form),
  divisionSlugs: form.teamFilter,
});

export const buildCreateBaseballMlbLeagueRequest = (form) => ({
  ...buildShared(form),
  divisionSlugs: form.teamFilter,
});
