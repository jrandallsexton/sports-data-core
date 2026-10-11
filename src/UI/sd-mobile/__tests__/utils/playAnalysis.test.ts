import { playAnalysisForLastPlay } from '@/src/utils/playAnalysis';

const CID = '00000000-0000-0000-0000-000000000001';
const items = [{ id: 'f1', factType: 'GameCount', text: "T. Cooley's first interception of the game." }];

describe('playAnalysisForLastPlay', () => {
  it('returns the facts while they belong to the last play', () => {
    expect(
      playAnalysisForLastPlay({ contestId: CID, lastPlayId: 'p1', lastPlayAnalysis: { playId: 'p1', items } }),
    ).toBe(items);
  });

  it('hides facts for an older play once the next play lands', () => {
    expect(
      playAnalysisForLastPlay({ contestId: CID, lastPlayId: 'p2', lastPlayAnalysis: { playId: 'p1', items } }),
    ).toBeNull();
  });

  it('waits for the play when the facts arrive first', () => {
    expect(playAnalysisForLastPlay({ contestId: CID, lastPlayAnalysis: { playId: 'p1', items } })).toBeNull();
  });

  it('returns null with no analysis, no record, or no items', () => {
    expect(playAnalysisForLastPlay({ contestId: CID, lastPlayId: 'p1' })).toBeNull();
    expect(playAnalysisForLastPlay(undefined)).toBeNull();
    expect(
      playAnalysisForLastPlay({ contestId: CID, lastPlayId: 'p1', lastPlayAnalysis: { playId: 'p1', items: [] } }),
    ).toBeNull();
  });
});
