import { useCallback, useEffect } from 'react';
import { useSectionCollapseStore } from '@/src/stores/sectionCollapseStore';

/**
 * Whether a named content section is collapsed, plus a toggle.
 *
 * Backed by a shared store rather than local state — see
 * sectionCollapseStore for why that matters: every MatchupCard mounts its
 * comparison modal eagerly, so per-instance state went stale the moment one
 * of them toggled.
 *
 * Sections start EXPANDED unless defaultCollapsed is set. Collapsing is an
 * escape valve for readers who find a section noisy, not a gate in front of
 * the content - except for long indexes (stacked stats categories), which
 * start collapsed so the tab opens as a scannable list of headers.
 */
export function useSectionCollapse(sectionKey: string, defaultCollapsed = false) {
  const collapsed = useSectionCollapseStore((s) => s.collapsed[sectionKey] ?? defaultCollapsed);
  const hydrate = useSectionCollapseStore((s) => s.hydrate);
  const toggleSection = useSectionCollapseStore((s) => s.toggle);

  useEffect(() => {
    hydrate(sectionKey, defaultCollapsed);
  }, [hydrate, sectionKey, defaultCollapsed]);

  const toggle = useCallback(
    () => toggleSection(sectionKey, defaultCollapsed),
    [toggleSection, sectionKey, defaultCollapsed],
  );

  return { collapsed, toggle };
}
