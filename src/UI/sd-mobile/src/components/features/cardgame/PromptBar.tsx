import React from 'react';
import { Pressable, StyleSheet, View, type LayoutChangeEvent } from 'react-native';
import { Text } from '@/src/components/ui/AppText';
import { CG, FONT_DISPLAY } from './palette';

// Prompt bar: the question, the selected card's rules (with a Play button when
// it's playable), and the prompt's buttons (Kick, Pass, No response...).
// Buttons share the title's row and wrap under it only when they don't fit: every
// pixel here comes out of the hand.

export interface PromptAction {
  label: string;
  onPress: () => void;
  primary?: boolean;
}

interface Props {
  title: string;
  /** Selected card: its name (gold) and full rules. */
  selected?: { name: string; rules: string } | null;
  text?: string;
  hint?: string;
  actions: PromptAction[];
  onLayout: (e: LayoutChangeEvent) => void;
}

export function PromptBar({ title, selected, text, hint, actions, onLayout }: Props) {
  return (
    <View style={styles.bar} onLayout={onLayout}>
      <View style={styles.top}>
        <Text style={styles.title}>{title}</Text>
        {actions.length ? (
          <View style={styles.actions}>
            {actions.map((a) => (
              <Pressable
                key={a.label}
                onPress={a.onPress}
                accessibilityRole="button"
                style={({ pressed }) => [styles.btn, a.primary && styles.primary, pressed && styles.pressed]}
              >
                <Text style={[styles.btnText, a.primary && styles.primaryText]}>{a.label}</Text>
              </Pressable>
            ))}
          </View>
        ) : null}
      </View>
      {selected ? (
        <Text style={styles.body} numberOfLines={4}>
          <Text style={styles.selName}>{selected.name.toUpperCase()} </Text>
          {selected.rules}
        </Text>
      ) : text ? (
        <Text style={[styles.body, styles.muted]}>{text}</Text>
      ) : null}
      {hint ? <Text style={[styles.body, styles.hint]}>{hint}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  bar: { backgroundColor: CG.ink, paddingHorizontal: 10, paddingVertical: 8, gap: 5, minHeight: 48 },
  top: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', justifyContent: 'space-between', gap: 6 },
  title: { fontFamily: FONT_DISPLAY, fontSize: 15, lineHeight: 18, color: CG.chalk, flexShrink: 1 },
  body: { fontSize: 12, lineHeight: 16, color: CG.chalk },
  muted: { opacity: 0.85 },
  hint: { opacity: 0.6 },
  selName: { fontFamily: FONT_DISPLAY, color: CG.gold },
  actions: { flexDirection: 'row', flexWrap: 'wrap', gap: 6, marginLeft: 'auto' },
  btn: {
    borderWidth: 2,
    borderColor: CG.chalk,
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 4,
  },
  primary: { backgroundColor: CG.gold, borderColor: CG.gold },
  pressed: { opacity: 0.7 },
  btnText: { fontSize: 13, fontWeight: '700', color: CG.chalk },
  primaryText: { color: CG.ink },
});
