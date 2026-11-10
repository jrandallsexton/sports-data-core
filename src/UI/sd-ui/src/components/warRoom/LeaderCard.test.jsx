import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import apiWrapper from "../../api/apiWrapper";
import LeaderCard from "./LeaderCard";

vi.mock("../../api/apiWrapper", () => ({
  default: {
    Analytics: {
      getAthleteSeasonLeaders: vi.fn(),
    },
  },
}));

const leader = (overrides = {}) => ({
  rank: 1,
  athleteId: "a1",
  athleteName: "Braden Atkinson",
  position: "QB",
  franchiseSlug: "oregon-state-beavers",
  franchiseShortName: "Oregon State",
  franchiseAbbreviation: "ORST",
  franchiseColorHex: "dc4405",
  franchiseLogoUrl: null,
  gamesPlayed: 9,
  value: 3169,
  displayValue: "3,169",
  ...overrides,
});

function renderCard() {
  return render(
    <LeaderCard title="Passing Yards" category="passing" stat="passingYards" seasonYear={2026} />
  );
}

describe("LeaderCard", () => {
  let consoleError;
  beforeEach(() => {
    consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
    apiWrapper.Analytics.getAthleteSeasonLeaders.mockReset();
  });
  afterEach(() => consoleError.mockRestore());

  it("renders ranked leaders with links and the formatted value", async () => {
    apiWrapper.Analytics.getAthleteSeasonLeaders.mockResolvedValue({
      data: {
        leaders: [
          leader(),
          leader({
            rank: 2,
            athleteId: "a2",
            athleteName: "Drew Mestemaker",
            franchiseSlug: "oklahoma-state-cowboys",
            franchiseShortName: "Oklahoma State",
            franchiseAbbreviation: "OKST",
            displayValue: "2,962",
          }),
        ],
      },
    });

    renderCard();

    expect(await screen.findByText("Braden Atkinson")).toBeInTheDocument();
    expect(screen.getByText("3,169")).toBeInTheDocument();
    expect(screen.getByText("QB · Oregon State")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Braden Atkinson" })).toHaveAttribute(
      "href",
      "/app/sport/football/ncaa/athlete/a1"
    );
    // No generated mark: the color roundel with the abbreviation stands in.
    expect(screen.getAllByRole("img", { name: "Oregon State" })[0]).toHaveTextContent("ORST");
    expect(apiWrapper.Analytics.getAthleteSeasonLeaders).toHaveBeenCalledWith(2026, {
      category: "passing",
      stat: "passingYards",
      perGame: false,
      limit: 10,
    });
  });

  it("refetches per game when toggled and shows the qualification note", async () => {
    apiWrapper.Analytics.getAthleteSeasonLeaders.mockResolvedValue({ data: { leaders: [leader()] } });

    renderCard();
    await screen.findByText("Braden Atkinson");

    fireEvent.click(screen.getByRole("button", { name: "Per game" }));

    await waitFor(() =>
      expect(apiWrapper.Analytics.getAthleteSeasonLeaders).toHaveBeenLastCalledWith(2026, {
        category: "passing",
        stat: "passingYards",
        perGame: true,
        limit: 10,
      })
    );
    expect(await screen.findByText("Minimum 75% of team games played.")).toBeInTheDocument();
  });

  it("shows an empty state when the season has no leaders", async () => {
    apiWrapper.Analytics.getAthleteSeasonLeaders.mockResolvedValue({ data: { leaders: [] } });

    renderCard();

    expect(await screen.findByText("No leaders for 2026.")).toBeInTheDocument();
  });

  it("shows an error state when the request fails", async () => {
    apiWrapper.Analytics.getAthleteSeasonLeaders.mockRejectedValue(new Error("boom"));

    renderCard();

    expect(await screen.findByText("Failed to load leaders")).toBeInTheDocument();
  });
});
