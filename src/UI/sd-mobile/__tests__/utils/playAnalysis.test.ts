import {
  PLAY_ANALYSIS_KEEP,
  playAnalysisForCard,
  playAnalysisForLastPlay,
  withPlayAnalysis,
} from '@/src/utils/playAnalysis';

const CID = '00000000-0000-0000-0000-000000000001';
const items = [{ id: 'f1', factType: 'GameCount', text: "T. Cooley's first interception of the game." }];

describe('playAnalysisForLastPlay', () => {
  it('returns the facts while they belong to the last play', () => {
    expect(
      playAnalysisForLastPlay({ contestId: CID, lastPlayId: 'p1', playAnalysisByPlayId: { p1: items } }),
    ).toBe(items);
  });

  it('hides facts for an older play once the next play lands', () => {
    expect(
      playAnalysisForLastPlay({ contestId: CID, lastPlayId: 'p2', playAnalysisByPlayId: { p1: items } }),
    ).toBeNull();
  });

  it('waits for the play when the facts arrive first', () => {
    expect(playAnalysisForLastPlay({ contestId: CID, playAnalysisByPlayId: { p1: items } })).toBeNull();
  });

  it('returns null with no analysis, no record, or no items', () => {
    expect(playAnalysisForLastPlay({ contestId: CID, lastPlayId: 'p1' })).toBeNull();
    expect(playAnalysisForLastPlay(undefined)).toBeNull();
    expect(
      playAnalysisForLastPlay({ contestId: CID, lastPlayId: 'p1', playAnalysisByPlayId: { p1: [] } }),
    ).toBeNull();
  });
});

describe('playAnalysisForCard', () => {
  const rest = [{ id: 'r1', factType: 'GameCount', text: 'from the matchup payload' }];
  const matchup = { lastPlayId: 'p1', playAnalysis: rest };

  it("uses the payload's facts on a cold load", () => {
    expect(playAnalysisForCard(undefined, matchup)).toBe(rest);
  });

  it("keeps the payload's facts while live updates are still on the same play", () => {
    expect(playAnalysisForCard({ contestId: CID, lastPlayId: 'p1' }, matchup)).toBe(rest);
  });

  it("drops the payload's facts once a newer play lands", () => {
    expect(playAnalysisForCard({ contestId: CID, lastPlayId: 'p2' }, matchup)).toBeNull();
  });

  it('prefers live facts for the current play', () => {
    const live = { contestId: CID, lastPlayId: 'p2', playAnalysisByPlayId: { p2: items } };
    expect(playAnalysisForCard(live, matchup)).toBe(items);
  });

  it('returns null when the payload has no facts', () => {
    expect(playAnalysisForCard(undefined, { lastPlayId: 'p1', playAnalysis: null })).toBeNull();
    expect(playAnalysisForCard(undefined, { lastPlayId: 'p1', playAnalysis: [] })).toBeNull();
  });
});

describe('withPlayAnalysis', () => {
  it('adds a play without touching the others', () => {
    const before = { p1: items };
    const after = withPlayAnalysis(before, 'p2', items);

    expect(Object.keys(after)).toEqual(['p1', 'p2']);
    expect(before).toEqual({ p1: items });
  });

  it('keeps only the most recent plays', () => {
    let map: ReturnType<typeof withPlayAnalysis> | undefined;
    for (let i = 1; i <= PLAY_ANALYSIS_KEEP + 2; i++) {
      map = withPlayAnalysis(map, `p${i}`, items);
    }

    expect(Object.keys(map!)).toHaveLength(PLAY_ANALYSIS_KEEP);
    expect(map!.p1).toBeUndefined();
    expect(map![`p${PLAY_ANALYSIS_KEEP + 2}`]).toBe(items);
  });
});
