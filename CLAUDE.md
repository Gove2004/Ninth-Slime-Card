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
- Do not move gameplay initialization earlier than `Boot.LoadAsset()` unless you have verified that `ConfigCore`, `AudioCore`, `SaveCore`, and hot-update code are already available.
- Be careful with reflection-heavy or generic-heavy gameplay changes in hot-update code: HybridCLR/AOT/link preservation can become relevant (`Assets/HybridCLRGenerate/AOTGenericReferences.cs`, `Assets/HybridCLRGenerate/link.xml`).

### Scene loading model
Scene loading is package-driven, not Build Settings-driven.

Important facts:
- Only `Boot.unity` is in `ProjectSettings/EditorBuildSettings.asset`.
- `Login`, `Home`, and `Battle` are loaded by string through `ResCore.LoadSceneAsync(...)`.
- Those scenes are expected to be available through YooAsset packaging, not through raw `SceneManager` build-index loading.

Relevant files:
- `Assets/Scripts/Boot.cs`
- `Assets/Scripts/HotUpdate/UI/Login/LoginPage.cs`
- `Assets/Scripts/HotUpdate/UI/Home/LevelSelect.cs`
- `Assets/Scripts/HotUpdate/UI/Battle/BattleResultOverlay.cs`

Implications:
- Prefer `ResCore.LoadSceneAsync(...)` for scene transitions in gameplay/UI code.
- Do not casually switch scene loading to raw `SceneManager.LoadScene(...)`; that can bypass the project’s package-loading assumptions.
- Scene names like `"Login"`, `"Home"`, and `"Battle"` are runtime contracts. Renaming scenes requires updating string load sites and verifying the packaged address still matches.
- Code immediately after `ResCore.LoadSceneAsync(...)` must not assume the next scene is already fully initialized.

### Scene flow
The current scene progression is:
- `Boot` → `Login` → `Home` → `Battle`

Relevant files:
- `Assets/Scripts/Boot.cs`
- `Assets/Scripts/HotUpdate/UI/Login/LoginPage.cs`
- `Assets/Scripts/HotUpdate/UI/Home/LevelSelect.cs`
- `Assets/Scripts/HotUpdate/Manager/BattleManager.cs`

`GameCore` (`Assets/Scripts/HotUpdate/Core/GameCore.cs`) is the static cross-scene state holder. It stores:
- TapTap account
- `playerData` (persistent save data: trophy count, achievement unlock state, run state)
- `runState` (current roguelike run: Lv number, player deck card IDs)
- Trophy management (`AddTrophy`, `SpendTrophy`, `GetTrophy`)
- Achievement management (`IsAchievementUnlocked`, `UnlockAchievement`)
- Run management (`StartNewRun`, `SaveRunState`, `LoadRunState`, `HasActiveRun`)

`SaveManager` (`Assets/Scripts/HotUpdate/Manager/SaveManager.cs`) persists `PlayerData` via `SaveCore`. Loaded on login.

### Scene structure observed in Unity Editor
Unity MCP inspection confirms the current scene shapes matter to runtime behavior.

`Battle.unity` root objects currently include:
- `Main Camera`
- `Global Light 2D`
- `BG`
- `Canvas`
- `EventSystem`
- `玩家`
- `敌人`
- `BattleManager`

Under `Canvas`, the current top-level UI roots are:
- `HandCards`
- `HUD`
- `PauseMenu`
- `BattleResultOverlay`
- `RoguelikeChoicePanel`

