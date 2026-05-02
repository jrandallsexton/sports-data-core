import React, { useMemo, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Alert,
  ScrollView,
} from 'react-native';
import { signOut } from 'firebase/auth';
import { useQueryClient } from '@tanstack/react-query';
import { useColorScheme, useThemeMode, type ThemeMode } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import { auth } from '@/src/lib/firebase';
import { useAuthStore } from '@/src/stores/authStore';
import { useCurrentUser, standingsKeys } from '@/src/hooks/useStandings';
import { SegmentedControl } from '@/src/components/ui/SegmentedControl';
import { usersApi } from '@/src/services/api/usersApi';
import { DEFAULT_TIMEZONE } from '@/src/utils/timeUtils';
import { TimezonePickerModal } from '@/src/components/features/settings/TimezonePickerModal';

// ─── Record card ──────────────────────────────────────────────────────────────

function RecordCard({
  label,
  wins,
  losses,
  pushes,
}: {
  label: string;
  wins: number;
  losses: number;
  pushes: number;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const pct = wins + losses > 0 ? ((wins / (wins + losses)) * 100).toFixed(1) : '—';

  return (
    <View style={[styles.recordCard, { backgroundColor: theme.card, borderColor: theme.border }]}>
      <Text style={[styles.recordLabel, { color: theme.textMuted }]}>{label}</Text>
      <Text style={[styles.recordValue, { color: theme.text }]}>
        {wins}-{losses}{pushes > 0 ? `-${pushes}` : ''}
      </Text>
      <Text style={[styles.recordPct, { color: theme.tint }]}>{pct}%</Text>
    </View>
  );
}

// ─── Settings row ─────────────────────────────────────────────────────────────

function SettingsRow({
  label,
  value,
  onPress,
  destructive,
}: {
  label: string;
  value?: string;
  onPress?: () => void;
  destructive?: boolean;
}) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  return (
    <TouchableOpacity
      style={[styles.settingsRow, { borderBottomColor: theme.separator }]}
      onPress={onPress}
      disabled={!onPress}
    >
      <Text style={[styles.settingsLabel, destructive && styles.destructive, { color: destructive ? theme.error : theme.text }]}>
        {label}
      </Text>
      {value && <Text style={[styles.settingsValue, { color: theme.textMuted }]}>{value}</Text>}
    </TouchableOpacity>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

const THEME_OPTIONS: { value: ThemeMode; label: string }[] = [
  { value: 'light', label: 'Light' },
  { value: 'dark', label: 'Dark' },
  { value: 'system', label: 'System' },
];

// Device default — Hermes exposes Intl.DateTimeFormat().resolvedOptions().
// We use this to pre-populate the picker when the user hasn't picked a tz
// yet. We do NOT auto-save the device tz; the user must explicitly confirm.
function detectDeviceTimezone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || DEFAULT_TIMEZONE;
  } catch {
    return DEFAULT_TIMEZONE;
  }
}

