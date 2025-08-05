// src/api/leagues/CreateLeagueRequest.js

export const buildCreateLeagueRequest = ({
  leagueName,
  description,
  pickType,
  tiebreaker,
  useConfidencePoints,
  rankingFilter,
  teamFilter,
  isPublic,
}) => {
  return {
    name: leagueName,
    description: description?.trim() || null,
    pickType: pickType.toLowerCase(),
    tiebreakerType: "totalPoints", // or dynamic if needed later
    tiebreakerTiePolicy: tiebreaker, // already string value like "earliest"
    rankingFilter: rankingFilter || null,
    conferenceSlugs: teamFilter,
    useConfidencePoints,
    isPublic,
  };
};
