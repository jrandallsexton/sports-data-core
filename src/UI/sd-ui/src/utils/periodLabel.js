const PERIOD_PREFIX = {
  baseball: 'I',  // Inning
  football: 'Q',  // Quarter
};

export function getPeriodPrefix(sport) {
  return PERIOD_PREFIX[sport] || 'P';
}
