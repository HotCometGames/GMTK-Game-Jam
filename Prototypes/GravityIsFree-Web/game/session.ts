export const TOTAL_JUMPS = 4 as const;
export const ROUND_COUNT = 3 as const;

export type GamePhase =
  | "title"
  | "playing"
  | "transition"
  | "dead"
  | "victory"
  | "paused";

export type FailureReason = "budget" | "hazard" | "void" | "manual";

export interface GameState {
  readonly phase: GamePhase;
  readonly attempt: number;
  /** Zero-based index into LEVELS. */
  readonly roundIndex: number;
  readonly totalJumps: typeof TOTAL_JUMPS;
  readonly jumpsUsed: number;
  /** jumpsUsed when the current round began. */
  readonly roundEntryJumpsUsed: number;
  readonly committedPerRound: readonly number[];
  readonly failureReason: FailureReason | null;
  /**
   * The most recently completed or failed ledger. The active round is included
   * when a run fails, so the recap can explain where every jump was spent.
   */
  readonly lastLedger: readonly number[] | null;
  readonly muted: boolean;
}

export type SessionAction =
  | { readonly type: "START_RUN" }
  | { readonly type: "ACCEPT_JUMP" }
  | { readonly type: "COMPLETE_ROUND" }
  | { readonly type: "BEGIN_NEXT_ROUND" }
  | { readonly type: "FAIL_RUN"; readonly reason: FailureReason }
  | { readonly type: "RESTART_AFTER_FAILURE" }
  | { readonly type: "TOGGLE_PAUSE" }
  | { readonly type: "TOGGLE_MUTE" };

export interface GameSessionStore {
  readonly getSnapshot: () => GameState;
  readonly subscribe: (listener: () => void) => () => void;
  readonly startRun: () => void;
  readonly acceptJump: () => boolean;
  readonly completeRound: () => boolean;
  readonly beginNextRound: () => boolean;
  readonly failRun: (reason: FailureReason) => boolean;
  readonly restartAfterFailure: () => boolean;
  readonly togglePause: () => void;
  readonly toggleMute: () => void;
}

function freezeState(state: GameState): GameState {
  return Object.freeze({
    ...state,
    committedPerRound: Object.freeze([...state.committedPerRound]),
    lastLedger:
      state.lastLedger === null
        ? null
        : Object.freeze([...state.lastLedger]),
  });
}

function initialState(): GameState {
  return freezeState({
    phase: "title",
    attempt: 0,
    roundIndex: 0,
    totalJumps: TOTAL_JUMPS,
    jumpsUsed: 0,
    roundEntryJumpsUsed: 0,
    committedPerRound: [],
    failureReason: null,
    lastLedger: null,
    muted: false,
  });
}

export function createGameSession(): GameSessionStore {
  let state = initialState();
  const listeners = new Set<() => void>();

  const getSnapshot = () => state;

  const subscribe = (listener: () => void) => {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  };

  const publish = (patch: Partial<GameState>) => {
    const next = freezeState({ ...state, ...patch });
    if (next === state) {
      return;
    }
    state = next;
    for (const listener of [...listeners]) {
      listener();
    }
  };

  const resetActiveRun = (phase: "playing", attempt: number) => {
    publish({
      phase,
      attempt,
      roundIndex: 0,
      jumpsUsed: 0,
      roundEntryJumpsUsed: 0,
      committedPerRound: [],
      failureReason: null,
    });
  };

  const failRun = (reason: FailureReason): boolean => {
    if (state.phase !== "playing" && state.phase !== "paused") {
      return false;
    }

    const roundSpend = state.jumpsUsed - state.roundEntryJumpsUsed;
    const lastLedger = [...state.committedPerRound, roundSpend];

    publish({
      phase: "dead",
      failureReason: reason,
      lastLedger,
    });
    return true;
  };

  const store: GameSessionStore = {
    getSnapshot,
    subscribe,

    startRun() {
      if (
        state.phase !== "title" &&
        state.phase !== "victory" &&
        state.phase !== "dead"
      ) {
        return;
      }
      resetActiveRun("playing", state.attempt + 1);
    },

    acceptJump() {
      if (state.phase !== "playing") {
        return false;
      }
      if (state.jumpsUsed >= TOTAL_JUMPS) {
        failRun("budget");
        return false;
      }
      publish({ jumpsUsed: state.jumpsUsed + 1 });
      return true;
    },

    completeRound() {
      if (state.phase !== "playing") {
        return false;
      }

      const roundSpend = state.jumpsUsed - state.roundEntryJumpsUsed;
      const committedPerRound = [...state.committedPerRound, roundSpend];
      if (state.roundIndex === ROUND_COUNT - 1) {
        publish({
          phase: "victory",
          committedPerRound,
          lastLedger: committedPerRound,
          failureReason: null,
        });
      } else {
        publish({
          phase: "transition",
          committedPerRound,
          failureReason: null,
        });
      }
      return true;
    },

    beginNextRound() {
      if (
        state.phase !== "transition" ||
        state.roundIndex >= ROUND_COUNT - 1
      ) {
        return false;
      }
      const roundIndex = state.roundIndex + 1;
      publish({
        phase: "playing",
        roundIndex,
        roundEntryJumpsUsed: state.jumpsUsed,
        failureReason: null,
      });
      return true;
    },

    failRun,

    restartAfterFailure() {
      if (state.phase !== "dead") {
        return false;
      }
      resetActiveRun("playing", state.attempt + 1);
      return true;
    },

    togglePause() {
      if (state.phase === "playing") {
        publish({ phase: "paused" });
      } else if (state.phase === "paused") {
        publish({ phase: "playing" });
      }
    },

    toggleMute() {
      publish({ muted: !state.muted });
    },
  };

  return Object.freeze(store);
}
