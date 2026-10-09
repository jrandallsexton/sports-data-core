import { render, screen } from "@testing-library/react";
import WeeklyScoresTable from "./WeeklyScoresTable";
import PlayerStandingsTable from "./PlayerStandingsTable";
import PlayerWeekLineupsTable from "./PlayerWeekLineupsTable";

const ME = "u-me";

describe("WeeklyScoresTable, Player Pick'em league", () => {
  it("shows lineup points instead of correct-pick scores", () => {
    render(
      <WeeklyScoresTable
        currentUserId={ME}
        scoresData={{
          groupType: "PlayerPickem",
          weeks: [
            {
              weekNumber: 5,
              userScores: [
                { userId: ME, userName: "Ann", score: 32, pickCount: 8, points: 31.5, playerCount: 8 },
                { userId: "u-cal", userName: "Cal", score: 0, pickCount: 0, points: 0, playerCount: 0 },
              ],
            },
          ],
        }}
      />
    );

    expect(screen.getByRole("columnheader", { name: "Member" })).toBeInTheDocument();
    expect(screen.getByText("31.5")).toBeInTheDocument();
    expect(screen.queryByText("32")).not.toBeInTheDocument();
    // No lineup that week reads as a dash, not a zero.
    expect(screen.getByText("-")).toBeInTheDocument();
  });
});

describe("PlayerStandingsTable", () => {
  it("ranks by total points, ties sharing a rank", () => {
    render(
      <PlayerStandingsTable
        currentUserId={ME}
        standings={{
          rows: [
            { userId: "a", displayName: "Ann", totalPoints: 120.4, weeklyWins: 2, weeks: [{}, {}] },
            { userId: "b", displayName: "Ben", totalPoints: 120.4, weeklyWins: 0, weeks: [{}, {}] },
            { userId: "c", displayName: "Cal", totalPoints: 80, weeklyWins: 0, weeks: [{}] },
          ],
        }}
      />
    );

    const rows = screen.getAllByRole("row").slice(1);
    expect(rows.map((r) => r.cells[0].textContent)).toEqual(["1", "1", "3"]);
    expect(screen.getByText("🏆 2")).toBeInTheDocument();
  });
});

describe("PlayerWeekLineupsTable", () => {
  it("lists each member's players with points, totals, and hidden slots", () => {
    render(
      <PlayerWeekLineupsTable
        currentUserId={ME}
        data={{
          members: [
            {
              userId: ME,
              displayName: "Ann",
              totalPoints: 23.9,
              hiddenSlotCount: 0,
              slots: [{ slotId: "QB", firstName: "CJ", lastName: "Carr", teamName: "Notre Dame", points: 23.9, statLine: "275 pass yds, 2 TD, 0 INT" }],
            },
            {
              userId: "u-ben",
              displayName: "Ben",
              totalPoints: 0,
              hiddenSlotCount: 2,
              slots: [],
            },
          ],
        }}
      />
    );

    expect(screen.getByText("C. Carr")).toBeInTheDocument();
    expect(screen.getAllByText("23.9")).toHaveLength(2); // slot points and the total
    expect(screen.getByText(/2 hidden until kickoff/)).toBeInTheDocument();
    expect(screen.getByText("275 pass yds, 2 TD, 0 INT")).toBeInTheDocument();
  });
});

describe("PlayerWeekLineupsTable states", () => {
  it("tells loading and failure apart from an empty week", () => {
    const { rerender } = render(<PlayerWeekLineupsTable data={null} currentUserId={ME} loading />);
    expect(screen.getByText("Loading lineups...")).toBeInTheDocument();
    expect(screen.queryByText("No lineups for this week.")).not.toBeInTheDocument();

    rerender(<PlayerWeekLineupsTable data={null} currentUserId={ME} error />);
    expect(screen.getByText(/load lineups for this week/)).toBeInTheDocument();
    expect(screen.queryByText("No lineups for this week.")).not.toBeInTheDocument();
  });
});
