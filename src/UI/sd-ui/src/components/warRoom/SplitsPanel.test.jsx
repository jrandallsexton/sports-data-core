import { render, screen, fireEvent, waitFor, within } from "@testing-library/react";
import apiWrapper from "../../api/apiWrapper";
import SplitsPanel, { formatLine } from "./SplitsPanel";

vi.mock("../../api/apiWrapper", () => ({
  default: {
    Analytics: {
      getFranchiseSeasonMetrics: vi.fn(),
      getFranchiseSplits: vi.fn(),
    },
  },
}));

const teams = [
  { franchiseSlug: "lsu-tigers", franchiseName: "LSU Tigers", conferenceParent: "FBS" },
  { franchiseSlug: "alabama-crimson-tide", franchiseName: "Alabama Crimson Tide", conferenceParent: "FBS" },
  { franchiseSlug: "montana-grizzlies", franchiseName: "Montana Grizzlies", conferenceParent: "FCS" },
];

const splits = (overrides = {}) => ({
  franchiseSlug: "lsu-tigers",
  franchiseName: "LSU Tigers",
  requestedSeasonFrom: 2017,
  seasonFrom: 2017,
  seasonTo: 2026,
  summary: {
    games: 2,
    wins: 1,
    losses: 1,
    ties: 0,
    atsWins: 1,
    atsLosses: 1,
    atsPushes: 0,
    avgMargin: 2.5,
    avgPointsFor: 24.5,
    avgPointsAgainst: 22,
    favoriteWins: 1,
    favoriteLosses: 0,
    underdogWins: 0,
    underdogLosses: 1,
  },
  games: [
    {
      contestId: "c1",
      seasonYear: 2025,
      week: 11,
      seasonPhaseTypeCode: 2,
      site: "Away",
      opponentSlug: "alabama-crimson-tide",
      opponentName: "Alabama Crimson Tide",
      opponentRankGoingIn: 8,
      pointsFor: 31,
      pointsAgainst: 24,
      result: "Win",
      spread: -3.5,
      atsResult: "Covered",
    },
    {
      contestId: "c2",
      seasonYear: 2024,
      week: null,
      seasonPhaseTypeCode: 3,
      site: "Neutral",
      opponentSlug: "ole-miss-rebels",
      opponentName: "Ole Miss Rebels",
      opponentRankGoingIn: 99,
      pointsFor: 18,
      pointsAgainst: 20,
      result: "Loss",
      spread: 2,
      atsResult: "NotCovered",
    },
  ],
  ...overrides,
});

async function pickLsu() {
  render(<SplitsPanel />);
  const select = await screen.findByLabelText("Team");
  await waitFor(() => expect(within(select).getByText("LSU Tigers")).toBeInTheDocument());
  fireEvent.change(select, { target: { value: "lsu-tigers" } });
}

describe("SplitsPanel", () => {
  let consoleError;
  beforeEach(() => {
    consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    apiWrapper.Analytics.getFranchiseSeasonMetrics.mockReset();
    apiWrapper.Analytics.getFranchiseSplits.mockReset();
    apiWrapper.Analytics.getFranchiseSeasonMetrics.mockResolvedValue({ data: teams });
  });
  afterEach(() => consoleError.mockRestore());

  it("lists only FBS teams and asks for a team before querying", async () => {
    render(<SplitsPanel />);
    const select = await screen.findByLabelText("Team");
    await waitFor(() => expect(within(select).getByText("LSU Tigers")).toBeInTheDocument());

    expect(within(select).queryByText("Montana Grizzlies")).not.toBeInTheDocument();
    expect(screen.getByText("Pick a team to see its splits.")).toBeInTheDocument();
    expect(apiWrapper.Analytics.getFranchiseSplits).not.toHaveBeenCalled();
  });

  it("renders the summary and games for the picked team", async () => {
    apiWrapper.Analytics.getFranchiseSplits.mockResolvedValue({ data: splits() });

    await pickLsu();

    expect(await screen.findByText("Alabama Crimson Tide")).toBeInTheDocument();
    expect(screen.getByText("Record (.500)")).toBeInTheDocument();
    expect(screen.getByText("+2.5")).toBeInTheDocument();
    expect(screen.getByText("W 31-24")).toBeInTheDocument();
    expect(screen.getByText("#8")).toBeInTheDocument();
    expect(screen.getByText("Cover")).toBeInTheDocument();
    // Postseason row shows Post; unranked opponent shows no rank.
    expect(screen.getByText("Post")).toBeInTheDocument();
    expect(screen.queryByText("#99")).not.toBeInTheDocument();
  });

  it("sends a chip as a filter and clears it on a second click", async () => {
    apiWrapper.Analytics.getFranchiseSplits.mockResolvedValue({ data: splits() });
    await pickLsu();
    await screen.findByText("Alabama Crimson Tide");

    fireEvent.click(screen.getByRole("button", { name: "Underdog" }));
    await waitFor(() =>
      expect(apiWrapper.Analytics.getFranchiseSplits).toHaveBeenLastCalledWith(
        "lsu-tigers",
        expect.objectContaining({ line: "Underdog" })
      )
    );

    fireEvent.click(screen.getByRole("button", { name: "Underdog" }));
    await waitFor(() => {
      const [, params] = apiWrapper.Analytics.getFranchiseSplits.mock.calls.at(-1);
      expect(params).not.toHaveProperty("line");
    });
  });

  it("says when a filter raised the season floor", async () => {
    apiWrapper.Analytics.getFranchiseSplits.mockResolvedValue({
      data: splits({ requestedSeasonFrom: 2005, seasonFrom: 2012, seasonTo: 2026 }),
    });

    await pickLsu();

    expect(await screen.findByText(/These filters have data from 2012/)).toBeInTheDocument();
  });

  it("shows an error when the request fails", async () => {
    apiWrapper.Analytics.getFranchiseSplits.mockRejectedValue(new Error("boom"));

    await pickLsu();

    expect(await screen.findByText("Failed to load splits")).toBeInTheDocument();
  });

  it("formats lines", () => {
    expect(formatLine(-3.5)).toBe("-3.5");
    expect(formatLine(2)).toBe("+2");
    expect(formatLine(0)).toBe("PK");
    expect(formatLine(null)).toBe("—");
  });
});
