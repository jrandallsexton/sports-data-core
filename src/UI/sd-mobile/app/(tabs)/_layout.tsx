import { Tabs } from 'expo-router';
import { Platform } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';

type IoniconsName = React.ComponentProps<typeof Ionicons>['name'];

function TabIcon({ name, color }: { name: IoniconsName; color: string }) {
  return <Ionicons name={name} size={24} color={color} />;
}

export default function TabLayout() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: theme.tint,
        tabBarInactiveTintColor: theme.tabIconDefault,
        tabBarStyle: {
          backgroundColor: theme.card,
          borderTopColor: theme.border,
          height: Platform.select({ ios: 90, android: 72 }),
          paddingTop: 6,
          paddingBottom: Platform.select({ ios: 30, android: 18, default: 8 }),
        },
        tabBarLabelStyle: { fontSize: 11, fontWeight: '600' },
        headerStyle: { backgroundColor: theme.card },
        headerShadowVisible: false,
        headerTintColor: theme.text,
        headerTitleStyle: { fontWeight: '700', fontSize: 18 },
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          // Header title is the sportDeets wordmark (matches web's top-right
          // brand link). Tab label stays "Home" — different surface, different
          // job: the wordmark brands the app, the tab label identifies the tab.
          // Override headerTitleStyle here so the wordmark picks up the accent
          // color (web parity — brand link uses var(--accent)). Other tabs
          // keep the default headerTintColor for their plain string titles.
          title: 'sportDeets',
          tabBarLabel: 'Home',
          headerTitleStyle: { fontWeight: '800', fontSize: 20, color: theme.tint },
          tabBarIcon: ({ color }) => (
            <TabIcon name="home-outline" color={color} />
          ),
        }}
      />
      <Tabs.Screen
        name="picks"
        options={{
          // Wordmark header (same treatment as the home tab) — the selected
          // league name already appears in the week selector just below, so
          // the header slot is free to reinforce brand. tabBarLabel keeps the
          // bottom-tab label as "Games".
          title: 'sportDeets',
          tabBarLabel: 'Games',
          headerTitleStyle: { fontWeight: '800', fontSize: 20, color: theme.tint },
          tabBarIcon: ({ color }) => (
            <TabIcon name="american-football-outline" color={color} />
          ),
        }}
      />
      <Tabs.Screen
        name="standings"
        options={{
          title: 'Standings',
          tabBarIcon: ({ color }) => (
            <TabIcon name="trophy-outline" color={color} />
          ),
        }}
      />
      <Tabs.Screen
        name="profile"
        options={{
          title: 'Profile',
          tabBarIcon: ({ color }) => (
            <TabIcon name="person-circle-outline" color={color} />
          ),
        }}
      />
      {/* Shared detail stack — game + team screens live here so back navigation is linear */}
      <Tabs.Screen name="(details)" options={{ href: null, headerShown: false }} />
    </Tabs>
  );
}
