import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import LeagueCreatePage from "./LeagueCreatePage";

// Division labels exactly as /ui/conferences returns them (checked against
// production 2026-09-28): "FBS", not "FBS (I-A)". Hoisted: the vi.mock
// factories below run before module-level constants initialize.
const { createNcaaSpy, toastError, CONFERENCES } = vi.hoisted(() => ({
  createNcaaSpy: vi.fn(),
  toastError: vi.fn(),
  CONFERENCES: [
    { slug: "sec", shortName: "SEC", division: "FBS" },
    { slug: "big-ten", shortName: "Big Ten", division: "FBS" },
    { slug: "fbs-indep", shortName: "FBS Indep.", division: "FBS" },
    { slug: "big-sky", shortName: "Big Sky", division: "FCS" },
    { slug: "gulf-south", shortName: "Gulf South", division: "NCAA Division II" },
  ],
}));
const FBS_SLUGS = ["sec", "big-ten", "fbs-indep"];

vi.mock("../../api/apiWrapper.js", () => ({
  default: {
    Conferences: {
      getConferenceNamesAndSlugs: vi.fn().mockResolvedValue({ data: CONFERENCES }),
    },
  },
}));

vi.mock("api/leagues/leaguesApi", () => ({
  default: {
    getSeasonWeeks: vi.fn().mockResolvedValue({ data: [] }),
    createFootballNcaaLeague: createNcaaSpy,
    createFootballNflLeague: vi.fn(),
    createBaseballMlbLeague: vi.fn(),
  },
}));

vi.mock("../../contexts/UserContext", () => ({
  useUserDto: () => ({
    userDto: { isAdmin: false },
    loading: false,
    refreshUserDto: vi.fn().mockResolvedValue(undefined),
  }),
}));

vi.mock("../../utils/leagueCreationGates", async (importOriginal) => ({
  ...(await importOriginal()),
  getLeagueCreationGates: vi.fn().mockResolvedValue({}),
}));

vi.mock("react-hot-toast", () => ({
  default: { error: toastError, success: vi.fn() },
}));

const renderPage = async () => {
  render(
    <MemoryRouter initialEntries={["/app/league/create?sport=FootballNcaa"]}>
      <LeagueCreatePage />
    </MemoryRouter>
  );
  // Conferences load asynchronously; SEC appearing means the list is in.
  await screen.findByLabelText("SEC");
};

// Through the real button, so the form's required-field validation runs:
// fill the two required fields first.
const submit = () => {
  fireEvent.change(screen.getByLabelText("League Name"), { target: { value: "All FBS" } });
  fireEvent.change(screen.getByLabelText("Pick Type"), { target: { value: "StraightUp" } });
  fireEvent.click(screen.getByRole("button", { name: "Create League" }));
};

const confirmCreate = () =>
  fireEvent.click(screen.getByRole("button", { name: "Confirm & Create" }));

const fbsOnlyCheckbox = () => screen.getByLabelText("FBS Only (I-A)");

beforeEach(() => {
  createNcaaSpy.mockReset();
  createNcaaSpy.mockResolvedValue({ id: "league-1" });
  toastError.mockReset();
});

describe("LeagueCreatePage NCAA conference scope", () => {
  it("lists only FBS conferences while FBS Only is checked (the division label is 'FBS')", async () => {
    await renderPage();

    expect(fbsOnlyCheckbox()).toBeChecked();
    expect(screen.getByLabelText("SEC")).toBeInTheDocument();
    expect(screen.getByLabelText("FBS Indep.")).toBeInTheDocument();
    expect(screen.queryByLabelText("Big Sky")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Gulf South")).not.toBeInTheDocument();
  });

  it("FBS Only with no ranking and no conferences creates the league with every FBS conference", async () => {
    await renderPage();

    expect(screen.getByText(/this league will include every game\s+with an FBS team/)).toBeInTheDocument();

    submit();

    expect(toastError).not.toHaveBeenCalled();
    expect(screen.getByRole("heading", { name: "Confirm League Settings" })).toBeInTheDocument();
    expect(screen.getByText("All FBS conferences (every game with an FBS team)")).toBeInTheDocument();

    confirmCreate();

    await waitFor(() => expect(createNcaaSpy).toHaveBeenCalledTimes(1));
    const payload = createNcaaSpy.mock.calls[0][0];
    expect([...payload.conferenceSlugs].sort()).toEqual([...FBS_SLUGS].sort());
    expect(payload.rankingFilter).toBeNull();
  });

  it("with FBS Only unchecked, no ranking and no conferences is still refused", async () => {
    await renderPage();

    fireEvent.click(fbsOnlyCheckbox());
    submit();

    expect(toastError).toHaveBeenCalledWith("Choose a ranking filter or at least one conference.");
    expect(screen.queryByRole("heading", { name: "Confirm League Settings" })).not.toBeInTheDocument();
  });

  it("an explicit conference pick is sent as-is, not expanded to all FBS", async () => {
    await renderPage();

    fireEvent.click(screen.getByLabelText("SEC"));
    expect(screen.queryByText(/this league will include every game/)).not.toBeInTheDocument();
    submit();
    confirmCreate();

    await waitFor(() => expect(createNcaaSpy).toHaveBeenCalledTimes(1));
    expect(createNcaaSpy.mock.calls[0][0].conferenceSlugs).toEqual(["sec"]);
  });

  it("re-checking FBS Only drops non-FBS conferences picked while it was off", async () => {
    await renderPage();

    fireEvent.click(fbsOnlyCheckbox()); // off: every division listed
    fireEvent.click(screen.getByLabelText("Big Sky"));
    fireEvent.click(fbsOnlyCheckbox()); // on again: Big Sky hidden AND deselected

    submit();
    confirmCreate();

    await waitFor(() => expect(createNcaaSpy).toHaveBeenCalledTimes(1));
    const slugs = createNcaaSpy.mock.calls[0][0].conferenceSlugs;
    expect(slugs).not.toContain("big-sky");
    expect([...slugs].sort()).toEqual([...FBS_SLUGS].sort());
  });
});
