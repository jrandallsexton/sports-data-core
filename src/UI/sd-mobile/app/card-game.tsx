import React from 'react';
import { Stack, useRouter } from 'expo-router';
import { useFonts } from 'expo-font';
import { View } from 'react-native';
import { CardGameScreen } from '@/src/components/features/cardgame/CardGameScreen';
import { CG, FONT_DISPLAY } from '@/src/components/features/cardgame/palette';

// Football card game -- a pass-the-time game, open to every signed-in user
// (D27). Full screen, no header: the score bar carries the back control.
//
// The display font loads here rather than at app start, so the game adds
// nothing to startup for everyone else.

export default function CardGameRoute() {
  const router = useRouter();
  const [fontsLoaded] = useFonts({
    [FONT_DISPLAY]: require('../assets/fonts/AlfaSlabOne-Regular.ttf'),
  });
  const exit = () => (router.canGoBack() ? router.back() : router.replace('/(tabs)'));

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      {fontsLoaded ? <CardGameScreen onExit={exit} /> : <View style={{ flex: 1, backgroundColor: CG.table }} />}
    </>
  );
}
