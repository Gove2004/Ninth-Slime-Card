# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

This is a Unity 6 project (`6000.0.59f2`) for a 2D card-battle game. Runtime code is split between a small built-in boot layer and a hot-update assembly loaded at startup.

Key packages in use:
- `com.code-philosophy.hybridclr` for hot-update assembly loading
- `com.tuyoogame.yooasset` for resource/package update workflow and scene/asset loading
- `com.gove.kits` for core runtime services (`MonoSingleton`, logging, config/storage/resource wrappers, time/random helpers, unit/effect system)
- `com.cysharp.unitask`
- TapTap SDK packages for login/achievement integration

## Common commands

No repo-specific build/test scripts were found. Development is primarily driven through the Unity Editor.

### Open the project
Open the repository root in Unity Editor **6000.0.59f2**.

### Run automated tests from CLI
Use Unity Test Runner in batch mode. This project includes `com.unity.test-framework`, but no test assemblies were found under `Assets/` at the time of writing.

Run all EditMode tests:
```bash
<UnityEditor> -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/EditModeTests.xml -quit
```

Run all PlayMode tests:
```bash
<UnityEditor> -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/PlayModeTests.xml -quit
```

Run a single test or fixture:
```bash
<UnityEditor> -batchmode -projectPath . -runTests -testPlatform EditMode -testFilter "FullyQualifiedTestNameOrFixture" -testResults Logs/SingleTest.xml -quit
```

### Build / run
No dedicated build entrypoint method was found in the repository. Builds appear to be initiated from the Unity Editor unless a build script is added later.

The active build settings include only the boot scene:
- `Assets/GameRes/Scenes/Boot.unity`

## Architecture

### Startup and hot-update boundary
The built-in assembly entrypoint is `Assets/Scripts/Boot.cs`.

Startup flow:
1. `Boot` initializes GoveKits core services and YooAsset.
2. `Boot` runs the package update workflow against a host/CDN.
3. After downloads complete, `Boot.LoadHotUpdateAsync()` loads AOT metadata and `HotUpdate.dll` via HybridCLR.
4. `Boot.LoadAsset()` initializes config/audio/save services.
5. The game then loads the `Login` scene through `ResCore.LoadSceneAsync("Login")`.

This means:
- Code under `Assets/Scripts/Boot.cs` is the non-hot-update bootstrap layer.
- Most gameplay/UI code lives under `Assets/Scripts/HotUpdate/**` and is intended to run from the hot-update assembly (`Assets/Scripts/HotUpdate/HotUpdate.asmdef`).
- If changing gameplay behavior, first check whether the code belongs in the hot-update assembly rather than the bootstrap layer.

### Scene flow
The current scene progression is:
- `Boot` → `Login` → `Home` → `Battle`

Relevant files:
- `Assets/Scripts/Boot.cs`
- `Assets/Scripts/HotUpdate/UI/Login/LoginPage.cs`
- `Assets/Scripts/HotUpdate/UI/Home/LevelSelect.cs`
- `Assets/Scripts/HotUpdate/Manager/BattleManager.cs`

`GameCore` (`Assets/Scripts/HotUpdate/Core/GameCore.cs`) is a small static cross-scene state holder. Right now it stores the TapTap account and the selected level name.

### Resource and config loading conventions
Card data is data-driven:
- `CardConfigData` is bound to the CSV config named `第九张史莱姆牌-工作表1` via `[ConfigPath(...)]`.
- `BaseCard` loads its row by `id` from `ConfigCore` during construction.
- Card art is loaded synchronously by the convention `Card_{名称}`.

Implications:
- Renaming card config rows or card sprites can break runtime lookup even if code still compiles.
- Card classes depend on data existing in config, not just on C# definitions.

### Card system
The card model is centered on `BaseCard` in `Assets/Scripts/HotUpdate/Cards/Cards/BaseCard.cs`.

Important behaviors:
- Each concrete card class overrides `id` and `OnUse(...)`.
- `PreUse()` spends mana through an effect.
- `PostUse()` removes the card from the user hand via `user.SpendCard(this)`.
- `Description()` performs placeholder substitution from config values.

`CardFactoryCore` scans the hot-update assembly for concrete `BaseCard` subclasses and instantiates them eagerly. Constructor side effects therefore matter: creating card instances immediately triggers config and sprite loading.

### Character / battle loop
The battle runtime centers on `BattleManager` plus `BaseCharacter`.

`BattleManager` responsibilities:
- find `Player` and `Enemy` in scene
- call `Setup()` on both
- connect targets both ways
- start the turn loop
- alternate turns with `StepTurn()`

`BaseCharacter` responsibilities:
- own attributes, hand cards, and deck cards
- gate card usage through mana/hand checks
- execute the card lifecycle (`PreUse` → `OnUse` → `PostUse`)
- manage turn hooks via `HookEffects`

The battle/effect model is built on GoveKits `UnitEffect`. For example, `UseCardEffect` is just a wrapper that calls `User.UseCard(Card)`.

### UI architecture
UI code in the hot-update assembly is scene-local and directly references gameplay singletons/state.

Examples:
- `HUD` polls `BattleManager.Instance` every frame and mirrors current player/enemy attributes.
- `CardContainer` instantiates `CardItem` prefabs, wires hover/drag/use callbacks, and triggers card use through `UseCardEffect`.
- `MessageToastManager` is used broadly for transient feedback.

This project currently favors direct coupling over an event bus for core battle/UI interactions. When changing UI behavior, trace the concrete singleton and callback path before introducing abstractions.

## Practical guidance for edits

- Prefer placing gameplay, battle, card, and scene UI logic under `Assets/Scripts/HotUpdate/**` unless the code must run before `HotUpdate.dll` is loaded.
- Be careful when editing boot/startup code in `Assets/Scripts/Boot.cs`: failures there block the whole game before the hot-update layer can start.
- For card-related changes, verify all three layers stay aligned: concrete card class, CSV config row, and sprite naming.
- For scene navigation, use the existing `ResCore.LoadSceneAsync(...)` flow instead of raw `SceneManager` calls unless there is a strong reason not to.
