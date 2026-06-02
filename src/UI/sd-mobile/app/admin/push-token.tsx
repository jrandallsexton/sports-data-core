import React, { useCallback, useEffect, useState } from 'react';
import { View, StyleSheet, TouchableOpacity, ScrollView } from 'react-native';
import * as Clipboard from 'expo-clipboard';
import { Stack } from 'expo-router';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import {
  getFcmToken,
  type FcmTokenResult,
} from '@/src/lib/notifications/pushNotifications';

// Dev / admin tool: surfaces the FCM device token so it can be copied
// and pasted into the admin/notifications/test-push API endpoint. Pure
// proof-of-concept screen — not the end-state notification settings UI.
// Once the production-shaped UserDeviceToken auto-registration ships
// (see docs/mobile/push-notifications.md), this screen stops being a
// manual copy-paste step and just becomes a "view your registered
// tokens" diagnostic.

export default function PushTokenScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const [result, setResult] = useState<FcmTokenResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [copyFeedback, setCopyFeedback] = useState<string | null>(null);

  const fetchToken = useCallback(async () => {
    setLoading(true);
    setCopyFeedback(null);
    try {
      const next = await getFcmToken();
      setResult(next);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchToken();
  }, [fetchToken]);

  const handleCopy = async () => {
    if (!result?.token) return;
    await Clipboard.setStringAsync(result.token);
    setCopyFeedback('Copied to clipboard.');
    setTimeout(() => setCopyFeedback(null), 1500);
  };

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Push Token (FCM)',
          headerBackTitle: 'Back',
        }}
      />
      <ScrollView
        style={[styles.container, { backgroundColor: theme.background }]}
        contentContainerStyle={styles.content}
      >
        <Text style={[styles.heading, { color: theme.text }]}>
          FCM Device Token
        </Text>
        <Text style={[styles.subhead, { color: theme.textMuted }]}>
          Paste into the admin/notifications/test-push endpoint to verify
          the push pipeline.
        </Text>

        <View style={[styles.statusRow, { borderColor: theme.border }]}>
          <Text style={[styles.statusLabel, { color: theme.textMuted }]}>
            Permission
          </Text>
          <Text
            style={[
              styles.statusValue,
              {
                color:
                  result?.permissionStatus === 'granted'
                    ? theme.pickCorrect
                    : result?.permissionStatus === 'denied'
                      ? theme.pickIncorrect
                      : theme.text,
              },
            ]}
          >
            {loading ? 'Checking…' : (result?.permissionStatus ?? '—')}
          </Text>
        </View>

        {result?.error ? (
          <View
            style={[
              styles.errorBox,
              {
                borderColor: theme.pickIncorrect,
                backgroundColor: theme.errorBg,
              },
            ]}
          >
            <Text style={[styles.errorText, { color: theme.errorText }]}>
              {result.error}
            </Text>
          </View>
        ) : null}

        <View
          style={[
            styles.tokenBox,
            { backgroundColor: theme.card, borderColor: theme.border },
          ]}
        >
          <Text
            style={[styles.tokenText, { color: theme.text }]}
            selectable
          >
            {loading
              ? 'Loading…'
              : result?.token
                ? result.token
                : result?.permissionStatus === 'denied'
                  ? 'Permission denied. Enable notifications in Settings → SportDeets → Notifications, then refresh.'
                  : 'No token available.'}
          </Text>
        </View>

        <View style={styles.actions}>
          <TouchableOpacity
            style={[
              styles.button,
              {
                backgroundColor: result?.token ? theme.tint : theme.border,
                opacity: result?.token ? 1 : 0.5,
              },
            ]}
            onPress={handleCopy}
            disabled={!result?.token}
          >
            <Text style={[styles.buttonText, { color: theme.textOnAccent }]}>
              Copy Token
            </Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[
              styles.button,
              { backgroundColor: theme.card, borderColor: theme.border, borderWidth: StyleSheet.hairlineWidth },
            ]}
            onPress={fetchToken}
            disabled={loading}
          >
            <Text style={[styles.buttonText, { color: theme.text }]}>
              Refresh
            </Text>
          </TouchableOpacity>
        </View>

        {copyFeedback ? (
          <Text style={[styles.feedback, { color: theme.pickCorrect }]}>
            {copyFeedback}
          </Text>
        ) : null}
      </ScrollView>
    </>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  content: { padding: 20, gap: 14 },
  heading: { fontSize: 22, fontWeight: '800' },
  subhead: { fontSize: 13, lineHeight: 18 },
  statusRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 12,
    borderRadius: 10,
    borderWidth: StyleSheet.hairlineWidth,
    marginTop: 6,
  },
  statusLabel: { fontSize: 12, fontWeight: '700', letterSpacing: 0.5, textTransform: 'uppercase' },
  statusValue: { fontSize: 14, fontWeight: '700' },
  errorBox: {
    padding: 10,
    borderRadius: 8,
    borderWidth: StyleSheet.hairlineWidth,
  },
  errorText: { fontSize: 13, lineHeight: 18 },
  tokenBox: {
    padding: 12,
    borderRadius: 10,
    borderWidth: StyleSheet.hairlineWidth,
    minHeight: 80,
  },
  tokenText: { fontSize: 12, lineHeight: 18, fontFamily: 'SpaceMono' },
  actions: { flexDirection: 'row', gap: 10, marginTop: 4 },
  button: {
    flex: 1,
    paddingVertical: 12,
    borderRadius: 10,
    alignItems: 'center',
  },
  buttonText: { fontSize: 14, fontWeight: '700' },
  feedback: { fontSize: 13, fontWeight: '600', textAlign: 'center' },
});
