// deetsMeter — model win-probability bars (mobile port of
// sd-ui/src/components/matchups/DeetsMeter.jsx).
//
// Renders up to two meters (SU / ATS) as a single bar split at the away-team
// percentage: away color fills the left segment, home color the right. The
// web version draws this with a hard-stop CSS linear-gradient; two
// flex-weighted Views produce the identical visual with no gradient library.
//
// Meter selection mirrors web exactly: the league's pickType shows only its
// own meter; a null/undefined pickType shows both. Renders nothing when the
// matchup has no predictions (older contests, pipeline not yet run) — the
// card simply tightens up, same as web.

import React from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { getTheme } from '@/constants/Colors';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import type { ContestPrediction, PickType } from '@/src/types/models';

interface DeetsMeterProps {
  predictions?: ContestPrediction[] | null;
  /** League pick mode — filters which meters render (web parity). */
  pickType?: PickType | null;
  homeFranchiseSeasonId: string;
  awayFranchiseSeasonId: string;
}

interface MeterData {
  awayPercentage: number;
  homePercentage: number;
}

/**
 * Percentage math ported verbatim from web: the winProbability belongs to
 * winnerFranchiseSeasonId; home gets round(p) or round(1-p) accordingly and
 * away is the complement, so the two always sum to exactly 100.
 */
function getPredictionData(
  prediction: ContestPrediction | undefined,
  homeFranchiseSeasonId: string
): MeterData | null {
  if (!prediction) return null;

  const isHomeFavored = prediction.winnerFranchiseSeasonId === homeFranchiseSeasonId;
  const winProbability = prediction.winProbability;

  const homePercentage = isHomeFavored
    ? Math.round(winProbability * 100)
    : Math.round((1 - winProbability) * 100);
  const awayPercentage = 100 - homePercentage;

  return { awayPercentage, homePercentage };
}

export function DeetsMeter({
  predictions,
  pickType,
  homeFranchiseSeasonId,
  awayFranchiseSeasonId,
}: DeetsMeterProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const straightUpData = getPredictionData(
    predictions?.find((p) => p.predictionType === 'StraightUp'),
    homeFranchiseSeasonId
  );
  const atsData = getPredictionData(
    predictions?.find((p) => p.predictionType === 'AgainstTheSpread'),
    homeFranchiseSeasonId
  );

  // Nothing to show — render nothing, exactly like web.
  if (!straightUpData && !atsData) {
    return null;
  }

  // Deliberately muted, NOT team colors (owner call, 2026-09-01): a
  // team-colored bar reads as authoritative, and this is a single model
  // signal that can — and often does — disagree with StatBot's pick.
  // Matches what the web app actually renders per theme (App.css
  // --meter-*-fallback; its --away-color/--home-color hooks are never
  // set): dark = grayscale #444/#666, light = the accent/accent-hover
  // blue pair (#0077cc/#005fa3 — theme.tint is the same #0077cc).
  const awayFill = scheme === 'dark' ? '#444' : theme.tint;
  const homeFill = scheme === 'dark' ? '#666' : '#005fa3';

  const renderMeter = (data: MeterData | null, label: string) => {
    if (!data) return null;

    const { awayPercentage, homePercentage } = data;

    return (
      <View style={styles.meterRow} testID={`deetsmeter-${label.toLowerCase()}`}>
        <View style={[styles.meterBar, { borderColor: theme.border }]}>
          {/* Hard-stop split at awayPercentage — web's linear-gradient
              equivalent. Zero-width segments are legal flex values, so a
              100/0 split renders as a solid bar rather than crashing. */}
          <View style={{ flex: awayPercentage, backgroundColor: awayFill }} />
          <View style={{ flex: homePercentage, backgroundColor: homeFill }} />

          {/* Overlays — absolutely positioned atop the split bar. */}
          <View style={styles.meterOverlay} pointerEvents="none">
            <Text style={styles.meterPercentage}>{awayPercentage}%</Text>
            <Text style={styles.meterPercentage}>{homePercentage}%</Text>
          </View>
          <View style={styles.meterMidline} pointerEvents="none" />
          <View style={styles.meterLabelWrap} pointerEvents="none">
            <Text style={styles.meterLabel}>{label}</Text>
          </View>
        </View>
      </View>
    );
  };

  // Which meters show is the league's mode: only SU in a StraightUp league,
  // only ATS in a spread league, both when no mode is supplied (web parity).
  const showSU = !pickType || pickType === 'StraightUp';
  const showATS = !pickType || pickType === 'AgainstTheSpread';

  return (
    <View style={styles.container} testID="deetsmeter">
      <Text style={[styles.header, { color: theme.tint }]}>deetsMeter™</Text>
      <View style={styles.meters}>
        {showSU && renderMeter(straightUpData, 'SU')}
        {showATS && renderMeter(atsData, 'ATS')}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: 12,
    paddingBottom: 4,
  },
  header: {
    fontSize: 12,
    fontWeight: '600',
    textAlign: 'center',
    letterSpacing: 0.8,
    marginBottom: 4,
  },
  meters: {
    flexDirection: 'row',
    gap: 16,
  },
  meterRow: {
    flex: 1,
    minWidth: 0,
  },
  meterBar: {
    flexDirection: 'row',
    height: 32,
    borderRadius: 16,
    borderWidth: 2,
    overflow: 'hidden',
    position: 'relative',
  },
  meterOverlay: {
    ...StyleSheet.absoluteFillObject,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 8,
    zIndex: 2,
  },
  meterMidline: {
    position: 'absolute',
    top: 0,
    bottom: 0,
    left: '50%',
    width: 2,
    marginLeft: -1,
    backgroundColor: 'rgba(255, 255, 255, 0.3)',
    zIndex: 1,
  },
  meterLabelWrap: {
    ...StyleSheet.absoluteFillObject,
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 3,
  },
  meterLabel: {
    fontSize: 10,
    fontWeight: '700',
    color: 'rgba(255, 255, 255, 0.6)',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  meterPercentage: {
    fontSize: 12,
    fontWeight: '700',
    color: '#ffffff',
    textShadowColor: 'rgba(0, 0, 0, 0.5)',
    textShadowOffset: { width: 0, height: 1 },
    textShadowRadius: 2,
  },
});

export default DeetsMeter;
