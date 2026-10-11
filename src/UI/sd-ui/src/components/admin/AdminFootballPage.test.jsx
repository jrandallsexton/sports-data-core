import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import AdminFootballPage from "./AdminFootballPage";
import apiWrapper from "../../api/apiWrapper";

const CONTEST_ID = "98035135-931b-34a2-1f9e-be42e1647453";

vi.mock("../../api/apiWrapper", () => ({
  default: {
    Admin: {
      getFootballMatchupForContest: vi.fn(),
      replayFootballContest: vi.fn(),
    },
  },
}));

vi.mock("../../contexts/ContestUpdatesContext", () => ({
  useContestUpdates: () => ({ getContestUpdate: () => null }),
}));

vi.mock("react-hot-toast", () => ({ default: { success: vi.fn(), error: vi.fn() } }));
vi.mock("./AdminHeader", () => ({ default: () => <div>stub:header</div> }));
vi.mock("./signalr-debug/FootballDebugCard", () => ({ default: () => <div>stub:debug</div> }));
vi.mock("../matchups/MatchupCard", () => ({ default: () => <div>stub:card</div> }));

function startReplay(interval) {
  render(<AdminFootballPage />);
  fireEvent.change(screen.getByLabelText("Seconds between plays:"), { target: { value: interval } });
  fireEvent.click(screen.getByRole("button", { name: "Start replay" }));
}

describe("AdminFootballPage replay interval", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("admin.football.debugContestId", CONTEST_ID);
    apiWrapper.Admin.getFootballMatchupForContest.mockResolvedValue({ data: {} });
    apiWrapper.Admin.replayFootballContest.mockResolvedValue({});
  });

  afterEach(() => vi.clearAllMocks());

  it("sends the chosen seconds between plays and remembers them", async () => {
    startReplay("5");

    await waitFor(() =>
      expect(apiWrapper.Admin.replayFootballContest).toHaveBeenCalledWith(CONTEST_ID, "ncaa", 5)
    );
    expect(localStorage.getItem("admin.football.replayIntervalSeconds")).toBe("5");
  });

  it("keeps the interval within 1 to 60 seconds", async () => {
    startReplay("0");
    await waitFor(() =>
      expect(apiWrapper.Admin.replayFootballContest).toHaveBeenLastCalledWith(CONTEST_ID, "ncaa", 1)
    );

    const button = await screen.findByRole("button", { name: "Start replay" });
    await waitFor(() => expect(button).not.toBeDisabled());
    fireEvent.change(screen.getByLabelText("Seconds between plays:"), { target: { value: "500" } });
    fireEvent.click(button);
    await waitFor(() =>
      expect(apiWrapper.Admin.replayFootballContest).toHaveBeenLastCalledWith(CONTEST_ID, "ncaa", 60)
    );
  });
});
