import { useCallback, useEffect, useState } from "react";

const KEY_PREFIX = "section-collapsed:";

/**
 * Storage disabled (private mode, blocked cookies) degrades toward SHOWING
 * content — expanded is what the product promises, so a failure must not
 * hide anything.
 */
function readCollapsed(sectionKey) {
  try {
    return window.localStorage.getItem(KEY_PREFIX + sectionKey) === "true";
  } catch {
    return false;
  }
}

/**
 * Remembers whether a named content section is collapsed, per device.
 *
 * Sections start EXPANDED. The comparison dialog exists to surface context
 * without the user having to ask for it, so hiding content by default would
 * invert that; collapsing is an escape valve for readers who find a section
 * noisy, not the starting state.
 *
 * Persistence is the point. A collapse that resets when the dialog reopens
 * would have to be redone on every matchup, every week — more irritating
 * than the scrolling it was meant to fix.
 *
 * Device-local on purpose: no round trip, works offline, and it is a display
 * preference rather than account data. If this graduates to a synced user
 * setting later, this hook is the single place that changes.
 *
 * Mobile parity: sd-mobile/src/hooks/useSectionCollapse.ts — keep the storage
 * key shape in step so the two surfaces stay conceptually identical.
 */
export function useSectionCollapse(sectionKey) {
  // Read during initialization rather than in an effect. localStorage is
  // synchronous, so this renders the correct state on the first paint with no
  // expanded-then-collapsed flicker — and, more importantly, it cannot go
  // stale. An effect that runs only on mount was the mobile bug: the dialog
  // there is rendered eagerly by every card, so each instance read once at
  // list-render time and never saw a later toggle. Web mounts this dialog on
  // open today, which masks the problem; reading here keeps it correct even
  // if that ever changes.
  const [collapsed, setCollapsed] = useState(() => readCollapsed(sectionKey));

  // Re-read if the component is reused for a different section.
  useEffect(() => {
    setCollapsed(readCollapsed(sectionKey));
  }, [sectionKey]);

  const toggle = useCallback(() => {
    setCollapsed((prev) => {
      const next = !prev;
      try {
        window.localStorage.setItem(KEY_PREFIX + sectionKey, next ? "true" : "false");
      } catch {
        // A failed write only means the section returns expanded next visit,
        // which is the default anyway.
      }
      return next;
    });
  }, [sectionKey]);

  return { collapsed, toggle };
}
