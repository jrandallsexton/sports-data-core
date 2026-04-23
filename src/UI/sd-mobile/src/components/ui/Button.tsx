import React from 'react';
import {
  TouchableOpacity,
  Text,
  ActivityIndicator,
  StyleSheet,
  type ViewStyle,
  type TextStyle,
} from 'react-native';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';
type Size = 'sm' | 'md' | 'lg';

interface ButtonProps {
  title: string;
  onPress: () => void;
  variant?: Variant;
  size?: Size;
  loading?: boolean;
  disabled?: boolean;
  style?: ViewStyle;
  textStyle?: TextStyle;
  fullWidth?: boolean;
}

export function Button({
  title,
  onPress,
  variant = 'primary',
  size = 'md',
  loading = false,
  disabled = false,
  style,
  textStyle,
  fullWidth = false,
}: ButtonProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);
  const isDisabled = disabled || loading;

  // Primary/secondary variants lean on the theme accent so the button
  // tracks light/dark mode the same way web does (--accent).
  const variantBg: Record<Variant, string> = {
    primary: theme.tint,
    secondary: 'transparent',
    ghost: 'transparent',
    danger: theme.error,
  };
  const variantBorder: Record<Variant, string | undefined> = {
    primary: undefined,
    secondary: theme.tint,
    ghost: undefined,
    danger: undefined,
  };
  const variantText: Record<Variant, string> = {
    primary: theme.textOnAccent,
    secondary: theme.tint,
    ghost: theme.tint,
    // Hard-coded white; do NOT swap for theme.textOnAccent. The danger
    // background is red (theme.error) regardless of mode, so the label needs
    // a consistent light foreground. theme.textOnAccent flips to near-black
    // in dark mode (it tracks theme.tint's accessibility pair), which would
    // render illegible on red. If we ever add a dedicated theme.textOnDanger,
    // switch to that.
    danger: '#fff',
  };

  return (
    <TouchableOpacity
      style={[
        styles.base,
        {
          backgroundColor: variantBg[variant],
          ...(variantBorder[variant]
            ? { borderWidth: 2, borderColor: variantBorder[variant] }
            : null),
        },
        sizeStyles[size],
        fullWidth && styles.fullWidth,
        isDisabled && styles.disabled,
        style,
      ]}
      onPress={onPress}
      disabled={isDisabled}
      activeOpacity={0.78}
    >
      {loading ? (
        // Spinner reuses the label's already-resolved color so loading and
        // non-loading states are visually identical per variant. Previously
        // re-derived by variant here and mismatched `danger` (label was
        // hard-coded '#fff', spinner flipped to theme.textOnAccent = #111
        // in dark mode).
        <ActivityIndicator size="small" color={variantText[variant]} />
      ) : (
        <Text style={[styles.text, { color: variantText[variant] }, sizeTextStyles[size], textStyle]}>
          {title}
        </Text>
      )}
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  base: {
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
  },
  fullWidth: {
    width: '100%',
  },
  disabled: {
    opacity: 0.48,
  },
  text: {
    fontWeight: '700',
  },
});

const sizeStyles: Record<Size, ViewStyle> = {
  sm: { paddingVertical: 8, paddingHorizontal: 14 },
  md: { paddingVertical: 13, paddingHorizontal: 20 },
  lg: { paddingVertical: 16, paddingHorizontal: 28 },
};

const sizeTextStyles: Record<Size, TextStyle> = {
  sm: { fontSize: 13 },
  md: { fontSize: 15 },
  lg: { fontSize: 17 },
};
