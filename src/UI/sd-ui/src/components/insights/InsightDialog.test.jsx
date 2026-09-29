import { render, screen } from "@testing-library/react";
import InsightDialog from "./InsightDialog";

vi.mock("../../contexts/UserContext", () => ({
  useUserDto: () => ({ userDto: { isAdmin: true } }),
}));
vi.mock("../../hooks/useUserTimeZone", () => ({
  useUserTimeZone: () => "America/New_York",
}));

const baseMatchup = {
  contestId: "c-1",
  away: "Away Team",
  awayShort: "AWAY",
  home: "Home Team",
  homeShort: "HOME",
  startDateUtc: "2026-09-26T19:30:00Z",
  straightUpWinner: "HOME",
  atsWinner: "AWAY",
  awayScore: 17,
  homeScore: 31,
};

const renderDialog = (matchup) =>
  render(<InsightDialog isOpen onClose={() => {}} matchup={matchup} loading={false} readOnly />);

describe("InsightDialog prediction tiles", () => {
  it("shows the Model Lab's picks and projected score", () => {
    renderDialog({ ...baseMatchup, overUnderPrediction: "Over" });

    expect(screen.getByText("AWAY 17 — 31 HOME")).toBeInTheDocument();
    expect(screen.getByText("Over/Under")).toBeInTheDocument();
    expect(screen.getByText("Over")).toBeInTheDocument();
  });

  it.each([
    ["Under", "Under"],
    ["None", "No pick"],
    [null, "—"],
  ])("labels over/under %s as %s", (value, label) => {
    renderDialog({ ...baseMatchup, overUnderPrediction: value });

    expect(screen.getByText("Over/Under")).toBeInTheDocument();
    // Exact-text match: each label is unique in the dialog (the score tile's
    // "—" sits inside a longer string, so it does not match "—" exactly).
    expect(screen.getByText(label)).toBeInTheDocument();
  });

  it("omits the Over/Under tile when the caller supplies no over/under (the user-facing preview)", () => {
    renderDialog(baseMatchup);

    expect(screen.queryByText("Over/Under")).not.toBeInTheDocument();
    expect(screen.getByText("AWAY 17 — 31 HOME")).toBeInTheDocument();
  });
});
