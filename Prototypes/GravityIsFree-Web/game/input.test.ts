import { describe, expect, it } from "vitest";
import {
  GAMEPAD_BUTTONS,
  readGamepad,
  risingEdge,
  type GamepadInput,
  type GamepadLike,
} from "./input";

function makeGamepad(
  pressed: number[] = [],
  horizontalAxis = 0,
): GamepadLike {
  return {
    axes: [horizontalAxis],
    buttons: Array.from({ length: 16 }, (_, index) => ({
      pressed: pressed.includes(index),
      value: pressed.includes(index) ? 1 : 0,
    })),
  };
}

describe("readGamepad", () => {
  it("maps the standard face and menu buttons", () => {
    const input = readGamepad(
      makeGamepad([
        GAMEPAD_BUTTONS.jump,
        GAMEPAD_BUTTONS.restart,
        GAMEPAD_BUTTONS.pause,
      ]),
    );

    expect(input).toMatchObject({
      jump: true,
      restart: true,
      pause: true,
    });
  });

  it("supports both the analog stick and d-pad", () => {
    expect(readGamepad(makeGamepad([], -0.7)).left).toBe(true);
    expect(readGamepad(makeGamepad([15])).right).toBe(true);
    expect(readGamepad(makeGamepad([], 0.2)).right).toBe(false);
  });

  it("returns a neutral snapshot without a connected controller", () => {
    expect(Object.values(readGamepad(null))).toEqual([
      false,
      false,
      false,
      false,
      false,
    ]);
  });
});

describe("risingEdge", () => {
  it("fires once when a held input first becomes active", () => {
    const neutral = readGamepad(null);
    const pressed: GamepadInput = { ...neutral, jump: true };

    expect(risingEdge(pressed, neutral, "jump")).toBe(true);
    expect(risingEdge(pressed, pressed, "jump")).toBe(false);
  });
});
