// Sentry MUST initialize before any other module that could throw at
// import time (Firebase, React Query, navigation). Side-effect import
// kept on its own line, above all other imports, for that reason.
import '@/src/lib/sentry';

import { DarkTheme, DefaultTheme, ThemeProvider as NavThemeProvider } from '@react-navigation/native';
import { useFonts } from 'expo-font';
import { Poppins_400Regular, Poppins_700Bold_Italic } from '@expo-google-fonts/poppins';
import { Stack, useRouter, useSegments } from 'expo-router';
import * as SplashScreen from 'expo-splash-screen';
import { useEffect } from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import * as Sentry from '@sentry/react-native';
import 'react-native-reanimated';

import { queryClient } from '@/src/lib/queryClient';
import { useAuthInit, useAuth } from '@/src/hooks/useAuth';
import { ThemeProvider, useThemeMode } from '@/src/lib/theme/ThemeContext';
import { SignalRGate } from '@/src/components/signalR/SignalRGate';

export {
  // Catch any errors thrown by the Layout component.
  ErrorBoundary,
} from 'expo-router';

export const unstable_settings = {
  initialRouteName: '(tabs)',
};

SplashScreen.preventAutoHideAsync();

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

  // Splash stays up until fonts load; the ThemeProvider's hydration state
  // then takes over (see RootLayoutNav) so users never see a system-resolved
  // flash before their persisted preference applies.
  if (!loaded) return null;

  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
        <RootLayoutNav />
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
  const { resolvedScheme, isHydrated } = useThemeMode();

  // Second splash gate: fonts loaded + theme hydrated from AsyncStorage.
  // Keeps the splash visible through the full startup path so the first
  // painted pixels already reflect the user's stored preference.
  useEffect(() => {
    if (isHydrated) SplashScreen.hideAsync();
  }, [isHydrated]);

  return (
    <NavThemeProvider value={resolvedScheme === 'dark' ? DarkTheme : DefaultTheme}>
      <Stack>
        <Stack.Screen name="(auth)" options={{ headerShown: false }} />
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="create-league" options={{ presentation: 'modal' }} />
        <Stack.Screen name="+not-found" />
      </Stack>
      <AuthGuard />
      <SignalRGate />
    </NavThemeProvider>
  );
}
