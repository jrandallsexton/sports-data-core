import React, { useMemo, useState } from 'react';
import {
  View,
  StyleSheet,
  TouchableOpacity,
  TextInput,
  Alert,
  Platform,
  ScrollView,
} from 'react-native';
import { signOut } from 'firebase/auth';
import { useQueryClient } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { signOutGoogle } from '@/src/lib/googleSignIn';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme, useThemeMode, type ThemeMode } from '@/src/lib/theme/ThemeContext';
import { useTextSize, type TextSize } from '@/src/lib/textSize/TextSizeContext';
import { getTheme } from '@/constants/Colors';
import { auth } from '@/src/lib/firebase';
import { useAuthStore } from '@/src/stores/authStore';
import { useCurrentUser, standingsKeys } from '@/src/hooks/useStandings';
import { SegmentedControl } from '@/src/components/ui/SegmentedControl';
import { usersApi } from '@/src/services/api/usersApi';
import { devicesApi } from '@/src/services/api/devicesApi';
import { getOrCreateInstallationId } from '@/src/lib/device/installationId';
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

const TEXT_SIZE_OPTIONS: { value: TextSize; label: string }[] = [
  { value: 'small', label: 'S' },
  { value: 'medium', label: 'M' },
  { value: 'large', label: 'L' },
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
  const { size: textSize, setSize: setTextSize } = useTextSize();
  const queryClient = useQueryClient();
  const router = useRouter();

  const deviceTz = useMemo(() => detectDeviceTimezone(), []);
  const [tzPickerOpen, setTzPickerOpen] = useState(false);
  const [tzMessage, setTzMessage] = useState('');

  const [editingDisplayName, setEditingDisplayName] = useState(false);
  const [displayNameInput, setDisplayNameInput] = useState('');
  const [displayNameSaving, setDisplayNameSaving] = useState(false);
  const [displayNameMessage, setDisplayNameMessage] = useState('');

  // The zone the user has saved, or the device default if nothing saved yet.
  // The picker uses this to highlight the current selection. The user-set
  // value is what the API persists; the device value is purely a sensible
  // pre-population so the picker isn't empty.
  const effectiveTimezone = me?.timezone || deviceTz;
  const isUserSet = !!me?.timezone;

  const performSignOut = async () => {
    // Clear the Google native session first so the next "Continue with
    // Google" tap re-prompts the account picker instead of silently
    // re-auth'ing the last-used account. Apple Sign-In has no
    // equivalent client-side session — iOS manages it system-wide via
    // the user's Apple ID settings, not the app.
    //
    // Independent try/catch from the Firebase sign-out below: Google's
    // clearing is best-effort cosmetic, but Firebase's signOut is the
    // load-bearing operation that actually flips the auth state and
    // triggers the redirect. If Google's signOut throws (e.g.,
    // configure() bridge issue), we still need Firebase's signOut to
    // run — otherwise the user taps Sign Out, sees no error (or only
    // the warn-log), and stays signed in.
    try {
      await signOutGoogle();
    } catch (err) {
      console.warn(
        '[ProfileScreen] Google sign-out failed (continuing to Firebase sign-out)',
        err,
      );
    }
    // Unregister this device so the signed-out account stops receiving pushes
    // on it. Best-effort and native-only (web never registers a device): the
    // JWT is still valid here, and a failure must not block sign-out. Done
    // before signOut(auth) so the Authorization header still attaches.
    if (Platform.OS !== 'web') {
      try {
        const installationId = await getOrCreateInstallationId();
        await devicesApi.unregisterDevice(installationId);
      } catch (err) {
        console.log('[ProfileScreen] device unregister failed (continuing to sign-out)', err);
      }
    }
    try {
      await signOut(auth);
      // Success path: useAuthInit's onAuthStateChanged listener observes
      // the auth flip → AuthGuard handles the redirect to /(auth)/welcome.
      // Drop all cached queries (current user, leagues, picks, …) so the next
      // login can't flash the previous user's cached identity before refetch.
      queryClient.clear();
    } catch (err) {
      console.error('[ProfileScreen] Firebase sign-out failed', err);
      Alert.alert(
        'Sign Out Failed',
        'We could not sign you out. Please check your connection and try again.',
      );
    }
  };

  const handleSignOut = () => {
    // Cross-platform confirmation. React Native Web's Alert.alert is a
    // stub that doesn't reliably fire onPress callbacks from the buttons
    // array, so the previous Alert.alert(...) confirmation deadlocked
    // on web — the destructive "Sign Out" button rendered but never
    // executed performSignOut. Use window.confirm on web (synchronous,
    // returns boolean) and the native Alert on iOS/Android (where the
    // buttons array works correctly).
    if (Platform.OS === 'web') {
      if (typeof window !== 'undefined' && window.confirm('Are you sure you want to sign out?')) {
        void performSignOut();
      }
      return;
    }
    Alert.alert('Sign Out', 'Are you sure you want to sign out?', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Sign Out',
        style: 'destructive',
        onPress: () => {
          void performSignOut();
        },
      },
    ]);
  };

  const performAccountDeletion = async () => {
    try {
      await usersApi.deleteAccount();
    } catch (err) {
      console.error('[ProfileScreen] account deletion failed', err);
      Alert.alert(
        'Deletion Failed',
        'We could not delete your account. Please check your connection and try again.',
      );
      return;
    }
    // The account (including the Firebase login) is gone server-side; clear the
    // local session so AuthGuard redirects to the welcome screen.
    try {
      await signOut(auth);
    } catch (err) {
      console.warn('[ProfileScreen] sign-out after deletion failed (already gone)', err);
    }
    // Always drop cached queries — the account is gone regardless of the signOut
    // result, so no cached identity/data may survive into the next login.
    queryClient.clear();
  };

  const handleDeleteAccount = () => {
    const message =
      'This permanently deletes your account and removes your personal data. '
      + 'Your league history stays (anonymized). This cannot be undone.';
    if (Platform.OS === 'web') {
      if (typeof window !== 'undefined' && window.confirm(message)) {
        void performAccountDeletion();
      }
      return;
    }
    Alert.alert('Delete Account?', message, [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Delete',
        style: 'destructive',
        onPress: () => {
          void performAccountDeletion();
        },
      },
    ]);
  };

  const beginEditDisplayName = () => {
    setDisplayNameInput(me?.displayName ?? '');
    setDisplayNameMessage('');
    setEditingDisplayName(true);
  };

  const handleDisplayNameSave = async () => {
    const next = displayNameInput.trim();
    if (!next) {
      // Don't discard the edit silently — tell the user and keep the editor open.
      setDisplayNameMessage('Display name is required.');
      return;
    }
    if (next === (me?.displayName ?? '')) {
      // No change — nothing to save; just close.
      setEditingDisplayName(false);
      return;
    }
    setDisplayNameSaving(true);
    setDisplayNameMessage('');
    try {
      await usersApi.updateDisplayName(next);
      await queryClient.invalidateQueries({ queryKey: standingsKeys.me });
      setEditingDisplayName(false);
    } catch (err) {
      console.warn('[ProfileScreen] display name update failed', err);
      setDisplayNameMessage('Could not save display name. Try again.');
    } finally {
      setDisplayNameSaving(false);
    }
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
        {me?.username ? (
          <Text style={[styles.heroEmail, { color: theme.textOnAccent, opacity: 0.85 }]}>
            @{me.username}
          </Text>
        ) : null}
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
          <SegmentedControl
            value={textSize}
            options={TEXT_SIZE_OPTIONS}
            onChange={setTextSize}
            accessibilityLabel="Text size"
          />
          <Text style={[styles.sectionHint, { color: theme.textMuted }]}>
            Affects all in-app text. Header brand stays fixed.
          </Text>
        </View>
      </View>

      {/* Account */}
      <View style={[styles.section, { backgroundColor: theme.card, borderColor: theme.border }]}>
        <Text style={[styles.sectionTitle, { color: theme.textMuted }]}>Account</Text>
        {/* Username is a stable handle — shown read-only. */}
        <SettingsRow
          label="Username"
          value={me?.username ? `@${me.username}` : '—'}
        />
        {editingDisplayName ? (
          <View style={[styles.fieldEditor, { borderBottomColor: theme.separator }]}>
            <Text style={[styles.settingsLabel, { color: theme.text }]}>Display Name</Text>
            <View style={styles.fieldEditRow}>
              <TextInput
                value={displayNameInput}
                onChangeText={setDisplayNameInput}
                maxLength={25}
                editable={!displayNameSaving}
                placeholder="display name"
                placeholderTextColor={theme.textMuted}
                accessibilityLabel="Display name"
                style={[styles.fieldInput, { color: theme.text, borderColor: theme.border, backgroundColor: theme.background }]}
              />
              <TouchableOpacity
                onPress={handleDisplayNameSave}
                disabled={displayNameSaving}
                accessibilityRole="button"
                accessibilityLabel="Save display name"
                accessibilityState={{ disabled: displayNameSaving }}
              >
                <Text style={[styles.fieldAction, { color: theme.tint }]}>
                  {displayNameSaving ? 'Saving…' : 'Save'}
                </Text>
              </TouchableOpacity>
              <TouchableOpacity
                onPress={() => setEditingDisplayName(false)}
                disabled={displayNameSaving}
                accessibilityRole="button"
                accessibilityLabel="Cancel display name edit"
                accessibilityState={{ disabled: displayNameSaving }}
              >
                <Text style={[styles.fieldAction, { color: theme.textMuted }]}>Cancel</Text>
              </TouchableOpacity>
            </View>
            {displayNameMessage ? (
              <Text style={[styles.fieldError, { color: theme.error }]}>{displayNameMessage}</Text>
            ) : null}
          </View>
        ) : (
          <SettingsRow
            label="Display Name"
            value={me?.displayName ?? 'Set display name'}
            onPress={beginEditDisplayName}
          />
        )}
        <SettingsRow
          label="Timezone"
          value={isUserSet ? effectiveTimezone : `${effectiveTimezone} (device)`}
          onPress={() => setTzPickerOpen(true)}
        />
        <SettingsRow label="Notifications" onPress={() => {}} />
        <SettingsRow label="Sign Out" onPress={handleSignOut} destructive />
        <SettingsRow label="Delete Account" onPress={handleDeleteAccount} destructive />
      </View>

      {/* Developer — admin-only diagnostics. Gated on isAdmin so it
          doesn't leak into the regular user surface; remove the gate
          once the push-token retrieval becomes a normal user setting. */}
      {me?.isAdmin ? (
        <View style={[styles.section, { backgroundColor: theme.card, borderColor: theme.border }]}>
          <Text style={[styles.sectionTitle, { color: theme.textMuted }]}>Developer</Text>
          <SettingsRow
            label="Push Token (FCM)"
            onPress={() => router.push('/admin/push-token')}
          />
        </View>
      ) : null}

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
  fieldEditor: {
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderBottomWidth: StyleSheet.hairlineWidth,
    gap: 8,
  },
  fieldEditRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
  },
  fieldInput: {
    flex: 1,
    borderWidth: StyleSheet.hairlineWidth,
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 8,
    fontSize: 16,
  },
  fieldAction: { fontSize: 15, fontWeight: '600' },
  fieldError: { fontSize: 13 },
});
