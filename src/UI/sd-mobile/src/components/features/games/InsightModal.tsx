import React from 'react';
import {
  Modal,
  View,
  Text,
  ScrollView,
  TouchableOpacity,
  Image,
  StyleSheet,
  ActivityIndicator,
} from 'react-native';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Colors, getTheme } from '@/constants/Colors';
import type { Matchup, PreviewResponse } from '@/src/types/models';
import { formatToUserTime } from '@/src/utils/timeUtils';
import { useUserTimeZone } from '@/src/hooks/useUserTimeZone';

// ─── Props ────────────────────────────────────────────────────────────────────

interface InsightModalProps {
  visible: boolean;
  onClose: () => void;
  matchup: Matchup;
  preview: PreviewResponse | null;
  isLoading: boolean;
}

// ─── Section heading ──────────────────────────────────────────────────────────

function Section({ label, children }: { label: string; children: React.ReactNode }) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  return (
    <View style={styles.section}>
      <Text style={[styles.sectionLabel, { color: Colors.brand.navy }]}>{label}</Text>
      {children}
    </View>
  );
}

// ─── InsightModal ─────────────────────────────────────────────────────────────

export function InsightModal({ visible, onClose, matchup, preview, isLoading }: InsightModalProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const userTz = useUserTimeZone();

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      onRequestClose={onClose}
    >
      <View style={[styles.container, { backgroundColor: theme.background }]}>
        {/* Header */}
        <View style={[styles.header, { borderBottomColor: theme.border }]}>
          <View style={styles.headerLeft} />
          <Text style={[styles.headerTitle, { color: theme.text }]}>AI Preview</Text>
          <TouchableOpacity onPress={onClose} style={styles.closeBtn} hitSlop={12}>
            <Text style={[styles.closeText, { color: theme.textMuted }]}>✕</Text>
          </TouchableOpacity>
        </View>

        {isLoading ? (
          <View style={styles.loadingContainer}>
            <ActivityIndicator size="large" color={Colors.brand.navy} />
            <Text style={[styles.loadingText, { color: theme.textMuted }]}>
              Loading preview…
            </Text>
          </View>
        ) : preview == null ? (
          <View style={styles.loadingContainer}>
            <Text style={[styles.emptyText, { color: theme.textMuted }]}>
              Preview not available.
            </Text>
          </View>
        ) : (
          <ScrollView
            contentContainerStyle={styles.scroll}
            showsVerticalScrollIndicator={false}
          >
            {/* Helmet row */}
            <View style={[styles.helmetRow, { backgroundColor: theme.card, borderColor: theme.border }]}>
              {matchup.awayLogoUri ? (
                <Image source={{ uri: matchup.awayLogoUri }} style={styles.helmet} />
              ) : (
                <View style={[styles.helmetPlaceholder, { backgroundColor: matchup.awayColor ?? Colors.brand.navy }]}>
                  <Text style={styles.helmetInitial}>{matchup.awayShort?.[0] ?? '?'}</Text>
                </View>
              )}
              <Text style={[styles.vsText, { color: theme.textMuted }]}>
                {matchup.awayShort} @ {matchup.homeShort}
              </Text>
              {matchup.homeLogoUri ? (
                <Image source={{ uri: matchup.homeLogoUri }} style={styles.helmet} />
              ) : (
                <View style={[styles.helmetPlaceholder, { backgroundColor: matchup.homeColor ?? Colors.brand.navy }]}>
                  <Text style={styles.helmetInitial}>{matchup.homeShort?.[0] ?? '?'}</Text>
                </View>
              )}
            </View>

            {/* Overview */}
            {!!preview.overview && (
              <Section label="Overview">
                <Text style={[styles.bodyText, { color: theme.text }]}>{preview.overview}</Text>
              </Section>
            )}

            {/* Analysis */}
            {!!preview.analysis && (
              <Section label="Analysis">
                <Text style={[styles.bodyText, { color: theme.text }]}>{preview.analysis}</Text>
              </Section>
            )}

            {/* Vegas implied */}
            {!!preview.vegasImpliedScore && (
              <Section label="Vegas Implied Score">
                <Text style={[styles.bodyText, { color: theme.text }]}>{preview.vegasImpliedScore}</Text>
              </Section>
            )}

            {/* sportDeets prediction */}
            {(!!preview.prediction || preview.awayScore != null || preview.homeScore != null) && (
              <Section label="sportDeets™ Prediction">
                {!!preview.prediction && (
                  <Text style={[styles.bodyText, { color: theme.text }]}>{preview.prediction}</Text>
                )}

                {/* Predicted score */}
                {(preview.awayScore != null && preview.homeScore != null) && (
                  <View style={[styles.scoreRow, { backgroundColor: theme.card, borderColor: theme.border }]}>
                    <View style={styles.scoreSide}>
                      <Text style={[styles.scoreTeam, { color: theme.textMuted }]}>{matchup.awayShort}</Text>
                      <Text style={[styles.scoreValue, { color: theme.text }]}>{preview.awayScore}</Text>
                    </View>
                    <Text style={[styles.scoreDash, { color: theme.textMuted }]}>–</Text>
                    <View style={styles.scoreSide}>
                      <Text style={[styles.scoreTeam, { color: theme.textMuted }]}>{matchup.homeShort}</Text>
                      <Text style={[styles.scoreValue, { color: theme.text }]}>{preview.homeScore}</Text>
                    </View>
                  </View>
                )}

                {/* Winners */}
                <View style={styles.winnerRow}>
                  {!!preview.straightUpWinner && (
                    <View style={[styles.winnerChip, { backgroundColor: '#EEF2FF', borderColor: Colors.brand.navy }]}>
                      <Text style={[styles.winnerLabel, { color: Colors.brand.navy }]}>SU</Text>
                      <Text style={[styles.winnerValue, { color: Colors.brand.navy }]}>{preview.straightUpWinner}</Text>
                    </View>
                  )}
                  {!!preview.atsWinner && (
                    <View style={[styles.winnerChip, { backgroundColor: '#EEF2FF', borderColor: Colors.brand.navy }]}>
                      <Text style={[styles.winnerLabel, { color: Colors.brand.navy }]}>ATS</Text>
                      <Text style={[styles.winnerValue, { color: Colors.brand.navy }]}>{preview.atsWinner}</Text>
                    </View>
                  )}
                </View>

                {/* Generated time */}
                {!!preview.generatedUtc && (
                  <Text style={[styles.generatedText, { color: theme.textMuted }]}>
                    Generated {formatToUserTime(preview.generatedUtc, userTz)}
                  </Text>
                )}
              </Section>
            )}

          </ScrollView>
        )}
      </View>
    </Modal>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 14,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  headerLeft: {
    width: 32,
  },
  headerTitle: {
    fontSize: 17,
    fontWeight: '700',
  },
  closeBtn: {
    width: 32,
    alignItems: 'flex-end',
  },
  closeText: {
    fontSize: 17,
    fontWeight: '400',
  },
  loadingContainer: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 12,
  },
  loadingText: {
    fontSize: 15,
  },
  emptyText: {
    fontSize: 15,
  },
  scroll: {
    padding: 16,
    gap: 16,
  },
  helmetRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 12,
    padding: 16,
    borderRadius: 12,
    borderWidth: StyleSheet.hairlineWidth,
    marginBottom: 4,
  },
  helmet: {
    width: 52,
    height: 52,
    resizeMode: 'contain',
  },
  helmetPlaceholder: {
    width: 52,
    height: 52,
    borderRadius: 26,
    alignItems: 'center',
    justifyContent: 'center',
  },
  helmetInitial: {
    color: '#fff',
    fontSize: 20,
    fontWeight: '700',
  },
  vsText: {
    fontSize: 13,
    fontWeight: '600',
    flex: 1,
    textAlign: 'center',
  },
  section: {
    gap: 8,
    marginTop: 8,
  },
  sectionLabel: {
    fontSize: 12,
    fontWeight: '800',
    textTransform: 'uppercase',
    letterSpacing: 0.8,
  },
  bodyText: {
    fontSize: 15,
    lineHeight: 22,
  },
  scoreRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: 10,
    borderWidth: StyleSheet.hairlineWidth,
    padding: 12,
    gap: 8,
    marginTop: 4,
  },
  scoreSide: {
    flex: 1,
    alignItems: 'center',
    gap: 2,
  },
  scoreTeam: {
    fontSize: 12,
    fontWeight: '600',
    textTransform: 'uppercase',
    letterSpacing: 0.3,
  },
  scoreValue: {
    fontSize: 28,
    fontWeight: '700',
  },
  scoreDash: {
    fontSize: 22,
    fontWeight: '300',
  },
  winnerRow: {
    flexDirection: 'row',
    gap: 8,
    flexWrap: 'wrap',
    marginTop: 4,
  },
  winnerChip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    borderWidth: 1.5,
    borderRadius: 8,
    paddingVertical: 6,
    paddingHorizontal: 10,
  },
  winnerLabel: {
    fontSize: 11,
    fontWeight: '800',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  winnerValue: {
    fontSize: 14,
    fontWeight: '700',
  },
  generatedText: {
    fontSize: 11,
    marginTop: 4,
  },
});
