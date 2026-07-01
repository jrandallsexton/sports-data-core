// Suppress known third-party dev-console deprecations BEFORE Sentry's
// console instrumentation hooks console.warn — otherwise each filtered
// warning still becomes a Sentry breadcrumb. See the file's header
// comment for the suppression list and removal triggers.
import '@/src/lib/silenceKnownWarnings';

// Sentry MUST initialize before any other module that could throw at
// import time (Firebase, React Query, navigation). Side-effect import
// kept on its own line, above all other imports, for that reason.
import '@/src/lib/sentry';

import { DarkTheme, DefaultTheme, ThemeProvider as NavThemeProvider } from '@react-navigation/native';
import { useFonts } from 'expo-font';
import { Poppins_400Regular, Poppins_700Bold_Italic } from '@expo-google-fonts/poppins';
import { Stack, useRouter, useSegments } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import * as Notifications from 'expo-notifications';
// Type-only import — erased at compile time. The runtime module is loaded via a
// gated dynamic import below so it never enters the web bundle (RNFirebase has
// no web implementation).
import { type FirebaseMessagingTypes } from '@react-native-firebase/messaging';
import { useEffect, useRef, useState } from 'react';
import { AppState, Platform } from 'react-native';
import { QueryClientProvider } from '@tanstack/react-query';
import * as Sentry from '@sentry/react-native';
import 'react-native-reanimated';

import { queryClient } from '@/src/lib/queryClient';
import { useAuthInit, useAuth } from '@/src/hooks/useAuth';
import { useRegisterPushDevice } from '@/src/hooks/useRegisterPushDevice';
import { ThemeProvider, useThemeMode } from '@/src/lib/theme/ThemeContext';
import { TextSizeProvider, useTextSize } from '@/src/lib/textSize/TextSizeContext';
import { SignalRGate } from '@/src/components/signalR/SignalRGate';

export {
  // Catch any errors thrown by the Layout component.
  ErrorBoundary,
} from 'expo-router';

export const unstable_settings = {
  initialRouteName: '(tabs)',
};

SplashScreen.preventAutoHideAsync();

// Notification handler — controls what happens when a push arrives
// while the app is in the foreground. iOS does not show foreground
// notifications by default; this config opts in to a banner + sound
// so developer testing can see notifications fire without having to
// background the app first. Set at module level so it's registered
// before any push could arrive.
//
// Web no-op: expo-notifications' web implementation doesn't support
// most of this API surface (no APNs / FCM equivalent in-browser), and
// calling setNotificationHandler / useLastNotificationResponse / the
// listeners on web throws UnavailabilityError. Gate the entire push
// surface on native and let the web bundle skip it cleanly.
if (Platform.OS !== 'web') {
  Notifications.setNotificationHandler({
    handleNotification: async () => ({
      shouldPlaySound: true,
      shouldSetBadge: false,
      shouldShowBanner: true,
      shouldShowList: true,
    }),
  });
}

/**
 * Watches auth state and redirects between (auth) and (tabs) groups.
 * Must render inside the navigator so useSegments/useRouter work.
 */
function AuthGuard() {
  const { user, isInitialized } = useAuth();
  const segments = useSegments();
  const router = useRouter();

  useEffect(() => {
    if (!isInitialized) return;

    const inAuthGroup = segments[0] === '(auth)';
    if (!user && !inAuthGroup) {
      // Cast — Expo Router's typed-routes generator hasn't picked up the
      // new welcome.tsx file yet at typecheck time; regenerates on next
      // expo start / build.
      router.replace('/(auth)/welcome' as never);
    } else if (user && inAuthGroup) {
      router.replace('/(tabs)');
    }
  }, [user, isInitialized, segments]);

  return null;
}

function RootLayout() {
  const [loaded, error] = useFonts({
    SpaceMono: require('../assets/fonts/SpaceMono-Regular.ttf'),
    Poppins_400Regular,
    Poppins_700Bold_Italic,
  });

  // Kick off Firebase auth listener immediately.
  useAuthInit();

  // Tag every Sentry event with the signed-in user (or clear on sign-out).
  // The store is the source of truth — useAuthInit already syncs Firebase
  // auth state into it, so this just mirrors that state into Sentry.
  const { user } = useAuth();
  useEffect(() => {
    if (user) {
      Sentry.setUser({ id: user.uid, email: user.email ?? undefined });
    } else {
      Sentry.setUser(null);
    }
  }, [user]);

  useEffect(() => {
    if (error) throw error;
  }, [error]);

  // Push notification surface is native-only — see NativePushDiagnostics
  // below. The component is rendered conditionally so the hooks it uses
  // (useLastNotificationResponse + addNotification*Listener under the
  // hood) never execute on web, where expo-notifications' implementation
  // throws UnavailabilityError.

  // Splash stays up until fonts load; the ThemeProvider's hydration state
  // then takes over (see RootLayoutNav) so users never see a system-resolved
  // flash before their persisted preference applies.
  if (!loaded) return null;

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <TextSizeProvider>
          <RootLayoutNav />
        </TextSizeProvider>
      </ThemeProvider>
    </QueryClientProvider>
  );
}

