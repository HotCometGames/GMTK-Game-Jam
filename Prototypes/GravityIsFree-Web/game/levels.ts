export interface Point {
  readonly x: number;
  readonly y: number;
}

export interface Rect extends Point {
  readonly width: number;
  readonly height: number;
}

export interface SolidDefinition {
  readonly id: string;
  readonly bounds: Rect;
  readonly oneWay?: boolean;
}

export interface HazardDefinition {
  readonly id: string;
  readonly bounds: Rect;
  readonly kind: "spikes" | "void";
}

export interface CatchZoneDefinition {
  readonly id: string;
  readonly bounds: Rect;
  readonly landing: Point;
  readonly requiredDirection?: "left" | "right";
  /** Catch helpers are normally invisible; opt in only for explicit tutorials. */
  readonly visible?: boolean;
}

export interface ConveyorDefinition {
  readonly id: string;
  readonly bounds: Rect;
  /** Horizontal pixels per second added while grounded on the belt. */
  readonly speed: number;
}

export interface FanDefinition {
  readonly id: string;
  readonly bounds: Rect;
  /** Acceleration/launch impulse supplied by the environment, never a jump. */
  readonly force: Point;
}

export interface SpringDefinition {
  readonly id: string;
  readonly bounds: Rect;
  /** Upward velocity; values are negative in screen coordinates. */
  readonly impulse: number;
}

export interface MovingPlatformDefinition {
  readonly id: string;
  readonly bounds: Rect;
  /** Destination for the platform's top-left corner. */
  readonly to: Point;
  readonly speed: number;
  readonly pauseMs: number;
}

export interface ExitDefinition {
  readonly id: string;
  readonly bounds: Rect;
  readonly kind: "upper" | "hidden" | "goal";
}

export type LevelId =
  | "trust-the-fall"
  | "borrowed-height"
  | "the-audit";

export interface LevelDefinition {
  readonly id: LevelId;
  readonly title: string;
  readonly subtitle: string;
  readonly worldBounds: Rect;
  readonly spawn: Point;
  readonly optimalJumps: number;
  readonly decoyJumps: number;
  readonly solids: readonly SolidDefinition[];
  readonly hazards: readonly HazardDefinition[];
  readonly catchZones: readonly CatchZoneDefinition[];
  readonly conveyors: readonly ConveyorDefinition[];
  readonly fans: readonly FanDefinition[];
  readonly springs: readonly SpringDefinition[];
  readonly movingPlatforms: readonly MovingPlatformDefinition[];
  readonly exits: readonly ExitDefinition[];
}

const TRUST_THE_FALL: LevelDefinition = {
  id: "trust-the-fall",
  title: "Trust the Fall",
  subtitle: "The shortest route is not always the cheapest.",
  worldBounds: { x: 0, y: 0, width: 2300, height: 1200 },
  spawn: { x: 170, y: 340 },
  optimalJumps: 0,
  decoyJumps: 1,
  solids: [
    {
      id: "starting-block",
      bounds: { x: 0, y: 400, width: 650, height: 800 },
    },
    {
      id: "second-block-upper-mass",
      bounds: { x: 850, y: 400, width: 1450, height: 430 },
    },
    {
      id: "second-block-lower-mass",
      bounds: { x: 850, y: 1030, width: 1450, height: 170 },
    },
  ],
  hazards: [
    {
      id: "gap-spikes",
      bounds: { x: 650, y: 1155, width: 200, height: 45 },
      kind: "spikes",
    },
  ],
  catchZones: [
    {
      id: "second-block-side-opening",
      bounds: { x: 625, y: 820, width: 330, height: 205 },
      landing: { x: 905, y: 975 },
      requiredDirection: "right",
      visible: false,
    },
  ],
  conveyors: [],
  fans: [],
  springs: [],
  movingPlatforms: [],
  exits: [
    {
      id: "visible-upper-portal",
      bounds: { x: 2130, y: 320, width: 92, height: 80 },
      kind: "upper",
    },
    {
      id: "hidden-side-portal",
      bounds: { x: 1050, y: 940, width: 92, height: 90 },
      kind: "hidden",
    },
  ],
};

