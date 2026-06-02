// Suppress known third-party deprecation noise from the dev console.
// Loaded as a side-effect import in app/_layout.tsx BEFORE Sentry's
// console instrumentation kicks in so the filtered messages never
// become breadcrumbs (otherwise each suppressed warn still wastes a
// Sentry breadcrumb slot and adds 6+ frames of instrumentation stack
// to the terminal output).
//
// Each entry MUST be a verifiable, harmless upstream warning that we
// cannot fix in our own code. Add the source library + version + a
// removal trigger so future maintainers can prune the list when
// upstream actually fixes it.

const SUPPRESSED_WARNING_FRAGMENTS = [
  // @react-navigation/elements (currently 2.9.18, pulled in by
  // expo-router) still passes pointerEvents as a top-level prop on
  // <View> in Screen.tsx, ResourceSavingView.tsx, Header.tsx, and
  // HeaderSearchBar.tsx. react-native-web 0.19+ deprecated this in
  // favor of style.pointerEvents. Fires on every screen render, so
  // SSR-bundled web reloads get flooded with otherwise-harmless
  // warnings. Remove this entry once @react-navigation/elements drops
  // the prop form.
  'props.pointerEvents is deprecated',

  // react-native-web 0.19+ deprecated shadow* style props
  // (shadowColor, shadowOffset, shadowOpacity, shadowRadius) in favor
  // of boxShadow on web. shadow* remains the correct styling on
  // native iOS/Android — RN-Web translates internally and just emits
  // the deprecation, so functionality is unaffected. Fires from both
  // @react-navigation/elements (Header / HeaderBackground) and from
  // our own shadowed surfaces (MatchupCard, Card, sign-in screen).
  // Rewriting our styles to Platform.select between shadow* and
  // boxShadow would scatter conditional logic for a purely cosmetic
  // web warning, so we silence at the source. Remove this entry once
  // either upstream stops emitting the warning or React Native's
  // shadow primitive unifies with web's boxShadow.
  '"shadow*" style props are deprecated',
];

// Dev-only. The deprecation warnings we're filtering are emitted by
// react-native-web's warnOnce, which itself only fires under __DEV__,
// so the shim accomplishes nothing in production builds. Skipping the
// patch in prod avoids the per-call overhead and eliminates the
// (small) risk of silently swallowing a real production warn that
// happens to overlap a suppressed fragment.
if (__DEV__) {
  const originalWarn = console.warn;
  console.warn = (...args: unknown[]) => {
    // Scan every string-typed arg, not just args[0]. RN-Web's warnOnce
    // (the source of our current suppressed warnings) passes the
    // message as a single string, but React's own warnings use the
    // format-string pattern console.warn('Warning: %s', detail) — so
    // a future suppression target's substantive text could live at
    // args[1+]. Scanning all string args keeps the filter robust
    // without affecting current cases.
    const matchesSuppressed = args.some(
      (arg) =>
        typeof arg === 'string' &&
        SUPPRESSED_WARNING_FRAGMENTS.some((fragment) => arg.includes(fragment)),
    );
    if (matchesSuppressed) {
      return;
    }
    originalWarn(...args);
  };
}
