// Sport-specific create-league request builders.
// Each maps FE form state to the shape expected by the corresponding BE endpoint.

// Convert a `<input type="date">` value (YYYY-MM-DD, no timezone) into an ISO
// instant that represents midnight/end-of-day in the caller's local timezone.
// Appending "Z" would wrongly treat the local calendar date as UTC, skewing
// the window by up to 24 hours for non-UTC users.
const toStartOfDayIso = (dateStr) => {
  if (!dateStr) return null;
  const [year, month, day] = dateStr.split("-").map(Number);
  return new Date(year, month - 1, day, 0, 0, 0).toISOString();
};

const toEndOfDayIso = (dateStr) => {
  if (!dateStr) return null;
  const [year, month, day] = dateStr.split("-").map(Number);
  return new Date(year, month - 1, day, 23, 59, 59).toISOString();
};

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

// Coerce to a non-negative integer; any string, decimal, negative, or non-numeric
// input collapses to 0. Protects the BE from malformed payloads if a caller other
// than our own form ever hits these builders.
const toNonNegativeInt = (value) => {
  const n = Number(value);
  return Number.isFinite(n) && n > 0 ? Math.floor(n) : 0;
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
  dropLowWeeksCount: toNonNegativeInt(dropLowWeeksCount),
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
