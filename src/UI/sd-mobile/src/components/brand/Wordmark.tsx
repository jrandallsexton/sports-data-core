import React from 'react';
import { View, Text, Image, StyleSheet } from 'react-native';
import { useColorScheme } from '@/src/lib/theme/ThemeContext';
import { getTheme } from '@/constants/Colors';

// Brand lockup: icon mark + two-tone italic wordmark.
//
// Mirrors the web Wordmark component (sd-ui/src/components/brand/Wordmark.jsx).
// "sport" inherits theme.tint (cyan in both themes). "Deets" inherits
// theme.text so it reads near-black on light and white on dark without a
// per-theme asset swap.
//
// Font: Poppins_700Bold_Italic loaded in app/_layout.tsx via useFonts.
// The Wordmark is the only place in the app that uses this weight, so
// loading it once at the root is the right place.

const iconMark = require('@/assets/images/icon.png');

interface WordmarkProps {
  /** Font size in points. Default 18 (tab header). Use 28+ for sign-in / splash surfaces. */
  size?: number;
}

export function Wordmark({ size = 18 }: WordmarkProps) {
  const scheme = useColorScheme();
  const theme = getTheme(scheme);

  const iconSize = Math.round(size * 1.15);

  return (
    <View style={styles.row}>
      <Image
        source={iconMark}
        style={{ width: iconSize, height: iconSize }}
        resizeMode="contain"
        accessibilityIgnoresInvertColors
      />
      <Text style={[styles.text, { fontSize: size }]}>
        <Text style={{ color: theme.tint }}>sport</Text>
        <Text style={{ color: theme.text }}>Deets</Text>
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  text: {
    fontFamily: 'Poppins_700Bold_Italic',
    // Don't set fontStyle/fontWeight: the italic + bold are baked into the
    // Poppins_700Bold_Italic .ttf. Setting them again would let RN fall
    // back to a synthesized bold/italic against another family if the font
    // hasn't loaded yet, masking the real problem.
    includeFontPadding: false,
  },
});
