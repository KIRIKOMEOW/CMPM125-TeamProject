# Night Shift — Game Design Document

Nov 3, 2026 · Futaba

> Source of truth for gameplay numbers. Exported from the shared design doc. Diagrams are described in text.

## Overview

**Night Shift** is a 2D top-down wave-survival shooter: you are Kestrel, a night-shift guard who fights with returning throwing blades and must hold a warehouse against 10 waves of intruders until dawn. Between waves you spend coins in a shop to upgrade your gear.

| Item | Detail |
| --- | --- |
| Course | CMPM 125, Module 1: Gameplay Project (200 pts) |
| Team | Solo developer |
| Engine | Unity 6 (6000.6.4f1), 2D mode |
| Platform | Windows build (PC, keyboard + mouse) |
| Session length | About 8 to 12 minutes for a full run |
| Art direction | Pixel art (32×32 characters, 16×16 tiles) with a limited dark, cold palette; a bright flashlight cone and warm blade trails carry the mood |
| Single scene | All gameplay, menus and the shop live in one scene, as the assignment requires |

### Art style: pixel art

- **Sprites:** Kestrel and enemies are 32×32 px with a 4-frame idle and a 6-frame run; tiles and props are 16×16; blades are 8×8 with a 2-frame spin.
- **Palette:** about 16 colours. Cold blues and greys for the warehouse; warm orange only for blades, coins and hits.
- **Unity setup:** Pixels Per Unit 32, Filter Mode Point, Compression None. Pixel Perfect Camera (2D Pixel Perfect package) at a 640×360 reference resolution, scaled up to 1920×1080.
- **Effects stay crisp:** hit flash uses a white-flash material, particles use square pixel textures, and camera shake moves in whole-pixel steps.
- **Sources:** original sprites drawn in Aseprite or Piskel, or CC0 pixel packs credited in the build.

## Core loop and controls

A run is 10 waves: fight a wave, spend coins in the shop, then fight a harder wave. Dying sends you to Game Over; clearing wave 10 is dawn and a win.

Flow: Main Menu → Fight wave N → (wave cleared) → Wave 10 cleared? → yes: Victory / no: Shop → next wave → Fight wave N+1. During a wave, HP reaching 0 → Game Over → retry → Main Menu.

Esc pauses the game at any point during a wave. The shop and all menus pause time with `Time.timeScale = 0`.

| Action | Input |
| --- | --- |
| Move | WASD |
| Aim | Mouse |
| Throw blade | Left mouse button |
| Fan throw (after unlock) | Right mouse button |
| Blade Recall | E or Space |
| Push crates | Walk into them |
| Pause | Esc |

## Rubric coverage

| Requirement | Pts | How Night Shift meets it | Shown in video |
| --- | --- | --- | --- |
| Physics interactions | 30 | Rigidbody2D movement for player and enemies; thrown blades stick in walls and apply knockback with `AddForce`; pushable crates; explosive barrels push everything in range with `Physics2D.OverlapCircleAll` | Throw a blade at a barrel next to a group of enemies and crates |
| Multiple menus | 30 | Main Menu, Options (volume slider), Pause (Esc), Between-wave Shop, Game Over, Victory | Walk through every menu in one run |
| In-game HUD | 30 | Read-only HUD: health bar, blades in hand, recall cooldown, wave number, coins, enemies left, wave timer | HUD updating as you take damage and spend coins |
| Content generated from the player | 30 | Spawner picks only spawn points at least 8 units from the player; enemy count and types scale with the wave number; drop table favours health when player HP is low and gives 50% more coins after a no-damage wave | Debug overlay of valid spawn points; low-HP drop rate |
| Pathfinding, player detection or AI decisions | 30 | Player detection: the Ranged Guard Bot uses raycasts for line of sight and a state machine (Patrol, Chase, Attack, Search) | Gizmo lines for the raycast and the current state label |
| Coding paradigms from class | 30 | Singletons (`GameManager`, `AudioManager`); object pooling for blades, enemies and pickups; coroutines for wave timing, Blade Recall, i-frames and hit flash; C# events to update the HUD | Hierarchy and Project window, plus the scripts |
| Game feel | 20 | Screen shake, hit stop, hit flash, knockback, i-frames, blade trails; difficulty curve tuned per wave; shop prices balanced to upgrade income | Side-by-side of a hit with and without feel effects |

## Game systems

### Player

- 100 HP, move speed 6, Rigidbody2D with zero gravity and high linear damping for tight stops.
- Throwing blades: 5 in hand, 25 damage each, knockback force 4. A thrown blade flies 10 units, then lies on the floor or sticks in a wall until recalled.
- 0.6 s of invincibility after each hit, with a flashing sprite.

### Signature move: Blade Recall

- Press E or Space to pull every blade on the floor back to Kestrel. Cooldown 4 s.
- Returning blades fly straight back and deal 25 damage to every enemy they pass through.
- An enemy hit by 3 or more returning blades is rooted for 1 s.
- The move rewards planning: throw blades past a group, then recall them through it.

