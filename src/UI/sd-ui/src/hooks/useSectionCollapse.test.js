import { describe, it, expect, beforeEach, vi } from "vitest";
import { act, renderHook } from "@testing-library/react";
import { useSectionCollapse } from "./useSectionCollapse";

describe("useSectionCollapse", () => {
  beforeEach(() => {
    window.localStorage.clear();
    vi.restoreAllMocks();
  });

  it("starts expanded when nothing has been stored", () => {
    // The dialog surfaces context without being asked, so a section the user
    // has never touched must not be hidden.
    const { result } = renderHook(() => useSectionCollapse("history.headToHead"));
    expect(result.current.collapsed).toBe(false);
  });

  it("restores a previous collapse so the choice survives reopening the dialog", () => {
    // This is the whole point: a toggle that reset each time would have to be
    // redone on every matchup, every week.
    window.localStorage.setItem("section-collapsed:history.lastSeason", "true");

    const { result } = renderHook(() => useSectionCollapse("history.lastSeason"));
    expect(result.current.collapsed).toBe(true);
  });

  it("persists the new state on toggle", () => {
    const { result } = renderHook(() => useSectionCollapse("history.headToHead"));

    act(() => result.current.toggle());
    expect(result.current.collapsed).toBe(true);
    expect(window.localStorage.getItem("section-collapsed:history.headToHead")).toBe("true");

    act(() => result.current.toggle());
    expect(result.current.collapsed).toBe(false);
    expect(window.localStorage.getItem("section-collapsed:history.headToHead")).toBe("false");
  });

  it("keys sections separately so collapsing one leaves the other alone", () => {
    window.localStorage.setItem("section-collapsed:history.lastSeason", "true");

    const { result: h2hResult } = renderHook(() =>
      useSectionCollapse("history.headToHead"));
    const { result: lastSeasonResult } = renderHook(() =>
      useSectionCollapse("history.lastSeason"));

    expect(lastSeasonResult.current.collapsed).toBe(true);
    expect(h2hResult.current.collapsed).toBe(false);
  });

  it("falls back to expanded when storage is unavailable", () => {
    // Private mode / blocked storage must not hide content — degrade toward
    // showing more, not less.
    vi.spyOn(window.localStorage, "getItem").mockImplementation(() => {
      throw new Error("storage disabled");
    });

    const { result } = renderHook(() => useSectionCollapse("history.headToHead"));
    expect(result.current.collapsed).toBe(false);
  });

  it("does not throw when a write is rejected", () => {
    vi.spyOn(window.localStorage, "setItem").mockImplementation(() => {
      throw new Error("quota exceeded");
    });

    const { result } = renderHook(() => useSectionCollapse("history.headToHead"));

    expect(() => act(() => result.current.toggle())).not.toThrow();
    // The in-memory state still flips, so the current session behaves.
    expect(result.current.collapsed).toBe(true);
  });
});
