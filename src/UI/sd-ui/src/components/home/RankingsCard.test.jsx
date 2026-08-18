import { render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import RankingsCard from "./RankingsCard";
import apiWrapper from "../../api/apiWrapper";
import SeasonApi from "../../api/seasonApi";

// apiWrapper is a default export → wrap the mock in { default }.
vi.mock("../../api/apiWrapper", () => ({
  default: {
    Rankings: {
      getSeasonRankings: vi.fn(),
    },
  },
}));

vi.mock("../../api/seasonApi", () => ({
  default: {
    getCurrentSeason: vi.fn(),
  },
}));

vi.mock("../../contexts/ThemeContext", () => ({
  useTheme: () => ({ theme: "dark" }),
}));

const entry = (rank, name, slug) => ({
  rank,
  franchiseName: name,
  franchiseSlug: slug,
  franchiseSeasonId: `fs-${rank}`,
  franchiseLogoUrlDark: `https://example.com/${slug}-dark.png`,
  wins: 0,
  losses: 0,
});

const apPoll = {
  pollId: "ap",
  pollName: "AP Top 25",
  entries: Array.from({ length: 25 }, (_, i) =>
    entry(i + 1, `Team ${i + 1}`, `team-${i + 1}`)
  ),
};

function renderCard() {
  return render(
    <MemoryRouter>
      <RankingsCard />
    </MemoryRouter>
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  SeasonApi.getCurrentSeason.mockResolvedValue({ data: { seasonYear: 2026 } });
});

test("renders the top 10 of the AP poll with a full-rankings link", async () => {
  apiWrapper.Rankings.getSeasonRankings.mockResolvedValue({
    data: [{ ...apPoll, pollId: "usa", pollName: "Coaches Poll" }, apPoll],
  });

  renderCard();

  await waitFor(() => {
    expect(screen.getByText("AP TOP 25")).toBeInTheDocument();
  });

  // Prefers 'ap' over the first-listed poll, shows exactly 10 rows.
  expect(screen.getByText("Team 1")).toBeInTheDocument();
  expect(screen.getByText("Team 10")).toBeInTheDocument();
  expect(screen.queryByText("Team 11")).not.toBeInTheDocument();

  const fullLink = screen.getByRole("link", { name: /full rankings/i });
  expect(fullLink).toHaveAttribute("href", "/app/sport/football/ncaa/rankings");

  // Rankings were requested for the RESOLVED season, not a literal.
  expect(apiWrapper.Rankings.getSeasonRankings).toHaveBeenCalledWith(2026);

  // Team rows deep-link with the resolved season year.
  expect(screen.getByRole("link", { name: "Open Team 1" })).toHaveAttribute(
    "href",
    "/app/sport/football/ncaa/team/team-1/2026"
  );
});

test("renders nothing when no polls exist for the season", async () => {
  apiWrapper.Rankings.getSeasonRankings.mockResolvedValue({ data: [] });

  const { container } = renderCard();

  await waitFor(() => {
    expect(apiWrapper.Rankings.getSeasonRankings).toHaveBeenCalled();
  });
  expect(container).toBeEmptyDOMElement();
});

test("renders nothing when no current season is sourced", async () => {
  SeasonApi.getCurrentSeason.mockRejectedValue(new Error("404"));

  const { container } = renderCard();

  // The rankings fetch must be skipped entirely — no season, no request.
  await waitFor(() => {
    expect(SeasonApi.getCurrentSeason).toHaveBeenCalled();
  });
  expect(apiWrapper.Rankings.getSeasonRankings).not.toHaveBeenCalled();
  expect(container).toBeEmptyDOMElement();
});
