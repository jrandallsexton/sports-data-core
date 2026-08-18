import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import RankingsPage from "./RankingsPage";

// Capture the props RankingsPage hands to the widget — these tests pin the
// param validation and scope gating, not the widget's rendering.
const { widgetSpy } = vi.hoisted(() => ({ widgetSpy: vi.fn() }));
vi.mock("../widgets/RankingsWidget", () => ({
  default: (props) => {
    widgetSpy(props);
    return <div data-testid="rankings-widget" />;
  },
}));

function renderAt(path) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/sport/:sport/:league/rankings" element={<RankingsPage />} />
        <Route path="/sport/:sport/:league/rankings/:seasonYear" element={<RankingsPage />} />
        <Route
          path="/sport/:sport/:league/rankings/:seasonYear/week/:week"
          element={<RankingsPage />}
        />
      </Routes>
    </MemoryRouter>
  );
}

beforeEach(() => {
  widgetSpy.mockClear();
});

test("valid season and week pass through as numbers", () => {
  renderAt("/sport/football/ncaa/rankings/2026/week/1");

  expect(widgetSpy).toHaveBeenCalledWith(
    expect.objectContaining({
      sport: "football",
      league: "ncaa",
      seasonYear: 2026,
      week: 1,
    })
  );
});

test("zero-valued params are rejected, not forwarded", () => {
  renderAt("/sport/football/ncaa/rankings/0000/week/00");

  expect(widgetSpy).toHaveBeenCalledWith(
    expect.objectContaining({ seasonYear: undefined, week: undefined })
  );
});

test("unsupported scope gets an honest empty state, no widget", () => {
  renderAt("/sport/baseball/mlb/rankings");

  expect(screen.getByText(/aren't available for this league yet/i)).toBeInTheDocument();
  expect(widgetSpy).not.toHaveBeenCalled();
});
