import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import AdminInstantAnalysisLabPage from "./AdminInstantAnalysisLabPage";
import apiWrapper from "../../api/apiWrapper";

const CONTEST_ID = "98035135-931b-34a2-1f9e-be42e1647453";
const RUN_ID = "11111111-2222-3333-4444-555555555555";

const summary = {
  runId: RUN_ID,
  contestId: CONTEST_ID,
  competitionId: "c",
  playCount: 3,
  factCount: 1,
  droppedCount: 1,
  templateVersion: 1,
  createdUtc: "2026-10-10T18:00:00Z",
  completedUtc: "2026-10-10T18:00:02Z",
};

const detail = {
  run: summary,
  plays: [
    { playId: "p1", sequenceNumber: "1", typeId: "5", text: "rush for 2", facts: [], dropped: [] },
    {
      playId: "p2",
      sequenceNumber: "8",
      typeId: "26",
      text: "pass intercepted by #0 T.Cooley",
      facts: [{ id: "f1", ordinal: 0, factType: "GameCount", text: "T. Cooley's first interception of the game.", priority: 40, facts: "{}", stars: null }],
      dropped: [{ factType: "SeasonCount", kind: "Interception", athleteSeasonId: null, reason: "No season document written before kickoff." }],
    },
  ],
};

vi.mock("../../api/apiWrapper", () => ({
  default: {
    Admin: {
      runInstantAnalysisLab: vi.fn(),
      getInstantAnalysisLabRuns: vi.fn(),
      getInstantAnalysisLabRun: vi.fn(),
      rateInstantAnalysisFact: vi.fn(),
    },
  },
}));

vi.mock("./AdminHeader", () => ({ default: () => <div>stub:header</div> }));

function renderPage() {
  return render(
    <MemoryRouter>
      <AdminInstantAnalysisLabPage />
    </MemoryRouter>
  );
}

describe("AdminInstantAnalysisLabPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    apiWrapper.Admin.runInstantAnalysisLab.mockResolvedValue({ data: summary });
    apiWrapper.Admin.getInstantAnalysisLabRun.mockResolvedValue({ data: detail });
    apiWrapper.Admin.rateInstantAnalysisFact.mockResolvedValue({ data: { playAnalysisId: "f1", stars: 3 } });
  });

  it("runs a lab replay and shows facts and dropped facts, hiding plays with neither", async () => {
    renderPage();

    fireEvent.change(screen.getByLabelText("Contest id"), { target: { value: CONTEST_ID } });
    fireEvent.click(screen.getByText("Run lab replay"));

    expect(await screen.findByText("T. Cooley's first interception of the game.")).toBeInTheDocument();
    expect(screen.getByText(/No season document written before kickoff\./)).toBeInTheDocument();
    expect(screen.queryByText("rush for 2")).not.toBeInTheDocument();
    expect(apiWrapper.Admin.runInstantAnalysisLab).toHaveBeenCalledWith("ncaa", CONTEST_ID);
    expect(apiWrapper.Admin.getInstantAnalysisLabRun).toHaveBeenCalledWith("ncaa", RUN_ID);
  });

  it("rates a fact", async () => {
    renderPage();

    fireEvent.change(screen.getByLabelText("Contest id"), { target: { value: CONTEST_ID } });
    fireEvent.click(screen.getByText("Run lab replay"));
    await screen.findByText("T. Cooley's first interception of the game.");

    fireEvent.click(screen.getByLabelText("Rate 3 stars"));

    await waitFor(() =>
      expect(apiWrapper.Admin.rateInstantAnalysisFact).toHaveBeenCalledWith("ncaa", "f1", 3)
    );
  });
});
