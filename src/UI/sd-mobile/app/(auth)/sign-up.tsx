import React from 'react';
import {
  View,
  TextInput,
  StyleSheet,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  TouchableOpacity,
} from 'react-native';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { router } from 'expo-router';
import { createUserWithEmailAndPassword, updateProfile } from 'firebase/auth';

import { Text } from '@/src/components/ui/AppText';
import { auth } from '@/src/lib/firebase';
import { useAuthStore } from '@/src/stores/authStore';
import { usersApi } from '@/src/services/api/usersApi';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Button } from '@/src/components/ui/Button';
import { Wordmark } from '@/src/components/brand/Wordmark';
import { getTheme } from '@/constants/Colors';

// ─── Validation schema ────────────────────────────────────────────────────────

const schema = z.object({
  displayName: z.string().trim().min(2, 'Please enter a display name'),
  email: z.string().email('Please enter a valid email'),
  password: z.string().min(6, 'Password must be at least 6 characters'),
});

type FormData = z.infer<typeof schema>;

// ─── Screen ───────────────────────────────────────────────────────────────────

/**
 * Email/password account creation — parity with the web's /signup
 * "Sign up with email" (EmailSignupForm.jsx). Federated sign-in (Google/
 * Apple) creates accounts transparently on the sign-in screen, so this
 * screen exists for users who want a plain email account (and for
 * onboarding tests with plus/alias addresses that have no Google
 * identity behind them).
 *
 * Flow mirrors web: createUserWithEmailAndPassword → updateProfile with
 * the display name → reload so downstream reads see the profile. The
 * backend user is provisioned server-side on the first authenticated
 * request (FirebaseAuthenticationMiddleware → GetOrCreateUserAsync) —
 * the client just lets AuthGuard in the root layout redirect once the
 * auth state lands.
 */
