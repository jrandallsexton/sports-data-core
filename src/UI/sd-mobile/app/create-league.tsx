import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  View,
  TextInput,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  Switch,
  TouchableOpacity,
  Alert,
  Modal,
} from 'react-native';
import DateTimePicker, {
  DateTimePickerAndroid,
  type DateTimePickerEvent,
} from '@react-native-community/datetimepicker';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useRouter, Stack, useLocalSearchParams } from 'expo-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { getTheme, type ColorScheme } from '@/constants/Colors';
import { Button } from '@/src/components/ui/Button';
import { SegmentedControl } from '@/src/components/ui/SegmentedControl';
import {
  leaguesApi,
  leaguesKeys,
  type CreateBaseballMlbLeagueRequest,
  type CreateFootballNcaaLeagueRequest,
  type CreateFootballNflLeagueRequest,
  type NcaaRankingFilter,
  type PickType,
  type SeasonWeekOption,
  type TiebreakerType,
} from '@/src/services/api/leaguesApi';
import { conferencesApi, conferencesKeys, type ConferenceOption } from '@/src/services/api/conferencesApi';
import { standingsKeys } from '@/src/hooks/useStandings';
import { useCurrentUser } from '@/src/hooks/useStandings';
import {
  useLeagueCreationGates,
  formatGateDateOrSoon,
} from '@/src/hooks/useLeagueCreationGates';

// ─── Sport config ─────────────────────────────────────────────────────────────
//
// Mirrors sd-ui/src/components/leagues/LeagueCreatePage.jsx. Division slugs
// match the BE seed data; NCAA omits a conference picker on mobile today
// (the web pulls the live list via ConferencesApi; a mobile Conferences API
// module is deferred — NCAA commissioners can still create leagues without
// a conference filter, just not cherry-pick specific conferences yet).

type SportKey = 'FootballNcaa' | 'FootballNfl' | 'BaseballMlb';

const NFL_DIVISIONS: { slug: string; shortName: string }[] = [
  { slug: 'afc-east', shortName: 'AFC East' },
  { slug: 'afc-north', shortName: 'AFC North' },
  { slug: 'afc-south', shortName: 'AFC South' },
  { slug: 'afc-west', shortName: 'AFC West' },
  { slug: 'nfc-east', shortName: 'NFC East' },
  { slug: 'nfc-north', shortName: 'NFC North' },
  { slug: 'nfc-south', shortName: 'NFC South' },
  { slug: 'nfc-west', shortName: 'NFC West' },
];

const MLB_DIVISIONS: { slug: string; shortName: string }[] = [
  { slug: 'american-league-east', shortName: 'AL East' },
  { slug: 'american-league-central', shortName: 'AL Cent' },
  { slug: 'american-league-west', shortName: 'AL West' },
  { slug: 'national-league-east', shortName: 'NL East' },
  { slug: 'national-league-central', shortName: 'NL Cent' },
  { slug: 'national-league-west', shortName: 'NL West' },
];

const SPORT_COPY: Record<SportKey, {
  label: string;
  emoji: string;
  namePlaceholder: string;
  descPlaceholder: string;
  tiebreakerTotalLabel: string;
}> = {
  FootballNcaa: {
    label: 'NCAA',
    emoji: '🏈',
    namePlaceholder: 'e.g., Saturday Showdown',
    descPlaceholder: 'A fun league for SEC fans.',
    tiebreakerTotalLabel: 'Closest Total',
  },
  FootballNfl: {
    label: 'NFL',
    emoji: '🏈',
    namePlaceholder: 'e.g., Sunday Funday',
    descPlaceholder: 'A fun league for NFL fans.',
    tiebreakerTotalLabel: 'Closest Total',
  },
  BaseballMlb: {
    label: 'MLB',
    emoji: '⚾',
    namePlaceholder: 'e.g., Ninth Inning',
    descPlaceholder: 'A fun league for MLB fans.',
    tiebreakerTotalLabel: 'Closest Runs',
  },
};

const VALID_SPORT_PARAMS = new Set<SportKey>([
  'FootballNcaa',
  'FootballNfl',
  'BaseballMlb',
]);

// ─── Grid layout helpers ──────────────────────────────────────────────────────

// Pick a column count for laying out an even number of pills in a balanced
// grid. Picks the factor pair closest to a square shape: 6 → 3 cols x 2 rows,
// 8 → 4 x 2, 4 → 2 x 2, 10 → 5 x 2. For non-even (or prime) counts the picker
// falls back to flexWrap upstream — the strict-even rule mirrors the user
// expectation that NFL (8) and MLB (6) should produce a balanced grid, while
// future odd counts (e.g. an NCAA conference picker) keep natural wrapping.
const balancedGridColumns = (count: number): number => {
  if (count <= 1) return 1;
  for (let c = Math.ceil(Math.sqrt(count)); c <= count; c++) {
    if (count % c === 0) return c;
  }
  return count;
};

const chunkInto = <T,>(arr: T[], size: number): T[][] => {
  const out: T[][] = [];
  for (let i = 0; i < arr.length; i += size) out.push(arr.slice(i, i + size));
  return out;
};

// ─── Validation schema ────────────────────────────────────────────────────────

// League Window. Web's "Week Range" mode is deliberately kept web-only (operator
// call, 2026-08-02) — Date Range already covers the same span semantics on
// mobile, and picking a phase-aware week range is a weekday-workbench act, not a
// phone-on-the-couch one. NOTE: the technical blocker is gone (the BE
// season-calendar endpoint shipped with #579, so web's Week Range now submits
// cleanly); reversing this is purely additive if the posture changes. See
// docs/mobile/web-parity-join-discovery.md.
const DURATION_FULL = 'full';
const DURATION_WEEKS = 'weeks';
const DURATION_DATES = 'dates';

// Suggested-description building blocks — mirrors sd-ui's LeagueCreatePage. A
// compact, glanceable tag prefilled into the (optional) description field so a
// commissioner doesn't leave it blank; it's what makes leagues legible on the
// home YourLeaguesCard for members in several leagues. Terse by design:
// "NCAAFB ATS w/Confidence", "MLB SU · Aug 29".
const SPORT_DESC_PHRASE: Record<SportKey, string> = {
  FootballNcaa: 'NCAAFB',
  FootballNfl: 'NFL',
  BaseballMlb: 'MLB',
};

const PICK_TYPE_DESC_PHRASE: Record<string, string> = {
  StraightUp: 'SU',
  AgainstTheSpread: 'ATS',
  OverUnder: 'O/U',
};

