"use client";

import Phaser from "phaser";

import { GravityAudio } from "./audio";
import {
  readGamepad,
  risingEdge,
  type GamepadInput,
} from "./input";
import { LEVELS, type LevelDefinition, type Rect } from "./levels";
import type { GameSessionStore } from "./session";

const VIEWPORT_WIDTH = 1280;
const VIEWPORT_HEIGHT = 720;
const PLAYER_WIDTH = 34;
const PLAYER_HEIGHT = 54;
const WALK_SPEED = 350;
const WALK_ACCELERATION = 2_250;
const GROUND_DRAG = 2_100;
const AIR_DRAG = 360;
const GRAVITY = 1_700;
const JUMP_SPEED = 690;
const COYOTE_MS = 100;
const JUMP_BUFFER_MS = 120;

const COLORS = {
  paper: 0xf7f1df,
  paperShadow: 0xeadfca,
  grid: 0xb9c5be,
  ink: 0xf08b38,
  inkDark: 0xc96528,
  charcoal: 0x2f3433,
  teal: 0x2a9d94,
  tealLight: 0x83ccc1,
  danger: 0xd9544d,
  portal: 0x7352a1,
} as const;

type Snapshot = ReturnType<GameSessionStore["getSnapshot"]>;
type Phase = Snapshot["phase"];

type RuntimeKeys = {
  leftA: Phaser.Input.Keyboard.Key;
  rightD: Phaser.Input.Keyboard.Key;
  leftArrow: Phaser.Input.Keyboard.Key;
  rightArrow: Phaser.Input.Keyboard.Key;
  jumpSpace: Phaser.Input.Keyboard.Key;
  jumpW: Phaser.Input.Keyboard.Key;
  jumpUp: Phaser.Input.Keyboard.Key;
  restart: Phaser.Input.Keyboard.Key;
  mute: Phaser.Input.Keyboard.Key;
  pause: Phaser.Input.Keyboard.Key;
};

type PhysicsRectangle = Phaser.GameObjects.Rectangle & {
  body: Phaser.Physics.Arcade.Body;
};

type StaticPhysicsRectangle = Phaser.GameObjects.Rectangle & {
  body: Phaser.Physics.Arcade.StaticBody;
};

type MovingPlatformRuntime = {
  definition: LevelDefinition["movingPlatforms"][number];
  object: PhysicsRectangle;
  visual: Phaser.GameObjects.Graphics;
  start: Phaser.Math.Vector2;
  target: Phaser.Math.Vector2;
  direction: 1 | -1;
  pauseUntil: number;
};

type RuntimeInput = {
  horizontal: number;
  jumpDown: boolean;
  jumpUp: boolean;
  jumpHeld: boolean;
  restartDown: boolean;
  muteDown: boolean;
  pauseDown: boolean;
};

export type GravityGameHandle = {
  destroy: () => void;
  focus: () => void;
};

class GravityScene extends Phaser.Scene {
  private readonly session: GameSessionStore;
  private readonly audio: GravityAudio;
  private readonly reducedMotion: boolean;

  private unsubscribe: (() => void) | null = null;
  private keys: RuntimeKeys | null = null;
  private player: PhysicsRectangle | null = null;
  private character: Phaser.GameObjects.Graphics | null = null;
  private level: LevelDefinition | null = null;
  private lastRoundIndex = -1;
  private previousPhase: Phase | null = null;
  private levelObjects: Phaser.GameObjects.GameObject[] = [];
  private colliders: Phaser.Physics.Arcade.Collider[] = [];
  private movingPlatforms: MovingPlatformRuntime[] = [];
  private portalVisuals: Phaser.GameObjects.Graphics[] = [];
  private fanVisuals: Phaser.GameObjects.Graphics[] = [];
  private caughtZones = new Set<string>();
  private triggeredExit = false;
  private inputLockedUntil = 0;
  private lastGroundedAt = Number.NEGATIVE_INFINITY;
  private jumpBufferedUntil = Number.NEGATIVE_INFINITY;
  private springCooldownUntil = 0;
  private transitionTimer: Phaser.Time.TimerEvent | null = null;
  private deathTimer: Phaser.Time.TimerEvent | null = null;
  private wasGrounded = false;
  private facing: -1 | 1 = 1;
  private previousGamepad: GamepadInput = {
    left: false,
    right: false,
    jump: false,
    restart: false,
    pause: false,
  };

  constructor(session: GameSessionStore, audio: GravityAudio) {
    super({ key: "gravity-is-free" });
    this.session = session;
    this.audio = audio;
    this.reducedMotion =
      typeof window !== "undefined" &&
      window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  }

  create(): void {
    this.keys = this.createKeys();
    this.input.mouse?.disableContextMenu();
    this.buildLevel(this.session.getSnapshot().roundIndex);
    this.unsubscribe = this.session.subscribe(() => {
      this.syncSession(this.session.getSnapshot());
    });
    this.events.once(Phaser.Scenes.Events.SHUTDOWN, this.cleanUp, this);
    this.events.once(Phaser.Scenes.Events.DESTROY, this.cleanUp, this);
    this.syncSession(this.session.getSnapshot(), true);
  }

