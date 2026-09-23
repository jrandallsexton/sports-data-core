import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import TeamAdmin from "./TeamAdmin";

const { enrichSpy } = vi.hoisted(() => ({ enrichSpy: vi.fn() }));
vi.mock("../../api/apiWrapper", () => ({
  default: { FranchiseAdmin: { enrichFranchiseSeason: enrichSpy } },
}));

const props = { sport: "football", league: "ncaa", slug: "sam-houston-bearkats", seasonYear: 2026 };

beforeEach(() => {
  enrichSpy.mockReset();
});

describe("TeamAdmin", () => {
  it("posts the enrich request for the routed team and season, and shows the correlation id", async () => {
    enrichSpy.mockResolvedValue({
      data: {
        franchiseId: "f-1",
        franchiseSeasonId: "fs-1",
        seasonYear: 2026,
        correlationId: "c0ffee00-0000-0000-0000-000000000001",
      },
    });

    render(<TeamAdmin {...props} />);
    fireEvent.click(screen.getByRole("button", { name: "Enrich 2026 season" }));

    expect(enrichSpy).toHaveBeenCalledWith("football", "ncaa", "sam-houston-bearkats", 2026);
    expect(await screen.findByRole("status")).toBeInTheDocument();
    expect(screen.getByText("c0ffee00-0000-0000-0000-000000000001")).toBeInTheDocument();
    expect(screen.getByText("fs-1")).toBeInTheDocument();
  });

  it("disables the button while the request is in flight", async () => {
    let resolve;
    enrichSpy.mockReturnValue(new Promise((r) => { resolve = r; }));

    render(<TeamAdmin {...props} />);
    const button = screen.getByRole("button", { name: "Enrich 2026 season" });
    fireEvent.click(button);

    expect(screen.getByRole("button", { name: "Requesting…" })).toBeDisabled();
    resolve({ data: { correlationId: "x", franchiseSeasonId: "y" } });
    await waitFor(() => expect(screen.getByRole("button", { name: "Enrich 2026 season" })).toBeEnabled());
  });

  it("surfaces the server's status and validation message on failure", async () => {
    enrichSpy.mockRejectedValue({
      response: { status: 404, data: { errors: { SeasonYear: ["Season 2026 not found for franchise 'sam-houston-bearkats'"] } } },
    });

    render(<TeamAdmin {...props} />);
    fireEvent.click(screen.getByRole("button", { name: "Enrich 2026 season" }));

    expect(await screen.findByRole("alert")).toBeInTheDocument();
    expect(screen.getByRole("alert").textContent).toContain("404: Season 2026 not found");
  });

  it("renders errorMessage from ValidationFailure objects (the Result<T> failure shape)", async () => {
    enrichSpy.mockRejectedValue({
      response: {
        status: 500,
        data: { errors: [{ propertyName: "Enrichment", errorMessage: "Enrichment was partially enqueued and then failed. CorrelationId=abc." }] },
      },
    });

    render(<TeamAdmin {...props} />);
    fireEvent.click(screen.getByRole("button", { name: "Enrich 2026 season" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("500: Enrichment was partially enqueued and then failed. CorrelationId=abc.");
    expect(alert.textContent).not.toContain("[object Object]");
  });

  it("falls back to the error message when the failure has no response body", async () => {
    enrichSpy.mockRejectedValue(new Error("Network Error"));

    render(<TeamAdmin {...props} />);
    fireEvent.click(screen.getByRole("button", { name: "Enrich 2026 season" }));

    await waitFor(() => expect(screen.getByRole("alert").textContent).toContain("Network Error"));
  });
});
