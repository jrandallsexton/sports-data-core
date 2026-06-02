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

const originalWarn = console.warn;
console.warn = (...args: unknown[]) => {
  const first = args[0];
  if (
    typeof first === 'string' &&
    SUPPRESSED_WARNING_FRAGMENTS.some((fragment) => first.includes(fragment))
  ) {
    return;
  }
  originalWarn(...args);
};
