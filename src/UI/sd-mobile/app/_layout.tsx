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
import { useEffect } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import * as Sentry from '@sentry/react-native';
import 'react-native-reanimated';

import { queryClient } from '@/src/lib/queryClient';
import { useAuthInit, useAuth } from '@/src/hooks/useAuth';
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
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldPlaySound: true,
    shouldSetBadge: false,
    shouldShowBanner: true,
    shouldShowList: true,
  }),
});

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
      router.replace('/(auth)/sign-in');
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

  // Push notification receive + tap diagnostics. v1 just logs — actual
  // deep-link routing (notification → /(tabs)/picks?leagueId=…&contestId=…)
  // lands in a follow-up once the test-push round-trip is proven. The
  // tap listener fires for both "tap while backgrounded" and "tap while
  // killed" cases by design — addNotificationResponseReceivedListener
  // also resolves any pending initial notification from cold start.
  useEffect(() => {
    const receivedSub = Notifications.addNotificationReceivedListener((notification) => {
      console.log('[push] received', {
        title: notification.request.content.title,
        body: notification.request.content.body,
        data: notification.request.content.data,
      });
    });
    const responseSub = Notifications.addNotificationResponseReceivedListener((response) => {
      console.log('[push] tapped', {
        actionIdentifier: response.actionIdentifier,
        data: response.notification.request.content.data,
      });
    });
    return () => {
      receivedSub.remove();
      responseSub.remove();
    };
  }, []);

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
        <Stack.Screen name="admin/push-token" options={{ title: 'Push Token' }} />
        <Stack.Screen name="+not-found" />
      </Stack>
      <AuthGuard />
      <SignalRGate />
    </NavThemeProvider>
  );
}
