# Level Design Brief — for Codex

You are writing `LevelSpec` JSON files that `LevelBuilder.cs` turns into real Unity levels.
Read this before writing a level, not just the JSON schema — the schema tells you the syntax,
this tells you what makes a level *good*.

## The core mechanic
The player has a shared move budget (`startingMoves`) for the whole level. Free actions:
walking and falling. Costed actions subtract from the budget:
- Jump: 1
- Dash: 1
- Push (crate): 1, but only if the destination tile is clear — a blocked push costs nothing
- Grapple: 2
- Wall-break: 2 (if unlocked in this level)

A level is "solved" when the player reaches the `NextLevel` trigger with moves left, and
"failed" if they try an action with zero moves left, or touch a `DeathTrigger`.

## What makes a level good (not just valid)
1. **The intended solution should cost exactly `parMoveCount`.** Work backwards: decide the
   path you want the player to find, count the actions it needs, and set `parMoveCount` to
   that number.
2. **`startingMoves` should give a little breathing room over `parMoveCount`**, not be
   drastically bigger. A gap of 0–1 moves keeps the "countdown" tension the game is about. A
   gap of 3+ makes the budget irrelevant.
3. **There should be a "naive" path and a "clever" path.** The obvious route (e.g. jump every
   gap individually) should cost MORE than `startingMoves` — that's what makes the player search
   for the efficient route instead of brute-forcing it.
4. **Every level must be solvable within `startingMoves` by at least one path.** Never design a
   level where the platform layout makes the intended solution mathematically impossible.
5. **Only include actions in `unlockedActions` that the level is actually designed around.**
   Don't unlock Dash "just in case" — every unlocked action should be necessary somewhere in
   the intended solution, or it's a dead mechanic the player never has a reason to use.

## Coordinate conventions
- All `x`/`y` values are in TILE units, not world units. `x: 5` means 5 tiles from the origin.
- Y increases UPWARD (standard math convention, not screen convention).
- The player prefab's origin is its feet — place `PlayerPrefab` at `y` one tile above the
  ground tile it should be standing on, so it doesn't spawn inside the floor.
- `DeathTrigger` is typically a wide, short trigger placed well below the lowest platform
  (e.g. `y: -5` relative to play area, spanning the full `gridWidth` as its `width`) to catch
  any fall into the void.
- `NextLevel` should be placed at the far end of the intended path, reachable only after
  completing it.

## Available tile types
9 x 9 grid
- `Top Left Tile` — solid, standard platform tile.
- `Top Middle Tile` — solid, standard platform tile.
- `Top Right Tile` — solid, standard platform tile.
- `Middle Left Tile` — solid, standard platform tile.
- `Middle Right Tile` — solid, standard platform tile.
- `Bottom Left Tile` — solid, standard platform tile.
- `Bottom Middle Tile` — solid, standard platform tile.
- `Bottom Left Right` — solid, standard platform tile.

- `Spikes` — instant death

2 x 2 door
- `Door Top Left` — Goes to next level
- `Door Top Right` — Goes to next level
- `Door Bottom Left` — Goes to next level
- `Door Bottom Right` — Goes to next level

## Available prefab types
- `PlayerPrefab` — exactly one per level, at the start of the intended path.
- `DeathTrigger` — one or more, covering any void the player could fall into.
- `NextLevel` — one per level, at the end.
- `MovableCrate` — zero or more; each one placed should matter to the intended solution (e.g.
  push it to fill a gap, or push it aside to reveal a path).

## Escalating difficulty across a run
If you're writing several levels in sequence:
- Early levels: fewer tiles, one action type, generous but not huge move margin.
- Mid levels: introduce a second action type, tighten the margin toward 0–1 spare moves,
  start requiring the "clever path" insight.
- Late levels: combine 2+ action types in one solution, tiles spread further apart, par tight
  enough that a single wasted move fails the level.

## Before finalizing a level JSON, sanity-check yourself
- Trace the intended solution path tile-by-tile and count its exact move cost. Does it equal
  `parMoveCount`?
- Is there any tile arrangement that makes the level literally unreachable (a gap wider than
  what any combination of unlocked actions can cross)?
- Does every unlocked action get used at least once in the intended solution?