// Wrap with Sentry so navigation transitions become breadcrumbs and the
// root error boundary forwards into the SDK.
export default Sentry.wrap(RootLayout);

/**
 * Native-only push notification receive + tap handling. Logs a privacy-safe
 * breadcrumb and dispatches deep-links by notification "kind". The first
 * wired kind is LeagueInvite → the league-invite preview (see
 * docs/mobile/league-invite-deep-link.md).
 *
 * Cold-start coverage: useLastNotificationResponse resolves to the
 * response that launched the app when the user tapped a notification
 * from a killed state. addNotificationResponseReceivedListener can
 * race the iOS launch-time dispatch (the tap fires before the JS
 * listener mounts) and silently drop that response, so we explicitly
 * handle the launching response via the hook. handledTapIdsRef dedupes
 * against any later listener delivery of the same response so the tap
 * handler doesn't run twice for a single user action.
 *
 * Navigation is deferred through pendingLeagueId rather than fired inline:
 * on cold start the launching tap can resolve before AuthGuard has settled
 * the initial route, so a direct router.push would race (and lose to) the
 * sign-in redirect. The auth-gated effect below flushes the route once the
 * user is authenticated and the nav tree is mounted.
 *
 * Rendered only when Platform.OS !== 'web' (see RootLayoutNav). The
 * hooks here all touch expo-notifications native modules that aren't
 * implemented on web.
 */
function NativePushDiagnostics() {
  const lastResponse = Notifications.useLastNotificationResponse();
  const handledTapIdsRef = useRef<Set<string>>(new Set());
  const router = useRouter();
  const segments = useSegments();
  const { user, isInitialized } = useAuth();
  const [pendingLeagueId, setPendingLeagueId] = useState<string | null>(null);

  useEffect(() => {
    // Privacy: log only non-content fields. console.log feeds Sentry
    // breadcrumbs through Sentry's console integration, so anything we
    // emit here ships with crash reports. notification title / body /
    // arbitrary data payload may carry user-specific content (LeagueInvite
    // leagueId, future contestId UUIDs tied to the recipient). The
    // notification identifier is a system-generated UUID — anonymous on
    // its own — and "kind" is a category label from our wire contract, not
    // user state. Keep both for correlation, drop the rest.
    const handleTap = (response: Notifications.NotificationResponse) => {
      const id = response.notification.request.identifier;
      if (handledTapIdsRef.current.has(id)) return;
      handledTapIdsRef.current.add(id);
      const data = response.notification.request.content.data ?? {};
      console.log('[push] tapped', {
        id,
        kind: data.kind,
        actionIdentifier: response.actionIdentifier,
      });

      // TEMP diagnostic — remove once the deep-link is confirmed. Store builds
      // have no console, so surface what the payload actually contains on-device
      // (which keys arrived, whether `kind`/`leagueId` are present, app state)
      // to Sentry. leagueId is a non-sensitive GUID; title/body are not captured.
      Sentry.captureMessage('[push] tapped (diag)', {
        level: 'info',
        tags: { pushKind: String(data.kind ?? 'none') },
        extra: {
          dataKeys: Object.keys(data),
          kind: data.kind ?? null,
          hasLeagueId: typeof data.leagueId === 'string',
          leagueId: typeof data.leagueId === 'string' ? data.leagueId : null,
          actionIdentifier: response.actionIdentifier,
          appState: AppState.currentState,
        },
      });

      // Deep-link dispatch. Stash the target; the auth-gated effect below
      // navigates once the router/auth tree is ready.
      if (data.kind === 'LeagueInvite' && typeof data.leagueId === 'string') {
        setPendingLeagueId(data.leagueId);
      }
    };

    if (lastResponse) {
      handleTap(lastResponse);
    }

    const receivedSub = Notifications.addNotificationReceivedListener((notification) => {
      console.log('[push] received', {
        id: notification.request.identifier,
        kind: notification.request.content.data?.kind,
      });
    });
    const responseSub = Notifications.addNotificationResponseReceivedListener(handleTap);
    return () => {
      receivedSub.remove();
      responseSub.remove();
    };
  }, [lastResponse]);

  // Deep-link dispatch via @react-native-firebase/messaging. On iOS RNFirebase
  // owns the FCM message, so the custom `data` payload lands in
  // remoteMessage.data — NOT expo-notifications' content.data (which arrives
  // EMPTY, confirmed via Sentry). Drive navigation from RNFirebase's open
  // handlers: onNotificationOpenedApp (tap from background) and
  // getInitialNotification (tap from a quit/cold start). Both funnel into the
  // same pendingLeagueId + auth-gated flush below.
  useEffect(() => {
    if (Platform.OS === 'web') return;

    const handleOpen = (
      remoteMessage: FirebaseMessagingTypes.RemoteMessage | null,
      source: string,
    ) => {
      if (!remoteMessage) return;
      const data = remoteMessage.data ?? {};
      // TEMP diagnostic — remove with the expo one once confirmed. Shows what
      // RNFirebase actually delivered vs. expo's empty content.data.
      Sentry.captureMessage('[push] rnfb open (diag)', {
        level: 'info',
        tags: { source },
        extra: {
          dataKeys: Object.keys(data),
          kind: typeof data.kind === 'string' ? data.kind : null,
          hasLeagueId: typeof data.leagueId === 'string',
        },
      });
      if (data.kind === 'LeagueInvite' && typeof data.leagueId === 'string') {
        setPendingLeagueId(data.leagueId);
      }
    };

    let unsubscribe: (() => void) | undefined;
    // Gated dynamic import keeps RNFirebase out of the web bundle.
    void (async () => {
      const messaging = (await import('@react-native-firebase/messaging')).default;
      // Tap while the app is backgrounded.
      unsubscribe = messaging().onNotificationOpenedApp((m) => handleOpen(m, 'background'));
      // Tap that cold-started the app from a quit state.
      const initial = await messaging().getInitialNotification();
      handleOpen(initial, 'quit');
    })();

    return () => unsubscribe?.();
  }, []);

  // Flush a pending deep-link once the user is authenticated and the nav
  // tree is ready — guards the cold-start race described above. Also wait
  // until AuthGuard has finished leaving the (auth) group: while the user is
  // still in (auth) right after sign-in, AuthGuard's router.replace('/(tabs)')
  // fires on the same render and would clobber our push. segments is the same
  // signal AuthGuard keys off, so once it's no longer '(auth)' the redirect
  // has settled. Keep pendingLeagueId cached until then.
  useEffect(() => {
    if (!pendingLeagueId) return;

    // TEMP diagnostic — remove with the tap one. Shows whether the flush is
    // blocked (and by which guard) vs. actually navigating.
    Sentry.captureMessage('[push] flush check (diag)', {
      level: 'info',
      extra: {
        isInitialized,
        hasUser: !!user,
        segment0: segments[0] ?? null,
        willNavigate: isInitialized && !!user && segments[0] !== '(auth)',
      },
    });

    if (!isInitialized || !user) return;
    if (segments[0] === '(auth)') return;
    const leagueId = pendingLeagueId;
    setPendingLeagueId(null);
    router.push({
      pathname: '/league-invite/[leagueId]',
      params: { leagueId },
    } as never);
  }, [pendingLeagueId, isInitialized, user, router, segments]);

  return null;
}

