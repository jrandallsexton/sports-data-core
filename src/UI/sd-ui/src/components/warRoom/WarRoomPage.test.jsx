import { render, screen, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import WarRoomPage from "./WarRoomPage";

// Panels are stubbed: this file tests the tab shell, not the widgets.
vi.mock("./SplitsPanel", () => ({ default: () => <div>splits-panel</div> }));
vi.mock("./LeadersPanel", () => ({ default: () => <div>leaders-panel</div> }));
vi.mock("./FranchiseMetricsGrid", () => ({ default: () => <div>metrics-panel</div> }));

const renderAt = (url = "/warroom") =>
  render(
    <MemoryRouter initialEntries={[url]}>
      <WarRoomPage />
    </MemoryRouter>
  );

describe("WarRoomPage tabs", () => {
  it("opens on Splits and mounts only the visited panel", () => {
    renderAt();

    expect(screen.getByRole("tab", { name: "Splits" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByText("splits-panel")).toBeVisible();
    expect(screen.queryByText("leaders-panel")).not.toBeInTheDocument();
    expect(screen.queryByText("metrics-panel")).not.toBeInTheDocument();
  });

  it("honors ?tab= and falls back to Splits for an unknown tab", () => {
    const { unmount } = renderAt("/warroom?tab=metrics");
    expect(screen.getByRole("tab", { name: "Franchise Season Metrics" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByText("metrics-panel")).toBeVisible();
    unmount();

    renderAt("/warroom?tab=nope");
    expect(screen.getByRole("tab", { name: "Splits" })).toHaveAttribute("aria-selected", "true");
  });

  it("switches tabs and keeps a visited panel mounted but hidden", () => {
    renderAt();

    fireEvent.click(screen.getByRole("tab", { name: "Season Leaders" }));

    expect(screen.getByText("leaders-panel")).toBeVisible();
    expect(screen.getByText("splits-panel")).not.toBeVisible();
  });

  it("moves between tabs with the arrow keys", () => {
    renderAt();

    fireEvent.keyDown(screen.getByRole("tab", { name: "Splits" }), { key: "ArrowLeft" });

    expect(screen.getByRole("tab", { name: "Franchise Season Metrics" })).toHaveAttribute("aria-selected", "true");
  });
});