  update(time: number, delta: number): void {
    const input = this.readInput();
    const snapshotBeforeInput = this.session.getSnapshot();

    this.handleSystemInput(input, snapshotBeforeInput);
    this.animateInk(time);

    const snapshot = this.session.getSnapshot();
    if (
      snapshot.phase !== "playing" ||
      !this.player ||
      !this.level
    ) {
      this.syncCharacter();
      return;
    }

    this.updateMovingPlatforms(time);
    this.updatePlayer(input, time, Math.min(delta, 34));
    this.applyEnvironment(time, Math.min(delta, 34));
    this.checkFailureAndExit();
    this.syncCharacter();
    this.updateCameraLookAhead();
  }

  private createKeys(): RuntimeKeys | null {
    const keyboard = this.input.keyboard;
    if (!keyboard) return null;

    return {
      leftA: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.A),
      rightD: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.D),
      leftArrow: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.LEFT),
      rightArrow: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.RIGHT),
      jumpSpace: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.SPACE),
      jumpW: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.W),
      jumpUp: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.UP),
      restart: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.R),
      mute: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.M),
      pause: keyboard.addKey(Phaser.Input.Keyboard.KeyCodes.ESC),
    };
  }

  private readInput(): RuntimeInput {
    const keys = this.keys;
    const gamepad = this.sampleGamepad();
    const keyLeft = Boolean(keys?.leftA.isDown || keys?.leftArrow.isDown);
    const keyRight = Boolean(keys?.rightD.isDown || keys?.rightArrow.isDown);
    const keyJumpHeld = Boolean(
      keys?.jumpSpace.isDown || keys?.jumpW.isDown || keys?.jumpUp.isDown,
    );
    const keyboardJumpDown = Boolean(
      keys &&
        (Phaser.Input.Keyboard.JustDown(keys.jumpSpace) ||
          Phaser.Input.Keyboard.JustDown(keys.jumpW) ||
          Phaser.Input.Keyboard.JustDown(keys.jumpUp)),
    );
    const keyboardJumpUp = Boolean(
      keys &&
        (Phaser.Input.Keyboard.JustUp(keys.jumpSpace) ||
          Phaser.Input.Keyboard.JustUp(keys.jumpW) ||
          Phaser.Input.Keyboard.JustUp(keys.jumpUp)),
    );

    const result: RuntimeInput = {
      horizontal: Phaser.Math.Clamp(
        (keyRight ? 1 : 0) -
          (keyLeft ? 1 : 0) +
          (gamepad.right ? 1 : 0) -
          (gamepad.left ? 1 : 0),
        -1,
        1,
      ),
      jumpDown:
        keyboardJumpDown ||
        risingEdge(gamepad, this.previousGamepad, "jump"),
      jumpUp:
        keyboardJumpUp ||
        (!gamepad.jump && this.previousGamepad.jump),
      jumpHeld: keyJumpHeld || gamepad.jump,
      restartDown:
        Boolean(keys && Phaser.Input.Keyboard.JustDown(keys.restart)) ||
        risingEdge(gamepad, this.previousGamepad, "restart"),
      muteDown: Boolean(keys && Phaser.Input.Keyboard.JustDown(keys.mute)),
      pauseDown:
        Boolean(keys && Phaser.Input.Keyboard.JustDown(keys.pause)) ||
        risingEdge(gamepad, this.previousGamepad, "pause"),
    };
    this.previousGamepad = gamepad;
    return result;
  }

  private sampleGamepad(): GamepadInput {
    if (typeof navigator === "undefined" || !navigator.getGamepads) {
      return readGamepad(null);
    }

    const pad = Array.from(navigator.getGamepads()).find(
      (candidate): candidate is Gamepad => candidate !== null,
    );
    return readGamepad(pad);
  }

  private handleSystemInput(input: RuntimeInput, snapshot: Snapshot): void {
    if (
      snapshot.phase === "title" &&
      (input.jumpDown || Math.abs(input.horizontal) > 0.1)
    ) {
      this.audio.unlock();
      this.session.startRun();
      return;
    }

    if (input.muteDown) {
      this.session.toggleMute();
    }

    if (
      input.pauseDown &&
      (snapshot.phase === "playing" || snapshot.phase === "paused")
    ) {
      this.session.togglePause();
    }

    if (input.restartDown) {
      if (snapshot.phase === "dead") {
        this.cancelDeathTimer();
        this.session.restartAfterFailure();
      } else if (snapshot.phase === "victory" || snapshot.phase === "title") {
        this.session.startRun();
      } else if (snapshot.phase === "transition") {
        // The session deliberately accepts failures only in an active round.
        // Advance the already-committed portal, then record the manual reset.
        this.session.beginNextRound();
        this.session.failRun("manual");
      } else {
        this.session.failRun("manual");
      }
    }

    if (snapshot.phase === "dead" && input.jumpDown) {
      this.cancelDeathTimer();
      this.session.restartAfterFailure();
    }
  }

  private updatePlayer(
    input: RuntimeInput,
    time: number,
    delta: number,
  ): void {
    const player = this.player;
    if (!player) return;
    const body = player.body;
    const grounded = body.blocked.down || body.touching.down;

    if (grounded) {
      this.lastGroundedAt = time;
      if (!this.wasGrounded && body.velocity.y >= 0) {
        this.audio.cue("land");
        if (!this.reducedMotion && this.character) {
          this.tweens.add({
            targets: this.character,
            scaleY: 0.82,
            duration: 45,
            yoyo: true,
            ease: "Sine.Out",
          });
        }
      }
    }

    if (time < this.inputLockedUntil) {
      body.setAccelerationX(0);
      body.setDragX(GROUND_DRAG);
      this.wasGrounded = grounded;
      return;
    }

    if (Math.abs(input.horizontal) > 0.05) {
      this.facing = input.horizontal < 0 ? -1 : 1;
      body.setAccelerationX(input.horizontal * WALK_ACCELERATION);
      body.setDragX(0);
    } else {
      body.setAccelerationX(0);
      body.setDragX(grounded ? GROUND_DRAG : AIR_DRAG);
    }
    body.setMaxVelocity(WALK_SPEED, 1_050);

    if (input.jumpDown) {
      this.audio.unlock();
      this.jumpBufferedUntil = time + JUMP_BUFFER_MS;
    }

    const hasCoyote = time - this.lastGroundedAt <= COYOTE_MS;
    if (this.jumpBufferedUntil >= time && (grounded || hasCoyote)) {
      this.jumpBufferedUntil = Number.NEGATIVE_INFINITY;
      if (this.session.acceptJump()) {
        body.setVelocityY(-JUMP_SPEED);
        this.lastGroundedAt = Number.NEGATIVE_INFINITY;
        this.audio.cue("jump");
      }
    }

    if (
      input.jumpUp &&
      !input.jumpHeld &&
      body.velocity.y < -215
    ) {
      body.setVelocityY(body.velocity.y * 0.46);
    }

    this.wasGrounded = grounded;
    void delta;
  }

  private applyEnvironment(time: number, delta: number): void {
    const level = this.level;
    const player = this.player;
    if (!level || !player) return;

    const playerRect = this.playerBounds();
    if (!playerRect) return;
    const body = player.body;
    const dt = delta / 1000;

    for (const conveyor of level.conveyors) {
      const topBand: Rect = {
        x: conveyor.bounds.x,
        y: conveyor.bounds.y - 10,
        width: conveyor.bounds.width,
        height: conveyor.bounds.height + 18,
      };
      const feet = playerRect.y + playerRect.height;
      if (
        intersects(playerRect, topBand) &&
        feet >= conveyor.bounds.y &&
        feet <= conveyor.bounds.y + conveyor.bounds.height + 10
      ) {
        body.velocity.x =
          conveyor.speed >= 0
            ? Math.max(body.velocity.x, conveyor.speed)
            : Math.min(body.velocity.x, conveyor.speed);
      }
    }

    for (const fan of level.fans) {
      if (!intersects(playerRect, fan.bounds)) continue;
      body.velocity.x = Phaser.Math.Clamp(
        body.velocity.x + fan.force.x * dt * 1.6,
        -700,
        700,
      );
      if (fan.force.y < 0) {
        body.velocity.y = Math.min(
          body.velocity.y + fan.force.y * dt * 3.2,
          fan.force.y * 0.62,
        );
      } else {
        body.velocity.y = Math.max(
          body.velocity.y + fan.force.y * dt * 3.2,
          fan.force.y * 0.62,
        );
      }
      body.velocity.y = Phaser.Math.Clamp(body.velocity.y, -850, 950);
      this.audio.fanPuff(time);
    }

    for (const spring of level.springs) {
      const springBand: Rect = {
        x: spring.bounds.x - 5,
        y: spring.bounds.y - 12,
        width: spring.bounds.width + 10,
        height: spring.bounds.height + 16,
      };
      if (
        time >= this.springCooldownUntil &&
        body.velocity.y >= -20 &&
        intersects(playerRect, springBand)
      ) {
        body.setVelocityY(
          spring.impulse > 0 ? -spring.impulse : spring.impulse,
        );
        this.springCooldownUntil = time + 260;
        this.lastGroundedAt = Number.NEGATIVE_INFINITY;
        this.audio.cue("spring");
      }
    }

    for (const zone of level.catchZones) {
      if (
        this.caughtZones.has(zone.id) ||
        body.velocity.y < 60 ||
        !intersects(playerRect, zone.bounds)
      ) {
        continue;
      }
      if (
        (zone.requiredDirection === "right" && this.facing !== 1) ||
        (zone.requiredDirection === "left" && this.facing !== -1)
      ) {
        continue;
      }
      this.caughtZones.add(zone.id);
      this.performFreeMantle(zone.landing, time);
      break;
    }
  }

  private performFreeMantle(
    landing: { x: number; y: number },
    time: number,
  ): void {
    const player = this.player;
    if (!player) return;
    const body = player.body;
    this.inputLockedUntil = time + (this.reducedMotion ? 80 : 260);
    body.stop();
    body.setAllowGravity(false);

    if (this.reducedMotion) {
      player.setPosition(landing.x, landing.y);
      body.setAllowGravity(true);
      body.updateFromGameObject();
      return;
    }

    this.tweens.add({
      targets: player,
      x: landing.x,
      y: landing.y,
      duration: 220,
      ease: "Sine.Out",
      onUpdate: () => body.updateFromGameObject(),
      onComplete: () => {
        body.setAllowGravity(true);
        body.setVelocityY(0);
      },
    });
  }

  private checkFailureAndExit(): void {
    const level = this.level;
    const playerBounds = this.playerBounds();
    if (!level || !playerBounds || this.triggeredExit) return;

    for (const hazard of level.hazards) {
      if (intersects(playerBounds, hazard.bounds)) {
        this.session.failRun(hazard.kind === "void" ? "void" : "hazard");
        return;
      }
    }

    const worldBottom = level.worldBounds.y + level.worldBounds.height;
    if (
      playerBounds.y > worldBottom + 120 ||
      playerBounds.x + playerBounds.width <
        level.worldBounds.x - 180 ||
      playerBounds.x >
        level.worldBounds.x + level.worldBounds.width + 180
    ) {
      this.session.failRun("void");
      return;
    }

    for (const exit of level.exits) {
      if (!intersects(playerBounds, exit.bounds)) continue;
      this.triggeredExit = true;
      this.audio.cue("portal");
      this.session.completeRound();
      return;
    }
  }

  private updateMovingPlatforms(time: number): void {
    for (const platform of this.movingPlatforms) {
      const body = platform.object.body;
      if (time < platform.pauseUntil) {
        body.setVelocity(0, 0);
        continue;
      }

      const destination =
        platform.direction === 1 ? platform.target : platform.start;
      const deltaX = destination.x - platform.object.x;
      const deltaY = destination.y - platform.object.y;
      const distance = Math.hypot(deltaX, deltaY);
      if (distance <= 4) {
        platform.object.setPosition(destination.x, destination.y);
        body.updateFromGameObject();
        platform.visual.setPosition(
          platform.object.x - platform.start.x,
          platform.object.y - platform.start.y,
        );
        body.setVelocity(0, 0);
        platform.direction = platform.direction === 1 ? -1 : 1;
        platform.pauseUntil = time + platform.definition.pauseMs;
        continue;
      }

      const speed = platform.definition.speed;
      body.setVelocity(
        (deltaX / distance) * speed,
        (deltaY / distance) * speed,
      );
      platform.visual.setPosition(
        platform.object.x - platform.start.x,
        platform.object.y - platform.start.y,
      );
    }
  }

  private updateCameraLookAhead(): void {
    const camera = this.cameras.main;
    const desired = -this.facing * 92;
    camera.followOffset.x = Phaser.Math.Linear(
      camera.followOffset.x,
      desired,
      0.045,
    );
  }

  private syncCharacter(): void {
    const player = this.player;
    const character = this.character;
    if (!player || !character) return;
    character.setPosition(player.x, player.y);
    character.scaleX = this.facing;
    character.rotation = Phaser.Math.Clamp(
      player.body.velocity.x / 4_500,
      -0.075,
      0.075,
    );
  }

  private playerBounds(): Rect | null {
    const player = this.player;
    if (!player) return null;
    const body = player.body;
    return {
      x: body.x,
      y: body.y,
      width: body.width,
      height: body.height,
    };
  }

  private syncSession(snapshot: Snapshot, initial = false): void {
    const priorPhase = this.previousPhase;
    this.audio.setMuted(snapshot.muted);

    const mustRebuild =
      snapshot.roundIndex !== this.lastRoundIndex ||
      (!initial &&
        snapshot.phase === "playing" &&
        (priorPhase === "dead" ||
          priorPhase === "title" ||
          priorPhase === "victory" ||
          priorPhase === "transition"));
    if (mustRebuild) {
      this.buildLevel(snapshot.roundIndex);
    }

    if (snapshot.phase === "playing") {
      this.physics.resume();
    } else {
      this.physics.pause();
    }

    if (
      snapshot.phase === "transition" &&
      priorPhase !== "transition"
    ) {
      this.beginPortalTransition();
    } else if (snapshot.phase === "dead" && priorPhase !== "dead") {
      this.beginDeathSequence();
    } else if (
      snapshot.phase === "victory" &&
      priorPhase !== "victory"
    ) {
      this.beginVictorySequence();
    }

    this.previousPhase = snapshot.phase;
  }

  private beginPortalTransition(): void {
    this.transitionTimer?.remove(false);
    if (!this.reducedMotion) {
      this.cameras.main.flash(150, 247, 241, 223);
      this.cameras.main.fadeOut(300, 247, 241, 223);
    }
    this.audio.cue("paper");
    this.transitionTimer = this.time.delayedCall(
      this.reducedMotion ? 140 : 430,
      () => {
        if (this.session.getSnapshot().phase === "transition") {
          this.session.beginNextRound();
          if (!this.reducedMotion) {
            this.cameras.main.fadeIn(220, 247, 241, 223);
          }
        }
      },
    );
  }

  private beginDeathSequence(): void {
    this.transitionTimer?.remove(false);
    this.audio.cue("death");
    if (this.character) {
      if (this.reducedMotion) {
        this.character.setAlpha(0.18);
      } else {
        this.tweens.add({
          targets: this.character,
          alpha: 0,
          scaleX: 1.4,
          scaleY: 0.1,
          angle: 8,
          duration: 420,
          ease: "Sine.In",
        });
      }
    }

    if (!this.reducedMotion) {
      this.cameras.main.shake(150, 0.005);
    }
    this.cancelDeathTimer();
  }

  private beginVictorySequence(): void {
    this.transitionTimer?.remove(false);
    this.cancelDeathTimer();
    this.audio.cue("victory");
    if (!this.reducedMotion) {
      this.cameras.main.flash(280, 247, 241, 223);
      for (const portal of this.portalVisuals) {
        this.tweens.add({
          targets: portal,
          alpha: 1,
          duration: 480,
          yoyo: true,
          repeat: 2,
          ease: "Sine.InOut",
        });
      }
    }
  }

  private cancelDeathTimer(): void {
    this.deathTimer?.remove(false);
    this.deathTimer = null;
  }

  private animateInk(time: number): void {
    if (this.reducedMotion) return;
    const pulse = 0.76 + Math.sin(time * 0.004) * 0.16;
    for (const visual of this.portalVisuals) {
      visual.setAlpha(pulse);
    }
    const airPulse = 0.56 + Math.sin(time * 0.006) * 0.18;
    for (const visual of this.fanVisuals) {
      visual.setAlpha(airPulse);
    }
  }

  private buildLevel(roundIndex: number): void {
    const level = LEVELS[roundIndex] ?? LEVELS[0];
    if (!level) return;

    this.clearLevel();
    this.level = level;
    this.lastRoundIndex = roundIndex;
    this.caughtZones.clear();
    this.triggeredExit = false;
    this.inputLockedUntil = 0;
    this.lastGroundedAt = Number.NEGATIVE_INFINITY;
    this.jumpBufferedUntil = Number.NEGATIVE_INFINITY;
    this.springCooldownUntil = 0;
    this.wasGrounded = false;

    this.physics.world.setBounds(
      level.worldBounds.x,
      level.worldBounds.y,
      level.worldBounds.width,
      level.worldBounds.height,
    );
    this.cameras.main.setBounds(
      level.worldBounds.x,
      level.worldBounds.y,
      level.worldBounds.width,
      level.worldBounds.height,
    );

    this.drawPaper(level.worldBounds);
    const solids = level.solids.map((solid) => {
      const visual = this.drawSolid(solid.bounds);
      const bodyObject = this.makeStaticBody(solid.bounds);
      return { definition: solid, object: bodyObject, visual };
    });

    for (const hazard of level.hazards) {
      this.drawHazard(hazard.bounds, hazard.kind);
    }
    for (const zone of level.catchZones) {
      if (zone.visible) {
        this.drawCatchZone(zone.bounds);
      }
    }
    for (const conveyor of level.conveyors) {
      this.drawConveyor(conveyor.bounds, conveyor.speed);
    }
    for (const fan of level.fans) {
      const visual = this.drawFan(fan.bounds, fan.force);
      this.fanVisuals.push(visual);
    }
    for (const spring of level.springs) {
      this.drawSpring(spring.bounds);
    }
    for (const exit of level.exits) {
      const visual = this.drawPortal(exit.bounds, exit.kind);
      this.portalVisuals.push(visual);
    }

    for (const platform of level.movingPlatforms) {
      const visual = this.drawMovingPlatform(platform.bounds);
      const object = this.makeDynamicPlatform(platform.bounds);
      this.movingPlatforms.push({
        definition: platform,
        object,
        visual,
        start: new Phaser.Math.Vector2(object.x, object.y),
        target: new Phaser.Math.Vector2(
          platform.to.x + platform.bounds.width / 2,
          platform.to.y + platform.bounds.height / 2,
        ),
        direction: 1,
        pauseUntil: 0,
      });
    }

    this.createPlayer(level.spawn);

    // Add colliders only after the player exists so all level geometry is
    // fully rebuilt as one deterministic unit.
    const player = this.player;
    if (!player) return;
    for (const solid of solids) {
      this.colliders.push(
        this.physics.add.collider(
          player,
          solid.object,
          undefined,
          solid.definition.oneWay
            ? () => this.canLandOnOneWay(solid.object)
            : undefined,
          this,
        ),
      );
    }
    for (const platform of this.movingPlatforms) {
      this.colliders.push(this.physics.add.collider(player, platform.object));
    }

    this.cameras.main.startFollow(player, false, 0.095, 0.1);
    this.cameras.main.setDeadzone(180, 100);
    this.cameras.main.setFollowOffset(-90, 28);
    this.cameras.main.centerOn(level.spawn.x, level.spawn.y);
    this.syncCharacter();
  }

  private createPlayer(spawn: { x: number; y: number }): void {
    const oldPlaceholder = this.player;
    if (oldPlaceholder) oldPlaceholder.destroy();

    const player = this.add
      .rectangle(spawn.x, spawn.y, PLAYER_WIDTH, PLAYER_HEIGHT, 0xffffff, 0)
      .setDepth(30);
    this.physics.add.existing(player);
    const physicsPlayer = player as PhysicsRectangle;
    const playerBody = physicsPlayer.body as Phaser.Physics.Arcade.Body;
    playerBody
      .setSize(PLAYER_WIDTH, PLAYER_HEIGHT)
      .setOffset(0, 0)
      .setAllowGravity(true)
      .setGravityY(GRAVITY)
      .setMaxVelocity(WALK_SPEED, 1_050)
      .setCollideWorldBounds(false);
    this.player = physicsPlayer;
    this.track(player);

    const character = this.add.graphics().setDepth(31);
    character.lineStyle(5, COLORS.ink, 1);
    character.strokeCircle(0, -17, 14);
    character.lineStyle(4, COLORS.inkDark, 0.86);
    character.strokeCircle(0, 12, 13);
    character.lineStyle(3, COLORS.charcoal, 0.9);
    character.strokeCircle(-5, -19, 1.25);
    character.strokeCircle(5, -19, 1.25);
    character.beginPath();
    character.arc(0, -15, 6, 0.3, Math.PI - 0.3, false);
    character.strokePath();
    character.lineStyle(4, COLORS.ink, 1);
    character.beginPath();
    character.moveTo(-7, 23);
    character.lineTo(-11, 28);
    character.lineTo(-3, 27);
    character.moveTo(7, 23);
    character.lineTo(11, 28);
    character.lineTo(3, 27);
    character.strokePath();
    this.character = character;
    this.track(character);
  }

  private makeStaticBody(bounds: Rect): StaticPhysicsRectangle {
    const object = this.add
      .rectangle(
        bounds.x + bounds.width / 2,
        bounds.y + bounds.height / 2,
        bounds.width,
        bounds.height,
        0xffffff,
        0,
      )
      .setVisible(false);
    this.physics.add.existing(object, true);
    this.track(object);
    return object as StaticPhysicsRectangle;
  }

  private makeDynamicPlatform(bounds: Rect): PhysicsRectangle {
    const object = this.add
      .rectangle(
        bounds.x + bounds.width / 2,
        bounds.y + bounds.height / 2,
        bounds.width,
        bounds.height,
        0xffffff,
        0,
      )
      .setVisible(false);
    this.physics.add.existing(object);
    const result = object as PhysicsRectangle;
    const body = result.body as Phaser.Physics.Arcade.Body;
    body
      .setAllowGravity(false)
      .setImmovable(true)
      .setSize(bounds.width, bounds.height);
    this.track(object);
    return result;
  }

  private canLandOnOneWay(platform: StaticPhysicsRectangle): boolean {
    const player = this.player;
    if (!player) return false;
    const body = player.body;
    return (
      body.velocity.y >= 0 &&
      body.y + body.height <= platform.body.y + 16
    );
  }

  private drawPaper(bounds: Rect): void {
    const graphics = this.add.graphics().setDepth(-30);
    graphics.fillStyle(COLORS.paper, 1);
    graphics.fillRect(bounds.x, bounds.y, bounds.width, bounds.height);
    graphics.lineStyle(1, COLORS.grid, 0.19);

    const grid = 40;
    for (
      let x = Math.floor(bounds.x / grid) * grid;
      x <= bounds.x + bounds.width;
      x += grid
    ) {
      graphics.lineBetween(x, bounds.y, x, bounds.y + bounds.height);
    }
    for (
      let y = Math.floor(bounds.y / grid) * grid;
      y <= bounds.y + bounds.height;
      y += grid
    ) {
      graphics.lineBetween(bounds.x, y, bounds.x + bounds.width, y);
    }
    graphics.lineStyle(2, COLORS.grid, 0.11);
    for (
      let x = Math.floor(bounds.x / 200) * 200;
      x <= bounds.x + bounds.width;
      x += 200
    ) {
      graphics.lineBetween(x, bounds.y, x, bounds.y + bounds.height);
    }
    for (
      let y = Math.floor(bounds.y / 200) * 200;
      y <= bounds.y + bounds.height;
      y += 200
    ) {
      graphics.lineBetween(bounds.x, y, bounds.x + bounds.width, y);
    }
    this.track(graphics);
  }

  private drawSolid(bounds: Rect): Phaser.GameObjects.Graphics {
    const graphics = this.add.graphics().setDepth(2);
    graphics.fillStyle(COLORS.paperShadow, 0.35);
    graphics.fillRect(bounds.x, bounds.y, bounds.width, bounds.height);
    this.markerRect(graphics, bounds, COLORS.ink, 4);
    graphics.lineStyle(2, COLORS.inkDark, 0.45);
    graphics.lineBetween(
      bounds.x + 3,
      bounds.y + 4,
      bounds.x + bounds.width - 4,
      bounds.y + 2,
    );
    this.track(graphics);
    return graphics;
  }

  private drawHazard(
    bounds: Rect,
    kind: "spikes" | "void",
  ): Phaser.GameObjects.Graphics {
    const graphics = this.add.graphics().setDepth(8);
    graphics.lineStyle(4, COLORS.danger, 1);
    if (kind === "void") {
      graphics.fillStyle(COLORS.danger, 0.08);
      graphics.fillRect(bounds.x, bounds.y, bounds.width, bounds.height);
      for (let x = bounds.x; x < bounds.x + bounds.width; x += 18) {
        graphics.lineBetween(
          x,
          bounds.y + 4,
          x + 12,
          bounds.y + bounds.height - 4,
        );
      }
    } else {
      const count = Math.max(1, Math.floor(bounds.width / 20));
      const spikeWidth = bounds.width / count;
      graphics.beginPath();
      graphics.moveTo(bounds.x, bounds.y + bounds.height);
      for (let i = 0; i < count; i += 1) {
        graphics.lineTo(
          bounds.x + spikeWidth * (i + 0.5),
          bounds.y,
        );
        graphics.lineTo(
          bounds.x + spikeWidth * (i + 1),
          bounds.y + bounds.height,
        );
      }
      graphics.strokePath();
    }
    this.track(graphics);
    return graphics;
  }

  private drawCatchZone(bounds: Rect): Phaser.GameObjects.Graphics {
    const graphics = this.add.graphics().setDepth(6).setAlpha(0.72);
    graphics.fillStyle(COLORS.teal, 0.1);
    graphics.fillRoundedRect(
      bounds.x,
      bounds.y,
      bounds.width,
      bounds.height,
      10,
    );
    graphics.lineStyle(3, COLORS.teal, 0.55);
    const edgeX = bounds.x + bounds.width - 4;
    graphics.beginPath();
    graphics.moveTo(edgeX - 14, bounds.y + 8);
    graphics.lineTo(edgeX, bounds.y + 16);
    graphics.lineTo(edgeX - 8, bounds.y + 28);
    graphics.strokePath();
    for (let i = 0; i < 5; i += 1) {
      graphics.fillStyle(COLORS.teal, 0.35 - i * 0.04);
      graphics.fillCircle(
        bounds.x + 8 + i * 12,
        bounds.y + bounds.height - 8 - (i % 2) * 5,
        2 + (i % 2),
      );
    }
    this.track(graphics);
    return graphics;
  }

  private drawConveyor(
    bounds: Rect,
    speed: number,
  ): Phaser.GameObjects.Graphics {
    const graphics = this.add.graphics().setDepth(7).setAlpha(0.82);
    graphics.fillStyle(COLORS.teal, 0.12);
    graphics.fillRoundedRect(
      bounds.x,
      bounds.y,
      bounds.width,
      bounds.height,
      8,
    );
    graphics.lineStyle(3, COLORS.teal, 0.9);
    graphics.strokeRoundedRect(
      bounds.x,
      bounds.y,
      bounds.width,
      bounds.height,
      8,
    );
    const direction = speed >= 0 ? 1 : -1;
    const centerY = bounds.y + bounds.height / 2;
    for (let x = bounds.x + 18; x < bounds.x + bounds.width - 8; x += 34) {
      const anchor = direction > 0 ? x : bounds.x + bounds.width - (x - bounds.x);
      graphics.beginPath();
      graphics.moveTo(anchor - direction * 8, centerY - 7);
      graphics.lineTo(anchor, centerY);
      graphics.lineTo(anchor - direction * 8, centerY + 7);
      graphics.strokePath();
    }
    this.track(graphics);
    return graphics;
  }

  private drawFan(
    bounds: Rect,
    force: { x: number; y: number },
  ): Phaser.GameObjects.Graphics {
    const graphics = this.add.graphics().setDepth(5).setAlpha(0.7);
    graphics.fillStyle(COLORS.tealLight, 0.08);
    graphics.fillRect(bounds.x, bounds.y, bounds.width, bounds.height);
    graphics.lineStyle(3, COLORS.teal, 0.75);
    const vertical = Math.abs(force.y) >= Math.abs(force.x);
    if (vertical) {
      const direction = force.y <= 0 ? -1 : 1;
      for (let x = bounds.x + 18; x < bounds.x + bounds.width; x += 30) {
        for (let y = bounds.y + 24; y < bounds.y + bounds.height; y += 58) {
          graphics.beginPath();
          graphics.moveTo(x, y + direction * 12);
          graphics.lineTo(x, y - direction * 12);
          graphics.lineTo(x - 5, y - direction * 5);
          graphics.moveTo(x, y - direction * 12);
          graphics.lineTo(x + 5, y - direction * 5);
          graphics.strokePath();
        }
      }
    } else {
      const direction = force.x >= 0 ? 1 : -1;
      for (let y = bounds.y + 18; y < bounds.y + bounds.height; y += 28) {
        for (let x = bounds.x + 20; x < bounds.x + bounds.width; x += 58) {
          graphics.beginPath();
          graphics.moveTo(x - direction * 12, y);
          graphics.lineTo(x + direction * 12, y);
          graphics.lineTo(x + direction * 5, y - 5);
          graphics.moveTo(x + direction * 12, y);
          graphics.lineTo(x + direction * 5, y + 5);
          graphics.strokePath();
        }
      }
    }
    graphics.fillStyle(COLORS.teal, 0.9);
    graphics.fillCircle(
      bounds.x + bounds.width / 2,
      bounds.y + bounds.height - 8,
      7,
    );
    this.track(graphics);
    return graphics;
  }

  private drawSpring(bounds: Rect): Phaser.GameObjects.Graphics {
    const graphics = this.add.graphics().setDepth(8).setAlpha(0.86);
    graphics.lineStyle(4, COLORS.teal, 1);
    graphics.lineBetween(
      bounds.x,
      bounds.y + 2,
      bounds.x + bounds.width,
      bounds.y + 2,
    );
    graphics.beginPath();
    graphics.moveTo(bounds.x + 3, bounds.y + bounds.height);
    const points = 6;
    for (let i = 1; i <= points; i += 1) {
      graphics.lineTo(
        bounds.x + (bounds.width - 6) * (i / points) + 3,
        i % 2 === 0 ? bounds.y + bounds.height : bounds.y + 4,
      );
    }
    graphics.strokePath();
    this.track(graphics);
    return graphics;
  }

  private drawPortal(
    bounds: Rect,
    kind: "upper" | "hidden" | "goal",
  ): Phaser.GameObjects.Graphics {
    const graphics = this.add.graphics().setDepth(12).setAlpha(0.82);
    const color = kind === "goal" ? COLORS.teal : COLORS.portal;
    graphics.fillStyle(color, 0.12);
    graphics.fillEllipse(
      bounds.x + bounds.width / 2,
      bounds.y + bounds.height / 2,
      bounds.width,
      bounds.height,
    );
    graphics.lineStyle(6, color, 0.92);
    graphics.strokeEllipse(
      bounds.x + bounds.width / 2,
      bounds.y + bounds.height / 2,
      bounds.width,
      bounds.height,
    );
    graphics.lineStyle(2, COLORS.ink, 0.58);
    graphics.strokeEllipse(
      bounds.x + bounds.width / 2 + 3,
      bounds.y + bounds.height / 2 - 2,
      bounds.width - 13,
      bounds.height - 10,
    );
    graphics.fillStyle(COLORS.paper, 1);
    graphics.fillRect(
      bounds.x - 8,
      bounds.y + bounds.height * 0.78,
      bounds.width + 16,
      bounds.height * 0.25,
    );
    graphics.lineStyle(4, color, 0.85);
    graphics.lineBetween(
      bounds.x + 3,
      bounds.y + bounds.height * 0.79,
      bounds.x + bounds.width - 3,
      bounds.y + bounds.height * 0.79,
    );
    this.track(graphics);
    return graphics;
  }

  private drawMovingPlatform(bounds: Rect): Phaser.GameObjects.Graphics {
    const graphics = this.add.graphics().setDepth(7).setAlpha(0.8);
    graphics.fillStyle(COLORS.teal, 0.12);
    graphics.fillRoundedRect(
      bounds.x,
      bounds.y,
      bounds.width,
      bounds.height,
      8,
    );
    graphics.lineStyle(4, COLORS.teal, 0.92);
    graphics.strokeRoundedRect(
      bounds.x,
      bounds.y,
      bounds.width,
      bounds.height,
      8,
    );
    graphics.lineStyle(2, COLORS.teal, 0.45);
    graphics.lineBetween(
      bounds.x + 12,
      bounds.y + bounds.height / 2,
      bounds.x + bounds.width - 12,
      bounds.y + bounds.height / 2,
    );
    this.track(graphics);
    return graphics;
  }

  private markerRect(
    graphics: Phaser.GameObjects.Graphics,
    bounds: Rect,
    color: number,
    width: number,
  ): void {
    graphics.lineStyle(width, color, 0.95);
    graphics.strokeRoundedRect(
      bounds.x,
      bounds.y,
      bounds.width,
      bounds.height,
      Math.min(5, bounds.height / 3),
    );
    graphics.lineStyle(Math.max(1, width - 2), color, 0.28);
    graphics.strokeRoundedRect(
      bounds.x + 2,
      bounds.y - 1,
      bounds.width - 3,
      bounds.height + 2,
      Math.min(5, bounds.height / 3),
    );
  }

  private track<T extends Phaser.GameObjects.GameObject>(object: T): T {
    this.levelObjects.push(object);
    return object;
  }

  private destroyColliders(): void {
    for (const collider of this.colliders) collider.destroy();
    this.colliders = [];
  }

  private clearLevel(): void {
    this.transitionTimer?.remove(false);
    this.transitionTimer = null;
    this.destroyColliders();
    for (const object of this.levelObjects) {
      this.tweens.killTweensOf(object);
      if (object.active) object.destroy();
    }
    this.levelObjects = [];
    this.movingPlatforms = [];
    this.portalVisuals = [];
    this.fanVisuals = [];
    this.player = null;
    this.character = null;
    this.cameras?.main?.stopFollow();
  }

  private cleanUp(): void {
    this.unsubscribe?.();
    this.unsubscribe = null;
    this.transitionTimer?.remove(false);
    this.transitionTimer = null;
    this.cancelDeathTimer();
    this.clearLevel();
  }
}

