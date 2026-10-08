import { act, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation, useNavigate } from "react-router-dom";
import LeaguePicksRouter from "./LeaguePicksRouter";

// /user/me as the router sees it; tests swap it between renders.
const { userState, refreshSpy } = vi.hoisted(() => ({
  userState: { userDto: null },
  refreshSpy: vi.fn(),
}));

vi.mock("../contexts/UserContext", () => ({
  useUserDto: () => ({
    userDto: userState.userDto,
    loading: false,
    refreshUserDto: refreshSpy,
  }),
}));
vi.mock("../contexts/LeagueContext", () => ({
  useLeagueContext: () => ({ selectedLeagueId: null }),
}));
vi.mock("../components/pickem/players/PlayerRosterBuilder", () => ({
  default: () => <div>roster builder</div>,
  WEEK: 1,
}));
vi.mock("../components/picks/PicksPage.jsx", () => ({ default: () => <div>picks page</div> }));

const LEAGUE_ID = "3085407b-8f7f-4ea2-8cea-0d217ff99398";
const playerLeague = (seasonWeekDetails) => ({
  id: LEAGUE_ID,
  groupType: "PlayerPickem",
  currentSeasonWeekId: "sw-6",
  seasonWeekDetails,
});

function CurrentPath() {
  return <div data-testid="path">{useLocation().pathname}</div>;
}

const renderAt = (path) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/league/:leagueId/picks" element={<LeaguePicksRouter />} />
        <Route
          path="/league/:leagueId/picks/phase/:phase/weeks/:week"
          element={<LeaguePicksRouter />}
        />
        <Route path="*" element={<CurrentPath />} />
      </Routes>
    </MemoryRouter>
  );

beforeEach(() => {
  vi.useFakeTimers();
  refreshSpy.mockReset();
});
afterEach(() => vi.useRealTimers());

describe("LeaguePicksRouter, new Player Pick'em league", () => {
  it("waits for bootstrap's weeks instead of falling back to week 1", () => {
    // /user/me read before bootstrap materialized the league's weeks.
    userState.userDto = { leagues: [playerLeague([])] };
    const view = renderAt(`/league/${LEAGUE_ID}/picks`);

    expect(screen.getByText("Setting up your league...")).toBeInTheDocument();
    act(() => vi.advanceTimersByTime(1000));
    expect(refreshSpy).toHaveBeenCalled();

    // The re-read lands with week 6.
    userState.userDto = {
      leagues: [playerLeague([{ seasonWeekId: "sw-6", week: 6, phase: "regular" }])],
    };
    view.rerender(
      <MemoryRouter initialEntries={[`/league/${LEAGUE_ID}/picks`]}>
        <Routes>
          <Route path="/league/:leagueId/picks" element={<LeaguePicksRouter />} />
          <Route path="*" element={<CurrentPath />} />
        </Routes>
      </MemoryRouter>
    );

    expect(screen.getByTestId("path")).toHaveTextContent(
      `/app/league/${LEAGUE_ID}/picks/phase/regular/weeks/6`
    );
  });

  it("stops waiting after the bounded retries and uses the existing fallback", () => {
    userState.userDto = { leagues: [playerLeague([])] };
    renderAt(`/league/${LEAGUE_ID}/picks`);

    act(() => vi.advanceTimersByTime(10_000));

    expect(refreshSpy).toHaveBeenCalledTimes(10);
    expect(screen.getByTestId("path")).toHaveTextContent(
      `/app/league/${LEAGUE_ID}/picks/phase/regular/weeks/1`
    );
  });
});

describe("LeaguePicksRouter, wait is per league", () => {
  it("a second new league still waits after the first exhausted its retries", () => {
    // One mounted router whose league changes (as in the app, where both
    // URLs render the same LeaguePicksRouter instance), so per-instance
    // state carries over unless it's keyed by league.
    const OTHER_ID = "11111111-2222-3333-4444-555555555555";
    userState.userDto = {
      leagues: [playerLeague([]), { ...playerLeague([]), id: OTHER_ID }],
    };
    function GoToOther() {
      const navigate = useNavigate();
      return (
        <button type="button" onClick={() => navigate(`/app/league/${OTHER_ID}/picks`)}>
          other
        </button>
      );
    }
    render(
      <MemoryRouter initialEntries={[`/app/league/${LEAGUE_ID}/picks`]}>
        <GoToOther />
        <Routes>
          <Route path="/app/league/:leagueId/picks" element={<LeaguePicksRouter />} />
          <Route
            path="/app/league/:leagueId/picks/phase/:phase/weeks/:week"
            element={<LeaguePicksRouter />}
          />
        </Routes>
      </MemoryRouter>
    );
    act(() => vi.advanceTimersByTime(10_000)); // first league gives up
    expect(screen.getByText("roster builder")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "other" }));

    expect(screen.getByText("Setting up your league...")).toBeInTheDocument();
  });
});

describe("LeaguePicksRouter, Player Pick'em week selection", () => {
  const twoWeeks = () => ({
    ...playerLeague([
      { seasonWeekId: "sw-6", week: 6, phase: "regular" },
      { seasonWeekId: "sw-7", week: 7, phase: "regular" },
    ]),
    currentSeasonWeekId: "sw-7",
  });

  it("stays on a league week the user picked, even when it isn't the current week", () => {
    userState.userDto = { leagues: [twoWeeks()] };
    renderAt(`/league/${LEAGUE_ID}/picks/phase/regular/weeks/6`);

    expect(screen.getByText("roster builder")).toBeInTheDocument();
  });

  it("redirects a week outside the league to its current week", () => {
    userState.userDto = { leagues: [twoWeeks()] };
    renderAt(`/league/${LEAGUE_ID}/picks/phase/regular/weeks/3`);

    expect(screen.getByTestId("path")).toHaveTextContent(
      `/app/league/${LEAGUE_ID}/picks/phase/regular/weeks/7`
    );
  });
});