// "Aug 29" from a YYYY-MM-DD value. Parsed at local midnight so the calendar day
// isn't shifted back by a UTC parse. null for empty input.
function formatDateShort(iso: string): string | null {
  if (!iso) return null;
  const d = new Date(`${iso}T00:00:00`);
  return Number.isNaN(d.getTime())
    ? null
    : d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

// The suggested tag, enriched by whatever's chosen so far. Gated on sport alone
// (always set) so it's robust; pick type / confidence / window refine it.
// `windowLabel` is a pre-formatted day/range string, or null for full season.
function buildSuggestedDescription(
  sport: SportKey,
  pickType: string,
  useConfidencePoints: boolean,
  windowLabel: string | null,
): string {
  const sportPhrase = SPORT_DESC_PHRASE[sport];
  const pickPhrase = PICK_TYPE_DESC_PHRASE[pickType];
  let tag = pickPhrase ? `${sportPhrase} ${pickPhrase}` : sportPhrase;
  if (useConfidencePoints) tag += ' w/Confidence';
  if (windowLabel) tag += ` · ${windowLabel}`;
  return tag;
}

const schema = z
  .object({
    sport: z.enum(['FootballNcaa', 'FootballNfl', 'BaseballMlb']),
    name: z.string().trim().min(1, 'Name is required').max(100, 'Name must be 100 characters or fewer'),
    description: z.string().max(500, 'Description must be 500 characters or fewer').optional(),
    pickType: z.enum(['StraightUp', 'AgainstTheSpread']),
    tiebreakerType: z.enum(['TotalPoints', 'EarliestSubmission']),
    useConfidencePoints: z.boolean(),
    isPublic: z.boolean(),
    rankingFilter: z.enum(['', 'AP_TOP_25', 'AP_TOP_20', 'AP_TOP_15', 'AP_TOP_10', 'AP_TOP_5']),
    divisionSlugs: z.array(z.string()),
    // NCAA-only: FBS conference slugs UNIONED with the ranking filter by the
    // matchup processor (a game survives on a rank hit OR a conference hit).
    conferenceSlugs: z.array(z.string()),
    durationMode: z.enum([DURATION_FULL, DURATION_WEEKS, DURATION_DATES]),
    // SeasonWeek ids for the Week Range window; empty string = unselected.
    startWeekId: z.string(),
    endWeekId: z.string(),
    joinPolicy: z.enum(['Open', 'CloseAtFirstGame']),
    // YYYY-MM-DD or empty string. Stored as a plain date string (no TZ) so the
    // submit-time conversion to ISO can anchor at local midnight / end-of-day
    // without timezone drift — matches web's toStartOfDayIso / toEndOfDayIso.
    startsOn: z.string(),
    endsOn: z.string(),
    dropLowWeeksCount: z.number().int().min(0).max(3),
  })
  .superRefine((data, ctx) => {
    if (data.durationMode === DURATION_WEEKS) {
      if (!data.startWeekId) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['startWeekId'], message: 'Start week is required' });
      }
      if (!data.endWeekId) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['endWeekId'], message: 'End week is required' });
      }
      return;
    }
    if (data.durationMode !== DURATION_DATES) return;
    if (!data.startsOn) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['startsOn'], message: 'Start date is required' });
    }
    if (!data.endsOn) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['endsOn'], message: 'End date is required' });
    }
    if (data.startsOn && data.endsOn && data.endsOn < data.startsOn) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['endsOn'], message: 'End date must be on or after the start date' });
    }
    // Mirror of the server `EffectiveEndsOn > now` rule. Recomputes today
    // at validation time so a long-running form session can't sneak through
    // a now-stale date.
    const today = getTodayIsoDate();
    if (data.endsOn && data.endsOn < today) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, path: ['endsOn'], message: "End date can't be in the past" });
    }
  });

type FormData = z.infer<typeof schema>;

const PICK_TYPE_OPTIONS: { value: FormData['pickType']; label: string }[] = [
  { value: 'StraightUp', label: 'Straight Up' },
  { value: 'AgainstTheSpread', label: 'Against Spread' },
];

// No "All"/"None" option on mobile (operator ruling 2026-08-18): the matchup
// slate is built from rank hits and conference hits only, and mobile has no
// conference picker yet — a rankings-free league here would have no games.
// Web offers "None" because its conference table satisfies the
// at-least-one-filter rule the server validator enforces.
const RANKING_OPTIONS: { value: FormData['rankingFilter']; label: string }[] = [
  { value: 'AP_TOP_25', label: 'Top 25' },
  { value: 'AP_TOP_20', label: 'Top 20' },
  { value: 'AP_TOP_15', label: 'Top 15' },
  { value: 'AP_TOP_10', label: 'Top 10' },
  { value: 'AP_TOP_5', label: 'Top 5' },
];

const VISIBILITY_OPTIONS: { value: 'private' | 'public'; label: string }[] = [
  { value: 'private', label: 'Private' },
  { value: 'public', label: 'Public' },
];

const WINDOW_OPTIONS: { value: 'full' | 'weeks' | 'dates'; label: string }[] = [
  { value: DURATION_FULL, label: 'Full Season' },
  { value: DURATION_WEEKS, label: 'Week Range' },
  { value: DURATION_DATES, label: 'Date Range' },
];

// Who can join, and until when. Mirrors sd-ui's create form. "Locked at
// kickoff" = CloseAtFirstGame: the roster closes when the league's first game
// starts. Applies to invite links too, not just public discovery.
const JOIN_POLICY_OPTIONS: { value: 'Open' | 'CloseAtFirstGame'; label: string }[] = [
  { value: 'Open', label: 'Open' },
  { value: 'CloseAtFirstGame', label: 'Locked at kickoff' },
];

// Stringified for SegmentedControl, which keys options by string value. Coerced
// back to number at the Controller boundary.
const DROP_LOW_WEEKS_OPTIONS: { value: string; label: string }[] = [
  { value: '0', label: 'None' },
  { value: '1', label: '1' },
  { value: '2', label: '2' },
  { value: '3', label: '3' },
];

// ─── Date helpers ─────────────────────────────────────────────────────────────

// Parse 'YYYY-MM-DD' into a local-midnight Date. Avoids `new Date(str)`'s
// implicit UTC interpretation for date-only strings, which would skew the
// display by up to 24 hours for users east/west of UTC.
const parseDateOnly = (s: string): Date => {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d);
};

const formatDateOnlyDisplay = (s: string): string =>
  parseDateOnly(s).toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });

const dateToIsoDateOnly = (d: Date): string => {
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
};

// Today as 'YYYY-MM-DD' anchored at the user's local calendar day. Used for
// the date-picker `minimumDate` floor and the Zod `superRefine` rule. The
// server-side `EffectiveEndsOn > now` validator is the trust boundary —
// these UI guards just prevent users from constructing an invalid window in
// the first place.
const getTodayIsoDate = (): string => dateToIsoDateOnly(new Date());

// Mirrors web's toStartOfDayIso / toEndOfDayIso. Anchored at the caller's
// local timezone — appending 'Z' would wrongly treat the local calendar
// date as UTC, skewing the window by up to 24 hours.
const toStartOfDayIso = (s: string): string | null => {
  if (!s) return null;
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d, 0, 0, 0).toISOString();
};

const toEndOfDayIso = (s: string): string | null => {
  if (!s) return null;
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, m - 1, d, 23, 59, 59).toISOString();
};

// ─── DateField ────────────────────────────────────────────────────────────────

type ThemePalette = ReturnType<typeof getTheme>;

type DateFieldProps = {
  value: string;
  onChange: (v: string) => void;
  placeholder: string;
  accessibilityLabel: string;
  theme: ThemePalette;
  scheme: ColorScheme;
  error?: string;
  minimumDate?: Date;
};

/**
 * Date input backed by each platform's own picker. A single
 * `<DateTimePicker display="default">` render path misbehaved on two of the
 * three targets, so each gets the interaction its picker is actually built for:
 *
 *  - **Android** — the imperative `DateTimePickerAndroid.open()` API. This is
 *    the library's documented Android path: it fires the dialog exactly once,
 *    where a declaratively-rendered picker can re-open or desync when the tree
 *    re-renders while the dialog is up.
 *  - **iOS** — a spinner in an explicit Cancel/Done modal, editing a *draft*
 *    date. `display="default"` renders the picker INLINE on iOS 14+, which is
 *    the stray calendar that appeared below the field. Worse, the committed
 *    value was fed straight back in as `value`, so every scroll tick
 *    round-tripped through `dateToIsoDateOnly` (which drops the time and
 *    re-anchors at local midnight) and snapped the wheel back — the "fighting
 *    it" symptom. The draft breaks that loop: nothing commits until Done.
 *  - **Web** — the library ships no web implementation; it renders `null` and
 *    console-warns, which is why Chrome showed no picker at all. A native
 *    `<input type="date">` stands in (react-native-web renders to the DOM, so a
 *    host element is legal here), and its value format is already the
 *    `YYYY-MM-DD` we store. Date inputs ignore `placeholder` and supply their
 *    own `mm/dd/yyyy` hint, so it's only used for the native branches' label.
 */
