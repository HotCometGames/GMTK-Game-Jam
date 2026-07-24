import { describe, expect, it, vi } from "vitest";

import { createGameSession, TOTAL_JUMPS } from "./session";

function start(session: ReturnType<typeof createGameSession>) {
  session.startRun();
  expect(session.getSnapshot().phase).toBe("playing");
}

function reachRound(
  session: ReturnType<typeof createGameSession>,
  spends: readonly number[],
) {
  for (const spend of spends) {
    for (let index = 0; index < spend; index += 1) {
      expect(session.acceptJump()).toBe(true);
    }
    expect(session.completeRound()).toBe(true);
    expect(session.beginNextRound()).toBe(true);
  }
}

describe("createGameSession", () => {
  it("accepts exactly four genuine jumps and kills on the fifth request", () => {
    const session = createGameSession();
    start(session);

    for (let index = 0; index < TOTAL_JUMPS; index += 1) {
      expect(session.acceptJump()).toBe(true);
    }

    expect(session.getSnapshot().jumpsUsed).toBe(4);
    expect(session.getSnapshot().phase).toBe("playing");
    expect(session.acceptJump()).toBe(false);
    expect(session.getSnapshot()).toMatchObject({
      phase: "dead",
      failureReason: "budget",
      jumpsUsed: 4,
      lastLedger: [4],
    });
  });

  it("tracks 0/1/3 round spending and wins on exactly four jumps", () => {
    const session = createGameSession();
    start(session);

    expect(session.completeRound()).toBe(true);
    expect(session.getSnapshot()).toMatchObject({
      phase: "transition",
      committedPerRound: [0],
    });
    expect(session.beginNextRound()).toBe(true);

    expect(session.acceptJump()).toBe(true);
    expect(session.completeRound()).toBe(true);
    expect(session.beginNextRound()).toBe(true);

    for (let index = 0; index < 3; index += 1) {
      expect(session.acceptJump()).toBe(true);
    }
    expect(session.completeRound()).toBe(true);

    expect(session.getSnapshot()).toMatchObject({
      phase: "victory",
      jumpsUsed: 4,
      committedPerRound: [0, 1, 3],
      lastLedger: [0, 1, 3],
    });
  });

  it("shows why naive and partially optimized runs exhaust the budget", () => {
    const naive = createGameSession();
    start(naive);
    reachRound(naive, [1, 3]);
    expect(naive.getSnapshot()).toMatchObject({
      roundIndex: 2,
      jumpsUsed: 4,
      committedPerRound: [1, 3],
    });
    expect(naive.acceptJump()).toBe(false);
    expect(naive.getSnapshot().lastLedger).toEqual([1, 3, 0]);

    const partial = createGameSession();
    start(partial);
    reachRound(partial, [0, 3]);
    expect(partial.acceptJump()).toBe(true);
    expect(partial.acceptJump()).toBe(false);
    expect(partial.getSnapshot().lastLedger).toEqual([0, 3, 1]);
  });

  it("preserves the failed ledger, attempts, and mute setting", () => {
    const session = createGameSession();
    start(session);
    session.toggleMute();
    session.acceptJump();
    session.failRun("hazard");
    session.restartAfterFailure();
    session.failRun("void");
    expect(session.restartAfterFailure()).toBe(true);
    session.acceptJump();
    session.failRun("manual");

    expect(session.getSnapshot()).toMatchObject({
      phase: "dead",
      attempt: 3,
      failureReason: "manual",
      lastLedger: [1],
      muted: true,
    });

    expect(session.restartAfterFailure()).toBe(true);
    expect(session.getSnapshot()).toMatchObject({
      phase: "playing",
      attempt: 4,
      roundIndex: 0,
      jumpsUsed: 0,
      committedPerRound: [],
      lastLedger: [1],
      muted: true,
    });
  });

  it("pauses only active play and lets mute work in every phase", () => {
    const session = createGameSession();
    session.togglePause();
    expect(session.getSnapshot().phase).toBe("title");

    start(session);
    session.togglePause();
    expect(session.getSnapshot().phase).toBe("paused");
    expect(session.acceptJump()).toBe(false);
    session.togglePause();
    expect(session.getSnapshot().phase).toBe("playing");

    session.toggleMute();
    expect(session.getSnapshot().muted).toBe(true);
  });

  it("publishes stable snapshots only when an action changes state", () => {
    const session = createGameSession();
    const listener = vi.fn();
    const unsubscribe = session.subscribe(listener);
    const titleSnapshot = session.getSnapshot();

    expect(session.acceptJump()).toBe(false);
    expect(session.getSnapshot()).toBe(titleSnapshot);
    expect(listener).not.toHaveBeenCalled();

    session.startRun();
    const playingSnapshot = session.getSnapshot();
    expect(playingSnapshot).not.toBe(titleSnapshot);
    expect(session.getSnapshot()).toBe(playingSnapshot);
    expect(listener).toHaveBeenCalledTimes(1);

    unsubscribe();
    session.toggleMute();
    expect(listener).toHaveBeenCalledTimes(1);
  });
});