const BORROWED_HEIGHT: LevelDefinition = {
  id: "borrowed-height",
  title: "Borrowed Height",
  subtitle: "The page can move you for free.",
  worldBounds: { x: 0, y: 0, width: 2800, height: 720 },
  spawn: { x: 140, y: 500 },
  optimalJumps: 1,
  decoyJumps: 3,
  solids: [
    {
      id: "start-floor",
      bounds: { x: 0, y: 560, width: 500, height: 160 },
    },
    {
      id: "obvious-step-one",
      bounds: { x: 600, y: 480, width: 250, height: 30 },
      oneWay: true,
    },
    {
      id: "obvious-step-two",
      bounds: { x: 930, y: 400, width: 245, height: 30 },
      oneWay: true,
    },
    {
      id: "obvious-step-three",
      bounds: { x: 1260, y: 320, width: 500, height: 30 },
    },
    {
      id: "duct-floor",
      bounds: { x: 500, y: 660, width: 1120, height: 60 },
    },
    {
      id: "fan-landing",
      bounds: { x: 1580, y: 430, width: 300, height: 30 },
      oneWay: true,
    },
    {
      id: "goal-runway",
      bounds: { x: 1980, y: 300, width: 820, height: 36 },
      oneWay: true,
    },
    {
      id: "duct-ceiling-lip",
      bounds: { x: 500, y: 535, width: 500, height: 24 },
    },
  ],
  hazards: [
    {
      id: "left-void",
      bounds: { x: 500, y: 704, width: 1120, height: 16 },
      kind: "void",
    },
    {
      id: "final-gap-spikes",
      bounds: { x: 1865, y: 686, width: 115, height: 34 },
      kind: "spikes",
    },
  ],
  catchZones: [],
  conveyors: [
    {
      id: "duct-conveyor",
      bounds: { x: 545, y: 630, width: 720, height: 30 },
      speed: 285,
    },
  ],
  fans: [
    {
      id: "duct-fan",
      bounds: { x: 1265, y: 355, width: 300, height: 305 },
      force: { x: 210, y: -1040 },
    },
  ],
  springs: [],
  movingPlatforms: [],
  exits: [
    {
      id: "visible-upper-portal",
      bounds: { x: 1640, y: 240, width: 94, height: 80 },
      kind: "upper",
    },
    {
      id: "goal-portal",
      bounds: { x: 2640, y: 220, width: 94, height: 80 },
      kind: "goal",
    },
  ],
};

const THE_AUDIT: LevelDefinition = {
  id: "the-audit",
  title: "The Audit",
  subtitle: "Every free lesson belongs in the final answer.",
  worldBounds: { x: 0, y: 0, width: 3600, height: 720 },
  spawn: { x: 140, y: 500 },
  optimalJumps: 3,
  decoyJumps: 5,
  solids: [
    {
      id: "start-floor",
      bounds: { x: 0, y: 560, width: 500, height: 160 },
    },
    {
      id: "island-one",
      bounds: { x: 620, y: 495, width: 240, height: 30 },
      oneWay: true,
    },
    {
      id: "decoy-island-two",
      bounds: { x: 1040, y: 425, width: 230, height: 30 },
      oneWay: true,
    },
    {
      id: "decoy-island-three",
      bounds: { x: 1460, y: 350, width: 230, height: 30 },
      oneWay: true,
    },
    {
      id: "lower-route-floor",
      bounds: { x: 810, y: 660, width: 810, height: 60 },
    },
    {
      id: "fan-landing",
      bounds: { x: 1635, y: 435, width: 330, height: 30 },
      oneWay: true,
    },
    {
      id: "goal-runway",
      bounds: { x: 2570, y: 205, width: 1030, height: 38 },
      oneWay: true,
    },
    {
      id: "fan-ceiling",
      bounds: { x: 1340, y: 155, width: 610, height: 28 },
    },
    {
      id: "route-separating-lip",
      bounds: { x: 1280, y: 515, width: 250, height: 25 },
    },
  ],
  hazards: [
    {
      id: "spring-miss-spikes",
      bounds: { x: 500, y: 686, width: 310, height: 34 },
      kind: "spikes",
    },
    {
      id: "lift-gap-void",
      bounds: { x: 1880, y: 704, width: 690, height: 16 },
      kind: "void",
    },
  ],
  catchZones: [],
  conveyors: [
    {
      id: "audit-conveyor",
      bounds: { x: 915, y: 630, width: 505, height: 30 },
      speed: 310,
    },
  ],
  fans: [
    {
      id: "audit-fan",
      bounds: { x: 1415, y: 350, width: 205, height: 310 },
      force: { x: 190, y: -1010 },
    },
  ],
  springs: [
    {
      id: "lesson-spring",
      bounds: { x: 825, y: 628, width: 90, height: 32 },
      impulse: -930,
    },
  ],
  movingPlatforms: [
    {
      id: "patient-lift",
      bounds: { x: 1920, y: 300, width: 190, height: 26 },
      to: { x: 2325, y: 220 },
      speed: 92,
      pauseMs: 900,
    },
  ],
  exits: [
    {
      id: "final-portal",
      bounds: { x: 3410, y: 125, width: 100, height: 80 },
      kind: "goal",
    },
  ],
};

export const LEVELS: readonly LevelDefinition[] = Object.freeze([
  TRUST_THE_FALL,
  BORROWED_HEIGHT,
  THE_AUDIT,
]);

export function getLevel(roundIndex: number): LevelDefinition {
  const level = LEVELS[roundIndex];
  if (!level) {
    throw new RangeError(`Unknown round index: ${roundIndex}`);
  }
  return level;
}
