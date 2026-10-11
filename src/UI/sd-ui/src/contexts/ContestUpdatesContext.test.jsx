import { act, renderHook } from "@testing-library/react";
import { ContestUpdatesProvider, useContestUpdates } from "./ContestUpdatesContext";
import { playAnalysisForLastPlay } from "../utils/playAnalysis";

const CONTEST = "c1";

const play = (playId) => ({
  contestId: CONTEST,
  playId,
  playDescription: `play ${playId}`,
  period: "Q1",
  clock: "10:00",
  awayScore: 0,
  homeScore: 0,
  isScoringPlay: false,
});

const analysis = (playId, text) => ({
  contestId: CONTEST,
  playId,
  items: [{ id: `${playId}-f`, factType: "GameCount", text }],
});

function setup() {
  vi.spyOn(console, "log").mockImplementation(() => {});
  return renderHook(() => useContestUpdates(), { wrapper: ContestUpdatesProvider });
}

const shown = (result) => playAnalysisForLastPlay(result.current.getContestUpdate(CONTEST));

describe("ContestUpdatesContext play analysis", () => {
  afterEach(() => vi.restoreAllMocks());

  it("attaches facts that arrive after their play", () => {
    const { result } = setup();

    act(() => result.current.handleFootballPlayCompleted(play("p1")));
    act(() => result.current.handlePlayAnalysis(analysis("p1", "first")));

    expect(shown(result)?.[0].text).toBe("first");
  });

  it("attaches facts that arrive before their play once the play lands", () => {
    const { result } = setup();

    act(() => result.current.handlePlayAnalysis(analysis("p1", "early")));
    expect(shown(result)).toBeNull();

    act(() => result.current.handleFootballPlayCompleted(play("p1")));
    expect(shown(result)?.[0].text).toBe("early");
  });

  it("drops the facts when the next play lands", () => {
    const { result } = setup();

    act(() => result.current.handleFootballPlayCompleted(play("p1")));
    act(() => result.current.handlePlayAnalysis(analysis("p1", "first")));
    act(() => result.current.handleFootballPlayCompleted(play("p2")));

    expect(shown(result)).toBeNull();
  });

  it("keeps the next play's facts that arrive before it while the current play's are showing", () => {
    const { result } = setup();

    act(() => result.current.handleFootballPlayCompleted(play("p1")));
    act(() => result.current.handlePlayAnalysis(analysis("p1", "first")));
    act(() => result.current.handlePlayAnalysis(analysis("p2", "early")));
    expect(shown(result)?.[0].text).toBe("first");

    act(() => result.current.handleFootballPlayCompleted(play("p2")));
    expect(shown(result)?.[0].text).toBe("early");
  });

  it("never lets a late analysis for an older play replace the current play's", () => {
    const { result } = setup();

    act(() => result.current.handleFootballPlayCompleted(play("p1")));
    act(() => result.current.handleFootballPlayCompleted(play("p2")));
    act(() => result.current.handlePlayAnalysis(analysis("p2", "current")));
    act(() => result.current.handlePlayAnalysis(analysis("p1", "stale")));

    expect(shown(result)?.[0].text).toBe("current");
  });
});
