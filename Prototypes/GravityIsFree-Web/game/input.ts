export const KEY_BINDINGS = {
  left: ["A", "ArrowLeft"],
  right: ["D", "ArrowRight"],
  jump: ["Space", "W", "ArrowUp"],
  restart: ["R"],
  pause: ["Escape"],
  mute: ["M"],
} as const;

export const GAMEPAD_BUTTONS = {
  jump: 0,
  restart: 8,
  pause: 9,
} as const;

export interface GamepadInput {
  left: boolean;
  right: boolean;
  jump: boolean;
  restart: boolean;
  pause: boolean;
}

type ButtonLike = Pick<GamepadButton, "pressed" | "value">;

export type GamepadLike = {
  axes: readonly number[];
  buttons: readonly ButtonLike[];
};

const isPressed = (
  gamepad: GamepadLike,
  button: number,
): boolean => Boolean(gamepad.buttons[button]?.pressed);

/**
 * Convert the browser Gamepad API shape into the small set of actions used by
 * the game. D-pad buttons are included because several controllers report
 * digital movement independently from the left stick.
 */
export function readGamepad(
  gamepad: GamepadLike | null | undefined,
  deadzone = 0.32,
): GamepadInput {
  if (!gamepad) {
    return {
      left: false,
      right: false,
      jump: false,
      restart: false,
      pause: false,
    };
  }

  const horizontal = gamepad.axes[0] ?? 0;

  return {
    left: horizontal < -deadzone || isPressed(gamepad, 14),
    right: horizontal > deadzone || isPressed(gamepad, 15),
    jump: isPressed(gamepad, GAMEPAD_BUTTONS.jump),
    restart: isPressed(gamepad, GAMEPAD_BUTTONS.restart),
    pause: isPressed(gamepad, GAMEPAD_BUTTONS.pause),
  };
}

export function risingEdge(
  current: GamepadInput,
  previous: GamepadInput,
  action: keyof GamepadInput,
): boolean {
  return current[action] && !previous[action];
}
