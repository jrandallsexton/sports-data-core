import { describe, it, expect } from "vitest";
import {
  ADVISOR_LEVELS,
  applicablePicks,
  applyLabel,
  describePick,
  describeStanding,
  describeStatBot,
  formatProbability,
  levelMeta,
} from "./advisorSheet";

describe("levelMeta", () => {
  it("lists four levels in increasing risk and resolves by key", () => {
    expect(ADVISOR_LEVELS.map((l) => l.key)).toEqual(["Prevent", "GoalLine", "QbDraw", "HailMary"]);
    expect(levelMeta("HailMary").name).toBe("Hail Mary");
  });

  it("falls back to the first level for an unknown key", () => {
    expect(levelMeta("Nope").key).toBe("Prevent");
  });
});

describe("formatProbability", () => {
  it("renders a whole percent and tolerates null", () => {
    expect(formatProbability(0.7312)).toBe("73%");
    expect(formatProbability(null)).toBeNull();
  });
});

describe("describePick", () => {
  it("speaks in probability, never spreads", () => {
    // deetsMeter = the probability; StatBot = the preview. Never the other way round.
    expect(describePick({ kind: "Lock", modelProbability: 0.85, previewAgrees: true })).toBe(
      "deetsMeter 85% — lock, StatBot agrees"
    );
    expect(describePick({ kind: "Lock", modelProbability: 0.85, previewAgrees: null })).toBe(
      "deetsMeter 85% — lock"
    );
    expect(describePick({ kind: "Lean", modelProbability: 0.55, previewAgrees: null })).toBe(
      "Coin flip (55%) — staying with the deetsMeter"
    );
    expect(describePick({ kind: "Lean", modelProbability: 0.78, previewAgrees: false })).toBe(
      "deetsMeter 78% but StatBot disagrees — treated as a coin flip, staying with the deetsMeter"
    );
    expect(describePick({ kind: "Flip", modelProbability: 0.48, previewAgrees: null })).toBe(
      "Coin flip — taking the other side (48%) to gain ground"
    );
    expect(describePick({ kind: "Flip", modelProbability: 0.3, previewAgrees: false })).toBe(
      "Coin flip — deetsMeter and StatBot split; taking StatBot's side (30%)"
    );
    expect(describePick({ kind: "NoPrediction" })).not.toMatch(/StatBot/);
    expect(describePick({ kind: "Locked" })).toMatch(/untouched/);
    expect(describePick({ kind: "NoPrediction" })).toMatch(/yourself/);
  });
});

describe("applicablePicks", () => {
  it("keeps only writable rows that would change something", () => {
    const picks = [
      { contestId: "a", kind: "Lock", franchiseSeasonId: "t1", differsFromExisting: true },
      { contestId: "b", kind: "Lock", franchiseSeasonId: "t2", differsFromExisting: false },
      { contestId: "c", kind: "Locked", franchiseSeasonId: "t3", differsFromExisting: false },
      { contestId: "d", kind: "NoPrediction", franchiseSeasonId: null, differsFromExisting: true },
      { contestId: "e", kind: "Flip", franchiseSeasonId: "t5", differsFromExisting: true },
    ];
    expect(applicablePicks(picks).map((p) => p.contestId)).toEqual(["a", "e"]);
    expect(applicablePicks(undefined)).toEqual([]);
  });

  it("never submits a valueless row in a confidence league", () => {
    const picks = [
      { contestId: "a", kind: "Lock", franchiseSeasonId: "t1", confidencePoints: 3, differsFromExisting: true },
      { contestId: "b", kind: "Lock", franchiseSeasonId: "t2", confidencePoints: null, differsFromExisting: true },
    ];
    expect(applicablePicks(picks, true).map((p) => p.contestId)).toEqual(["a"]);
    expect(applicablePicks(picks, false).map((p) => p.contestId)).toEqual(["a", "b"]);
  });
});

