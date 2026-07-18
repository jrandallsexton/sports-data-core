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
} from 'react-native';
import DateTimePicker, {
  type DateTimePickerEvent,
} from '@react-native-community/datetimepicker';
import { Text } from '@/src/components/ui/AppText';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useRouter, Stack, useLocalSearchParams } from 'expo-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { getTheme } from '@/constants/Colors';
import { Button } from '@/src/components/ui/Button';
import { SegmentedControl } from '@/src/components/ui/SegmentedControl';
import {
  leaguesApi,
  type CreateBaseballMlbLeagueRequest,
  type CreateFootballNcaaLeagueRequest,
  type CreateFootballNflLeagueRequest,
  type NcaaRankingFilter,
  type PickType,
  type TiebreakerType,
} from '@/src/services/api/leaguesApi';
import { standingsKeys } from '@/src/hooks/useStandings';
import { useCurrentUser } from '@/src/hooks/useStandings';

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

// League Window. Web's "Week Range" mode is intentionally omitted on mobile —
// Date Range already covers the same span semantics and the web's Week Range
// UI itself errors on submit pending a BE season calendar endpoint.
const DURATION_FULL = 'full';
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
    durationMode: z.enum([DURATION_FULL, DURATION_DATES]),
    // YYYY-MM-DD or empty string. Stored as a plain date string (no TZ) so the
    // submit-time conversion to ISO can anchor at local midnight / end-of-day
    // without timezone drift — matches web's toStartOfDayIso / toEndOfDayIso.
    startsOn: z.string(),
    endsOn: z.string(),
    dropLowWeeksCount: z.number().int().min(0).max(3),
  })
  .superRefine((data, ctx) => {
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

const RANKING_OPTIONS: { value: FormData['rankingFilter']; label: string }[] = [
  { value: '', label: 'All' },
  { value: 'AP_TOP_25', label: 'Top 25' },
  { value: 'AP_TOP_10', label: 'Top 10' },
  { value: 'AP_TOP_5', label: 'Top 5' },
];

const VISIBILITY_OPTIONS: { value: 'private' | 'public'; label: string }[] = [
  { value: 'private', label: 'Private' },
  { value: 'public', label: 'Public' },
];

const WINDOW_OPTIONS: { value: 'full' | 'dates'; label: string }[] = [
  { value: DURATION_FULL, label: 'Full Season' },
  { value: DURATION_DATES, label: 'Date Range' },
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
  error?: string;
  minimumDate?: Date;
};

// Pressable that shows the formatted date (or placeholder) and opens the
// native picker on tap. Android dismisses the picker after any interaction;
// iOS keeps it on screen until the user taps away, so we drive visibility
// from local state and only commit form value on `event.type === 'set'`.
function DateField({ value, onChange, placeholder, accessibilityLabel, theme, error, minimumDate }: DateFieldProps) {
  const [show, setShow] = useState(false);

  const dateValue = value ? parseDateOnly(value) : new Date();
  const display = value ? formatDateOnlyDisplay(value) : placeholder;
  const hasValue = value.length > 0;

  const handleChange = (event: DateTimePickerEvent, selectedDate?: Date) => {
    if (Platform.OS === 'android') setShow(false);
    if (event.type === 'set' && selectedDate) {
      onChange(dateToIsoDateOnly(selectedDate));
    }
  };

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
        onPress={() => setShow(true)}
        accessibilityRole="button"
        accessibilityLabel={accessibilityLabel}
      >
        <Text style={{ color: hasValue ? theme.text : theme.textMuted, fontSize: 16 }}>
          {display}
        </Text>
      </TouchableOpacity>
      {show && (
        <DateTimePicker
          value={dateValue}
          mode="date"
          display="default"
          onChange={handleChange}
          minimumDate={minimumDate}
        />
      )}
      {error && <Text style={[styles.fieldError, { color: theme.error }]}>{error}</Text>}
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
      rankingFilter: '',
      // NFL/MLB: preselect all divisions so "include everyone" is one click.
      // NCAA: empty (no conference picker UI yet).
      divisionSlugs:
        initialSport === 'FootballNfl'
          ? NFL_DIVISIONS.map((d) => d.slug)
          : initialSport === 'BaseballMlb'
          ? MLB_DIVISIONS.map((d) => d.slug)
          : [],
      durationMode: DURATION_FULL,
      startsOn: '',
      endsOn: '',
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

  // Suggested description window: single day, a date range, or null (full season).
  const descriptionWindowLabel = (() => {
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
    if (sport !== 'FootballNcaa') {
      setValue('rankingFilter', '');
    }
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

  const sportOptions = useMemo<{ value: SportKey; label: string }[]>(() => {
    // Emoji pulled from SPORT_COPY so the icon stays in lockstep with the
    // Divisions header (which also reads copy.emoji) — single source of truth.
    const fmt = (k: SportKey) => ({ value: k, label: `${SPORT_COPY[k].emoji} ${SPORT_COPY[k].label}` });
    const base: { value: SportKey; label: string }[] = [fmt('FootballNcaa'), fmt('FootballNfl')];
    if (isAdmin) base.push(fmt('BaseballMlb'));
    return base;
  }, [isAdmin]);

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

  const createMutation = useMutation({
    mutationFn: async (data: FormData) => {
      const window =
        data.durationMode === DURATION_DATES
          ? { startsOn: toStartOfDayIso(data.startsOn), endsOn: toEndOfDayIso(data.endsOn) }
          : { startsOn: null, endsOn: null };

      const base = {
        name: data.name.trim(),
        description: data.description?.trim() || null,
        pickType: data.pickType as PickType,
        tiebreakerType: data.tiebreakerType as TiebreakerType,
        tiebreakerTiePolicy: 'EarliestSubmission' as const,
        useConfidencePoints: data.useConfidencePoints,
        isPublic: data.isPublic,
        dropLowWeeksCount: data.dropLowWeeksCount,
        ...window,
      };

      if (data.sport === 'FootballNcaa') {
        const payload: CreateFootballNcaaLeagueRequest = {
          ...base,
          rankingFilter:
            data.rankingFilter === '' ? null : (data.rankingFilter as NcaaRankingFilter),
          conferenceSlugs: [],
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
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: standingsKeys.me });
      router.back();
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

  const onSubmit = (data: FormData) => createMutation.mutate(data);

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

          {/* Drop Low Weeks */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>Drop Low Weeks</Text>
            <Controller
              control={control}
              name="dropLowWeeksCount"
              render={({ field: { onChange, value } }) => (
                <SegmentedControl
                  value={String(value)}
                  options={DROP_LOW_WEEKS_OPTIONS}
                  onChange={(v) => onChange(Number(v))}
                  accessibilityLabel="Drop Low Weeks"
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
                  onChange={(v) => onChange(v as 'full' | 'dates')}
                  accessibilityLabel="League Window"
                />
              )}
            />

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
                        error={errors.endsOn?.message}
                        minimumDate={parseDateOnly(endsOnMinIsoDate)}
                      />
                    )}
                  />
                </View>
              </View>
            )}
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

          <Button
            title="Create League"
            onPress={handleSubmit(onSubmit)}
            loading={createMutation.isPending}
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
    </>
  );
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
  input: {
    borderWidth: 1.5,
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 16,
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
});
