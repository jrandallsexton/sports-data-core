import React, { useState } from 'react';
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
import { sendPasswordResetEmail } from 'firebase/auth';
import { router, useLocalSearchParams } from 'expo-router';

import { Text } from '@/src/components/ui/AppText';
import { auth } from '@/src/lib/firebase';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { Button } from '@/src/components/ui/Button';
import { Wordmark } from '@/src/components/brand/Wordmark';
import { getTheme } from '@/constants/Colors';

// ─── Validation schema ────────────────────────────────────────────────────────

const schema = z.object({
  email: z.string().email('Please enter a valid email'),
});

type FormData = z.infer<typeof schema>;

// ─── Screen ───────────────────────────────────────────────────────────────────

export default function ForgotPasswordScreen() {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  // Sign-in hands over whatever address was already typed so the user doesn't
  // retype it after a failed login.
  const { email: prefilledEmail } = useLocalSearchParams<{ email?: string }>();

  const [sent, setSent] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const {
    control,
    handleSubmit,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { email: prefilledEmail ?? '' },
  });

  const onSubmit = async (data: FormData) => {
    setFormError(null);
    try {
      await sendPasswordResetEmail(auth, data.email.trim());
      setSent(true);
    } catch (e) {
      const code = (e as { code?: string })?.code ?? '';

      // Never disclose whether an address has an account. An unknown address
      // gets the same confirmation a real one does — otherwise this screen
      // becomes an account-enumeration oracle. (Firebase projects with email
      // enumeration protection enabled already return success here; this
      // keeps the behavior identical either way.)
      if (code === 'auth/user-not-found' || code === 'auth/invalid-email') {
        setSent(true);
        return;
      }

      if (code === 'auth/too-many-requests') {
        setFormError('Too many attempts. Please wait a few minutes and try again.');
        return;
      }

      if (code === 'auth/network-request-failed') {
        setFormError('Network error. Check your connection and try again.');
        return;
      }

      setFormError('Something went wrong. Please try again.');
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
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
    >
      <ScrollView contentContainerStyle={styles.inner} keyboardShouldPersistTaps="handled">
        <View style={styles.header}>
          <Wordmark />
        </View>

        {sent ? (
          <View style={styles.card}>
            <Text style={[styles.title, { color: theme.text }]}>Check your email</Text>
            <Text style={[styles.body, { color: theme.textMuted }]}>
              If an account exists for {getValues('email').trim()}, we&apos;ve sent a
              link to reset your password. It may take a minute to arrive — check
              your spam folder if you don&apos;t see it.
            </Text>
            <Button
              title="Back to Sign In"
              onPress={backToSignIn}
              fullWidth
              size="lg"
              style={{ marginTop: 16 }}
            />
          </View>
        ) : (
          <View style={styles.card}>
            <Text style={[styles.title, { color: theme.text }]}>Reset your password</Text>
            <Text style={[styles.body, { color: theme.textMuted }]}>
              Enter your email and we&apos;ll send you a link to reset your password.
            </Text>

            <View style={styles.field}>
              <Text style={[styles.label, { color: theme.textMuted }]}>Email</Text>
              <Controller
                control={control}
                name="email"
                render={({ field: { onChange, onBlur, value } }) => (
                  <TextInput
                    style={[
                      styles.input,
                      {
                        backgroundColor: theme.card,
                        borderColor: errors.email ? theme.error : theme.border,
                        color: theme.text,
                      },
                    ]}
                    placeholder="you@example.com"
                    placeholderTextColor={theme.textMuted}
                    autoCapitalize="none"
                    autoComplete="email"
                    keyboardType="email-address"
                    onBlur={onBlur}
                    onChangeText={onChange}
                    value={value}
                  />
                )}
              />
              {errors.email && (
                <Text style={[styles.fieldError, { color: theme.error }]}>
                  {errors.email.message}
                </Text>
              )}
            </View>

            {formError && (
              <Text style={[styles.fieldError, { color: theme.error }]}>{formError}</Text>
            )}

            <Button
              title="Send reset link"
              onPress={handleSubmit(onSubmit)}
              loading={isSubmitting}
              fullWidth
              size="lg"
              style={{ marginTop: 8 }}
            />

            <TouchableOpacity style={styles.backLink} onPress={backToSignIn}>
              <Text style={[styles.backText, { color: theme.tint }]}>
                ← Back to sign in
              </Text>
            </TouchableOpacity>
          </View>
        )}
      </ScrollView>
    </KeyboardAvoidingView>
  );
}

// ─── Styles ───────────────────────────────────────────────────────────────────

const styles = StyleSheet.create({
  container: { flex: 1 },
  inner: { flexGrow: 1, justifyContent: 'center', padding: 20 },
  header: { alignItems: 'center', marginBottom: 32, gap: 8 },
  card: { gap: 4 },
  title: { fontSize: 22, fontWeight: '700', marginBottom: 4 },
  body: { fontSize: 14, lineHeight: 20, marginBottom: 12 },
  field: { marginBottom: 12 },
  label: { fontSize: 13, fontWeight: '600', marginBottom: 6 },
  input: {
    borderWidth: 1,
    borderRadius: 10,
    paddingHorizontal: 14,
    paddingVertical: 12,
    fontSize: 16,
  },
  fieldError: { fontSize: 13, marginTop: 6 },
  backLink: { alignSelf: 'center', marginTop: 20, padding: 8 },
  backText: { fontSize: 14, fontWeight: '600' },
});
