# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

StackBuffs is a Stardew Valley SMAPI mod written in C# (.NET 6.0). Its intent is to allow buffs to stack rather than be replaced. The project is at early scaffold stage — only the SMAPI template `ModEntry.cs` exists; no mod logic has been implemented yet.

A `manifest.json` file is **required** but does not yet exist. It must be created at the project root before the mod will load in-game.

## Build

```
dotnet build
```

The `Pathoschild.Stardew.ModBuildConfig` NuGet package (v4.4.0) auto-detects the local Stardew Valley installation and copies the built DLL plus `manifest.json` into `<Stardew Valley>/Mods/StackBuffs/` on each build.

To build in Release mode (for distribution):
```
dotnet build --configuration Release
```

## Running / Testing

There is no automated test suite. Testing is done by launching Stardew Valley via SMAPI after building:

1. Build the project (`dotnet build`).
2. Launch Stardew Valley through SMAPI (the game executable wrapped by SMAPI, not the vanilla exe).
3. SMAPI loads all mods from its `Mods/` folder; check the SMAPI console for load errors or `Monitor.Log` output.

SMAPI's console window shows all `this.Monitor.Log(...)` calls at runtime, which is the primary debugging mechanism.

## Architecture

### SMAPI Mod Pattern

Every SMAPI mod has a single entry class inheriting from `Mod`:

```csharp
internal sealed class ModEntry : Mod
{
    public override void Entry(IModHelper helper) { ... }
}
```

- `Entry` is called once on load. Wire up all event handlers here via `helper.Events.*`.
- `this.Monitor` — logging; use `LogLevel.Debug/Info/Warn/Error`.
- `this.Helper` — access to game APIs, content, config, reflection, multiplayer, etc.
- `this.ModManifest` — the mod's manifest metadata at runtime.

### Key SMAPI Event Namespaces

| Namespace | Purpose |
|---|---|
| `helper.Events.GameLoop` | Save load/unload, update ticks, day start/end |
| `helper.Events.Player` | Inventory changes, warping |
| `helper.Events.World` | NPC/object/location changes |
| `helper.Events.Input` | Keyboard, mouse, controller input |
| `helper.Events.Display` | Rendering hooks |
| `helper.Events.Multiplayer` | Multiplayer sync |

### Buff System (Stardew Valley internals)

Buffs live on `Game1.player.buffs` (a `BuffManager`). Each `Buff` has an `id` string, effects (`BuffEffects`), duration, and source. The vanilla system replaces an existing buff with the same `id` rather than stacking it — that replacement behavior is what this mod targets.

To intercept buff application, hook into `GameLoop.UpdateTicked` or use Harmony to patch `BuffManager` methods.

### manifest.json (must be created)

```json
{
  "Name": "StackBuffs",
  "Author": "<your name>",
  "Version": "1.0.0",
  "Description": "Allows food and drink buffs to stack instead of replacing each other.",
  "UniqueID": "<Author>.StackBuffs",
  "EntryDll": "StackBuffs.dll",
  "MinimumApiVersion": "4.0.0",
  "UpdateKeys": []
}
```

`UniqueID` must be globally unique on NexusMods (convention: `Author.ModName`).

## Dependencies

- **Pathoschild.Stardew.ModBuildConfig** — NuGet package that handles SMAPI references and deploy-on-build. No manual reference to SMAPI DLLs is needed in the csproj.
- **Harmony** — if patching game internals (not yet added); add via NuGet `0Harmony` and initialize in `Entry`.
