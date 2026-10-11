import { playAnalysisForLastPlay } from "./playAnalysis";

const items = [{ id: "f1", factType: "GameCount", text: "T. Cooley's first interception of the game." }];

describe("playAnalysisForLastPlay", () => {
  it("returns the facts while they belong to the last play", () => {
    expect(playAnalysisForLastPlay({ lastPlayId: "p1", lastPlayAnalysis: { playId: "p1", items } })).toBe(items);
  });

  it("hides facts for an older play once the next play lands", () => {
    expect(playAnalysisForLastPlay({ lastPlayId: "p2", lastPlayAnalysis: { playId: "p1", items } })).toBeNull();
  });

  it("waits for the play when the facts arrive first", () => {
    expect(playAnalysisForLastPlay({ lastPlayAnalysis: { playId: "p1", items } })).toBeNull();
  });

  it("returns null with no analysis, no update, or no items", () => {
    expect(playAnalysisForLastPlay({ lastPlayId: "p1" })).toBeNull();
    expect(playAnalysisForLastPlay(null)).toBeNull();
    expect(playAnalysisForLastPlay({ lastPlayId: "p1", lastPlayAnalysis: { playId: "p1", items: [] } })).toBeNull();
  });
});
