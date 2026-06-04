import React from 'react';
import {
  View,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  SafeAreaView,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import { Text } from '@/src/components/ui/AppText';
import { Wordmark } from '@/src/components/brand/Wordmark';
import { Button } from '@/src/components/ui/Button';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';

// Mobile landing — condensed from the web LandingPage (Hero + Feature
// Highlights + How It Works + Footer). For the mobile auth stack we
// only need enough surface to communicate the value prop before the
// sign-in card. Folds the web's "How It Works" section into the
// feature value props (the two were largely redundant once trimmed).
//
// Single "Get Started" CTA at the bottom routes to the existing
// sign-in screen, which now handles new-user creation transparently
// via Google / Apple sign-in — no separate sign-up path needed.

const features: ReadonlyArray<{
  icon: keyof typeof Ionicons.glyphMap;
  title: string;
  body: string;
}> = [
  {
    icon: 'bulb-outline',
    title: 'Pick Smarter',
    body: 'Insider stats, matchup breakdowns, and AI-driven insights — every game.',
  },
  {
    icon: 'trophy-outline',
    title: 'Crush Your Friends',
    body: 'Dominate your private leagues. Bragging rights guaranteed.',
  },
  {
    icon: 'options-outline',
    title: 'Pick Your Way',
    body: 'Straight up, against the spread, over/under — your call, every week.',
  },
];

export default function WelcomeScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const router = useRouter();

  return (
    <SafeAreaView style={[styles.safe, { backgroundColor: theme.background }]}>
      <ScrollView
        contentContainerStyle={styles.scroll}
        showsVerticalScrollIndicator={false}
        bounces={false}
      >
        {/* Hero */}
        <View style={styles.hero}>
          <Wordmark size={36} />
          <Text style={[styles.tagline, { color: theme.text }]}>
            Win Your Picks. Crush Your Friends.
          </Text>
          <Text style={[styles.subhead, { color: theme.textMuted }]}>
            Data-driven insights for every matchup.
          </Text>
        </View>

        {/* Feature cards — condensed from web's "Why sportDeets?" +
            "How It Works". Three is the sweet spot for mobile: enough
            to communicate breadth without forcing a scroll on standard
            phone heights. */}
        <View style={styles.features}>
          {features.map(({ icon, title, body }) => (
            <View
              key={title}
              style={[
                styles.featureCard,
                {
                  backgroundColor: theme.card,
                  borderColor: theme.border,
                },
              ]}
            >
              <View style={[styles.iconWrap, { backgroundColor: theme.accentMuted }]}>
                <Ionicons name={icon} size={22} color={theme.tint} />
              </View>
              <View style={styles.featureBody}>
                <Text style={[styles.featureTitle, { color: theme.text }]}>{title}</Text>
                <Text style={[styles.featureCopy, { color: theme.textMuted }]}>{body}</Text>
              </View>
            </View>
          ))}
        </View>

        {/* CTA — single primary action. The existing sign-in screen
            handles Google / Apple / email & password in one place, so
            new vs. returning user is the same destination. */}
        <View style={styles.ctaStack}>
          <Button
            title="Get Started"
            onPress={() => router.push('/(auth)/sign-in')}
            fullWidth
            size="lg"
          />
          <TouchableOpacity
            style={styles.signInLink}
            onPress={() => router.push('/(auth)/sign-in')}
            hitSlop={10}
          >
            <Text style={[styles.signInLinkText, { color: theme.textMuted }]}>
              Already have an account?{' '}
              <Text style={[styles.signInLinkAccent, { color: theme.tint }]}>Sign in</Text>
            </Text>
          </TouchableOpacity>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1 },
  scroll: {
    flexGrow: 1,
    paddingHorizontal: 24,
    paddingTop: 32,
    paddingBottom: 32,
    gap: 28,
  },
  hero: {
    alignItems: 'center',
    gap: 12,
  },
  tagline: {
    fontSize: 24,
    fontWeight: '800',
    textAlign: 'center',
    lineHeight: 30,
    marginTop: 16,
  },
  subhead: {
    fontSize: 15,
    textAlign: 'center',
    lineHeight: 21,
    paddingHorizontal: 8,
  },
  features: {
    gap: 12,
  },
  featureCard: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 14,
    padding: 14,
    borderRadius: 14,
    borderWidth: StyleSheet.hairlineWidth,
  },
  iconWrap: {
    width: 44,
    height: 44,
    borderRadius: 22,
    alignItems: 'center',
    justifyContent: 'center',
  },
  featureBody: { flex: 1, gap: 2 },
  featureTitle: {
    fontSize: 15,
    fontWeight: '700',
  },
  featureCopy: {
    fontSize: 13,
    lineHeight: 18,
  },
  ctaStack: {
    gap: 12,
    alignItems: 'center',
    marginTop: 'auto',
    paddingTop: 8,
  },
  signInLink: { paddingVertical: 4 },
  signInLinkText: {
    fontSize: 14,
  },
  signInLinkAccent: {
    fontWeight: '700',
  },
});
