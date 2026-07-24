import { describe, expect, it } from "vitest";

import { getLevel, LEVELS, type LevelDefinition, type Rect } from "./levels";

function rectIsInside(inner: Rect, outer: Rect) {
  return (
    inner.x >= outer.x &&
    inner.y >= outer.y &&
    inner.width > 0 &&
    inner.height > 0 &&
    inner.x + inner.width <= outer.x + outer.width &&
    inner.y + inner.height <= outer.y + outer.height
  );
}

function allPlacedObjects(level: LevelDefinition) {
  return [
    ...level.solids,
    ...level.hazards,
    ...level.catchZones,
    ...level.conveyors,
    ...level.fans,
    ...level.springs,
    ...level.movingPlatforms,
    ...level.exits,
  ];
}

describe("LEVELS", () => {
  it("declares the intended 0/1/3 solution and 1/3/5 decoy costs", () => {
    expect(LEVELS.map((level) => level.optimalJumps)).toEqual([0, 1, 3]);
    expect(LEVELS.map((level) => level.decoyJumps)).toEqual([1, 3, 5]);
    expect(LEVELS.map((level) => level.id)).toEqual([
      "trust-the-fall",
      "borrowed-height",
      "the-audit",
    ]);
  });

  it("keeps every placed object and spawn inside positive world bounds", () => {
    for (const level of LEVELS) {
      expect(level.worldBounds).toMatchObject({ x: 0, y: 0 });
      expect(level.worldBounds.height).toBeGreaterThanOrEqual(720);
      expect(level.worldBounds.width).toBeGreaterThanOrEqual(1280);
      expect(level.spawn.x).toBeGreaterThanOrEqual(level.worldBounds.x);
      expect(level.spawn.y).toBeGreaterThanOrEqual(level.worldBounds.y);
      expect(level.spawn.x).toBeLessThan(
        level.worldBounds.x + level.worldBounds.width,
      );
      expect(level.spawn.y).toBeLessThan(
        level.worldBounds.y + level.worldBounds.height,
      );

      for (const object of allPlacedObjects(level)) {
        expect(
          rectIsInside(object.bounds, level.worldBounds),
          `${level.id}/${object.id} must fit inside its world`,
        ).toBe(true);
      }

      for (const platform of level.movingPlatforms) {
        expect(
          rectIsInside(
            {
              x: platform.to.x,
              y: platform.to.y,
              width: platform.bounds.width,
              height: platform.bounds.height,
            },
            level.worldBounds,
          ),
          `${level.id}/${platform.id} destination must fit inside its world`,
        ).toBe(true);
      }
    }
  });

  it("uses unique object IDs within each level", () => {
    for (const level of LEVELS) {
      const ids = allPlacedObjects(level).map((object) => object.id);
      expect(new Set(ids).size, `${level.id} has duplicate IDs`).toBe(
        ids.length,
      );
    }
  });

  it("provides the required puzzle machinery", () => {
    for (const level of LEVELS) {
      expect(level.solids.length).toBeGreaterThan(0);
      expect(level.hazards.length).toBeGreaterThan(0);
      expect(level.exits.length).toBeGreaterThan(0);
    }

    expect(LEVELS[0].catchZones.length).toBeGreaterThan(0);
    expect(LEVELS[0].exits.map((exit) => exit.kind)).toEqual([
      "upper",
      "hidden",
    ]);
    expect(LEVELS[1].conveyors.length).toBeGreaterThan(0);
    expect(LEVELS[1].fans.length).toBeGreaterThan(0);
    expect(LEVELS[2].springs.length).toBeGreaterThan(0);
    expect(LEVELS[2].movingPlatforms.length).toBeGreaterThan(0);
  });

  it("keeps round one's danger and side opening below the initial view", () => {
    const roundOne = LEVELS[0];
    const [startBlock, upperMass, lowerMass] = roundOne.solids;
    const spikes = roundOne.hazards[0];
    const sideOpening = roundOne.catchZones[0];

    expect(roundOne.worldBounds.height).toBe(1200);
    expect(roundOne.solids.map((solid) => solid.id)).toEqual([
      "starting-block",
      "second-block-upper-mass",
      "second-block-lower-mass",
    ]);
    expect(startBlock.bounds.x + startBlock.bounds.width).toBeLessThan(
      upperMass.bounds.x,
    );
    expect(upperMass.bounds.x).toBe(lowerMass.bounds.x);
    expect(upperMass.bounds.width).toBe(lowerMass.bounds.width);
    expect(upperMass.bounds.y + upperMass.bounds.height).toBeLessThan(
      lowerMass.bounds.y,
    );
    expect(spikes.bounds.y).toBeGreaterThan(720);
    expect(sideOpening.visible).toBe(false);
    expect(sideOpening.landing.x).toBeGreaterThanOrEqual(upperMass.bounds.x);
    expect(sideOpening.landing.y).toBeGreaterThan(
      upperMass.bounds.y + upperMass.bounds.height,
    );
    expect(sideOpening.landing.y).toBeLessThan(lowerMass.bounds.y);
  });

  it("rejects invalid round lookups", () => {
    expect(getLevel(0)).toBe(LEVELS[0]);
    expect(() => getLevel(-1)).toThrow(RangeError);
    expect(() => getLevel(3)).toThrow("Unknown round index");
  });
});
