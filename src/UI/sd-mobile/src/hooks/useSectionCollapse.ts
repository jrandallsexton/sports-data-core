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
 * Sections start EXPANDED. Collapsing is an escape valve for readers who
 * find a section noisy, not a gate in front of the content.
 */
export function useSectionCollapse(sectionKey: string) {
  const collapsed = useSectionCollapseStore((s) => s.collapsed[sectionKey] ?? false);
  const hydrate = useSectionCollapseStore((s) => s.hydrate);
  const toggleSection = useSectionCollapseStore((s) => s.toggle);

  useEffect(() => {
    hydrate(sectionKey);
  }, [hydrate, sectionKey]);

  const toggle = useCallback(() => toggleSection(sectionKey), [toggleSection, sectionKey]);

  return { collapsed, toggle };
}
