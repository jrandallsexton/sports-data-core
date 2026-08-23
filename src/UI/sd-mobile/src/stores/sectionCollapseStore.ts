import { create } from 'zustand';
import AsyncStorage from '@react-native-async-storage/async-storage';

const KEY_PREFIX = 'section-collapsed:';

/** Keys with a storage read in flight — see hydrate(). */
const hydrating = new Set<string>();

interface SectionCollapseState {
  /** Collapsed flags by section key. Absent = expanded (the default). */
  collapsed: Record<string, boolean>;
  hydrated: boolean;
  hydrate: (sectionKey: string) => void;
  toggle: (sectionKey: string) => void;
}

/**
 * Collapse state for named content sections, SHARED across every component
 * instance and persisted per device.
 *
 * Shared is the load-bearing word. Every MatchupCard in a list renders its
 * own comparison modal eagerly (visible={false} until opened), so per-instance
 * state meant each modal read storage once at list-render time — before any
 * toggle had happened — and never heard about a later write. Collapsing a
 * section on one game then opening another showed it expanded again. A single
 * store means every mounted modal reflects the change immediately.
 *
 * Sections start EXPANDED: the card surfaces context without being asked, so
 * collapsing is an escape valve, never the starting state. Reads and writes
 * both degrade toward showing content.
 */
export const useSectionCollapseStore = create<SectionCollapseState>((set, get) => ({
  collapsed: {},
  hydrated: false,

  hydrate: (sectionKey: string) => {
    // Only the first mount of a given key touches storage; after that the
    // store IS the source of truth for this session. The in-flight guard
    // matters as much as the resolved one: a slate mounts dozens of cards in
    // the same tick, all before the first read resolves, so without it every
    // card would issue its own redundant read of the same key.
    if (Object.prototype.hasOwnProperty.call(get().collapsed, sectionKey)) return;
    if (hydrating.has(sectionKey)) return;
    hydrating.add(sectionKey);

    AsyncStorage.getItem(KEY_PREFIX + sectionKey)
      .then((v) => {
        hydrating.delete(sectionKey);
        set((state) =>
          // A toggle may have landed while the read was in flight — never
          // clobber a value the user just chose.
          Object.prototype.hasOwnProperty.call(state.collapsed, sectionKey)
            ? state
            : { collapsed: { ...state.collapsed, [sectionKey]: v === 'true' }, hydrated: true });
      })
      .catch(() => {
        hydrating.delete(sectionKey);
        set((state) =>
          Object.prototype.hasOwnProperty.call(state.collapsed, sectionKey)
            ? state
            : { collapsed: { ...state.collapsed, [sectionKey]: false }, hydrated: true });
      });
  },

  toggle: (sectionKey: string) => {
    const next = !(get().collapsed[sectionKey] ?? false);
    set((state) => ({ collapsed: { ...state.collapsed, [sectionKey]: next } }));

    // Fire-and-forget: a failed write only means the section returns expanded
    // next launch, which is the default anyway.
    AsyncStorage.setItem(KEY_PREFIX + sectionKey, next ? 'true' : 'false').catch(() => {});
  },
}));