### Enemies

| Enemy | HP | Behaviour | First wave | Coins |
| --- | --- | --- | --- | --- |
| Rusher | 50 | Moves straight at the player and deals 10 contact damage | 1 | 3 |
| Guard Bot (ranged) | 75 | State machine: Patrol between waypoints, Chase when a raycast sees the player, Attack inside 6 units, Search the last seen spot for 3 s, then Patrol | 3 | 4 |
| Brute | 200 | Slow, takes half knockback, charges after a 0.8 s wind-up | 6 | 10 |

### Wave spawner

- Enemy budget per wave = 4 + 3 × wave number. Rushers cost 1, Guard Bots 2, Brutes 4.
- Enemies spawn in bursts every 1.5 s from spawn points at least 8 units from the player.
- After the last enemy dies: 3 s pause, then the shop opens. Wave 10 cleared = dawn = Victory.

### Drops (adaptive)

- Every enemy drops coins. Base chance of an extra pickup is 15%.
- Player HP under 40%: health packs get 3× weight. Wave cleared without taking damage: the next wave drops 50% more coins.

### Shop

| Upgrade | Effect per level | Max level | Price per level (coins) |
| --- | --- | --- | --- |
| Throw rate | +15% | 3 | 20 / 35 / 55 |
| Damage | +10 per blade | 3 | 25 / 40 / 60 |
| Max HP | +25 and full heal | 3 | 20 / 35 / 55 |
| Move speed | +10% | 2 | 15 / 30 |
| Fan throw unlock | throws 3 blades in a fan, right mouse button | 1 | 80 |
| Heal | Restore 50 HP | Unlimited | 15 |

### Physics props

- Crates: dynamic Rigidbody2D, can be pushed by the player and blocked by enemies; useful as cover from Guard Bots.
- Explosive barrels: explode on 2 hits, 60 damage and force 12 in a 3-unit radius, respawn the next wave.

## Game feel and balance

### Feedback on every hit

| Event | Feedback |
| --- | --- |
| Player throws a blade | Blade trail, small camera kick, throw sound with random pitch |
| Blade hits enemy (thrown or recalled) | White hit flash 0.08 s, knockback, hit sound, small particle burst |
| Enemy dies | Hit stop 0.05 s, camera shake, coin burst that is pulled toward the player |
| Player is hit | Red screen edge, stronger camera shake, 0.6 s invincibility |
| Barrel explodes | Large shake, hit stop 0.1 s, explosion force on nearby bodies |

### Controls feel

- Movement accelerates to full speed in about 0.1 s and stops almost instantly, so dodging feels precise.
- Aim follows the mouse every frame.
- The arena is about 1.5 screens wide with crates and pillars that break line of sight, so ranged enemies cannot shoot from off-screen.

### Difficulty curve

- Waves 1 to 2 teach throwing and recalling blades with Rushers only.
- Wave 3 adds Guard Bots and introduces cover. Wave 6 adds Brutes.
- Each wave also gives enemies +5% speed, capped at +40% by wave 9.
- Target income is about 40 coins per wave, so the player can buy one upgrade per wave and must choose between power and healing.
- Playtest goal: an average player reaches wave 6 on a first run and can win within 2 to 3 runs.

## Technical architecture

Three layers: singleton managers (GameManager, AudioManager, PoolManager) ← gameplay (PlayerController, WaveSpawner, Enemy AI, drops and props) call `Manager.Instance`; gameplay and managers fire C# events → UI (HUDController, MenuController, ShopController) only listens.

| Script | Paradigm shown |
| --- | --- |
| `GameManager` | Singleton; owns game state (Menu, Playing, Shop, Paused, GameOver, Victory) and fires events |
| `AudioManager` | Singleton with `DontDestroyOnLoad`; plays pooled one-shot sounds |
| `PoolManager` | Object pooling with a queue per prefab for blades, enemies, pickups |
| `WaveSpawner` | Coroutine that spawns bursts with `WaitForSeconds`; spawn distance check |
| `PlayerController` / `Health` | Component references (`GetComponent`, `[SerializeField]`); coroutines for Blade Recall, i-frames and hit flash |
| `GuardBotAI` | Enum state machine; `Physics2D.Raycast` for line of sight |
| `ExplosiveBarrel` | `Physics2D.OverlapCircleAll` + `AddForce` |
| `CameraShake` / `HitStop` | Coroutines using unscaled time |

## Scope, schedule and submission

Due Wed Nov 4, 2026 at 2:00 pm.

### Cut list (in order, if time runs short)

1. Brute enemy
2. Fan throw unlock
3. Separate Options menu (move the volume slider into the Pause menu)
4. Wave timer on the HUD

Never cut: the Shop, the Guard Bot, and Blade Recall.