export default function ProfileScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const { user } = useAuthStore();
  const { data: me } = useCurrentUser();
  const { mode, setMode } = useThemeMode();
  const queryClient = useQueryClient();

  const deviceTz = useMemo(() => detectDeviceTimezone(), []);
  const [tzPickerOpen, setTzPickerOpen] = useState(false);
  const [tzMessage, setTzMessage] = useState('');

  // The zone the user has saved, or the device default if nothing saved yet.
  // The picker uses this to highlight the current selection. The user-set
  // value is what the API persists; the device value is purely a sensible
  // pre-population so the picker isn't empty.
  const effectiveTimezone = me?.timezone || deviceTz;
  const isUserSet = !!me?.timezone;

  const handleSignOut = () => {
    Alert.alert('Sign Out', 'Are you sure you want to sign out?', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Sign Out',
        style: 'destructive',
        onPress: () => signOut(auth),
      },
    ]);
  };

  const handleTimezoneSelect = async (newTz: string) => {
    if (!newTz) return;
    setTzMessage('');
    try {
      await usersApi.updateTimezone(newTz);
      // Refresh /user/me so every consumer (useUserTimeZone, etc.) picks up
      // the new value on its next render.
      await queryClient.invalidateQueries({ queryKey: standingsKeys.me });
      setTzMessage('Saved.');
      // Auto-close after a brief moment so the user sees the confirmation.
      setTimeout(() => {
        setTzPickerOpen(false);
        setTzMessage('');
      }, 600);
    } catch (err) {
      console.warn('[ProfileScreen] timezone update failed', err);
      setTzMessage('Could not save timezone. Try again?');
    }
  };

  const displayName = me?.displayName ?? user?.displayName ?? user?.email ?? '—';
  // Profile stats not available from /user/me — show empty until extended
  const seasonRecord = { wins: 0, losses: 0, pushes: 0 };
  const careerRecord = { wins: 0, losses: 0, pushes: 0 };

  return (
    <ScrollView
      style={[styles.container, { backgroundColor: theme.background }]}
      showsVerticalScrollIndicator={false}
    >
      {/* Avatar + name — hero uses the theme tint so light/dark feel distinct.
          Avatar chip uses theme.card + theme.text as a contrasting "card on
          tint" inset; those tokens are already designed to pair, so contrast
          is correct in both modes without introducing a new accent-inverse
          token or hard-coding a brand color. */}
      <View style={[styles.hero, { backgroundColor: theme.tint }]}>
        <View style={[styles.avatar, { backgroundColor: theme.card }]}>
          <Text style={[styles.avatarInitial, { color: theme.text }]}>
            {(displayName[0] ?? '?').toUpperCase()}
          </Text>
        </View>
        <Text style={[styles.heroName, { color: theme.textOnAccent }]}>{displayName}</Text>
        <Text style={[styles.heroEmail, { color: theme.textOnAccent, opacity: 0.7 }]}>
          {user?.email}
        </Text>
      </View>

      {/* Records */}
      <View style={styles.records}>
        <RecordCard
          label="This Season"
          wins={seasonRecord.wins}
          losses={seasonRecord.losses}
          pushes={seasonRecord.pushes}
        />
        <RecordCard
          label="Career"
          wins={careerRecord.wins}
          losses={careerRecord.losses}
          pushes={careerRecord.pushes}
        />
      </View>

      {/* Appearance */}
      <View style={[styles.section, { backgroundColor: theme.card, borderColor: theme.border }]}>
        <Text style={[styles.sectionTitle, { color: theme.textMuted }]}>Appearance</Text>
        <View style={styles.sectionBody}>
          <SegmentedControl
            value={mode}
            options={THEME_OPTIONS}
            onChange={setMode}
            accessibilityLabel="Theme"
          />
          <Text style={[styles.sectionHint, { color: theme.textMuted }]}>
            System follows your device's light/dark setting.
          </Text>
        </View>
      </View>

      {/* Account */}
      <View style={[styles.section, { backgroundColor: theme.card, borderColor: theme.border }]}>
        <Text style={[styles.sectionTitle, { color: theme.textMuted }]}>Account</Text>
        <SettingsRow
          label="Timezone"
          value={isUserSet ? effectiveTimezone : `${effectiveTimezone} (device)`}
          onPress={() => setTzPickerOpen(true)}
        />
        <SettingsRow label="Edit Profile" onPress={() => {}} />
        <SettingsRow label="Notifications" onPress={() => {}} />
        <SettingsRow label="Sign Out" onPress={handleSignOut} destructive />
      </View>

      <View style={{ height: 40 }} />

      <TimezonePickerModal
        visible={tzPickerOpen}
        onClose={() => {
          setTzPickerOpen(false);
          setTzMessage('');
        }}
        currentTimezone={effectiveTimezone}
        onSelect={handleTimezoneSelect}
        message={tzMessage}
      />
    </ScrollView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { flex: 1 },
  hero: {
    alignItems: 'center',
    paddingTop: 40,
    paddingBottom: 32,
    gap: 6,
  },
  avatar: {
    width: 72,
    height: 72,
    borderRadius: 36,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 8,
  },
  avatarInitial: { fontSize: 32, fontWeight: '800' },
  heroName: { fontSize: 22, fontWeight: '800' },
  heroEmail: { fontSize: 13 },
  records: {
    flexDirection: 'row',
    gap: 12,
    padding: 14,
  },
  recordCard: {
    flex: 1,
    borderRadius: 12,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 16,
    alignItems: 'center',
    gap: 4,
  },
  recordLabel: { fontSize: 11, fontWeight: '700', textTransform: 'uppercase', letterSpacing: 0.5 },
  recordValue: { fontSize: 20, fontWeight: '800' },
  recordPct: { fontSize: 18, fontWeight: '700' },
  section: {
    marginHorizontal: 14,
    marginBottom: 14,
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
    overflow: 'hidden',
  },
  sectionTitle: {
    fontSize: 11,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
    paddingHorizontal: 16,
    paddingTop: 14,
    paddingBottom: 6,
  },
  sectionBody: {
    paddingHorizontal: 16,
    paddingBottom: 14,
    gap: 10,
  },
  sectionHint: {
    fontSize: 12,
  },
  settingsRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  settingsLabel: { fontSize: 16 },
  settingsValue: { fontSize: 14 },
  destructive: { fontWeight: '600' },
});
