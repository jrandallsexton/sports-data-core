import { fireEvent, render, screen, within } from "@testing-library/react";
import TeamStatistics from "./TeamStatistics";

// Wire shape of GET /ui/teamcard/.../statistics: FranchiseSeasonStatisticDto.
// The human-readable name is `statisticValue` (StatFormattingService writes
// the friendly label there); `statisticKey` is the raw ESPN key. There is no
// `statistic` property, which is why the Statistic column rendered blank for
// a year before this test existed.
const stats = {
  statistics: {
    defensive: [
      {
        category: "defensive",
        categoryDisplayName: "Defensive",
        statisticKey: "totalTackles",
        statisticValue: "Total Tackles",
        displayValue: "106",
        perGameDisplayValue: null,
        rank: 45,
      },
      {
        category: "defensive",
        categoryDisplayName: "Defensive",
        statisticKey: "sacks",
        statisticValue: "Sacks",
        displayValue: "6",
        perGameDisplayValue: "2.0",
        rank: 1,
      },
    ],
    passing: [
      {
        category: "passing",
        categoryDisplayName: "Passing",
        statisticKey: "netPassingYards",
        // No friendly label on the wire: fall back to the raw key, never blank.
        statisticValue: null,
        displayValue: "812",
        rank: 12,
      },
    ],
  },
  gamesPlayed: 3,
};

function rows() {
  return screen.getAllByRole("row").slice(1); // drop the header row
}

describe("TeamStatistics", () => {
  it("shows the statistic name from statisticValue alongside value, per game, and rank", () => {
    render(<TeamStatistics seasonYear={2026} stats={stats} />);

    const [tackles, sacks] = rows();
    expect(within(tackles).getAllByRole("cell").map((c) => c.textContent)).toEqual([
      "Total Tackles",
      "106",
      "-",
      "45",
    ]);
    expect(within(sacks).getAllByRole("cell").map((c) => c.textContent)).toEqual([
      "Sacks",
      "6",
      "2.0",
      "1",
    ]);
  });

  it("falls back to the raw key when no friendly label is on the wire", () => {
    render(<TeamStatistics seasonYear={2026} stats={stats} />);
    fireEvent.click(screen.getByRole("button", { name: "Passing" }));

    const [passing] = rows();
    expect(within(passing).getAllByRole("cell")[0].textContent).toBe("netPassingYards");
  });

  it("accepts the axios envelope shape too", () => {
    render(<TeamStatistics seasonYear={2026} stats={{ data: stats }} />);
    expect(screen.getByText("Total Tackles")).toBeInTheDocument();
  });

  it("labels category tabs from categoryDisplayName", () => {
    render(<TeamStatistics seasonYear={2026} stats={stats} />);
    expect(screen.getByRole("button", { name: "Defensive" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Passing" })).toBeInTheDocument();
  });
});