function DateField({
  value,
  onChange,
  placeholder,
  accessibilityLabel,
  theme,
  scheme,
  error,
  minimumDate,
}: DateFieldProps) {
  const [iosOpen, setIosOpen] = useState(false);
  const [draft, setDraft] = useState<Date | null>(null);

  // Seed at or after `minimumDate`. With an empty field the fallback is today,
  // which can precede a later floor — the End field's floor is `startsOn` when
  // that's in the future. iOS clamps the wheel's *display* to minimumDate but
  // doesn't fire onChange for that clamp, so a straight Done would commit the
  // earlier seeded date rather than the one on screen. Android's dialog opens
  // on this value too, so it wants the same floor.
  const parsedValue = value ? parseDateOnly(value) : new Date();
  const dateValue =
    minimumDate && parsedValue < minimumDate ? minimumDate : parsedValue;
  const display = value ? formatDateOnlyDisplay(value) : placeholder;
  const hasValue = value.length > 0;

  const commit = (d: Date) => onChange(dateToIsoDateOnly(d));

  const openPicker = () => {
    if (Platform.OS === 'android') {
      DateTimePickerAndroid.open({
        value: dateValue,
        mode: 'date',
        minimumDate,
        onChange: (event: DateTimePickerEvent, selected?: Date) => {
          if (event.type === 'set' && selected) commit(selected);
        },
      });
      return;
    }
    // Seed the draft from the current value so Done-without-scrolling is a
    // no-op commit rather than a jump to today.
    setDraft(dateValue);
    setIosOpen(true);
  };

  const errorNode = error ? (
    <Text style={[styles.fieldError, { color: theme.error }]}>{error}</Text>
  ) : null;

  if (Platform.OS === 'web') {
    return (
      <>
        {React.createElement('input', {
          type: 'date',
          value,
          min: minimumDate ? dateToIsoDateOnly(minimumDate) : undefined,
          'aria-label': accessibilityLabel,
          onChange: (e: { target: { value: string } }) => onChange(e.target.value),
          style: {
            backgroundColor: theme.card,
            color: theme.text,
            borderWidth: 1.5,
            borderStyle: 'solid',
            borderColor: error ? theme.error : theme.border,
            borderRadius: 10,
            padding: '12px 14px',
            fontSize: 16,
            fontFamily: 'inherit',
            width: '100%',
            boxSizing: 'border-box',
          },
          // eslint-disable-next-line @typescript-eslint/no-explicit-any -- DOM host
          // element: RN's JSX typings don't declare intrinsic web elements.
        } as any)}
        {errorNode}
      </>
    );
  }

  return (
    <>
      <TouchableOpacity
        style={[
          styles.input,
          {
            backgroundColor: theme.card,
            borderColor: error ? theme.error : theme.border,
            justifyContent: 'center',
          },
        ]}
        onPress={openPicker}
        accessibilityRole="button"
        accessibilityLabel={accessibilityLabel}
      >
        <Text style={{ color: hasValue ? theme.text : theme.textMuted, fontSize: 16 }}>
          {display}
        </Text>
      </TouchableOpacity>

      {/* iOS only — Android's dialog is driven imperatively above. */}
      <Modal
        visible={iosOpen}
        transparent
        animationType="slide"
        onRequestClose={() => setIosOpen(false)}
      >
        <TouchableOpacity
          style={styles.pickerBackdrop}
          activeOpacity={1}
          onPress={() => setIosOpen(false)}
          accessibilityRole="button"
          accessibilityLabel="Dismiss date picker"
        />
        <View style={[styles.pickerSheet, { backgroundColor: theme.card, borderTopColor: theme.border }]}>
          <View style={[styles.pickerBar, { borderBottomColor: theme.border }]}>
            <TouchableOpacity onPress={() => setIosOpen(false)} hitSlop={12}>
              <Text style={[styles.pickerBarAction, { color: theme.textMuted }]}>Cancel</Text>
            </TouchableOpacity>
            <Text style={[styles.pickerBarTitle, { color: theme.text }]}>{accessibilityLabel}</Text>
            <TouchableOpacity
              onPress={() => {
                if (draft) commit(draft);
                setIosOpen(false);
              }}
              hitSlop={12}
            >
              <Text style={[styles.pickerBarAction, styles.pickerBarDone, { color: theme.tint }]}>
                Done
              </Text>
            </TouchableOpacity>
          </View>
          <DateTimePicker
            value={draft ?? dateValue}
            mode="date"
            display="spinner"
            // Without this the wheel follows the OS appearance, which renders
            // dark text on the dark sheet when the in-app theme is overridden.
            themeVariant={scheme}
            minimumDate={minimumDate}
            onChange={(_event: DateTimePickerEvent, selected?: Date) => {
              if (selected) setDraft(selected);
            }}
          />
        </View>
      </Modal>

      {errorNode}
    </>
  );
}

// Formats a week's UTC boundary for picker rows without local-TZ drift — a
// Sep 6 00:00Z start must not render as Sep 5 in western timezones.
function fmtWeekDateUtc(iso: string): string {
  return new Date(iso).toLocaleDateString(undefined, {
    month: 'short',
    day: 'numeric',
    timeZone: 'UTC',
  });
}

const weekOptionLabel = (w: SeasonWeekOption) =>
  `${w.label}: ${fmtWeekDateUtc(w.startDateUtc)}–${fmtWeekDateUtc(w.endDateUtc)}`;

interface WeekFieldProps {
  value: string;
  onChange: (id: string) => void;
  options: SeasonWeekOption[];
  placeholder: string;
  accessibilityLabel: string;
  theme: ReturnType<typeof getTheme>;
  error?: string;
}

/**
 * Season-week selector for the Week Range window. A field-styled button
 * opening a bottom-sheet list — one cross-platform Modal (unlike DateField
 * there's no native picker involved, so no platform split is needed).
 */
