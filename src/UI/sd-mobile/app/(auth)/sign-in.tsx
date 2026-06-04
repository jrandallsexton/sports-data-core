import React, { useEffect, useState } from 'react';
import {
  View,
  TextInput,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  TouchableOpacity,
} from 'react-native';
import * as AppleAuthentication from 'expo-apple-authentication';
import { Text } from '@/src/components/ui/AppText';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { signInWithEmailAndPassword } from 'firebase/auth';
import { auth } from '@/src/lib/firebase';
import {
  signInWithGoogle,
  GoogleSignInCancelled,
} from '@/src/lib/googleSignIn';
import {
  signInWithApple,
  isAppleSignInAvailable,
  AppleSignInCancelled,
} from '@/src/lib/appleSignIn';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Button } from '@/src/components/ui/Button';
import { Wordmark } from '@/src/components/brand/Wordmark';
import { getTheme } from '@/constants/Colors';

// ─── Validation schema ────────────────────────────────────────────────────────

const schema = z.object({
  email: z.string().email('Please enter a valid email'),
  password: z.string().min(6, 'Password must be at least 6 characters'),
});

type FormData = z.infer<typeof schema>;

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function SignInScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  // Apple Sign-In availability is async (checks iOS version + Apple ID
  // status); resolves to false on Android and on iOS < 13. The button is
  // suppressed entirely when unavailable.
  const [appleAvailable, setAppleAvailable] = useState(false);
  const [thirdPartyBusy, setThirdPartyBusy] = useState(false);

  useEffect(() => {
    isAppleSignInAvailable().then(setAppleAvailable).catch(() => setAppleAvailable(false));
  }, []);

  const {
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
    clearErrors,
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
  });

  const onSubmit = async ({ email, password }: FormData) => {
    try {
      await signInWithEmailAndPassword(auth, email, password);
      // AuthGuard in root _layout.tsx handles the redirect.
    } catch (err: unknown) {
      const code = (err as { code?: string })?.code ?? '';
      if (
        code === 'auth/user-not-found' ||
        code === 'auth/wrong-password' ||
        code === 'auth/invalid-credential'
      ) {
        setError('root', { message: 'Invalid email or password.' });
      } else if (code === 'auth/too-many-requests') {
        setError('root', { message: 'Too many attempts. Please try again later.' });
      } else {
        setError('root', { message: 'Sign in failed. Please try again.' });
      }
    }
  };

  const handleThirdPartySignIn = async (
    provider: 'google' | 'apple',
  ) => {
    if (thirdPartyBusy) return;
    setThirdPartyBusy(true);
    clearErrors('root');
    try {
      if (provider === 'google') {
        await signInWithGoogle();
      } else {
        await signInWithApple();
      }
      // AuthGuard handles redirect.
    } catch (err: unknown) {
      if (err instanceof GoogleSignInCancelled || err instanceof AppleSignInCancelled) {
        // User backed out of the picker — no error UI, just unbusy.
        return;
      }
      const code = (err as { code?: string })?.code ?? '';
      if (code === 'auth/account-exists-with-different-credential') {
        // The email is already registered with a different provider
        // (typically email/password). Account-linking auto-flow is
        // deferred — for now, point the user at their existing method.
        setError('root', {
          message: 'An account with this email already exists. Sign in with your password to continue.',
        });
      } else {
        setError('root', {
          message: err instanceof Error ? err.message : 'Sign in failed. Please try again.',
        });
      }
    } finally {
      setThirdPartyBusy(false);
    }
  };

  return (
    <KeyboardAvoidingView
      style={[styles.container, { backgroundColor: theme.background }]}
      behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
    >
      <ScrollView
        contentContainerStyle={styles.inner}
        keyboardShouldPersistTaps="handled"
        showsVerticalScrollIndicator={false}
      >
        {/* Brand header */}
        <View style={styles.header}>
          <Wordmark size={32} />
          <Text style={[styles.tagline, { color: theme.textMuted }]}>NCAAFB & NFL Pick'em</Text>
        </View>

        {/* Card */}
        <View
          style={[styles.card, { backgroundColor: theme.card, borderColor: theme.border }]}
        >
          <Text style={[styles.cardTitle, { color: theme.text }]}>Sign in</Text>

          {/* Root error */}
          {errors.root && (
            <View style={styles.errorBanner}>
              <Text style={[styles.errorBannerText, { color: theme.error }]}>
                {errors.root.message}
              </Text>
            </View>
          )}

          {/* Third-party providers — surfaced above email/password so the
              path of least friction (federated sign-in) is the first thing
              users see. Apple button only renders on iOS where the API is
              available; Google works on both platforms. */}
          <View style={styles.thirdPartyStack}>
            <Button
              title="Continue with Google"
              onPress={() => handleThirdPartySignIn('google')}
              loading={thirdPartyBusy}
              fullWidth
              size="lg"
              variant="secondary"
            />
            {appleAvailable ? (
              <AppleAuthentication.AppleAuthenticationButton
                buttonType={AppleAuthentication.AppleAuthenticationButtonType.SIGN_IN}
                buttonStyle={
                  scheme === 'dark'
                    ? AppleAuthentication.AppleAuthenticationButtonStyle.WHITE
                    : AppleAuthentication.AppleAuthenticationButtonStyle.BLACK
                }
                cornerRadius={10}
                style={styles.appleButton}
                onPress={() => handleThirdPartySignIn('apple')}
              />
            ) : null}
          </View>

          {/* Divider */}
          <View style={styles.dividerRow}>
            <View style={[styles.dividerLine, { backgroundColor: theme.border }]} />
            <Text style={[styles.dividerText, { color: theme.textMuted }]}>or</Text>
            <View style={[styles.dividerLine, { backgroundColor: theme.border }]} />
          </View>

          {/* Email */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>Email</Text>
            <Controller
              control={control}
              name="email"
              render={({ field: { onChange, value, onBlur } }) => (
                <TextInput
                  style={[
                    styles.input,
                    {
                      backgroundColor: theme.background,
                      borderColor: errors.email ? theme.error : theme.border,
                      color: theme.text,
                    },
                  ]}
                  placeholder="you@example.com"
                  placeholderTextColor={theme.textMuted}
                  onChangeText={onChange}
                  onBlur={onBlur}
                  value={value}
                  autoCapitalize="none"
                  keyboardType="email-address"
                  textContentType="emailAddress"
                  autoComplete="email"
                  returnKeyType="next"
                />
              )}
            />
            {errors.email && (
              <Text style={[styles.fieldError, { color: theme.error }]}>
                {errors.email.message}
              </Text>
            )}
          </View>

          {/* Password */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>Password</Text>
            <Controller
              control={control}
              name="password"
              render={({ field: { onChange, value, onBlur } }) => (
                <TextInput
                  style={[
                    styles.input,
                    {
                      backgroundColor: theme.background,
                      borderColor: errors.password ? theme.error : theme.border,
                      color: theme.text,
                    },
                  ]}
                  placeholder="••••••••"
                  placeholderTextColor={theme.textMuted}
                  onChangeText={onChange}
                  onBlur={onBlur}
                  value={value}
                  secureTextEntry
                  textContentType="password"
                  autoComplete="current-password"
                  returnKeyType="done"
                  onSubmitEditing={handleSubmit(onSubmit)}
                />
              )}
            />
            {errors.password && (
              <Text style={[styles.fieldError, { color: theme.error }]}>
                {errors.password.message}
              </Text>
            )}
          </View>

          <Button
            title="Sign In"
            onPress={handleSubmit(onSubmit)}
            loading={isSubmitting}
            fullWidth
            size="lg"
            style={{ marginTop: 8 }}
          />

          {/* TODO: wire password-reset flow. TouchableOpacity has no onPress
              today — visually a button, functionally a no-op. Two paths when
              we pick this up: (a) call Firebase `sendPasswordResetEmail(auth,
              email)` inline with a simple prompt for the address, or (b)
              navigate to a dedicated reset screen. CodeRabbit flagged this on
              PR #274; deferred pending product decision on the flow. */}
          <TouchableOpacity style={styles.forgotLink}>
            <Text style={[styles.forgotText, { color: theme.tint }]}>
              Forgot password?
            </Text>
          </TouchableOpacity>
        </View>
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { flex: 1 },
  inner: { flexGrow: 1, justifyContent: 'center', padding: 20 },
  header: { alignItems: 'center', marginBottom: 32, gap: 8 },
  tagline: {
    fontSize: 13,
    marginTop: 4,
    fontWeight: '600',
    textTransform: 'uppercase',
    letterSpacing: 1.5,
  },
  card: {
    borderRadius: 16,
    padding: 24,
    borderWidth: 1,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.08,
    shadowRadius: 12,
    elevation: 4,
  },
  cardTitle: { fontSize: 22, fontWeight: '700', marginBottom: 20 },
  errorBanner: {
    backgroundColor: '#FEF2F2',
    borderRadius: 8,
    padding: 12,
    marginBottom: 16,
    borderLeftWidth: 3,
    borderLeftColor: '#EF4444',
  },
  errorBannerText: { fontSize: 14 },
  thirdPartyStack: {
    gap: 10,
    marginBottom: 18,
  },
  appleButton: {
    width: '100%',
    height: 48,
  },
  dividerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    marginBottom: 18,
  },
  dividerLine: {
    flex: 1,
    height: StyleSheet.hairlineWidth,
  },
  dividerText: {
    fontSize: 12,
    fontWeight: '600',
    textTransform: 'uppercase',
    letterSpacing: 1,
  },
  field: { marginBottom: 16 },
  label: {
    fontSize: 12,
    fontWeight: '700',
    marginBottom: 6,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  input: {
    borderWidth: 1.5,
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 13,
    fontSize: 16,
  },
  fieldError: { fontSize: 12, marginTop: 4 },
  forgotLink: { alignItems: 'center', marginTop: 18 },
  forgotText: { fontSize: 14, fontWeight: '600' },
});
