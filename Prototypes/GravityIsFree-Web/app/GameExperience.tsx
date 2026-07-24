"use client";

import {
  useCallback,
  useEffect,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import { LEVELS } from "../game/levels";
import { createGameSession } from "../game/session";

const TOTAL_JUMPS = 4;

type GameRuntimeHandle = {
  destroy(): void;
  focus(): void;
};

const failureCopy = {
  budget: {
    eyebrow: "ONE JUMP TOO MANY",
    title: "The ink ran out.",
    body: "That route cost more than four jumps. Find a place to let the page carry you.",
  },
  hazard: {
    eyebrow: "RED INK",
    title: "Mind the sharp bits.",
    body: "The whole run resets, but the route you learned is still yours.",
  },
  void: {
    eyebrow: "OFF THE PAGE",
    title: "Gravity kept going.",
    body: "Some falls are shortcuts. That one was just a fall.",
  },
  manual: {
    eyebrow: "FRESH SHEET",
    title: "Rewind the run.",
    body: "Try a cheaper line through the rooms you already know.",
  },
} as const;

function CrossMark() {
  return <span className="pip-cross" aria-hidden="true" />;
}

function JumpPips({
  used,
  compact = false,
}: {
  used: number;
  compact?: boolean;
}) {
  const remaining = Math.max(0, TOTAL_JUMPS - used);

  return (
    <div
      className={`jump-budget${compact ? " jump-budget-compact" : ""}`}
      aria-label={`${remaining} of ${TOTAL_JUMPS} jumps remaining`}
    >
      <span className="budget-label">JUMPS</span>
      <div className="jump-pips" aria-hidden="true">
        {Array.from({ length: TOTAL_JUMPS }, (_, index) => {
          const spent = index < used;
          return (
            <span
              className={`jump-pip${spent ? " jump-pip-spent" : ""}`}
              key={index}
            >
              <span>{index + 1}</span>
              {spent ? <CrossMark /> : null}
            </span>
          );
        })}
      </div>
    </div>
  );
}

function ControlKey({
  keys,
  label,
}: {
  keys: readonly string[];
  label: string;
}) {
  return (
    <span className="control-item">
      <span className="control-keys" aria-hidden="true">
        {keys.map((key) => (
          <kbd key={key}>{key}</kbd>
        ))}
      </span>
      <span>{label}</span>
    </span>
  );
}

export function GameExperience() {
  const [session] = useState(createGameSession);
  const state = useSyncExternalStore(
    session.subscribe,
    session.getSnapshot,
    session.getSnapshot,
  );
  const gameMountRef = useRef<HTMLDivElement>(null);
  const runtimeRef = useRef<GameRuntimeHandle | null>(null);

  const activeLevel = LEVELS[state.roundIndex] ?? LEVELS[0];

  useEffect(() => {
    const mountNode = gameMountRef.current;
    if (!mountNode) {
      return;
    }

    let cancelled = false;
    let mountedRuntime: GameRuntimeHandle | null = null;

    void import("../game/runtime").then(({ mountGravityGame }) => {
      if (cancelled) {
        return;
      }

      mountedRuntime = mountGravityGame(mountNode, session);
      runtimeRef.current = mountedRuntime;
    });

    return () => {
      cancelled = true;
      mountedRuntime?.destroy();
      if (runtimeRef.current === mountedRuntime) {
        runtimeRef.current = null;
      }
    };
  }, [session]);

  const focusGame = useCallback(() => {
    window.requestAnimationFrame(() => runtimeRef.current?.focus());
  }, []);

  const startRun = useCallback(() => {
    session.startRun();
    focusGame();
  }, [focusGame, session]);

  const retryRun = useCallback(() => {
    session.restartAfterFailure();
    focusGame();
  }, [focusGame, session]);

  const resumeRun = useCallback(() => {
    session.togglePause();
    focusGame();
  }, [focusGame, session]);

  const failure =
    failureCopy[state.failureReason ?? "void"] ?? failureCopy.void;

  return (
    <main className="game-page">
      <div className="page-doodle page-doodle-one" aria-hidden="true">
        ↓
      </div>
      <div className="page-doodle page-doodle-two" aria-hidden="true">
        gravity = free
      </div>

      <header className="site-heading">
        <div>
          <p className="kicker">A THREE-ROUND PLATFORM PUZZLE</p>
          <h1>Gravity Is Free</h1>
        </div>
        <p className="site-thesis">
          Four jumps. Three rooms.
          <br />
          Spend them like they matter.
        </p>
      </header>

      <section
        className="game-shell"
        aria-label="Gravity Is Free game"
        onMouseDown={focusGame}
      >
        <div className="game-frame">
          <div
            className="game-canvas"
            ref={gameMountRef}
            role="application"
            aria-label="Interactive platform game canvas"
          />

          <div className="hud-layer">
            <div className="hud-top">
              <div className="round-stamp">
                <span>ROUND</span>
                <strong>{state.roundIndex + 1}</strong>
                <span>/ {LEVELS.length}</span>
              </div>

              <div className="room-label" aria-live="polite">
                <span>{activeLevel.title}</span>
                <small>{activeLevel.subtitle}</small>
              </div>

              <JumpPips used={state.jumpsUsed} />
            </div>

            <div className="hud-actions">
              <button
                className="icon-button"
                type="button"
                onClick={() => session.toggleMute()}
                aria-label={state.muted ? "Turn sound on" : "Mute sound"}
                aria-pressed={state.muted}
              >
                <span aria-hidden="true">{state.muted ? "SOUND ×" : "SOUND ∿"}</span>
              </button>
              <button
                className="icon-button"
                type="button"
                onClick={() => session.togglePause()}
                aria-label={state.phase === "paused" ? "Resume game" : "Pause game"}
                aria-pressed={state.phase === "paused"}
                disabled={
                  state.phase === "title" ||
                  state.phase === "dead" ||
                  state.phase === "victory"
                }
              >
                <span aria-hidden="true">
                  {state.phase === "paused" ? "RESUME ▶" : "PAUSE Ⅱ"}
                </span>
              </button>
            </div>

          </div>

          {state.phase === "title" ? (
            <div className="game-overlay title-overlay">
              <div className="paper-card title-card">
                <span className="tape tape-left" aria-hidden="true" />
                <span className="tape tape-right" aria-hidden="true" />
                <p className="overlay-kicker">YOUR ENTIRE RUN GETS</p>
                <div className="title-budget">
                  <strong>4</strong>
                  <span>JUMPS</span>
                </div>
                <p className="title-equation">
                  3 rooms <span aria-hidden="true">×</span> 1 shared budget
                </p>
                <p className="title-rule">
                  Reach every portal. If you ask for a fifth jump, the page
                  erases itself.
                </p>
                <button className="marker-button" type="button" onClick={startRun}>
                  START THE RUN <span aria-hidden="true">→</span>
                </button>
                <p className="start-help">Move with A/D · Jump with Space</p>
              </div>
            </div>
          ) : null}

          {state.phase === "transition" ? (
            <div className="transition-wash" aria-live="polite">
              <span>PATH RECORDED</span>
            </div>
          ) : null}

          {state.phase === "dead" ? (
            <div
              className="game-overlay death-overlay"
              role="dialog"
              aria-modal="true"
              aria-labelledby="death-title"
            >
              <div className="paper-card death-card">
                <span className="erasure-swipe" aria-hidden="true" />
                <p className="overlay-kicker">{failure.eyebrow}</p>
                <h2 id="death-title">{failure.title}</h2>
                <p>{failure.body}</p>
                <div className="death-stats">
                  <span>
                    ATTEMPT <strong>{state.attempt}</strong>
                  </span>
                  <JumpPips used={state.jumpsUsed} compact />
                </div>
                <button className="marker-button" type="button" onClick={retryRun} autoFocus>
                  TRY A CHEAPER ROUTE <span aria-hidden="true">↻</span>
                </button>
                <p className="start-help">Press jump to skip the erasure</p>
              </div>
            </div>
          ) : null}

          {state.phase === "paused" ? (
            <div
              className="game-overlay pause-overlay"
              role="dialog"
              aria-modal="true"
              aria-labelledby="pause-title"
            >
              <div className="paper-card pause-card">
                <p className="overlay-kicker">PENCIL DOWN</p>
                <h2 id="pause-title">Run paused.</h2>
                <p>The budget will wait. Gravity, surprisingly, will too.</p>
                <div className="overlay-button-row">
                  <button className="marker-button" type="button" onClick={resumeRun} autoFocus>
                    KEEP GOING <span aria-hidden="true">▶</span>
                  </button>
                  <button
                    className="text-button"
                    type="button"
                    onClick={() => session.failRun("manual")}
                  >
                    Restart the whole run
                  </button>
                </div>
              </div>
            </div>
          ) : null}

          {state.phase === "victory" ? (
            <div
              className="game-overlay victory-overlay"
              role="dialog"
              aria-modal="true"
              aria-labelledby="victory-title"
            >
              <div className="paper-card victory-card">
                <span className="victory-burst" aria-hidden="true">
                  ✦
                </span>
                <p className="overlay-kicker">
                  {state.jumpsUsed === TOTAL_JUMPS
                    ? "THE MATH WORKS"
                    : "YOU BENT THE MATH"}
                </p>
                <h2 id="victory-title">
                  {state.jumpsUsed === TOTAL_JUMPS
                    ? "Exactly four."
                    : `${state.jumpsUsed} jumps. Under budget.`}
                </h2>
                <p>
                  {state.jumpsUsed === TOTAL_JUMPS
                    ? "You spent every jump, wasted none, and let the level do the rest."
                    : "You found an even cheaper line through the page. That deserves to count."}
                </p>
                <JumpPips used={state.jumpsUsed} />
                <div className="victory-stats">
                  <span>JUMPS USED</span>
                  <strong>{state.jumpsUsed} / {TOTAL_JUMPS}</strong>
                  <span>ATTEMPTS</span>
                  <strong>{state.attempt}</strong>
                </div>
                <button className="marker-button" type="button" onClick={startRun} autoFocus>
                  PLAY IT AGAIN <span aria-hidden="true">↻</span>
                </button>
              </div>
            </div>
          ) : null}

          <p className="sr-only" aria-live="polite">
            Round {state.roundIndex + 1} of {LEVELS.length}.{" "}
            {TOTAL_JUMPS - state.jumpsUsed} jumps remaining.
          </p>
        </div>

        <div className="controls-strip" aria-label="Game controls">
          <div className="controls-primary">
            <ControlKey keys={["A", "D"]} label="MOVE" />
            <ControlKey keys={["SPACE"]} label="JUMP" />
            <ControlKey keys={["R"]} label="RESTART" />
            <ControlKey keys={["ESC"]} label="PAUSE" />
          </div>
          <div className="attempt-mark">
            <span>ATTEMPT</span>
            <strong>{state.attempt}</strong>
          </div>
        </div>
      </section>

      <footer className="game-footer">
        <p>
          <span className="teal-dot" aria-hidden="true" />
          Teal things move you for free.
        </p>
        <p>Keyboard + gamepad supported</p>
      </footer>
    </main>
  );
}