Important observed constraints:
- `BattleResultOverlay` already exists in the scene and is treated as required by `BattleManager`.
- `RoguelikeChoicePanel` exists in the scene (default active but hidden by its script's `Start()`). Contains `Btns` (Add/Remove/Skip buttons), two `ScrollView` areas (card choice / deck removal), and a `Header`. Uses `SetAsLastSibling()` to manage z-order between overlapping areas.
- `PauseMenu` is scene-authored and defaults inactive.
- The end-turn button object is currently under `Canvas/HandCards/OverTurn`; `BattleManager` binds it by the object name `"OverTurn"` rather than by serialized reference.
- `HUD` and `CardContainer` depend on scene-authored player/enemy/UI objects already existing when battle startup runs.

`Home.unity` currently has a simpler scene shape:
- `Main Camera`
- `Canvas`
- `Global Light 2D`
- `EventSystem`

`Login.unity` currently contains:
- `Main Camera`
- `Canvas`
- `Global Light 2D`
- `EventSystem`
- `MessageToastCanvas`

A key scene-lifecycle implication from Unity inspection:
- `MessageToastManager` is present in `Login.unity` (`MessageToastCanvas`) but was not found in `Home.unity`. Future edits must not assume every scene contains its own toast manager; toast behavior depends on the startup path and/or singleton persistence.

### Resource and config loading conventions
Card data is data-driven:
- `CardConfigData` is bound to the CSV config named `第九张史莱姆牌-工作表1` via `[ConfigPath(...)]`.
- The actual file is `Assets/GameRes/Configs/第九张史莱姆牌-工作表1.csv`.
- `AchievementConfigData` is bound to `第九张史莱姆牌-工作表2` via `[ConfigPath(...)]`.
- The actual file is `Assets/GameRes/Configs/第九张史莱姆牌-工作表2.csv`.
- `BaseCard` loads its row by `id` from `ConfigCore` during construction.
- Card art is loaded synchronously by the convention `Card_{名称}`.

Implications:
- Renaming the CSV file/exported sheet name without updating `[ConfigPath(...)]` breaks card loading.
- Renaming card config rows or card sprites can break runtime lookup even if code still compiles.
- Card classes depend on data existing in config, not just on C# definitions.
- `BaseCard.Description()` only supports the placeholders `[费用]`, `[数值]`, `[数值1]`, `[数值2]`, `[数值3]`. Config authors must use only those tokens.

### Asset address conventions
This repo relies heavily on string-based runtime loads.

Examples:
- card sprites: `Card_{名称}` in `Assets/Scripts/HotUpdate/Cards/Cards/BaseCard.cs`
- toast prefab: `"MessageToast"` in `Assets/Scripts/HotUpdate/Manager/MessageToastManager.cs`
- scenes: `"Login"`, `"Home"`, `"Battle"`
- audio currently assumes `"Audio/{name}"` in `Assets/Scripts/HotUpdate/Manager/AudioManager.cs`

Implications:
- Filename changes matter more than folder layout in many places.
- Before renaming scenes, assets, prefabs, CSV labels, or UI objects, search for string-based runtime lookups.
- Verify audio address assumptions before wiring new gameplay to `AudioManager`; the current address pattern is not obviously aligned with actual audio asset layout.

### Card system
The card model is centered on `BaseCard` in `Assets/Scripts/HotUpdate/Cards/Cards/BaseCard.cs`.

Important behaviors:
- Each concrete card class overrides `id` and `OnUse(...)`.
- `PreUse()` spends mana.
- `PostUse()` currently moves the card out of hand/discards it.
- `Description()` performs placeholder substitution from config values.
- `ResolveTarget(...)` is part of the runtime target contract.
- `RuntimeCost` allows one-shot cost overrides for effects like temporary discounting.

`CardFactoryCore` scans the hot-update assembly for concrete `BaseCard` subclasses and instantiates them eagerly. Constructor side effects therefore matter: creating card instances immediately triggers config and sprite loading.

Important constraints:
- Card factory bootstrap depends on `ConfigCore` and `ResCore` already being initialized.
- One broken card row or renamed sprite can poison card registration early.
- `CardFactoryCore` maps both by `id` and by `Name`, so duplicate ids or duplicate localized names will silently overwrite earlier registrations.
- The expanded CSV is now backed by many more `Cards_*.cs` shard files; when adding a card, keep the shard grouping aligned with its series/id range instead of creating arbitrary new files.

### Character / battle loop
The battle runtime centers on `BattleManager` plus `BaseCharacter`. The game uses a roguelike progression loop.

**Roguelike flow**: Home → Lv.1 battle → victory → 5 rounds of card choices (add/remove/skip) → Lv.2 battle → ... → defeat → run reset to Lv.1.

`BattleManager` responsibilities:
- find `Player` and `Enemy` in scene
- bind battle-scene UI objects
- call `Setup()` on both (Player loads deck from `GameCore.runState`, Enemy generates deck by Lv)
- connect targets both ways
- start and advance the turn loop
- handle play-card requests, enemy turns, and battle end
- on victory: save player deck to runState, show `RoguelikeChoicePanel`, then advance Lv and restart
- on defeat: reset runState, show `BattleResultOverlay`

`RunState` (`Assets/Scripts/HotUpdate/Core/RunState.cs`) tracks the current run:
- `currentLv`: current level number
- `playerDeckIds`: list of card IDs in the player's deck
- Persisted in `PlayerData.runState` via `SaveCore`
- Saved when entering each battle, loaded on login

`BaseCharacter` responsibilities:
- own attributes, hand cards, deck cards, and discard cards
- gate card usage through mana/hand checks
- execute the card lifecycle (`PreUse` → `OnUse` → `PostUse`)
- manage turn hooks via `HookEffects`
- carry transient per-turn state for combo/tech cards (`CardsPlayedThisTurn`, `TechCardsPlayedThisTurn`, damage taken, next-card modifiers)
- `BuildStarterDeck()` is virtual: Player overrides to load from `runState.playerDeckIds`, Enemy overrides to generate by Lv

**Enemy scaling** (by Lv):
- MaxHP: `10 + (Lv-1) * 5`
- Mana regen: `1 + Lv / 3` (integer division, so Lv1-3=1, Lv4-6=2, etc.)
- Deck size: `5 + (Lv-1)`
- Deck composition: at least 1/3 attack cards, rest random from allowed series
- Series unlock: Lv1=初始, Lv2+=七罪, Lv3+=血族, Lv4+=坚固, Lv5+=all

**Player scaling**: MaxHP `10 + (Lv-1)`, draws 2 cards per turn, starts with 1 card in hand.

The battle/effect model is built on GoveKits `UnitEffect`.

Important hidden constraints:
- `BattleManager` currently relies on scene discovery (`FindFirstObjectByType`) and some name-based UI binding.
- The end-turn button is currently found by the GameObject name `"OverTurn"`. Renaming that object will break battle input without a compile error.
- `BattleResultOverlay` is treated as a required scene object in `Battle.unity`.
- `RoguelikeChoicePanel` is found via `FindFirstObjectByType(..., FindObjectsInactive.Include)`.
- `BaseCharacter.UseCard(...)` now consumes several one-shot runtime modifiers (extra triggers, next-card damage bonus, next-card cost reduction, next-tech extra trigger, tech damage bonus). If a new card is meant to affect only the very next play, prefer using these fields instead of mutating every card in hand.
- The enemy turn plays cards one by one with 0.5s delay between each (`TryActOnce()`), showing each card name as a toast.

### UI architecture
UI code in the hot-update assembly is scene-local and directly references gameplay singletons/state.

Examples:
- `HUD` reads `BattleManager.Instance` and reflects current player/enemy attributes.
- `CardContainer` renders the player hand and forwards play intent into `BattleManager`.
- `CardItem` owns drag input and a screen-threshold-based “play” gesture.
- `MessageToastManager` uses a message queue (0.5s interval) to prevent overlapping toasts.
- `RoguelikeChoicePanel` handles 5 rounds of post-victory card choices (add/remove/skip).
- `BattleResultOverlay` shows defeat screen with restart/home buttons.
- `HomePage` includes trophy display (top-right), achievements panel (AechiPanel), and start/continue game.

Important constraints:
- `CardItem` currently decides play intent by dragging above a screen-height threshold. This is not a true drop-zone/targeting system.
- `RoguelikeChoicePanel` uses `SetAsLastSibling()` to manage z-order between its overlapping child areas (ScrollView vs Btns). The two ScrollViews are full-screen and will block buttons if not reordered.
- `MessageToastManager.ShowMessage()` queues messages and displays them one at a time with 0.5s delay.

### Singleton and manager lifecycle assumptions
Do not assume `MonoSingleton<T>` means “global persistent service” in this repo.

Observed pattern:
- Several managers are attached directly to scene objects (`Boot`, `BattleManager`, `MessageToastManager`, etc.).
- Future edits must verify whether a manager is scene-local, scene-required, or truly persistent across scene loads.

Practical guidance:
- Before using `XManager.Instance` from a new scene, verify that the scene contains that manager or that the singleton base truly persists it.
- If a manager must work across scenes, explicitly design:
  - initialization timing
  - scene rebinding
  - teardown/unsubscribe behavior
- `BattleManager` in particular is tightly coupled to `Battle.unity` scene shape and should be treated as battle-scene-specific unless intentionally redesigned.

## Practical guidance for edits

- Prefer placing gameplay, battle, card, and scene UI logic under `Assets/Scripts/HotUpdate/**` unless the code must run before `HotUpdate.dll` is loaded.
- Be careful when editing boot/startup code in `Assets/Scripts/Boot.cs`: failures there block the whole game before the hot-update layer can start.
- For card-related changes, verify all three layers stay aligned: concrete card class, CSV config row, and sprite naming.
- For scene navigation, use the existing `ResCore.LoadSceneAsync(...)` flow instead of raw `SceneManager` calls unless there is a strong reason not to.
- Before renaming scene objects, asset files, or config files, search for string-based lookups first.
- Treat `BattleManager`, `BattleResultOverlay`, `CardContainer`, `HUD`, and the `OverTurn` button as tightly coupled scene contracts for the current Battle implementation.
- Do not assume a new level/scene state variable is already wired into gameplay just because it exists in `GameCore`.
- When changing battle restart/rematch behavior, pay close attention to event subscription cleanup and scene-object rebinding timing.
- When editing UI behavior in `Battle`, prefer checking the actual scene hierarchy with Unity MCP before making assumptions from code alone.
- Prefer expressing new card mechanics with existing primitives in `BaseCharacter` and `UnitAttributeEffect` before adding another bespoke `UnitEffect` subclass. Recent additions such as `ReplaceHand(...)`, `ExtendHookEffects(...)`, and next-card modifier fields were added specifically to keep card logic local.
- For temporary hand-replacement or delayed-copy cards, be careful with `PostUse()` discard semantics: replacing the whole hand inside `OnUse()` can orphan the currently resolving card unless you explicitly discard it first or override `PostUse()`.
