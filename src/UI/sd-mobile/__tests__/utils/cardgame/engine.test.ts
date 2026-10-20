import { createGame, seededRng, DB, Abandoned, type CardGame } from '@/src/utils/cardgame/engine';

// Answers every prompt with its first card (or its first button), on a
// microtask so the engine isn't re-entered from inside its own notify().
function autoAnswer(pick: (g: CardGame) => unknown) {
  let game: CardGame;
  let queued = false;
  const onChange = () => {
    if (queued || !game?.prompt) return;
    queued = true;
    queueMicrotask(() => {
      queued = false;
      if (game.prompt) game.resolve(pick(game));
    });
  };
  return { bind: (g: CardGame) => (game = g), onChange };
}

const firstChoice = (g: CardGame) => {
  const p = g.prompt!;
  return p.cards.length ? p.cards[0] : p.buttons[0]?.value ?? null;
};

describe('card game engine', () => {
  it('plays CPU-vs-CPU games to a final score', async () => {
    for (let seed = 1; seed <= 20; seed++) {
      const g = createGame({ rng: seededRng(seed), delayMs: 0, human: false });
      await g.run();
      const [a, b] = g.state.players;
      expect(g.state.over).toBe(true);
      expect(a.score).toBeGreaterThanOrEqual(0);
      expect(b.score).toBeGreaterThanOrEqual(0);
      expect(g.state.log[g.state.log.length - 1].text).toMatch(/^Final/);
    }
  });

  it('plays a full game with a human seat answering every prompt', async () => {
    for (let seed = 1; seed <= 5; seed++) {
      const answer = autoAnswer(firstChoice);
      const g = createGame({ rng: seededRng(seed), delayMs: 0, onChange: answer.onChange });
      answer.bind(g);
      await g.run();
      expect(g.state.over).toBe(true);
      expect(g.prompt).toBeNull();
      expect(g.state.log.some((e) => /^You (play|answer)|^CPU read .* You ran/.test(e.text))).toBe(true);
    }
  });

  it('replays identically from the same seed', async () => {
    const play = async () => {
      const g = createGame({ rng: seededRng(42), delayMs: 0, human: false });
      await g.run();
      return g.state.log.map((e) => e.text);
    };
    expect(await play()).toEqual(await play());
  });

  it('holds a correctly read play to its read yards (D20)', async () => {
    const plays = Object.values(DB).flatMap((c) => (c.play ? [c.play] : []));
    // Held-to values: a play's read yards, or Broken Tackle's "yards minus 3" on a run.
    const allowed = new Set([
      ...plays.map((p) => p.readYards ?? 0),
      ...plays.filter((p) => p.type === 'run').map((p) => (p.yards ?? 0) - 3),
    ]);
    const held: number[] = [];
    for (let seed = 1; seed <= 10; seed++) {
      const g = createGame({ rng: seededRng(seed), delayMs: 0, human: false });
      await g.run();
      for (const e of g.state.log) {
        const m = /^Read\. Held to (\d+)\.$/.exec(e.text);
        if (m) held.push(Number(m[1]));
      }
    }
    expect(held.length).toBeGreaterThan(0);
    for (const n of held) expect(allowed.has(n)).toBe(true);
  });

  it('stops a game when abandoned', async () => {
    const g = createGame({ rng: seededRng(7), delayMs: 1, human: false });
    const running = g.run();
    g.abandon();
    await expect(running).rejects.toBeInstanceOf(Abandoned);
  });

  it('rejects run() when abandoned while a human prompt is open', async () => {
    const g = createGame({ rng: seededRng(7), delayMs: 1 });
    const running = g.run();
    while (!g.prompt) await new Promise((r) => setTimeout(r, 1));
    g.abandon();
    await expect(running).rejects.toBeInstanceOf(Abandoned);
  });
});
