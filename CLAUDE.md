# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Godot 4.7 / .NET 8 RTS-style tactical game ("tacticals"). C# only — no GDScript. Main scene is `Scenes/Core/Main.tscn`.

The `.csproj` has a `ProjectReference` to `../tacticals-api-server/tactical-api-common`, a **sibling repo outside this working directory**. It must be checked out next to this one or the build fails. Shared domain types (e.g. `tacticals_api_server.Domain.Battle`) come from there.

## Commands

```bash
# Build (Godot.NET.Sdk pulls in the engine SDK; plain dotnet build works)
dotnet build tacticals.csproj

# Run the client
/Applications/Godot_mono\ 4.app/Contents/MacOS/Godot --path .

# Run a dedicated server (headless auto-starts the ENet server; see Main._Ready)
/Applications/Godot_mono\ 4.app/Contents/MacOS/Godot --path . --headless -- --port=20000
```

There is no test suite, linter, or CI. Verification is by running the game.

## Architecture

### Node ownership and singletons

`Main` (`Code/Core/Main.cs`) is the root and holds static `Current`. Several managers follow the same idiom: a `Node` in `Main.tscn` that assigns `static Current` in `_Ready()` — `BattleServer`, `BattleNetwork`, `AudioManager` (as `Main.Current.Audio`), `ProjectileManager` (as `Main.Current.Projectiles`), `GameDebug`, `PlayerInput`. Reach them through those statics, not by node path.

`Main.NavigateTo(NAVIGATE_TARGET, NavigateContext)` shows/hides the three menu nodes (StartMenu / LobbyMenu / BattleMenu) — they are all instanced siblings, never swapped scenes. `Main.StartGame(scenePath)` swaps the level under the `Map` node and calls `IGameMap.GenerateLevel()`.

### Maps

Any level implements `IGameMap` (`Plains` for procedural multiplayer, `CampainDemo` for the hand-built demo). Two generation paths in `MapGenerator`:

- `GenerateMap()` — procedural: forest heatmap → bases → structures → trees. Used by `Plains`.
- `MapExistingSurface()` — derives the grid by binning `MeshInstance3D` vertices of an authored terrain into cells. Used by `CampainDemo`.

Both produce `MapBlock[][]` and initialize the `FlowFieldManager` for pathing. `MapConstants.BLOCK_SIZE` (15) converts world XZ ↔ grid indices; `BIOMEHEATMAPSCALE` (2) is the sub-cell resolution of the biome/height data stored in each block's `BiomeInfo`.

The generated map is serialized to JSON (`MapGenerator.ToJson`) and pushed to peers so every client builds the identical level — see `BattleNetwork.OnBattleStart`.

### Entities

```
CharacterBody3D → TeamEntity → MovableTeamEntity → Soldier / Tank / Heli
```

`TeamEntity` owns membership (`TeamMembership`), passengers (`IPassengers`), damage, and terrain raycasting. `MovableTeamEntity` adds movement, flocking separation, and patrol checkpoints.

**State machine idiom** (follow it when adding entity behaviour): each entity holds a current `TeamEntityStates` plus a *queue* of pending states. `_Process` dispatches with a chain of `IsInState(...)` checks to `HandleXxx(delta)` methods, falling through to `HandleIdle`. A handler that finishes calls `TransitionToNextState()`, which dequeues via `GetNextState()` (returning IDLE when empty). Commands enqueue with `EnqueueState(state, arg, cleanFirst)` — the `arg` is the state's payload, e.g. a `Vector2` destination for `ONTHEWAY`.

Entities register themselves into `EntityGroup` string groups (`ground-units`, `soldiers`, `enemies`, …) in `_Ready`; selection, separation, and enemy lookup all query those groups via `GetTree().GetNodesInGroup`.

### Player and input

`PlayerInput` is a `MultiplayerSynchronizer` whose authority is set to the owning peer; it holds camera state and command flags as plain fields that get network-synced. `Player._Process` reads those flags and runs the command handlers (`HandleSelectCommand`, `HandleMoveToCommand`, `HandlePatrolCommand`, `HandleBoardCommand`, `HandleExitCommand`), which translate to `TeamEntity.Command(...)` / `EnqueueState(...)`. Input action names live in `project.godot` (`select_entity`, `move_army`, `board_entity`, …).

### Projectiles

Shooting does not spawn nodes. Entities call `Main.Current.Projectiles.Spawn(new Projectile{...})`; `ProjectileManager` steps all projectiles in a struct array in `_PhysicsProcess` and raycasts, calling `TeamEntity.TakeHit(damage, shooter, hitPos)` on impact. Add per-projectile behaviour there, not in the shooter.

### Networking

Two distinct layers, easy to confuse:

- **`BattleServer`** — HTTP client against the REST API at `ServerApiUrl` (hardcoded IP in `Code/Game/BattleServer.cs`) for lobby/battle registration, join, ready state, auth token. Async, wrapped in blocking `Task.Run(...).Result` at call sites.
- **`BattleNetwork`** — Godot ENet RPCs between peers (`[Rpc(MultiplayerApi.RpcMode.AnyPeer)]`). It validates the caller against `BattleServer` state before acting (see the `Owner != callerId` check in `OnBattleStart`), then fans out to peers with `RpcId`.

### Debug rendering

`GameDebug.Current` collects per-frame draw requests (`RegisterFov`, `RegisterPatrolPath`) into lists that are drawn as an `ImmediateMesh` and cleared every `_Process`. Register from the entity each frame; toggled by `PlayerInput.Current.DebugToggle`.

## Conventions

- Tabs in most `Code/Game` and `Code/UI` files, 4 spaces in `Code/Maps` — match the surrounding file.
- `.cs.uid` files next to scripts are Godot resource IDs; they are tracked and must not be deleted or regenerated by hand.
- Scene `.tscn` files reference scripts by `uid://`, so renaming/moving a `.cs` file requires updating the scene through the editor rather than by path edit.