/**
 * Native-only side-effect component: silently registers this device's FCM
 * token with the API once the user is authenticated and permission is already
 * granted. Rendered (not just hook-called) so it sits alongside the other
 * native-only push surfaces and never executes on web.
 */
function PushDeviceRegistrar() {
  useRegisterPushDevice();
  return null;
}

function RootLayoutNav() {
  // Feed the user-resolved scheme into React Navigation so native header
  // transitions and focus rings match the chosen theme.
  const { resolvedScheme, isHydrated: themeHydrated } = useThemeMode();
  const { isHydrated: textSizeHydrated } = useTextSize();

  // Second splash gate: fonts loaded + BOTH user preferences hydrated from
  // AsyncStorage. Keeps the splash visible through the full startup path
  // so the first painted pixels already reflect every stored preference
  // (no theme flash, no font-size flash on launch).
  useEffect(() => {
    if (themeHydrated && textSizeHydrated) SplashScreen.hideAsync();
  }, [themeHydrated, textSizeHydrated]);

  return (
    <NavThemeProvider value={resolvedScheme === 'dark' ? DarkTheme : DefaultTheme}>
      <Stack>
        <Stack.Screen name="(auth)" options={{ headerShown: false }} />
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="create-league" options={{ presentation: 'modal' }} />
        <Stack.Screen name="league-invite/[leagueId]" options={{ presentation: 'modal' }} />
        <Stack.Screen name="admin/push-token" options={{ title: 'Push Token' }} />
        <Stack.Screen name="+not-found" />
      </Stack>
      <AuthGuard />
      <SignalRGate />
      {Platform.OS !== 'web' ? (
        <>
          <NativePushDiagnostics />
          <PushDeviceRegistrar />
        </>
      ) : null}
    </NavThemeProvider>
  );
}