describe("applyLabel", () => {
  const rows = [
    { contestId: "a", franchiseSeasonId: "t1" },
    { contestId: "b", franchiseSeasonId: "t2" },
    { contestId: "c", franchiseSeasonId: "t3" },
  ];

  it("says Set on an unpicked week, never Replace", () => {
    expect(applyLabel(rows, {})).toBe("Set 3 picks");
    expect(applyLabel(rows, undefined)).toBe("Set 3 picks");
  });

  it("says Replace only when every affected row already has a pick", () => {
    const picked = { a: { franchiseSeasonId: "x" }, b: { franchiseSeasonId: "y" }, c: { franchiseSeasonId: "z" } };
    expect(applyLabel(rows, picked)).toBe("Replace 3 picks");
    expect(applyLabel(rows.slice(0, 1), picked)).toBe("Replace 1 pick");
  });

  it("splits the count when it is a mix", () => {
    expect(applyLabel(rows, { a: { franchiseSeasonId: "x" } })).toBe("Set 2, replace 1");
  });

  it("handles nothing to do", () => {
    expect(applyLabel([], {})).toBe("Nothing to change");
  });
});

describe("describeStanding", () => {
  it("handles unranked and leading", () => {
    expect(describeStanding({ rank: null })).toMatch(/No scored weeks/);
    expect(
      describeStanding({ rank: 1, deficit: 0, gamesThisWeek: 4, maxPointsThisWeek: 10, regularSeasonWeeksLeft: 6 })
    ).toBe("You're leading. 4 games this week, up to 10 points on the table. 6 weeks left in the regular season.");
  });

  it("says a perfect week takes the lead only with the leader scoring at pace", () => {
    expect(
      describeStanding({
        rank: 3, deficit: 10, gamesThisWeek: 5, maxPointsThisWeek: 15, regularSeasonWeeksLeft: 6,
        canCloseGapThisWeek: true, leaderExpectedThisWeek: 4, leaderName: "Leader",
      })
    ).toBe(
      "You're 3rd, 10 points behind Leader. 5 games this week, up to 15 points on the table. 6 weeks left in the regular season. A perfect week takes the lead even if Leader adds about 4 on this slate at their per-game pace."
    );
  });

  it("offers a reachable rank instead of an unattainable target, and never forecasts games", () => {
    const text = describeStanding({
      rank: 5, deficit: 714, gamesThisWeek: 21, maxPointsThisWeek: 231, regularSeasonWeeksLeft: 9,
      canCloseGapThisWeek: false, bestCaseRankThisWeek: 3, leaderExpectedThisWeek: 190,
      nextAheadName: "Dave", nextAheadRank: 4, pointsBehindNextAhead: 40, nextAheadExpectedThisWeek: 130,
      leaderName: "Aesop",
    });
    expect(text).toBe(
      "You're 5th, 714 points behind Aesop. 21 games this week, up to 231 points on the table. 9 weeks left in the regular season. With everyone at pace, a perfect week moves you to 3rd at best. Dave in 4th is 40 points ahead and, at their per-game pace, adds about 130 on this slate."
    );
    // Never a per-game target for the user, never a season game count, and
    // never "a week" — projections are for this slate only.
    expect(text).not.toMatch(/a game to|left this season|a week/);
  });

  it("says so when nobody is reachable, and omits weeks when the calendar was unavailable", () => {
    const text = describeStanding({
      rank: 5, deficit: 714, gamesThisWeek: 21, maxPointsThisWeek: 231, regularSeasonWeeksLeft: null,
      canCloseGapThisWeek: false, bestCaseRankThisWeek: 5, leaderExpectedThisWeek: 190,
      nextAheadName: "Dave", nextAheadRank: 4, pointsBehindNextAhead: 300, nextAheadExpectedThisWeek: 130,
      leaderName: "Aesop",
    });
    expect(text).toBe(
      "You're 5th, 714 points behind Aesop. 21 games this week, up to 231 points on the table. Dave in 4th is 300 points ahead and, at their per-game pace, adds about 130 on this slate — out of reach this week, so play for next."
    );
    expect(text).not.toMatch(/week left|weeks left|a week/);
  });
});

describe("describeStatBot", () => {
  it("is honest when StatBot trails the user", () => {
    expect(describeStatBot({ rank: 2, statBot: { rank: 5, pointsPerGame: 4.1 } })).toMatch(/behind you/);
    expect(describeStatBot({ rank: 6, statBot: { rank: 2, pointsPerGame: 6.2 } })).toBe(
      "StatBot is #2 in this league, 6.2 points per game."
    );
    expect(describeStatBot({ rank: 1, statBot: null })).toBeNull();
  });
});