/**
 * Mounts the complete Phaser runtime into an empty browser container.
 *
 * The React layer owns the accessible HUD and the session store; Phaser owns
 * only canvas rendering, physics, and input. Keeping the store outside the
 * Scene also makes Strict Mode remounts and deterministic session tests safe.
 */
export function mountGravityGame(
  container: HTMLElement,
  session: GameSessionStore,
): GravityGameHandle {
  if (typeof window === "undefined") {
    throw new Error("Gravity Is Free can only be mounted in a browser.");
  }

  const audio = new GravityAudio();
  const scene = new GravityScene(session, audio);
  let destroyed = false;
  let pointerListener: (() => void) | null = null;

  const game = new Phaser.Game({
    type: Phaser.CANVAS,
    parent: container,
    width: VIEWPORT_WIDTH,
    height: VIEWPORT_HEIGHT,
    backgroundColor: COLORS.paper,
    antialias: true,
    roundPixels: false,
    scene: [scene],
    scale: {
      mode: Phaser.Scale.FIT,
      autoCenter: Phaser.Scale.CENTER_BOTH,
      width: VIEWPORT_WIDTH,
      height: VIEWPORT_HEIGHT,
    },
    fps: {
      target: 60,
      min: 30,
      forceSetTimeOut: false,
    },
    physics: {
      default: "arcade",
      arcade: {
        gravity: { x: 0, y: 0 },
        debug: false,
        fixedStep: true,
        fps: 60,
      },
    },
    callbacks: {
      postBoot: (bootedGame) => {
        const canvas = bootedGame.canvas;
        canvas.tabIndex = 0;
        canvas.setAttribute("role", "img");
        canvas.setAttribute(
          "aria-label",
          "Gravity Is Free platform puzzle. Use A and D or the arrow keys to move, and Space, W, or Up to jump.",
        );
        pointerListener = () => {
          canvas.focus({ preventScroll: true });
          audio.unlock();
        };
        canvas.addEventListener("pointerdown", pointerListener);
      },
    },
  });

  return {
    focus: () => {
      if (!destroyed) game.canvas?.focus({ preventScroll: true });
    },
    destroy: () => {
      if (destroyed) return;
      destroyed = true;
      if (pointerListener && game.canvas) {
        game.canvas.removeEventListener("pointerdown", pointerListener);
      }
      game.destroy(true);
      audio.destroy();
      container.replaceChildren();
    },
  };
}

function intersects(a: Rect, b: Rect): boolean {
  return (
    a.x < b.x + b.width &&
    a.x + a.width > b.x &&
    a.y < b.y + b.height &&
    a.y + a.height > b.y
  );
}
