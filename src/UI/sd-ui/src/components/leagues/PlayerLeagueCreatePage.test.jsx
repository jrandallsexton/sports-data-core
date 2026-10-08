import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import PlayerLeagueCreatePage from "./PlayerLeagueCreatePage";
import {
  toStartOfDayIso,
  toEndOfDayIso,
} from "api/leagues/requests/createLeagueRequests";

// Season calendar dated far in the future so no week counts as past.
// Hoisted: the vi.mock factories below run before module-level constants.
const { createPlayerSpy, WEEKS } = vi.hoisted(() => ({
  createPlayerSpy: vi.fn(),
  WEEKS: [
    {
      id: "sw-6",
      label: "Week 6",
      startDateUtc: "2099-10-04T06:00:00Z",
      endDateUtc: "2099-10-11T05:59:59Z",
    },
    {
      id: "sw-7",
      label: "Week 7",
      startDateUtc: "2099-10-11T06:00:00Z",
      endDateUtc: "2099-10-18T05:59:59Z",
    },
  ],
}));

vi.mock("api/leagues/leaguesApi", () => ({
  default: {
    getSeasonWeeks: vi.fn().mockResolvedValue({ weeks: WEEKS }),
    createPlayerLeague: createPlayerSpy,
  },
}));

vi.mock("../../contexts/UserContext", () => ({
  useUserDto: () => ({
    userDto: { isAdmin: true },
    loading: false,
    refreshUserDto: vi.fn().mockResolvedValue(true),
  }),
}));

const renderPage = () =>
  render(
    <MemoryRouter initialEntries={["/app/league/create/players"]}>
      <PlayerLeagueCreatePage />
    </MemoryRouter>
  );

const fillName = () =>
  fireEvent.change(screen.getByLabelText("League Name"), {
    target: { value: "Friday Night Rosters" },
  });

const submit = () =>
  fireEvent.click(screen.getByRole("button", { name: "Create League" }));

const chooseTab = (name) => fireEvent.click(screen.getByRole("tab", { name }));

beforeEach(() => {
  createPlayerSpy.mockReset();
  createPlayerSpy.mockResolvedValue({ id: "pl-1" });
});

describe("PlayerLeagueCreatePage League Window", () => {
  it("Full Season sends no bounds", async () => {
    renderPage();
    fillName();
    submit();

    await waitFor(() => expect(createPlayerSpy).toHaveBeenCalledTimes(1));
    expect(createPlayerSpy.mock.calls[0][0]).toMatchObject({ startsOn: null, endsOn: null });
  });

  it("Week Range sends the selected weeks' real UTC boundaries, raw", async () => {
    renderPage();
    fillName();
    chooseTab("Week Range");
    await screen.findAllByRole("option", { name: /Week 6/ });

    fireEvent.change(screen.getByLabelText("Start Week"), { target: { value: "sw-6" } });
    fireEvent.change(screen.getByLabelText("End Week"), { target: { value: "sw-7" } });
    submit();

    await waitFor(() => expect(createPlayerSpy).toHaveBeenCalledTimes(1));
    expect(createPlayerSpy.mock.calls[0][0]).toMatchObject({
      startsOn: "2099-10-04T06:00:00Z",
      endsOn: "2099-10-18T05:59:59Z",
    });
  });

  it("Week Range without both weeks is refused before any POST", async () => {
    renderPage();
    fillName();
    chooseTab("Week Range");
    submit();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Choose a start and end week for your league."
    );
    expect(createPlayerSpy).not.toHaveBeenCalled();
  });

  it("Date Range sends the user's local calendar days, like the team form", async () => {
    renderPage();
    fillName();
    chooseTab("Date Range");

    fireEvent.change(screen.getByLabelText("Start Date"), { target: { value: "2099-10-07" } });
    fireEvent.change(screen.getByLabelText("End Date"), { target: { value: "2099-10-11" } });
    submit();

    await waitFor(() => expect(createPlayerSpy).toHaveBeenCalledTimes(1));
    expect(createPlayerSpy.mock.calls[0][0]).toMatchObject({
      startsOn: toStartOfDayIso("2099-10-07"),
      endsOn: toEndOfDayIso("2099-10-11"),
    });
  });

  it("Date Range ending before it starts is refused before any POST", async () => {
    renderPage();
    fillName();
    chooseTab("Date Range");

    fireEvent.change(screen.getByLabelText("Start Date"), { target: { value: "2099-10-11" } });
    fireEvent.change(screen.getByLabelText("End Date"), { target: { value: "2099-10-07" } });
    // The End Date input's `min` makes the native picker refuse this, so a
    // button click never reaches the handler. Submit the form directly: the
    // keyboard / paste path that gets past `min` is what the guard covers.
    fireEvent.submit(screen.getByRole("button", { name: "Create League" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "End date must be on or after the start date."
    );
    expect(createPlayerSpy).not.toHaveBeenCalled();
  });
});
