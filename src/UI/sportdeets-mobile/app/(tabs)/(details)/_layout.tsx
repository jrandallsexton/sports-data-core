import { Stack, useRouter } from 'expo-router';
import { TouchableOpacity, Text, StyleSheet, Platform } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useColorScheme } from 'react-native';
import { useNavigationState } from '@react-navigation/native';
import { getTheme } from '@/constants/Colors';

function BackButton() {
  const theme = getTheme(useColorScheme());
  const router = useRouter();

  // Read backTitle from the currently focused route's params.
  // useLocalSearchParams won't work here because it reads the layout's own
  // params, not the child screen's. So we pull from the navigation state.
  const backTitle = useNavigationState((state) => {
    const currentRoute = state.routes[state.index];
    return (currentRoute.params as { backTitle?: string } | undefined)?.backTitle;
  });

  return (
    <TouchableOpacity
      onPress={() => router.back()}
      style={styles.backButton}
      hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
      activeOpacity={0.6}
    >
      <Ionicons name="chevron-back" size={24} color={theme.tint} />
      <Text style={[styles.backLabel, { color: theme.tint }]}>{backTitle || 'Back'}</Text>
    </TouchableOpacity>
  );
}

export default function DetailsStackLayout() {
  const theme = getTheme(useColorScheme());

  return (
    <Stack
      screenOptions={{
        headerStyle: { backgroundColor: theme.card },
        headerShadowVisible: false,
        headerTintColor: theme.tint,
        headerTitle: '',
        headerBackVisible: false,
        headerLeft: () => <BackButton />,
      }}
    />
  );
}

const styles = StyleSheet.create({
  backButton: {
    flexDirection: 'row',
    alignItems: 'center',
    marginLeft: Platform.OS === 'ios' ? -8 : 0,
  },
  backLabel: {
    fontSize: 17,
    marginLeft: -2,
  },
});
