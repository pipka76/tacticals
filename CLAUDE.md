# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Godot 4.7 / .NET 8 RTS-style tactical game ("tacticals"). C# only — no GDScript. Main scene is `Scenes/Core/Main.tscn`.

The `.csproj` has a `ProjectReference` to `../tacticals-api-server/tactical-api-common`, a **sibling repo outside this working directory**. It must be checked out next to this one or the build fails. Shared domain types (e.g. `tacticals_api_server.Domain.Battle`) come from there.

## Commands

```bash
# Build (Godot.NET.Sdk pulls in the engine SDK; plain dotnet build works)
dotnet build tacticals.csproj

# Run the client (--api= overrides BattleServer's hardcoded API address, e.g. for a local test API)
/Applications/Godot_mono\ 4.app/Contents/MacOS/Godot --path . -- --api=http://127.0.0.1:6000/

# Run a dedicated server (headless auto-starts the ENet server; see Main._Ready)
/Applications/Godot_mono\ 4.app/Contents/MacOS/Godot --path . --headless -- --port=20000 --join-timeout=600
```

There is no test suite, linter, or CI. Verification is by running the game.

## Architecture

### Node ownership and singletons

`Main` (`Code/Core/Main.cs`) is the root and holds static `Current`. Several managers follow the same idiom: a `Node` in `Main.tscn` that assigns `static Current` in `_Ready()` — `BattleServer`, `BattleNetwork`, `AudioManager` (as `Main.Current.Audio`), `ProjectileManager` (as `Main.Current.Projectiles`), `GameDebug`, `PlayerInput`. Reach them through those statics, not by node path.

`Main.NavigateTo(NAVIGATE_TARGET, NavigateContext)` shows one of the menu nodes under `CanvasLayer` and hides the rest (`HideMenus()` hides them all) — they are all instanced siblings, never swapped scenes. State that has to survive from menu to menu (battle id, owner flag, army layout) lives in `Main.Current.Session` (`ClientSession`), not in `NavigateContext`. `Main.StartGame(scenePath, map = null)` swaps the level under the `Map` node and calls `IGameMap.LoadLevel(map)` when given a map, `GenerateLevel()` otherwise.

Battle flow through the menus: **StartMenu** (army grid; Battle requires login) → **LobbyMenu** (list/create/join on the API) → **BattleMenu** (polls `battle/get` every 1s; ready toggles; owner's Start once all are ready) → **PrepareForBattleMenu** (owner calls `battle/start`, others `battle/endpoint`; connects to the game server, shows the minimap of the map it sends, Deploy sends the army) → level built on `BattleNetwork.BeginBattle`. Any failure goes back to the lobby with a message. The army grid is the reusable `ArmyGrid` scene (StartMenu and PrepareForBattle); its layout moves between them as a string (`GetLayout`/`SetLayout`).

### Maps

Any level implements `IGameMap` (`Plains` for procedural multiplayer, `CampainDemo` for the hand-built demo). Two generation paths in `MapGenerator`:

- `GenerateMap()` — procedural: forest heatmap → bases → structures → trees. Used by `Plains`.
- `MapExistingSurface()` — derives the grid by binning `MeshInstance3D` vertices of an authored terrain into cells. Used by `CampainDemo`.

Both produce `MapBlock[][]` and initialize the `FlowFieldManager` for pathing. `MapConstants.BLOCK_SIZE` (15) converts world XZ ↔ grid indices; `BIOMEHEATMAPSCALE` (2) is the sub-cell resolution of the biome/height data stored in each block's `BiomeInfo`.

In multiplayer the game server generates the map once, packs it (`MapGenerator.Pack`: gzipped JSON with `IncludeFields`, since `MapBlock` keeps its data in fields) and sends it to each player, who builds the identical level with `IGameMap.LoadLevel` (`MapGenerator.PrepareFlowField` fills the flow field for a received map). `Minimap.CreateImage(map)` renders the minimap picture outside a level, too.

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

Easy to confuse:

- **`BattleServer`** — player-side HTTP client against the REST API at `ServerApiUrl` (hardcoded IP in `Code/Game/BattleServer.cs`, overridable with `--api=`) for login, battle registration, join/leave, ready state, start. The login token is attached to every request; the API identifies players by **profile id** (`Battle.Owner`, `Peer.Id` are profile Guids), never by ENet peer id. Await it from `async void` UI handlers; don't block on `.Result`.
- **`ServerAuth`** — headless only. Clients connect with the one-time join ticket from `battle/start` / `battle/endpoint` (`Main3d.JoinServer(host, port, ticket)` sends it in Godot's auth handshake); the server redeems it with the API before completing the handshake, so `PeerConnected` only fires for verified players. Map ENet peer → profile with `ServerAuth.Current.GetIdentity(peerId)`. A server started by hand (no API env vars) accepts everyone.
- **`ServerApi`** — the game server's own HTTP calls back to the API (ticket redeem, finish report), authenticated with the per-instance token from the environment.
- **`ServerLifecycle`** — headless only (added by `Main3d`). The API launches one server process per battle and treats the process exiting as the battle ending, so this node decides when to quit: `FinishBattle(statistics)` reports results to the API (`battle/server/finish`, token from env) then exits 0; all players leaving exits 2; nobody joining within `--join-timeout` (default 60s) exits 3. Exit codes live in `GameServerExit` (tactical-api-common).
- **`BattleNetwork`** — Godot ENet RPCs for PrepareForBattle; same node path on both sides. Server (`StartHosting`, called by `Main3d` before the ENet server starts): generates and packs the map, sends it to each player on `PeerConnected` (`ReceiveMap`), records armies from `SubmitDeploy`, and once every player in the battle (list read from `battle/get` via `ServerApi`) has deployed, sends `BeginBattle` to all. A player disconnecting before that no longer counts. Client: raises `MapReceived` / `DeployProgressChanged` for PrepareForBattle and builds the level on `BeginBattle`. Gameplay itself is not replicated yet: each client spawns only its own player.

### Debug rendering

`GameDebug.Current` collects per-frame draw requests (`RegisterFov`, `RegisterPatrolPath`) into lists that are drawn as an `ImmediateMesh` and cleared every `_Process`. Register from the entity each frame; toggled by `PlayerInput.Current.DebugToggle`.

## Conventions

- Tabs in most `Code/Game` and `Code/UI` files, 4 spaces in `Code/Maps` — match the surrounding file.
- `.cs.uid` files next to scripts are Godot resource IDs; they are tracked and must not be deleted or regenerated by hand.
- Scene `.tscn` files reference scripts by `uid://`, so renaming/moving a `.cs` file requires updating the scene through the editor rather than by path edit.