export default function SignUpScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const {
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
    setError,
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { displayName: '', email: '', password: '' },
  });

  // If the user abandons sign-up after a name rejection (back to welcome /
  // sign-in), the hold must not outlive this screen — a stuck hold with a
  // signed-in user would trap them in the auth group forever.
  React.useEffect(
    () => () => useAuthStore.getState().setSignupHold(false),
    [],
  );

  const onSubmit = async ({ displayName, email, password }: FormData) => {
    // Hold AuthGuard's redirect while the typed display name is persisted —
    // the PATCH below can reject (server profanity filter) and the rejection
    // must land inline on THIS form, not after navigation.
    useAuthStore.getState().setSignupHold(true);
    try {
      // Retry path: if the first attempt's PATCH was rejected, the Firebase
      // account already exists and we are signed in — creating again would
      // fail with email-already-in-use. Reuse the session, but ONLY for the
      // same email: an edited email on retry must not silently save the name
      // to the first account while the form shows a different address.
      const existingUser = auth.currentUser;
      if (
        existingUser?.email &&
        existingUser.email.toLowerCase() !== email.trim().toLowerCase()
      ) {
        // Keep the hold — releasing it here would let AuthGuard redirect the
        // signed-in user into the app mid-error.
        setError('email', {
          message: `This signup already created an account for ${existingUser.email}. Keep that email here; you can change it later in Profile.`,
        });
        return;
      }
      const firebaseUser =
        existingUser ??
        (await createUserWithEmailAndPassword(auth, email.trim(), password)).user;

      // Persist the typed name through the VALIDATED path FIRST — the
      // Firebase profile is only written after the backend accepts, so a
      // rejected name (profanity filter) never lands anywhere, not even at
      // the identity provider. The first authenticated call here also
      // auto-provisions the backend account (with a generated name, since
      // this token predates any profile update); the PATCH applies the real
      // one.
      let nameAccepted = true;
      try {
        await usersApi.updateDisplayName(displayName.trim());
      } catch (patchErr: unknown) {
        const serverMessage =
          (patchErr as { response?: { data?: { errors?: { errorMessage?: string }[] } }; })
            ?.response?.data?.errors?.[0]?.errorMessage;
        if (serverMessage) {
          // Validation rejection (e.g. "That display name isn't allowed.") —
          // keep the hold, show it on the field, let the user fix and retry.
          setError('displayName', { message: serverMessage });
          return;
        }
        // Transport failure: don't strand a created account on the sign-up
        // screen — proceed with the generated name; Profile can fix it later.
        nameAccepted = false;
        console.warn('[SignUp] display name PATCH failed (non-validation)', patchErr);
      }

      if (nameAccepted) {
        // Mirror the accepted name onto the Firebase profile; reload so the
        // local user object reflects it immediately.
        await updateProfile(firebaseUser, { displayName: displayName.trim() });
        await firebaseUser.reload();
      }
      // Release the hold — AuthGuard in root _layout.tsx now redirects.
      useAuthStore.getState().setSignupHold(false);
    } catch (err: unknown) {
      const code = (err as { code?: string })?.code ?? '';
      if (code === 'auth/email-already-in-use') {
        setError('root', {
          message: 'An account with this email already exists. Sign in instead.',
        });
      } else if (code === 'auth/invalid-email') {
        setError('email', { message: 'Please enter a valid email' });
      } else if (code === 'auth/weak-password') {
        setError('password', { message: 'Please choose a stronger password.' });
      } else if (code === 'auth/too-many-requests') {
        setError('root', { message: 'Too many attempts. Please try again later.' });
      } else if (code === 'auth/network-request-failed') {
        setError('root', { message: 'Network error. Check your connection and try again.' });
      } else {
        setError('root', { message: 'Sign up failed. Please try again.' });
      }
      // No account materialized (or it pre-existed) — nothing to hold for.
      useAuthStore.getState().setSignupHold(false);
    }
  };

  const backToSignIn = () => {
    if (router.canGoBack()) {
      router.back();
      return;
    }
    router.replace('/(auth)/sign-in');
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
          <Text style={[styles.cardTitle, { color: theme.text }]}>Create account</Text>

          {/* Root error */}
          {errors.root && (
            <View style={styles.errorBanner}>
              <Text style={[styles.errorBannerText, { color: theme.error }]}>
                {errors.root.message}
              </Text>
            </View>
          )}

          {/* Display name */}
          <View style={styles.field}>
            <Text style={[styles.label, { color: theme.textMuted }]}>Display Name</Text>
            <Controller
              control={control}
              name="displayName"
              render={({ field: { onChange, value, onBlur } }) => (
                <TextInput
                  style={[
                    styles.input,
                    {
                      backgroundColor: theme.background,
                      borderColor: errors.displayName ? theme.error : theme.border,
                      color: theme.text,
                    },
                  ]}
                  placeholder="How you'll appear in leagues"
                  placeholderTextColor={theme.textMuted}
                  onChangeText={onChange}
                  onBlur={onBlur}
                  value={value}
                  autoCapitalize="words"
                  textContentType="name"
                  autoComplete="name"
                  returnKeyType="next"
                />
              )}
            />
            {errors.displayName && (
              <Text style={[styles.fieldError, { color: theme.error }]}>
                {errors.displayName.message}
              </Text>
            )}
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
                  placeholder="At least 6 characters"
                  placeholderTextColor={theme.textMuted}
                  onChangeText={onChange}
                  onBlur={onBlur}
                  value={value}
                  secureTextEntry
                  textContentType="newPassword"
                  autoComplete="new-password"
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
            title="Create Account"
            onPress={handleSubmit(onSubmit)}
            loading={isSubmitting}
            fullWidth
            size="lg"
            style={{ marginTop: 8 }}
          />

          <TouchableOpacity style={styles.signInLink} onPress={backToSignIn} hitSlop={10}>
            <Text style={[styles.signInText, { color: theme.textMuted }]}>
              Already have an account?{' '}
              <Text style={[styles.signInAccent, { color: theme.tint }]}>Sign in</Text>
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
  signInLink: { alignItems: 'center', marginTop: 18 },
  signInText: { fontSize: 14 },
  signInAccent: { fontWeight: '700' },
});
