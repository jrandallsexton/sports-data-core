import React, { useEffect, useState } from 'react';
import {
  Modal,
  View,
  TextInput,
  Switch,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';
import type { LeagueSummary } from '@/src/services/api/leaguesApi';

interface Props {
  visible: boolean;
  league: LeagueSummary | null;
  submitting: boolean;
  onClose: () => void;
  onConfirm: (name: string, inviteMembers: boolean) => void;
}

/**
 * Duplicate-league sheet. Mirrors sd-ui's CloneLeagueDialog: name (pre-filled
 * "<Original> (Copy)") plus an option to invite the source league's members.
 * Config and the slate are copied server-side; picks are not.
 */
export function CloneLeagueModal({ visible, league, submitting, onClose, onConfirm }: Props) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const [name, setName] = useState('');
  const [inviteMembers, setInviteMembers] = useState(false);

  // The sheet stays mounted so it can animate, so initial state goes stale —
  // `league` is null on first mount and changes as the user picks a different
  // card. Re-seed each time it opens. Keyed on visible + league id only, so a
  // background leagues refetch can't wipe a half-typed name.
  useEffect(() => {
    if (!visible || !league) return;
    setName(`${league.name} (Copy)`);
    setInviteMembers(false);
  }, [visible, league?.id]); // eslint-disable-line react-hooks/exhaustive-deps -- seed only on open

  const trimmed = name.trim();
  const canSubmit = !submitting && trimmed.length > 0;

  return (
    <Modal
      visible={visible && !!league}
      animationType="slide"
      presentationStyle="pageSheet"
      onRequestClose={submitting ? undefined : onClose}
    >
      <View style={[styles.container, { backgroundColor: theme.background }]}>
        <View style={[styles.header, { borderBottomColor: theme.border }]}>
          <Text style={[styles.headerTitle, { color: theme.text }]}>Duplicate league</Text>
          <TouchableOpacity onPress={onClose} disabled={submitting} hitSlop={12}>
            <Text style={[styles.closeText, { color: theme.textMuted }]}>✕</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.body}>
          <Text style={[styles.hint, { color: theme.textMuted }]}>
            Create a copy of {league?.name} with the same settings and games.
            Picks aren&rsquo;t copied.
          </Text>

          <Text style={[styles.label, { color: theme.text }]}>Name</Text>
          <TextInput
            value={name}
            onChangeText={setName}
            editable={!submitting}
            autoFocus
            selectTextOnFocus
            placeholder="League name"
            placeholderTextColor={theme.textMuted}
            style={[
              styles.input,
              { color: theme.text, borderColor: theme.border, backgroundColor: theme.card },
            ]}
            accessibilityLabel="Name for the duplicated league"
          />

          <View style={styles.toggleRow}>
            <Text style={[styles.toggleLabel, { color: theme.text }]} numberOfLines={2}>
              Invite members from {league?.name}
            </Text>
            <Switch
              value={inviteMembers}
              onValueChange={setInviteMembers}
              disabled={submitting}
              accessibilityLabel="Invite members from the source league"
            />
          </View>
        </View>

        <View style={[styles.footer, { borderTopColor: theme.border }]}>
          <TouchableOpacity
            style={[styles.button, styles.cancel, { borderColor: theme.border }]}
            onPress={onClose}
            disabled={submitting}
          >
            <Text style={[styles.buttonText, { color: theme.text }]}>Cancel</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[
              styles.button,
              styles.confirm,
              { backgroundColor: theme.tint },
              !canSubmit && styles.disabled,
            ]}
            onPress={() => onConfirm(trimmed, inviteMembers)}
            disabled={!canSubmit}
          >
            <Text style={[styles.buttonText, { color: theme.textOnAccent }]}>
              {submitting ? 'Creating…' : 'Create copy'}
            </Text>
          </TouchableOpacity>
        </View>

        {submitting && (
          <View style={styles.savingOverlay}>
            <ActivityIndicator size="small" color={theme.tint} />
          </View>
        )}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  headerTitle: { fontSize: 18, fontWeight: '700' },
  closeText: { fontSize: 20, fontWeight: '600' },
  body: { flex: 1, paddingHorizontal: 16, paddingTop: 16, gap: 8 },
  hint: { fontSize: 14, lineHeight: 20, marginBottom: 8 },
  label: { fontSize: 13, fontWeight: '600' },
  input: {
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    fontSize: 16,
  },
  toggleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: 12,
    paddingTop: 12,
  },
  toggleLabel: { fontSize: 14, fontWeight: '600', flexShrink: 1 },
  footer: {
    flexDirection: 'row',
    gap: 12,
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderTopWidth: StyleSheet.hairlineWidth,
  },
  button: {
    flex: 1,
    paddingVertical: 12,
    borderRadius: 10,
    alignItems: 'center',
  },
  cancel: { borderWidth: 1 },
  confirm: {},
  disabled: { opacity: 0.5 },
  buttonText: { fontSize: 15, fontWeight: '700' },
  savingOverlay: {
    ...StyleSheet.absoluteFillObject,
    alignItems: 'center',
    justifyContent: 'center',
  },
});
