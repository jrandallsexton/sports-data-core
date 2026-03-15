import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import SeasonOverview from "./SeasonOverview";
import apiWrapper from "../../api/apiWrapper";

// Mock apiWrapper
jest.mock("../../api/apiWrapper", () => ({
  Season: {
    getSeasonOverview: jest.fn(),
  },
  Rankings: {
    getRankingsByWeekId: jest.fn(),
  },
}));

// Mock useNavigate
const mockNavigate = jest.fn();
jest.mock("react-router-dom", () => ({
  ...jest.requireActual("react-router-dom"),
  useNavigate: () => mockNavigate,
}));

function renderWithRouter(seasonYear = "2025") {
  return render(
    <MemoryRouter initialEntries={[`/football/${seasonYear}`]}>
      <Routes>
        <Route path="/football/:seasonYear" element={<SeasonOverview />} />
      </Routes>
    </MemoryRouter>
  );
}

const mockOverviewData = {
  data: {
    weeks: [
      { id: "aaa-111", number: 1, label: "Preseason - Week 1", seasonPhaseName: "Preseason" },
      { id: "bbb-222", number: 1, label: "Week 1", seasonPhaseName: "Regular Season" },
      { id: "ccc-333", number: 2, label: "Week 2", seasonPhaseName: "Regular Season" },
    ],
    polls: [
      { slug: "ap", shortName: "AP", name: "AP Poll" },
      { slug: "usa", shortName: "Coaches", name: "Coaches Poll" },
    ],
  },
};

const mockRankingsData = {
  data: {
    entries: [
      {
        rank: 1,
        franchiseName: "Georgia",
        franchiseLogoUrl: "https://example.com/georgia.png",
        wins: 12,
        losses: 0,
        points: 1525,
        firstPlaceVotes: 61,
        trend: "0",
      },
      {
        rank: 2,
        franchiseName: "Michigan",
        franchiseLogoUrl: "https://example.com/michigan.png",
        wins: 11,
        losses: 1,
        points: 1480,
        firstPlaceVotes: 2,
        trend: "1",
      },
    ],
  },
};

describe("SeasonOverview", () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it("renders loading state initially", () => {
    apiWrapper.Season.getSeasonOverview.mockReturnValue(new Promise(() => {}));

    renderWithRouter();

    expect(screen.getByText("Loading season overview...")).toBeInTheDocument();
  });

  it("renders season data after API call resolves", async () => {
    apiWrapper.Season.getSeasonOverview.mockResolvedValue(mockOverviewData);
    apiWrapper.Rankings.getRankingsByWeekId.mockResolvedValue(mockRankingsData);

    renderWithRouter();

    await waitFor(() => {
      expect(screen.getByText("Season Overview - 2025")).toBeInTheDocument();
    });

    // Verify selectors are populated
    expect(screen.getByLabelText("Year")).toBeInTheDocument();
    expect(screen.getByLabelText("Week")).toBeInTheDocument();
    expect(screen.getByLabelText("Poll")).toBeInTheDocument();

    // Verify rankings load with default selections (first week id, first poll slug)
    await waitFor(() => {
      expect(apiWrapper.Rankings.getRankingsByWeekId).toHaveBeenCalledWith(
        "aaa-111",
        "ap"
      );
    });

    // Verify rankings table
    await waitFor(() => {
      expect(screen.getByText("Georgia")).toBeInTheDocument();
    });
    expect(screen.getByText("Michigan")).toBeInTheDocument();
  });

  it("renders error state when overview API fails", async () => {
    apiWrapper.Season.getSeasonOverview.mockRejectedValue(
      new Error("API Error")
    );

    renderWithRouter();

    await waitFor(() => {
      expect(
        screen.getByText("Error loading season overview. Please try again.")
      ).toBeInTheDocument();
    });
  });

  it("renders empty state when no weeks or polls", async () => {
    apiWrapper.Season.getSeasonOverview.mockResolvedValue({
      data: { weeks: [], polls: [] },
    });

    renderWithRouter();

    await waitFor(() => {
      expect(
        screen.getByText("No season data available for 2025.")
      ).toBeInTheDocument();
    });
  });

  it("fetches rankings when week selection changes", async () => {
    apiWrapper.Season.getSeasonOverview.mockResolvedValue(mockOverviewData);
    apiWrapper.Rankings.getRankingsByWeekId.mockResolvedValue(mockRankingsData);

    renderWithRouter();

    // Wait for initial load
    await waitFor(() => {
      expect(screen.getByText("Season Overview - 2025")).toBeInTheDocument();
    });

    // Wait for initial rankings fetch
    await waitFor(() => {
      expect(apiWrapper.Rankings.getRankingsByWeekId).toHaveBeenCalledTimes(1);
    });

    // Change week selector to "Week 2" (id: ccc-333)
    const weekSelect = screen.getByLabelText("Week");
    await userEvent.selectOptions(weekSelect, "ccc-333");

    await waitFor(() => {
      expect(apiWrapper.Rankings.getRankingsByWeekId).toHaveBeenCalledWith(
        "ccc-333",
        "ap"
      );
    });
  });
});
