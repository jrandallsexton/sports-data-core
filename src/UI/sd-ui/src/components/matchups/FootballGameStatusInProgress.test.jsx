import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import FootballGameStatusInProgress from "./FootballGameStatusInProgress";

const items = [
  { id: "a", factType: "SeasonCount", text: "T. Cooley's second interception of the season." },
  { id: "b", factType: "GameCount", text: "T. Cooley's first interception of the game." },
  { id: "c", factType: "TeamGameCount", text: "LSU's first takeaway of the game." },
];

function renderStatus(props) {
  return render(
    <MemoryRouter>
      <FootballGameStatusInProgress
        awayShort="MCN"
        homeShort="LSU"
        period="Q1"
        clock="12:38"
        lastPlayDescription="J.Strong pass intercepted by T.Cooley"
        {...props}
      />
    </MemoryRouter>
  );
}

describe("FootballGameStatusInProgress instant analysis", () => {
  it("shows the first facts under the last play, in order", () => {
    renderStatus({ playAnalysis: items });

    const block = screen.getByTestId("play-analysis");
    expect(block).toHaveTextContent("T. Cooley's second interception of the season.");
    expect(block).toHaveTextContent("T. Cooley's first interception of the game.");
    expect(block).not.toHaveTextContent("LSU's first takeaway of the game.");
    expect(block.textContent.indexOf("second interception")).toBeLessThan(block.textContent.indexOf("first interception"));
  });

  it("shows nothing without facts", () => {
    renderStatus({ playAnalysis: null });

    expect(screen.queryByTestId("play-analysis")).not.toBeInTheDocument();
  });
});