function WeekField({
  value,
  onChange,
  options,
  placeholder,
  accessibilityLabel,
  theme,
  error,
}: WeekFieldProps) {
  const [open, setOpen] = useState(false);
  const selected = options.find((w) => w.id === value) ?? null;

  return (
    <>
      <TouchableOpacity
        style={[
          styles.input,
          {
            backgroundColor: theme.card,
            borderColor: error ? theme.error : theme.border,
            justifyContent: 'center',
          },
        ]}
        onPress={() => setOpen(true)}
        accessibilityRole="button"
        accessibilityLabel={accessibilityLabel}
      >
        <Text style={{ color: selected ? theme.text : theme.textMuted, fontSize: 16 }}>
          {selected ? weekOptionLabel(selected) : placeholder}
        </Text>
      </TouchableOpacity>

      <Modal
        visible={open}
        transparent
        animationType="slide"
        onRequestClose={() => setOpen(false)}
      >
        <TouchableOpacity
          style={styles.pickerBackdrop}
          activeOpacity={1}
          onPress={() => setOpen(false)}
          accessibilityRole="button"
          accessibilityLabel="Dismiss week picker"
        />
        <View style={[styles.pickerSheet, { backgroundColor: theme.card, borderTopColor: theme.border }]}>
          <View style={[styles.pickerBar, { borderBottomColor: theme.border }]}>
            <TouchableOpacity onPress={() => setOpen(false)} hitSlop={12}>
              <Text style={[styles.pickerBarAction, { color: theme.textMuted }]}>Cancel</Text>
            </TouchableOpacity>
            <Text style={[styles.pickerBarTitle, { color: theme.text }]}>{accessibilityLabel}</Text>
            <View style={styles.pickerBarSpacer} />
          </View>
          <ScrollView style={styles.weekList}>
            {options.map((w) => {
              const active = w.id === value;
              return (
                <TouchableOpacity
                  key={w.id}
                  onPress={() => {
                    onChange(w.id);
                    setOpen(false);
                  }}
                  accessibilityRole="button"
                  accessibilityLabel={weekOptionLabel(w)}
                  style={[styles.weekRow, { borderBottomColor: theme.border }]}
                >
                  <Text
                    style={{
                      color: active ? theme.tint : theme.text,
                      fontSize: 16,
                      fontWeight: active ? '700' : '400',
                    }}
                  >
                    {weekOptionLabel(w)}
                  </Text>
                </TouchableOpacity>
              );
            })}
          </ScrollView>
        </View>
      </Modal>

      {error ? <Text style={{ color: theme.error, fontSize: 12 }}>{error}</Text> : null}
    </>
  );
}

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function CreateLeagueScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const queryClient = useQueryClient();
  const params = useLocalSearchParams<{ sport?: string }>();
  const { data: me } = useCurrentUser();
  const isAdmin = me?.isAdmin === true;
  // Active league-creation gates: { FootballNcaa: "2026-08-17T00:00:00Z", ... }.
  // A sport present is locked until that instant (e.g. NCAAFB awaiting AP Poll
  // release). Empty until loaded / on fetch failure — the server guard is the
  // real enforcement. See docs/features/league-creation-availability-gate.md.
  const gates = useLeagueCreationGates();

  // Safe initial sport for useForm defaultValues (which are cached on first
  // render and don't respond to later changes). MLB is admin-gated and
  // /user/me is async, so we can't know at form-init time whether the user
  // is allowed to land on MLB — we unconditionally defer MLB to a
  // post-mount effect that promotes the form once isAdmin resolves.
  // NCAA/NFL deep-links still preselect immediately.
  const initialSport = useMemo<SportKey>(() => {
    const raw = params.sport;
    if (!raw || !VALID_SPORT_PARAMS.has(raw as SportKey)) return 'FootballNcaa';
    if (raw === 'BaseballMlb') return 'FootballNcaa';
    return raw as SportKey;
  }, [params.sport]);

  const {
    control,
    handleSubmit,
    formState: { errors },
    watch,
    setValue,
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      sport: initialSport,
      name: '',
      description: '',
      pickType: 'StraightUp',
      tiebreakerType: 'TotalPoints',
      useConfidencePoints: false,
      isPublic: false,
      // NCAA requires a ranking filter on mobile (no conference picker yet);
      // '' is only valid for sports where the filter doesn't apply.
      rankingFilter: initialSport === 'FootballNcaa' ? 'AP_TOP_25' : '',
      // NFL/MLB: preselect all divisions so "include everyone" is one click.
      // NCAA: empty (no conference picker UI yet).
      divisionSlugs:
        initialSport === 'FootballNfl'
          ? NFL_DIVISIONS.map((d) => d.slug)
          : initialSport === 'BaseballMlb'
          ? MLB_DIVISIONS.map((d) => d.slug)
          : [],
      durationMode: DURATION_FULL,
      joinPolicy: 'Open',
      startsOn: '',
      endsOn: '',
      startWeekId: '',
      endWeekId: '',
      conferenceSlugs: [],
      dropLowWeeksCount: 0,
    },
  });

  const sport = watch('sport');
  const divisionSlugs = watch('divisionSlugs');
  const durationMode = watch('durationMode');
  const pickType = watch('pickType');
  const useConfidencePoints = watch('useConfidencePoints');
  // Watched for two reasons: (1) the end-date picker clamps its
  // minimumDate to startsOn so the user can't pick an end earlier than
  // the start, and (2) if the user moves startsOn *past* an already-set
  // endsOn, the effect below clamps endsOn forward to keep the window
  // valid without waiting for submit-time validation to flag it.
  const startsOn = watch('startsOn');
  const endsOn = watch('endsOn');

  useEffect(() => {
    if (durationMode !== DURATION_DATES) return;
    if (!startsOn || !endsOn) return;
    if (endsOn < startsOn) {
      setValue('endsOn', startsOn, { shouldDirty: true, shouldValidate: true });
    }
  }, [durationMode, startsOn, endsOn, setValue]);

  // Season weeks for the Week Range window. Fetched lazily — only once the
  // user actually selects that mode — and keyed by sport so switching sports
  // serves the right season's weeks.
  const startWeekId = watch('startWeekId');
  const endWeekId = watch('endWeekId');
  const { data: seasonWeeks = [] } = useQuery({
    // The endpoint returns an envelope ({ seasonYear, weeks }) — unwrap to
    // the list here so everything downstream works with a plain array
    // (web twin: `data?.weeks ?? []`). Completed weeks are excluded — a new
    // league can't start in a week that has already ended (the web picker
    // disables them via the same endDateUtc test).
    queryKey: leaguesKeys.seasonWeeks(sport),
    queryFn: async () => (await leaguesApi.getSeasonWeeks(sport)).data?.weeks ?? [],
    enabled: durationMode === DURATION_WEEKS,
    select: (weeks) =>
      weeks.filter((w) => new Date(w.endDateUtc).getTime() > Date.now()),
  });
  const startWeekIndex = seasonWeeks.findIndex((w) => w.id === startWeekId);
  const endWeekIndex = seasonWeeks.findIndex((w) => w.id === endWeekId);
  const startWeekObj = startWeekIndex >= 0 ? seasonWeeks[startWeekIndex] : null;
  const endWeekObj = endWeekIndex >= 0 ? seasonWeeks[endWeekIndex] : null;

  // Mirror of web: picking a start week after the current end week clamps
  // the end forward so the window can't invert.
  useEffect(() => {
    if (durationMode !== DURATION_WEEKS) return;
    if (!startWeekId) return;
    if (!endWeekId || (endWeekIndex >= 0 && startWeekIndex > endWeekIndex)) {
      setValue('endWeekId', startWeekId, { shouldDirty: true, shouldValidate: true });
    }
  }, [durationMode, startWeekId, endWeekId, startWeekIndex, endWeekIndex, setValue]);

  // Drop Low Weeks can't equal or exceed the playable weeks — a one-week
  // Week Range dropping 1 week would drop everything (web applies the same
  // weekCount-1 cap). Only computable in weeks mode; other modes keep the
  // static 0-3 options.
  const selectedWeekCount =
    durationMode === DURATION_WEEKS && startWeekIndex >= 0 && endWeekIndex >= 0
      ? endWeekIndex - startWeekIndex + 1
      : null;
  const dropMax =
    selectedWeekCount !== null ? Math.max(0, Math.min(3, selectedWeekCount - 1)) : 3;
  const dropLowWeeksCount = watch('dropLowWeeksCount');
  useEffect(() => {
    if (dropLowWeeksCount > dropMax) {
      setValue('dropLowWeeksCount', dropMax, { shouldDirty: true, shouldValidate: true });
    }
  }, [dropLowWeeksCount, dropMax, setValue]);

  // Suggested description window: weeks ("Week 3" / "Week 3 – Week 8"),
  // dates (single day or range), or null (full season).
  const descriptionWindowLabel = (() => {
    if (durationMode === DURATION_WEEKS) {
      if (!startWeekObj || !endWeekObj) return null;
      return startWeekObj.id === endWeekObj.id
        ? startWeekObj.label
        : `${startWeekObj.label} – ${endWeekObj.label}`;
    }
    if (durationMode !== DURATION_DATES) return null;
    const s = formatDateShort(startsOn);
    const e = formatDateShort(endsOn);
    if (!s && !e) return null;
    // Single-day is decided by the raw ISO values, not the formatted labels —
    // the label drops the year, so dates a year apart would format identically.
    if (s && e) return startsOn === endsOn ? s : `${s}–${e}`;
    return s || e;
  })();

  const suggestedDescription = buildSuggestedDescription(
    sport,
    pickType,
    useConfidencePoints,
    descriptionWindowLabel,
  );

  // Prefill the description with the suggested tag until the user edits it, so
  // the field is populated without ever clobbering deliberate input. RHF holds
  // the value, so the submit payload picks it up automatically.
  const descriptionEditedRef = useRef(false);
  useEffect(() => {
    if (descriptionEditedRef.current) return;
    setValue('description', suggestedDescription);
  }, [suggestedDescription, setValue]);

  // Today as 'YYYY-MM-DD' for the date-picker floors. Memoized so re-renders
  // during the form session don't create a new Date object per render, but
  // intentionally NOT recomputed at midnight — the Zod superRefine catches
  // a stale "today" on submit if the user leaves the form open overnight.
  const todayIsoDate = useMemo(() => getTodayIsoDate(), []);
  const endsOnMinIsoDate =
    startsOn && startsOn > todayIsoDate ? startsOn : todayIsoDate;

  const copy = SPORT_COPY[sport];
  const isNcaa = sport === 'FootballNcaa';

  // Reset division selection + ranking when sport changes. NCAA's ranking
  // filter doesn't apply to NFL/MLB, and slugs don't overlap across sports.
  //
  // Skip the mount run — defaultValues already set divisionSlugs correctly
  // based on initialSport. Running this effect on mount would redundantly
  // re-write the same values, and would clobber any future form-state
  // restoration (e.g., if we ever hydrate a draft from storage).
  const prevSportRef = useRef<SportKey | null>(null);
  useEffect(() => {
    if (prevSportRef.current === null) {
      prevSportRef.current = sport;
      return;
    }
    if (prevSportRef.current === sport) return;
    prevSportRef.current = sport;

    if (sport === 'FootballNfl') {
      setValue('divisionSlugs', NFL_DIVISIONS.map((d) => d.slug));
    } else if (sport === 'BaseballMlb') {
      setValue('divisionSlugs', MLB_DIVISIONS.map((d) => d.slug));
    } else {
      setValue('divisionSlugs', []);
    }
    // NCAA always carries a ranking filter on mobile (no "None" — see
    // RANKING_OPTIONS); other sports don't use one. Conference picks are
    // NCAA-only and don't survive a sport switch.
    setValue('rankingFilter', sport === 'FootballNcaa' ? 'AP_TOP_25' : '');
    setValue('conferenceSlugs', []);
    // Week ids are per-sport (each sport has its own season weeks) — a
    // sport switch invalidates any prior selection.
    setValue('startWeekId', '');
    setValue('endWeekId', '');
  }, [sport, setValue]);

  // Cold-launch admin deep-link: /user/me may still be loading when the form
  // initializes, so initialSport deferred ?sport=BaseballMlb to here. Once
  // isAdmin flips true, promote the form to MLB. The division-reset effect
  // above then picks up the sport change and seeds divisionSlugs correctly.
  // Non-admins never enter this branch (sportOptions also hides MLB for them).
  useEffect(() => {
    if (!isAdmin) return;
    if (params.sport !== 'BaseballMlb') return;
    if (sport === 'BaseballMlb') return;
    setValue('sport', 'BaseballMlb');
  }, [isAdmin, params.sport, sport, setValue]);

  // The sports this user may create, before gating (MLB is admin-only). Single
  // source so the visible picker and the locked-sport fallback never drift.
  const availableSportsForUser = useMemo<SportKey[]>(
    () => ['FootballNcaa', 'FootballNfl', ...(isAdmin ? (['BaseballMlb'] as SportKey[]) : [])],
    [isAdmin],
  );

  // If the preselected / deep-linked sport is gated from creation, fall back to
  // the first open sport so the form isn't stuck on a hidden option. Mirrors the
  // MLB promotion effect above, inverted; runs once gates resolve.
  useEffect(() => {
    if (!gates[sport]) return; // current sport open
    const fallback = availableSportsForUser.find((k) => !gates[k]);
    if (fallback && fallback !== sport) {
      setValue('sport', fallback);
    }
  }, [gates, sport, availableSportsForUser, setValue]);

  const sportOptions = useMemo<{ value: SportKey; label: string }[]>(() => {
    // Emoji pulled from SPORT_COPY so the icon stays in lockstep with the
    // Divisions header (which also reads copy.emoji) — single source of truth.
    const fmt = (k: SportKey) => ({ value: k, label: `${SPORT_COPY[k].emoji} ${SPORT_COPY[k].label}` });
    // Hide sports currently gated from creation — they surface as an "opens
    // {date}" note below the picker instead. The server enforces the same gate.
    return availableSportsForUser.filter((k) => !gates[k]).map(fmt);
  }, [availableSportsForUser, gates]);

  // Every selectable sport is gated → nothing can be created (availableSportsForUser
  // is never empty, so an empty option list means all-locked). Disable submission
  // and show an unavailable note; the server enforces this too.
  const allSportsLocked = sportOptions.length === 0;

  // Gated football sports to call out under the picker (e.g. "NCAA leagues open
  // Aug 17"). MLB isn't advertised here — it's admin-only and unannounced.
  const gateNotes = useMemo(
    () =>
      (['FootballNcaa', 'FootballNfl'] as SportKey[])
        .filter((k) => gates[k])
        .map((k) => ({
          key: k,
          text: `${SPORT_COPY[k].emoji} ${SPORT_COPY[k].label} leagues open ${formatGateDateOrSoon(gates[k])}`,
        })),
    [gates],
  );

  const currentDivisions = useMemo(() => {
    if (sport === 'FootballNfl') return NFL_DIVISIONS;
    if (sport === 'BaseballMlb') return MLB_DIVISIONS;
    return [];
  }, [sport]);

  const toggleDivision = (slug: string) => {
    const next = divisionSlugs.includes(slug)
      ? divisionSlugs.filter((s) => s !== slug)
      : [...divisionSlugs, slug];
    setValue('divisionSlugs', next, { shouldDirty: true });
  };

  // FBS conferences for the NCAA picker. The endpoint returns EVERY
  // classification; mobile shows FBS only (operator ruling 2026-08-18 —
  // FCS/D2/D3 conference selection stays a web-only surface). Optional
  // additions: the matchup processor UNIONS these with the ranking filter.
  const conferenceSlugs = watch('conferenceSlugs');
  const { data: fbsConferences = [] } = useQuery({
    queryKey: conferencesKeys.all,
    queryFn: async () => (await conferencesApi.getConferenceNamesAndSlugs()).data,
    enabled: isNcaa,
    select: (all) =>
      all
        .filter((c) => c.division === 'FBS')
        .sort((a, b) => a.shortName.localeCompare(b.shortName)),
  });

  const toggleConference = (slug: string) => {
    const next = conferenceSlugs.includes(slug)
      ? conferenceSlugs.filter((s) => s !== slug)
      : [...conferenceSlugs, slug];
    setValue('conferenceSlugs', next, { shouldDirty: true });
  };

  const createMutation = useMutation({
    mutationFn: async (data: FormData) => {
      // Week Range carries the selected weeks' real UTC boundaries — raw ISO
      // pass-through, NOT the date-input local-midnight conversion (web
      // parity: buildWindow in createLeagueRequests.js).
      const window =
        data.durationMode === DURATION_DATES
          ? { startsOn: toStartOfDayIso(data.startsOn), endsOn: toEndOfDayIso(data.endsOn) }
          : data.durationMode === DURATION_WEEKS
          ? {
              startsOn: seasonWeeks.find((w) => w.id === data.startWeekId)?.startDateUtc ?? null,
              endsOn: seasonWeeks.find((w) => w.id === data.endWeekId)?.endDateUtc ?? null,
            }
          : { startsOn: null, endsOn: null };

      const base = {
        name: data.name.trim(),
        description: data.description?.trim() || null,
        pickType: data.pickType as PickType,
        tiebreakerType: data.tiebreakerType as TiebreakerType,
        tiebreakerTiePolicy: 'EarliestSubmission' as const,
        useConfidencePoints: data.useConfidencePoints,
        isPublic: data.isPublic,
        joinPolicy: data.joinPolicy,
        dropLowWeeksCount: data.dropLowWeeksCount,
        // Explicit window shape — WeekRange can never be inferred from the
        // dates server-side, so it must always be sent (web parity).
        leagueWindow:
          data.durationMode === DURATION_DATES
            ? ('DateRange' as const)
            : data.durationMode === DURATION_WEEKS
            ? ('WeekRange' as const)
            : ('FullSeason' as const),
        ...window,
      };

      if (data.sport === 'FootballNcaa') {
        const payload: CreateFootballNcaaLeagueRequest = {
          ...base,
          rankingFilter:
            data.rankingFilter === '' ? null : (data.rankingFilter as NcaaRankingFilter),
          conferenceSlugs: data.conferenceSlugs,
        };
        return leaguesApi.createFootballNcaaLeague(payload).then((r) => r.data);
      }

      if (data.sport === 'FootballNfl') {
        const payload: CreateFootballNflLeagueRequest = {
          ...base,
          divisionSlugs: data.divisionSlugs,
        };
        return leaguesApi.createFootballNflLeague(payload).then((r) => r.data);
      }

      const payload: CreateBaseballMlbLeagueRequest = {
        ...base,
        divisionSlugs: data.divisionSlugs,
      };
      return leaguesApi.createBaseballMlbLeague(payload).then((r) => r.data);
    },
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: standingsKeys.me });
      // Parity with web: land on the new league's detail page (invite
      // friends, review settings, or delete if you changed your mind) rather
      // than bouncing back to wherever creation started. replace() so Back
      // doesn't return to the spent create form.
      router.replace({
        pathname: '/league/[leagueId]',
        params: { leagueId: created.id },
      } as never);
    },
    onError: (err: unknown) => {
      const serverMessage =
        (err as { response?: { data?: { errors?: { errorMessage?: string }[] } } })
          ?.response?.data?.errors?.[0]?.errorMessage;
      Alert.alert(
        'Could not create league',
        serverMessage || 'Something went wrong. Please try again.',
      );
    },
  });

  // Two-step submit, parity with web's "Confirm League Settings" dialog:
  // a valid form opens a summary the user confirms before the POST fires.
  // The validated snapshot is held in state so the modal renders from what
  // will actually be sent, not live form values.
  const [pendingData, setPendingData] = useState<FormData | null>(null);

  const onSubmit = (data: FormData) => {
    if (allSportsLocked) return; // nothing creatable; the button is also disabled
    setPendingData(data);
  };

  const confirmCreate = () => {
    if (!pendingData || createMutation.isPending) return;
    createMutation.mutate(pendingData, {
      // Close the modal on success only — on error it stays up behind the
      // Alert so the user can retry without re-validating the form.
      onSuccess: () => setPendingData(null),
    });
  };

  // Tiebreaker options use sport-aware labels for the "total" variant.
  const tiebreakerOptions: { value: FormData['tiebreakerType']; label: string }[] = [
    { value: 'TotalPoints', label: copy.tiebreakerTotalLabel },
    { value: 'EarliestSubmission', label: 'Earliest Pick' },
  ];

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Create League',
          presentation: 'modal',
          headerStyle: { backgroundColor: theme.card },
          headerTintColor: theme.text,
        }}
      />
      <KeyboardAvoidingView
        style={[styles.container, { backgroundColor: theme.background }]}
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      >
        <ScrollView
          contentContainerStyle={styles.inner}
          keyboardShouldPersistTaps="handled"
          showsVerticalScrollIndicator={false}
        >
          {/* Sport picker */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>Sport</Text>
            <Controller
              control={control}
              name="sport"
              render={({ field: { onChange, value } }) => (
                <SegmentedControl
                  value={value}
                  options={sportOptions}
                  onChange={onChange}
                  accessibilityLabel="Sport"
                />
              )}
            />
            {gateNotes.map((n) => (
              <Text key={n.key} style={[styles.gateNote, { color: theme.textMuted }]}>
                {n.text}
              </Text>
            ))}
          </View>

          {/* Name */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>League Name</Text>
            <Controller
              control={control}
              name="name"
              render={({ field: { onChange, value, onBlur } }) => (
                <TextInput
                  style={[
                    styles.input,
                    {
                      backgroundColor: theme.card,
                      borderColor: errors.name ? theme.error : theme.border,
                      color: theme.text,
                    },
                  ]}
                  placeholder={copy.namePlaceholder}
                  placeholderTextColor={theme.textMuted}
                  onChangeText={onChange}
                  onBlur={onBlur}
                  value={value}
                  maxLength={100}
                  returnKeyType="next"
                />
              )}
            />
            {errors.name && (
              <Text style={[styles.fieldError, { color: theme.error }]}>{errors.name.message}</Text>
            )}
          </View>

          {/* Pick type */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>Pick Type</Text>
            <Controller
              control={control}
              name="pickType"
              render={({ field: { onChange, value } }) => (
                <SegmentedControl
                  value={value}
                  options={PICK_TYPE_OPTIONS}
                  onChange={onChange}
                  accessibilityLabel="Pick Type"
                />
              )}
            />
          </View>

          {/* Tiebreaker */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>Tiebreaker</Text>
            <Controller
              control={control}
              name="tiebreakerType"
              render={({ field: { onChange, value } }) => (
                <SegmentedControl
                  value={value}
                  options={tiebreakerOptions}
                  onChange={onChange}
                  accessibilityLabel="Tiebreaker"
                />
              )}
            />
          </View>

          {/* League Window */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>League Window</Text>
            <Controller
              control={control}
              name="durationMode"
              render={({ field: { onChange, value } }) => (
                <SegmentedControl
                  value={value}
                  options={WINDOW_OPTIONS}
                  onChange={(v) => onChange(v as 'full' | 'weeks' | 'dates')}
                  accessibilityLabel="League Window"
                />
              )}
            />

            {durationMode === DURATION_WEEKS && (
              <View style={styles.dateRow}>
                <View style={styles.dateCol}>
                  <Text style={[styles.label, { color: theme.textMuted }]}>Start Week</Text>
                  <Controller
                    control={control}
                    name="startWeekId"
                    render={({ field: { onChange, value } }) => (
                      <WeekField
                        value={value}
                        onChange={onChange}
                        options={seasonWeeks}
                        placeholder="Select week"
                        accessibilityLabel="Start Week"
                        theme={theme}
                        error={errors.startWeekId?.message}
                      />
                    )}
                  />
                </View>
                <View style={styles.dateCol}>
                  <Text style={[styles.label, { color: theme.textMuted }]}>End Week</Text>
                  <Controller
                    control={control}
                    name="endWeekId"
                    render={({ field: { onChange, value } }) => (
                      <WeekField
                        value={value}
                        onChange={onChange}
                        options={seasonWeeks}
                        placeholder="Select week"
                        accessibilityLabel="End Week"
                        theme={theme}
                        error={errors.endWeekId?.message}
                      />
                    )}
                  />
                </View>
              </View>
            )}

            {durationMode === DURATION_DATES && (
              <View style={styles.dateRow}>
                <View style={styles.dateCol}>
                  <Text style={[styles.label, { color: theme.textMuted }]}>Start</Text>
                  <Controller
                    control={control}
                    name="startsOn"
                    render={({ field: { onChange, value } }) => (
                      <DateField
                        value={value}
                        onChange={onChange}
                        placeholder="Select start"
                        accessibilityLabel="Start Date"
                        theme={theme}
                        scheme={scheme}
                        error={errors.startsOn?.message}
                        minimumDate={parseDateOnly(todayIsoDate)}
                      />
                    )}
                  />
                </View>
                <View style={styles.dateCol}>
                  <Text style={[styles.label, { color: theme.textMuted }]}>End</Text>
                  <Controller
                    control={control}
                    name="endsOn"
                    render={({ field: { onChange, value } }) => (
                      <DateField
                        value={value}
                        onChange={onChange}
                        placeholder="Select end"
                        accessibilityLabel="End Date"
                        theme={theme}
                        scheme={scheme}
                        error={errors.endsOn?.message}
                        minimumDate={parseDateOnly(endsOnMinIsoDate)}
                      />
                    )}
                  />
                </View>
              </View>
            )}
          </View>

          {/* Drop Low Weeks */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>Drop Low Weeks</Text>
            <Controller
              control={control}
              name="dropLowWeeksCount"
              render={({ field: { onChange, value } }) => (
                <SegmentedControl
                  value={String(value)}
                  options={DROP_LOW_WEEKS_OPTIONS.filter((o) => Number(o.value) <= dropMax)}
                  onChange={(v) => onChange(Number(v))}
                  accessibilityLabel="Drop Low Weeks"
                />
              )}
            />
          </View>

          {/* Ranking filter — NCAA only */}
          {isNcaa && (
            <View style={styles.field}>
              <Text style={[styles.label, { color: theme.textMuted }]}>🏆 Rankings</Text>
              <Controller
                control={control}
                name="rankingFilter"
                render={({ field: { onChange, value } }) => (
                  <SegmentedControl
                    value={value}
                    options={RANKING_OPTIONS}
                    onChange={onChange}
                    accessibilityLabel="Ranking Filter"
                  />
                )}
              />
            </View>
          )}

          {/* FBS conferences — NCAA only, optional additions to the ranking
              filter (the matchup processor unions rank hits and conference
              hits). FBS only on mobile; the full every-classification table
              is a web surface. */}
          {isNcaa && fbsConferences.length > 0 && (
            <View style={styles.field}>
              <Text style={[styles.label, { color: theme.textMuted }]}>
                🏈 Conferences (optional)
              </Text>
              <View style={styles.divisionGrid}>
                {fbsConferences.map((conf) => {
                  const selected = conferenceSlugs.includes(conf.slug);
                  return (
                    <TouchableOpacity
                      key={conf.slug}
                      style={[
                        styles.divisionChip,
                        {
                          backgroundColor: selected ? theme.tint : theme.card,
                          borderColor: selected ? theme.tint : theme.border,
                        },
                      ]}
                      onPress={() => toggleConference(conf.slug)}
                      activeOpacity={0.75}
                      accessibilityRole="checkbox"
                      accessibilityState={{ checked: selected }}
                    >
                      <Text
                        style={[
                          styles.divisionChipText,
                          { color: selected ? theme.textOnAccent : theme.text },
                        ]}
                      >
                        {conf.shortName}
                      </Text>
                    </TouchableOpacity>
                  );
                })}
              </View>
            </View>
          )}

          {/* Division picker — NFL + MLB.
              Even pill counts use a balanced grid (NFL = 4x2, MLB = 3x2) so
              the layout stays visually consistent across phone widths instead
              of reflowing to 5x1 vs 4x2 by screen size. Odd counts fall back
              to flexWrap. */}
          {currentDivisions.length > 0 && (
            <View style={styles.field}>
              <Text style={[styles.label, { color: theme.textMuted }]}>
                {copy.emoji} Divisions
              </Text>
              {currentDivisions.length % 2 === 0 ? (
                <View style={styles.divisionGridStacked}>
                  {chunkInto(
                    currentDivisions,
                    balancedGridColumns(currentDivisions.length),
                  ).map((rowDivs, rowIdx) => (
                    <View key={rowIdx} style={styles.divisionRow}>
                      {rowDivs.map((div) => {
                        const selected = divisionSlugs.includes(div.slug);
                        return (
                          <TouchableOpacity
                            key={div.slug}
                            style={[
                              styles.divisionChip,
                              styles.divisionChipFlex,
                              {
                                backgroundColor: selected ? theme.tint : theme.card,
                                borderColor: selected ? theme.tint : theme.border,
                              },
                            ]}
                            onPress={() => toggleDivision(div.slug)}
                            activeOpacity={0.75}
                            accessibilityRole="checkbox"
                            accessibilityState={{ checked: selected }}
                          >
                            <Text
                              style={[
                                styles.divisionChipText,
                                styles.divisionChipTextCentered,
                                { color: selected ? theme.textOnAccent : theme.text },
                              ]}
                            >
                              {div.shortName}
                            </Text>
                          </TouchableOpacity>
                        );
                      })}
                    </View>
                  ))}
                </View>
              ) : (
                <View style={styles.divisionGrid}>
                  {currentDivisions.map((div) => {
                    const selected = divisionSlugs.includes(div.slug);
                    return (
                      <TouchableOpacity
                        key={div.slug}
                        style={[
                          styles.divisionChip,
                          {
                            backgroundColor: selected ? theme.tint : theme.card,
                            borderColor: selected ? theme.tint : theme.border,
                          },
                        ]}
                        onPress={() => toggleDivision(div.slug)}
                        activeOpacity={0.75}
                        accessibilityRole="checkbox"
                        accessibilityState={{ checked: selected }}
                      >
                        <Text
                          style={[
                            styles.divisionChipText,
                            // Selected chip bg is theme.tint → foreground must
                            // be its paired textOnAccent token (white in light,
                            // near-black in dark). Hard-coded '#fff' was
                            // illegible on dark-mode's light-cyan tint.
                            { color: selected ? theme.textOnAccent : theme.text },
                          ]}
                        >
                          {div.shortName}
                        </Text>
                      </TouchableOpacity>
                    );
                  })}
                </View>
              )}
            </View>
          )}

          {/* Confidence points */}
          <Controller
            control={control}
            name="useConfidencePoints"
            render={({ field: { onChange, value } }) => (
              <View style={[styles.switchRow, { borderColor: theme.border, backgroundColor: theme.card }]}>
                <View style={styles.switchTextWrap}>
                  <Text style={[styles.switchTitle, { color: theme.text }]}>Confidence Points</Text>
                  <Text style={[styles.switchSub, { color: theme.textMuted }]}>
                    Members rank picks to weight harder calls.
                  </Text>
                </View>
                <Switch
                  value={value}
                  onValueChange={onChange}
                  trackColor={{ false: theme.border, true: theme.tint }}
                  thumbColor="#fff"
                />
              </View>
            )}
          />

          {/* Visibility */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>Visibility</Text>
            <Controller
              control={control}
              name="isPublic"
              render={({ field: { onChange, value } }) => (
                <SegmentedControl
                  value={value ? 'public' : 'private'}
                  options={VISIBILITY_OPTIONS}
                  onChange={(v) => onChange(v === 'public')}
                  accessibilityLabel="Visibility"
                />
              )}
            />
          </View>

          {/* Join Policy — who can join, and until when */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>Who can join</Text>
            <Controller
              control={control}
              name="joinPolicy"
              render={({ field: { onChange, value } }) => (
                <SegmentedControl
                  value={value}
                  options={JOIN_POLICY_OPTIONS}
                  onChange={(v) => onChange(v as 'Open' | 'CloseAtFirstGame')}
                  accessibilityLabel="Who can join"
                />
              )}
            />
          </View>

          {/* Description last: optional flavor, and its suggested tag derives
              from the parameters chosen above — so by the time the user reaches
              it, the field is prefilled with a fully informed suggestion they
              can accept, edit, or clear. */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>Description (optional)</Text>
            <Controller
              control={control}
              name="description"
              render={({ field: { onChange, value, onBlur } }) => (
                <TextInput
                  style={[
                    styles.input,
                    styles.multiline,
                    {
                      backgroundColor: theme.card,
                      borderColor: errors.description ? theme.error : theme.border,
                      color: theme.text,
                    },
                  ]}
                  placeholder={copy.descPlaceholder}
                  placeholderTextColor={theme.textMuted}
                  onChangeText={(text) => {
                    descriptionEditedRef.current = true;
                    onChange(text);
                  }}
                  onBlur={onBlur}
                  value={value ?? ''}
                  maxLength={500}
                  multiline
                  textAlignVertical="top"
                />
              )}
            />
            {errors.description && (
              <Text style={[styles.fieldError, { color: theme.error }]}>
                {errors.description.message}
              </Text>
            )}
          </View>

          {allSportsLocked && (
            <Text style={[styles.gateNote, { color: theme.textMuted, marginTop: 12 }]}>
              League creation isn’t open yet. Check back when your sport unlocks.
            </Text>
          )}

          <Button
            title="Create League"
            onPress={handleSubmit(onSubmit)}
            loading={createMutation.isPending}
            disabled={allSportsLocked}
            fullWidth
            size="lg"
            style={{ marginTop: 12 }}
          />

          <Button
            title="Cancel"
            onPress={() => router.back()}
            variant="ghost"
            fullWidth
            size="md"
            style={{ marginTop: 4 }}
          />
        </ScrollView>
      </KeyboardAvoidingView>

      {/* Confirm League Settings — parity with web's pre-create summary
          dialog. Renders from the validated pendingData snapshot, not live
          form values. Stays open on a failed POST (behind the error Alert)
          so the user can retry without re-validating. */}
      <Modal
        visible={pendingData !== null}
        animationType="slide"
        presentationStyle="pageSheet"
        onRequestClose={() => {
          if (!createMutation.isPending) setPendingData(null);
        }}
      >
        {pendingData && (
          <View style={[styles.confirmContainer, { backgroundColor: theme.background }]}>
            <View style={[styles.confirmHeader, { borderBottomColor: theme.border }]}>
              <Text style={[styles.confirmTitle, { color: theme.text }]}>
                Confirm League Settings
              </Text>
            </View>
            <ScrollView contentContainerStyle={styles.confirmBody}>
              {buildConfirmRows(pendingData, seasonWeeks, fbsConferences).map(({ label, value }) => (
                <View
                  key={label}
                  style={[styles.confirmRow, { borderBottomColor: theme.border }]}
                >
                  <Text style={[styles.confirmLabel, { color: theme.textMuted }]}>
                    {label}
                  </Text>
                  <Text style={[styles.confirmValue, { color: theme.text }]}>
                    {value}
                  </Text>
                </View>
              ))}
            </ScrollView>
            <View style={[styles.confirmFooter, { borderTopColor: theme.border }]}>
              <Button
                title="Back"
                onPress={() => setPendingData(null)}
                variant="secondary"
                size="md"
                disabled={createMutation.isPending}
                style={styles.confirmFooterButton}
              />
              <Button
                title="Confirm & Create"
                onPress={confirmCreate}
                loading={createMutation.isPending}
                size="md"
                style={styles.confirmFooterButton}
              />
            </View>
          </View>
        )}
      </Modal>
    </>
  );
}

// ─── Confirm-dialog summary rows ──────────────────────────────────────────────

// Mirrors the field list of web's "Confirm League Settings" dialog, derived
// from the validated form snapshot. Kept outside the component so it stays a
// pure FormData -> rows mapping.
function buildConfirmRows(
  data: FormData,
  seasonWeeks: SeasonWeekOption[],
  conferences: ConferenceOption[],
): { label: string; value: string }[] {
  const copy = SPORT_COPY[data.sport];

  const divisionNames =
    data.sport === 'FootballNfl'
      ? NFL_DIVISIONS
      : data.sport === 'BaseballMlb'
        ? MLB_DIVISIONS
        : [];
  const selectedDivisions = data.divisionSlugs
    .map((slug) => divisionNames.find((d) => d.slug === slug)?.shortName ?? slug)
    .join(', ');

  const pickTypeLabel =
    PICK_TYPE_OPTIONS.find((o) => o.value === data.pickType)?.label ?? data.pickType;
  const tiebreakerLabel =
    data.tiebreakerType === 'TotalPoints' ? copy.tiebreakerTotalLabel : 'Earliest Pick';

  const startWeek = seasonWeeks.find((w) => w.id === data.startWeekId);
  const endWeek = seasonWeeks.find((w) => w.id === data.endWeekId);
  const windowLabel =
    data.durationMode === DURATION_DATES
      ? `${data.startsOn ? formatDateOnlyDisplay(data.startsOn) : '—'} to ${
          data.endsOn ? formatDateOnlyDisplay(data.endsOn) : '—'
        }`
      : data.durationMode === DURATION_WEEKS
      ? startWeek && endWeek
        ? startWeek.id === endWeek.id
          ? startWeek.label
          : `${startWeek.label} to ${endWeek.label}`
        : '—'
      : 'Full Season';

  const rows: { label: string; value: string }[] = [
    { label: 'Name', value: data.name.trim() },
    { label: 'Sport', value: copy.label },
  ];
  if (divisionNames.length > 0) {
    rows.push({ label: 'Divisions', value: selectedDivisions || 'None selected' });
  }
  if (data.sport === 'FootballNcaa') {
    rows.push({
      label: 'Ranking Filter',
      value:
        RANKING_OPTIONS.find((o) => o.value === data.rankingFilter)?.label ?? data.rankingFilter,
    });
    if (data.conferenceSlugs.length > 0) {
      rows.push({
        label: 'Conferences',
        value: data.conferenceSlugs
          .map((slug) => conferences.find((c) => c.slug === slug)?.shortName ?? slug)
          .join(', '),
      });
    }
  }
  rows.push(
    { label: 'Pick Type', value: pickTypeLabel },
    { label: 'Tiebreaker', value: tiebreakerLabel },
    { label: 'Confidence Points', value: data.useConfidencePoints ? 'Yes' : 'No' },
    {
      label: 'Drop Low Weeks',
      value: data.dropLowWeeksCount === 0 ? 'None. Use All Weeks' : `${data.dropLowWeeksCount}`,
    },
    { label: 'League Window', value: windowLabel },
    {
      label: 'Joining',
      value:
        data.joinPolicy === 'CloseAtFirstGame'
          ? 'Locked at kickoff'
          : 'Open while the league is live',
    },
    { label: 'Visibility', value: data.isPublic ? 'Public' : 'Private' },
    { label: 'Pick Deadline', value: '5 minutes before kickoff (not configurable)' },
    { label: 'Description', value: data.description?.trim() || 'None' },
  );
  return rows;
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { flex: 1 },
  inner: { padding: 20, paddingBottom: 40, gap: 16 },
  field: { gap: 6 },
  label: {
    fontSize: 12,
    fontWeight: '700',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  gateNote: {
    fontSize: 12,
    lineHeight: 16,
  },
  input: {
    borderWidth: 1.5,
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 16,
  },
  pickerBarSpacer: {
    // Balances the Cancel action so the sheet title stays centered.
    width: 52,
  },
  weekList: {
    maxHeight: 380,
  },
  weekRow: {
    paddingVertical: 13,
    paddingHorizontal: 18,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  multiline: {
    minHeight: 80,
    paddingTop: 12,
  },
  fieldError: { fontSize: 12 },
  switchRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 14,
    borderRadius: 10,
    borderWidth: StyleSheet.hairlineWidth,
    gap: 12,
  },
  switchTextWrap: { flex: 1, gap: 2 },
  switchTitle: { fontSize: 15, fontWeight: '600' },
  switchSub: { fontSize: 12 },
  divisionGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  divisionGridStacked: {
    gap: 8,
  },
  divisionRow: {
    flexDirection: 'row',
    gap: 8,
  },
  divisionChip: {
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 999,
    borderWidth: 1.5,
  },
  divisionChipFlex: {
    flex: 1,
    alignItems: 'center',
  },
  divisionChipText: {
    fontSize: 13,
    fontWeight: '600',
  },
  divisionChipTextCentered: {
    textAlign: 'center',
  },
  dateRow: {
    flexDirection: 'row',
    gap: 12,
    marginTop: 12,
  },
  dateCol: {
    flex: 1,
    gap: 6,
  },
  // iOS date-picker sheet. Anchored to the bottom over a dimmed backdrop so the
  // wheel never displaces form content the way the old inline picker did.
  pickerBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.4)',
  },
  pickerSheet: {
    borderTopWidth: StyleSheet.hairlineWidth,
    paddingBottom: 24,
  },
  pickerBar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  pickerBarTitle: { fontSize: 15, fontWeight: '700' },
  pickerBarAction: { fontSize: 16 },
  pickerBarDone: { fontWeight: '700' },
  // Confirm League Settings sheet.
  confirmContainer: { flex: 1 },
  confirmHeader: {
    paddingHorizontal: 20,
    paddingVertical: 16,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  confirmTitle: { fontSize: 18, fontWeight: '700' },
  confirmBody: { paddingHorizontal: 20, paddingBottom: 12 },
  confirmRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: 16,
    paddingVertical: 11,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  confirmLabel: { fontSize: 13, fontWeight: '600', flexShrink: 0 },
  confirmValue: { fontSize: 13, textAlign: 'right', flex: 1 },
  confirmFooter: {
    flexDirection: 'row',
    gap: 12,
    paddingHorizontal: 20,
    paddingVertical: 14,
    borderTopWidth: StyleSheet.hairlineWidth,
  },
  confirmFooterButton: { flex: 1 },
});
