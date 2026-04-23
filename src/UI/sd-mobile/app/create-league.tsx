import React, { useEffect, useMemo } from 'react';
import {
  View,
  Text,
  TextInput,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  Switch,
  TouchableOpacity,
  Alert,
} from 'react-native';
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

// ─── Validation schema ────────────────────────────────────────────────────────

const schema = z.object({
  sport: z.enum(['FootballNcaa', 'FootballNfl', 'BaseballMlb']),
  name: z.string().trim().min(1, 'Name is required').max(100, 'Name must be 100 characters or fewer'),
  description: z.string().max(500, 'Description must be 500 characters or fewer').optional(),
  pickType: z.enum(['StraightUp', 'AgainstTheSpread']),
  tiebreakerType: z.enum(['TotalPoints', 'EarliestSubmission']),
  useConfidencePoints: z.boolean(),
  isPublic: z.boolean(),
  rankingFilter: z.enum(['', 'AP_TOP_25', 'AP_TOP_20', 'AP_TOP_15', 'AP_TOP_10', 'AP_TOP_5']),
  divisionSlugs: z.array(z.string()),
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

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function CreateLeagueScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();
  const queryClient = useQueryClient();
  const params = useLocalSearchParams<{ sport?: string }>();
  const { data: me } = useCurrentUser();
  const isAdmin = me?.isAdmin === true;

  const initialSport = useMemo<SportKey>(() => {
    const raw = params.sport;
    if (raw && VALID_SPORT_PARAMS.has(raw as SportKey)) return raw as SportKey;
    return 'FootballNcaa';
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
    },
  });

  const sport = watch('sport');
  const divisionSlugs = watch('divisionSlugs');
  const copy = SPORT_COPY[sport];
  const isNcaa = sport === 'FootballNcaa';

  // Reset division selection + ranking when sport changes. NCAA's ranking
  // filter doesn't apply to NFL/MLB, and slugs don't overlap across sports.
  useEffect(() => {
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

  const sportOptions = useMemo<{ value: SportKey; label: string }[]>(() => {
    const base: { value: SportKey; label: string }[] = [
      { value: 'FootballNcaa', label: 'NCAA' },
      { value: 'FootballNfl', label: 'NFL' },
    ];
    if (isAdmin) base.push({ value: 'BaseballMlb', label: 'MLB' });
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
      const base = {
        name: data.name.trim(),
        description: data.description?.trim() || null,
        pickType: data.pickType as PickType,
        tiebreakerType: data.tiebreakerType as TiebreakerType,
        tiebreakerTiePolicy: 'EarliestSubmission' as const,
        useConfidencePoints: data.useConfidencePoints,
        isPublic: data.isPublic,
        dropLowWeeksCount: 0,
        startsOn: null,
        endsOn: null,
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

          {/* Description */}
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
                  onChangeText={onChange}
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

          {/* Division picker — NFL + MLB */}
          {currentDivisions.length > 0 && (
            <View style={styles.field}>
              <Text style={[styles.label, { color: theme.textMuted }]}>
                {copy.emoji} Divisions
              </Text>
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
                          { color: selected ? '#fff' : theme.text },
                        ]}
                      >
                        {div.shortName}
                      </Text>
                    </TouchableOpacity>
                  );
                })}
              </View>
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
  divisionChip: {
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 999,
    borderWidth: 1.5,
  },
  divisionChipText: {
    fontSize: 13,
    fontWeight: '600',
  },
});
