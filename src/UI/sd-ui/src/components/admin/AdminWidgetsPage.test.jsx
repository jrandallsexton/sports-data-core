import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import AdminWidgetsPage from "./AdminWidgetsPage";

// The gallery's job is to mount every registered widget and keep rendering
// when one of them throws. The widgets themselves are stubbed - each has its
// own data path and this test is about the frames, not the widgets.
vi.mock("../../api/apiWrapper", () => ({
  default: {
    Picks: {
      getAccuracyChartForSynthetic: vi.fn(() => Promise.resolve({ data: { weeklyAccuracy: [] } })),
      getAccuracyChartForUser: vi.fn(() => Promise.resolve({ data: [] })),
    },
  },
}));

vi.mock("../widgets/AiAccuracyWidget", () => ({ default: () => <div>stub:ai-accuracy</div> }));
vi.mock("../widgets/AiRecordWidget", () => ({ default: () => <div>stub:ai-record</div> }));
vi.mock("../widgets/PickAccuracyWidget", () => ({ default: () => <div>stub:pick-accuracy</div> }));
vi.mock("../widgets/PickRecordWidget", () => ({ default: () => <div>stub:pick-record</div> }));
vi.mock("../widgets/LeaderboardWidget", () => ({
  // The one that blows up on this season's data.
  default: () => {
    throw new Error("leaderboard exploded");
  },
}));
vi.mock("../widgets/NewsWidget", () => ({ default: () => <div>stub:news</div> }));
vi.mock("../widgets/TipWeekWidget", () => ({ default: () => <div>stub:tip-week</div> }));

function renderPage() {
  return render(
    <MemoryRouter>
      <AdminWidgetsPage />
    </MemoryRouter>
  );
}

describe("AdminWidgetsPage", () => {
  let consoleError;
  beforeEach(() => {
    // React logs the caught error; keep the test output clean.
    consoleError = vi.spyOn(console, "error").mockImplementation(() => {});
  });
  afterEach(() => consoleError.mockRestore());

  it("frames every registered widget with its title", async () => {
    renderPage();

    for (const title of [
      "AI Accuracy (chart)",
      "AI Record (card)",
      "Pick Accuracy (chart)",
      "Pick Record (card)",
      "Leaderboard",
      "News",
      "Tip of the Week",
    ]) {
      expect(screen.getByRole("heading", { name: title })).toBeInTheDocument();
    }

    // Chart widgets mount once their DTOs resolve.
    expect(await screen.findByText("stub:ai-accuracy")).toBeInTheDocument();
    expect(await screen.findByText("stub:pick-accuracy")).toBeInTheDocument();
  });

  it("keeps rendering the other widgets when one throws, and shows the error in its frame", () => {
    renderPage();

    const alert = screen.getByRole("alert");
    expect(alert).toHaveTextContent("Leaderboard threw while rendering.");
    expect(alert).toHaveTextContent("leaderboard exploded");

    expect(screen.getByText("stub:ai-record")).toBeInTheDocument();
    expect(screen.getByText("stub:news")).toBeInTheDocument();
    expect(screen.getByText("stub:tip-week")).toBeInTheDocument();
  });
});
