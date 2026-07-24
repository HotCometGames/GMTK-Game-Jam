# Gravity Is Free

A three-round, whiteboard-styled platform puzzle about finishing an entire run
with only four jumps. Falling, airflow, springs, conveyors, patience, and
momentum are free.

## Play locally

Requires Node.js 22.13 or newer.

```bash
npm install
npm run dev
```

Open the local URL printed by the development server.

## Controls

| Action | Keyboard | Gamepad |
| --- | --- | --- |
| Move | A / D or arrow keys | Left stick or D-pad |
| Jump | Space, W, or Up | South face button |
| Restart run | R | Back / Select |
| Pause | Escape | Start |
| Mute | M | — |

Only a valid player-initiated jump spends one of the four jump pips.
Environmental launches and ledge catches are free. Any death or attempted
fifth jump restarts the complete three-round run.

## Checks

```bash
npm test
npm run build
```

The tests cover jump accounting, session transitions, failure ledgers,
controller mappings, and level-data invariants.
