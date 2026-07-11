import React, { useEffect, useState } from 'react';
import { View, StyleSheet, ScrollView, Switch, ActivityIndicator } from 'react-native';
import { Stack } from 'expo-router';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { usersApi } from '@/src/services/api/usersApi';
import type { NotificationPreferences } from '@/src/types/models';

// Per-category push-notification opt-out screen. The API owns these flags
// (canonical); it publishes an event the Notification service projects into the
// table its dispatchers read to gate sends. A user with no saved row is treated
// as fully opted-in, so the defaults here are all-on.
// See docs/mobile/notification-preferences.md.

const notificationKeys = {
  preferences: ['user', 'me', 'notification-preferences'] as const,
};

// Field key → user-facing label + one-line description. Grouped into sections
// below. Order within a section is deliberate (most-valued first).
type PrefKey = keyof NotificationPreferences;

interface ToggleMeta {
  key: PrefKey;
  label: string;
  description: string;
}

interface Section {
  title: string;
  items: ToggleMeta[];
}

const SECTIONS: Section[] = [
  {
    title: 'Your picks',
    items: [
      {
        key: 'pickResultEnabled',
        label: 'Pick results',
        description: 'When your picks are graded after a game finalizes.',
      },
      {
        key: 'pickDeadlineReminderEnabled',
        label: 'Pick deadline reminders',
        description: "A nudge before a week's picks lock.",
      },
      {
        key: 'contestStartReminderEnabled',
        label: 'Kickoff reminders',
        description: 'When a game you picked is about to start.',
      },
    ],
  },
  {
    title: 'Leagues',
    items: [
      {
        key: 'leagueInviteEnabled',
        label: 'League invites',
        description: "When someone invites you to a pick'em league.",
      },
      {
        key: 'membershipEnabled',
        label: 'League membership updates',
        description: 'When members join or leave your leagues.',
      },
    ],
  },
  {
    title: 'Games',
    items: [
      {
        key: 'matchupPreviewEnabled',
        label: 'Matchup previews',
        description: 'AI-generated previews for your upcoming matchups.',
      },
      {
        key: 'scheduleChangeEnabled',
        label: 'Schedule changes',
        description: 'When a game you picked is rescheduled.',
      },
      {
        key: 'oddsChangedEnabled',
        label: 'Line moves',
        description: 'When the spread or total for your games shifts.',
      },
    ],
  },
];

const ALL_ENABLED: NotificationPreferences = {
  pickResultEnabled: true,
  pickDeadlineReminderEnabled: true,
  contestStartReminderEnabled: true,
  leagueInviteEnabled: true,
  membershipEnabled: true,
  matchupPreviewEnabled: true,
  scheduleChangeEnabled: true,
  oddsChangedEnabled: true,
};

export default function NotificationSettingsScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const queryClient = useQueryClient();

  const {
    data: serverPrefs,
    isLoading,
    isError,
    refetch,
  } = useQuery<NotificationPreferences>({
    queryKey: notificationKeys.preferences,
    queryFn: () => usersApi.getNotificationPreferences().then((r) => r.data),
  });

  // Local mirror so toggles feel instant. Seeded from the server once loaded,
  // and re-seeded whenever a fresh server copy arrives.
  const [prefs, setPrefs] = useState<NotificationPreferences | null>(null);
  useEffect(() => {
    if (serverPrefs) setPrefs(serverPrefs);
  }, [serverPrefs]);

  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const mutation = useMutation<
    unknown,
    unknown,
    NotificationPreferences,
    { previous: NotificationPreferences }
  >({
    mutationFn: (next) => usersApi.updateNotificationPreferences(next),
    // Snapshot pre-change state (for revert) and optimistically apply it.
    onMutate: (next) => {
      const previous = prefs ?? ALL_ENABLED;
      setPrefs(next);
      setErrorMessage(null);
      return { previous };
    },
    onError: (_err, _next, context) => {
      if (context) setPrefs(context.previous);
      setErrorMessage('Could not save that change. Please try again.');
    },
    onSuccess: (_data, next) => {
      // Keep the cache in step so navigating away/back shows the saved state.
      queryClient.setQueryData(notificationKeys.preferences, next);
    },
  });

  const toggle = (key: PrefKey) => {
    if (!prefs) return;
    // Full-replacement PATCH — send the whole set with this one flag flipped.
    mutation.mutate({ ...prefs, [key]: !prefs[key] });
  };

  const effective = prefs ?? ALL_ENABLED;

  return (
    <>
      <Stack.Screen options={{ title: 'Notifications', headerBackTitle: 'Back' }} />
      <ScrollView
        style={[styles.container, { backgroundColor: theme.background }]}
        contentContainerStyle={styles.content}
      >
        {isLoading ? (
          <View style={styles.centered}>
            <ActivityIndicator color={theme.tint} />
          </View>
        ) : isError ? (
          <View style={styles.centered}>
            <Text style={[styles.errorText, { color: theme.error }]}>
              Couldn't load your notification settings.
            </Text>
            <Text
              style={[styles.retry, { color: theme.tint }]}
              onPress={() => refetch()}
            >
              Tap to retry
            </Text>
          </View>
        ) : (
          <>
            <Text style={[styles.intro, { color: theme.textMuted }]}>
              Choose which push notifications you'd like to receive. Turning one
              off stops that kind of notification everywhere.
            </Text>

            {errorMessage ? (
              <Text style={[styles.errorBanner, { color: theme.error }]}>
                {errorMessage}
              </Text>
            ) : null}

            {SECTIONS.map((section) => (
              <View key={section.title} style={styles.section}>
                <Text style={[styles.sectionTitle, { color: theme.textMuted }]}>
                  {section.title}
                </Text>
                <View
                  style={[
                    styles.card,
                    { backgroundColor: theme.card, borderColor: theme.border },
                  ]}
                >
                  {section.items.map((item, idx) => (
                    <View
                      key={item.key}
                      style={[
                        styles.row,
                        idx > 0 && {
                          borderTopColor: theme.border,
                          borderTopWidth: StyleSheet.hairlineWidth,
                        },
                      ]}
                    >
                      <View style={styles.rowText}>
                        <Text style={[styles.rowLabel, { color: theme.text }]}>
                          {item.label}
                        </Text>
                        <Text style={[styles.rowDesc, { color: theme.textMuted }]}>
                          {item.description}
                        </Text>
                      </View>
                      <Switch
                        value={effective[item.key]}
                        onValueChange={() => toggle(item.key)}
                        trackColor={{ true: theme.tint, false: theme.border }}
                        accessibilityLabel={item.label}
                      />
                    </View>
                  ))}
                </View>
              </View>
            ))}
          </>
        )}
      </ScrollView>
    </>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  content: { padding: 16, gap: 8, paddingBottom: 40 },
  centered: { paddingVertical: 60, alignItems: 'center', gap: 10 },
  errorText: { fontSize: 14 },
  retry: { fontSize: 14, fontWeight: '700' },
  intro: { fontSize: 13, lineHeight: 18, marginBottom: 4 },
  errorBanner: { fontSize: 13, fontWeight: '600', marginBottom: 4 },
  section: { gap: 8, marginTop: 8 },
  sectionTitle: {
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 0.5,
    textTransform: 'uppercase',
    marginLeft: 4,
  },
  card: {
    borderRadius: 12,
    borderWidth: StyleSheet.hairlineWidth,
    overflow: 'hidden',
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 14,
    gap: 12,
  },
  rowText: { flex: 1, gap: 2 },
  rowLabel: { fontSize: 15, fontWeight: '600' },
  rowDesc: { fontSize: 12, lineHeight: 16 },
});
