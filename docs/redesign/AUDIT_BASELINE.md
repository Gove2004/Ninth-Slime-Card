# AUDIT_BASELINE —— 《第九张史莱姆牌》工程现状基线审计

> 只读审计产出。审计人：baseline-audit。日期：2026-10-01。
> 目标：为「UI + 核心玩法重设计」及「纯单机化」提供可引用的基线。
> 工程：Unity **6000.3.25f1**（ProjectSettings/ProjectVersion.txt），Android + TapTap，YooAsset 离线随包，HybridCLR 已移除。
> 说明：本文件由审计结论整理，未修改仓库内任何其它文件。

---

## 1. 工程结构基线

### 1.1 程序集划分（Assets 下的 asmdef）

| asmdef 路径 | name | references | autoReferenced |
|---|---|---|---|
| `Assets/Scripts/HotUpdate/HotUpdate.asmdef` | `HotUpdate` | 11 个（见下） | **true** |
| `Assets/Scripts/Core/ArchitectureCore.asmdef` | `NinthsSlime.ArchitectureCore` | `NinthsSlime.ScriptableObjects` | true |
| `Assets/ScriptableObjects/ScriptableObjects.asmdef` | `NinthsSlime.ScriptableObjects` | （无） | true |
| `Assets/Plugins/Demigiant/DOTween/Modules/DOTween.Modules.asmdef` | `DOTween.Modules` | （无） | — |
| `Packages/com.gove.kits/Runtime/GoveKits.Runtime.asmdef` | `GoveKits` | TMP / DOTween / YooAsset / MessagePack 相关 | true |
| `Packages/com.gove.kits/Editor/GoveKits.Editor.asmdef` | `GoveKits.Editor` | Editor only | true |

HotUpdate 的 11 个 references（已解析 GUID → 程序集名，含外部包）：

1. `7d5ef2062f3704e1ab74aac0e4d5a1a7` → **TapSDK.Core.Runtime**
2. `10560023d8780423cb943c7a324b69f2` → **TapSDK.Core.Mobile.Runtime**
3. `19c358263559d43cea9c760b06c3f151` → **TapSDK.Achievement.Runtime**
4. `5207337eee02b46819212e1934d58d16` → **TapSDK.Achievement.Mobile.Runtime**
5. `e8754b6153389406c963cd52996cc80f` → **TapSDK.Login.Runtime**
6. `6ff51c32c188e424b97bac52a5cb5184` → **TapSDK.Login.Mobile.Runtime**
7. `6055be8ebefd69e48b49212b09b47b2f` → Unity.TextMeshPro
8. `077f815a79aa0e844aa0d560eeb65641` → **GoveKits**
9. `bb05cab7d802aa5468f8f2f86840d984` → Unity.TextMeshPro.Tests
10. `8e38dc47975016a41a9eb39d5991f4bc` → DOTween.Modules
11. `e34a5702dd353724aa315fb8011f08c3` → YooAsset

> **关键**：HotUpdate 直接 asmdef 引用了 6 个 TapSDK 程序集。要彻底移除 TapTap，必须同时改这个 asmdef（见第 5 节）。

### 1.2 Assembly-CSharp 与 HotUpdate 的边界

- `Assets/Scripts/HotUpdate/**` 全部 .cs → 程序集 **HotUpdate**（含 Cards/Character/Core/Effect/Manager/Static/UI/Util）。
- `Assets/Scripts/Core/**` → 程序集 **NinthsSlime.ArchitectureCore**（未被引用的并行架构层，见 3.3）。
- **`Assets/Scripts/Boot.cs` 是唯一落在预定义程序集 Assembly-CSharp 的业务脚本**（`Assets/Scripts/` 根目录下无 asmdef）。
- HotUpdate `autoReferenced:true` → 预定义程序集（Assembly-CSharp，即 Boot）可自动引用 HotUpdate；Boot 实际只用到 GoveKits（`ConfigCore/AudioCore/SaveCore`）与 YooAsset，不直接引用 HotUpdate 类型（`Assets/Scripts/Boot.cs:11`）。

---

## 2. 场景清单

### 2.1 全部 .unity（Assets 下）

| 场景 | 行数 | 用途 | 是否在 Build Settings |
|---|---|---|---|
| `Assets/GameRes/Scenes/Boot.unity` | 1244 | 启动/资源初始化 | **是**（`ProjectSettings/EditorBuildSettings.asset:9`） |
| `Assets/GameRes/Scenes/Login.unity` | 980 | 登录（离线直通） | 否（YooAsset 地址加载） |
| `Assets/GameRes/Scenes/Home.unity` | 14135 | 主页/图鉴/成就/设置/选关 | 否 |
| `Assets/GameRes/Scenes/Battle.unity` | 5729 | 战斗 | 否 |
| `Assets/Home.unity` | 1241 | **游离/疑似废弃场景（不在 GameRes/Scenes，需确认）** | 否 |
| `Assets/Settings/Scenes/URP2DSceneTemplate.unity` | 350 | URP 2D 模板场景 | 否 |

> 除 Boot 外的业务场景都通过 `ResCore.LoadSceneAsync("<文件名>")` 由 YooAsset 加载；场景地址 = 文件名（AddressByFileName）。

### 2.2 UI 是「内联在场景」还是「用 Prefab」

**结论：以场景内联为主，Prefab 极少。** Assets 下**总共只有 6 个 .prefab**，全在 `Assets/GameRes/Prefabs/UI/`：

- `AechiItem.prefab`（成就条目）
- `Button.prefab`（通用按钮）
- `CardItem.prefab`（手牌卡视图）
- `ChoiceItem.prefab`（选牌/图鉴卡格）
- `Level.prefab`（关卡入口按钮）
- `MessageToast.prefab`（飘字）

**场景内组件规模证据（内联对象计数）**：

| 场景 | GameObject | MonoBehaviour | Canvas 组件 | Image(UI) | TextMeshProUGUI |
|---|---|---|---|---|---|
| Boot.unity | 13 | 13 | 1 | 3 | 2 |
| Login.unity | 8 | 12 | **2** | 1 | 1 |
| Home.unity | 134 | 171 | 1 | **59** | **33** |
| Battle.unity | 47 | 72 | 1 | 17 | 13 |

（Canvas 组件按 YAML 块 `Canvas:` 计数；Image 按其 m_Script GUID `fe87c0e1cc204ed48ad3b37840f39efc`；TMP 按 GUID `f4688fdb7df04437aeb418b961361dc5`。）

**Prefab 实例分布（场景内 `m_SourcePrefab`）**：

- Home.unity：10 个实例 = 6×`Button.prefab` + 1×`AechiItem.prefab` + 3×`Level.prefab`
- Battle.unity：6 个实例 = 5×`Button.prefab` + 1×来源 guid `be88b1c2581432749a0171e9bd615db0`（**该 guid 不在仓库 Assets/Packages 内，疑似外部包 Prefab，需人工确认，见第 7 节**）
- Login.unity：1 个 = `Button.prefab`
- Boot.unity：0 个

**战斗面板全部内联，不是 Prefab**：Battle 场景 Canvas 子节点依次为 `HandCards` / `HUD` / (无名) / `BattleResultOverlay` / (无名)；`RoguelikeChoicePanel`(Battle.unity:1515)、`CardContainer`(Battle.unity:1912)、`PauseMenu`(Battle.unity:4104)、`HUD`(Battle.unity:4843)、`BattleResultOverlay`(Battle.unity:3076) 均为场景内联。

### 2.3 CanvasScaler 设置（四场景一致，内联在 Canvas 上）

- `Boot.unity:734-739` / `Login.unity:688-693` 与 `801-806`（Login 有第二个 Canvas）/ `Home.unity:10204-10209` / `Battle.unity:653-658`
- 参数：`m_UiScaleMode:1`（ScaleWithScreenSize）、`m_ReferenceResolution:{x:800,y:600}`、`m_ScreenMatchMode:0`（MatchWidthOrHeight）、`m_MatchWidthOrHeight:0`（**Match=Width**）
- **安全区适配：全仓零命中**（对 `safeArea/SafeArea/notch` 的 grep 无结果）。

### 2.4 手改 YAML 可行性

- Boot(1.2k)/Login(1.0k)：可手改。
- Battle(5.7k)：勉强，但序列化 `fileID` 引用密集，风险高。
- **Home(14.1k)：不建议手改**，应走 Unity 编辑器或代码驱动重建。
- 游离的 `Assets/Home.unity`(1.2k) 建议先确认归属再处理。

---

## 3. UI 代码清单

### 3.1 `Assets/Scripts/HotUpdate/UI/**`（18 个）

| 路径 | 类 | 职责一句话 | 建 UI 方式 |
|---|---|---|---|
| UI/Battle/HUD.cs | `HUD` | Update 轮询刷新玩家/敌人 HP、MP 文本与血条 | 依赖场景内联（4×RollTMP + 1×Image，序列化） |
| UI/Battle/CardContainer.cs | `CardContainer` | 手牌增删/reflow + 卡牌详情浮层动效 | 依赖序列化引用 + 实例化 CardItem prefab |
| UI/Battle/HandCardFanLayout.cs | `HandCardFanLayout` | 手牌**扇形布局**（角度/半径/间距，DOTween 动画） | 纯代码驱动（参数 public 字段） |
| UI/Battle/CardItem.cs | `CardItem` | 单张手牌视图：悬停放大、拖拽出牌 | 依赖 CardItem.prefab 内联引用 |
| UI/Battle/RoguelikeChoicePanel.cs | `RoguelikeChoicePanel` | 胜利后 5 轮选牌（加牌/删牌/跳过） | **半代码建**（运行期 Instantiate ChoiceItem + `transform.Find`） |
| UI/Battle/BattleResultOverlay.cs | `BattleResultOverlay` | 失败结算浮层（重开/回主页） | 依赖场景内联（序列化 Button/TMP） |
| UI/Battle/PauseMenu.cs | `PauseMenu` | 暂停菜单（暂停/继续；"立即结算"未实现） | 依赖场景内联（序列化） |
| UI/Home/HomePage.cs | `HomePage` | 主页导航中枢（6 按钮 + 6 互斥面板 + 返回） | 依赖场景内联（**18 个序列化字段**） |
| UI/Home/CodexPanel.cs | `CodexPanel` | 图鉴：系列筛选 + 8/页分页 + 详情浮层 + 锁定灰化 | **纯代码建**（运行期 `transform.Find` + new GameObject/Image/TMP） |
| UI/Home/AechiPanel.cs | `AechiPanel` | 成就列表容器（实例化 AechiItem） | 半代码建（`content` 序列化 + Instantiate） |
| UI/Home/LevelSelect.cs | `LevelSelect` | 「开始游戏 / 继续 Lv.X」入口，点按进 Battle | 依赖场景内联（序列化 TMP/Image） |
| UI/Home/SettingsPanel.cs | `SettingsPanel` | 音量滑条 + 语言下拉，写 PlayerPrefs | 依赖场景内联（序列化 Slider/Dropdown） |
| UI/Login/LoginPage.cs | `LoginPage` | 登录页（离线直通 Home） | 依赖场景内联（序列化 Button/TMP） |
| UI/UIItem/AechiItem.cs | `AechiItem` | 单条成就视图（进度/购买） | 依赖 AechiItem.prefab 序列化引用 |
| UI/UIItem/MessageToast.cs | `MessageToast` | 飘字提示，上移动画后自销毁 | 依赖 MessageToast.prefab 序列化引用 |
| UI/UIItem/PanelScaleSHowHide.cs | `PanelScaleSHowHide` | 面板显隐（**仅 SetActive，无动画**） | 纯代码 |
| UI/UIItem/RollTMP.cs | `RollTMP` | 数字滚动/直设 TextMeshProUGUI | 纯代码（组件） |
| UI/EventCenter.cs | `GameEvents` + `EventCenter` | 静态事件总线（Dictionary<string,Action<object>>） | 纯代码 |

> 计数为 **18 个 .cs**（含 EventCenter.cs；不含 UI/UIItem/CardItem 的重复说明）。

### 3.2 绑定方式分类（重做 UI 的关注点）

- **序列化拖引用**：HomePage、BattleResultOverlay、AechiPanel、LevelSelect、SettingsPanel、LoginPage、HUD、CardContainer、CardItem、MessageToast、AechiItem、PauseMenu。
- **字符串 `transform.Find` / `GetComponent` 硬绑定**（脆弱）：CodexPanel（`"Panel/Content"`、`"Panel/FilterBar/FilterContent"`、`"Panel/NavBottom/BtnPrev"`、`"Text (TMP)"`、`"Text (TMP) (1)"`、`"Image"`，CodexPanel.cs:52-75,251,263,307-308）；RoguelikeChoicePanel（:148-149,237-238,259,353）。
- **按对象名查找**：BattleManager 靠 `button.name == "OverTurn"`（BattleManager.cs:358）；`FindFirstObjectByType<Player/Enemy/CardContainer/BattleResultOverlay>`（:393-398）。
- **纯代码建 UI**：CodexPanel（筛选按钮/锁图标全部 `new GameObject`）、RoguelikeChoicePanel/AechiPanel（列表项实例化）、MessageToastManager（实例化 toast prefab）。
- **按资源地址加载**：`ResCore.LoadAssetSync("Card_{名}")`（BaseCard.cs:89、CodexPanel.cs:452、RoguelikeChoicePanel.cs:359）、`"MessageToast"`（MessageToastManager.cs:17）。

### 3.3 未被引用的并行架构层（死代码，重做时勿误用）

`Assets/Scripts/Core/**`（程序集 `NinthsSlime.ArchitectureCore`）：
`Event/EventSystem.cs`、`Event/GameEventListener.cs`、`Services/AchievementService.cs`、`Services/AudioService.cs`、`Services/IAchievementService.cs`、`Services/IAudioService.cs`、`Services/IEventManager.cs`、`Services/ISaveService.cs`、`Services/SaveService.cs`、`Unit/UnitBehaviour.cs`、`Validation/ArchitectureValidator.cs`。
除 `UnitBehaviour` 被 `BaseCharacter` 继承（BaseCharacter.cs:6）外，其余**无任何场景/脚本引用**。

---

## 4. 美术资源清单

### 4.1 分类统计（`Assets/GameRes/Arts/`）

| 目录 | PNG 数量 | 体积 | 尺寸抽样 | 命名规律 |
|---|---|---|---|---|
| `Images/Card/` | **159** | **230.49 MB** | 全部 `1086×1448` | 严格 `Card_{卡名}.png`（与配置表 名称 字段一一对应） |
| `Images/UI/` | 7 | 4.12 MB | 1024×1024×3、806×1062、1141×1138、828×78、1495×1483 | 混杂：`Panel.png`、`MessageToastBG.png`、`blood.png`、`mana.png`、`score.png` + 2 张 `Generated Image …（乱名）` |
| `Images/Character/` | 2 | 2.23 MB | 1800×1800、2048×2048 | `敌人.png`、`果冻_拿剑.png` |
| `Images/Level/` | 2 | 1.05 MB | 1254×1254、452×459 | `奖杯.png`、`标准.png` |
| `Images/BG/` | 1 | 1.04 MB | 2848×1276 | `BaseBG.png` |
| **Arts 合计 PNG** | **171** | **≈238.9 MB** | — | — |
| `Audios/Music` | — | 22 MB | 4×mp3：`战斗bgm1/2`、`标题界面bgm1/2` | 中文命名 |
| `Audios/Sound` | — | 179 KB | `UI.ogg`、`出牌/回蓝/回血/抽牌/毒液/斩击` | 中文命名，按名字加载 |
| `Fonts` | — | 20 MB | `Alibaba_PuHuiTi_2.0_65_Medium`（ttf + TMP asset） | — |

> **数量口径说明**：`Assets/GameRes/Arts` 下为 **171 张 PNG**；`Assets` 全域共 **176 张 PNG**，多出的 5 张是编辑器插件图标（`Assets/Plugins/Demigiant/DOTween/Editor/Imgs/*` 4 张 + `Assets/TextMesh Pro/Sprites/EmojiOne.png`），与游戏资源无关。任务书中的"172"与本口径相差 1，以本文件实测为准。

### 4.2 九宫格（Sprite Border）与图集（Atlas）

- **九宫格：唯一 1 张 = `Assets/GameRes/Arts/Images/UI/Panel.png`**（meta：`textureType: 8` Sprite / `spriteMode: 2` Multiple / `spriteBorder: {x:141,y:404,z:143,w:402}`）。
- 其余 170 张 PNG 的 `spriteBorder` 均为 `{0,0,0,0}`（卡图/UI 均为普通 Single Sprite，`textureType: 8`）。
- **图集（Atlas）：游戏资源无任何 .spriteatlas**；仓库内仅 `Packages/com.unity.2d.tooling/Samples~/...` 的官方示例图集，与项目无关。→ **当前没有做图集合批**。

### 4.3 图片 vs 纯色/代码绘制

- 图片：卡面、卡框、魔晶图标（CardItem.prefab:69,416,495 引用 sprite）、`Panel.png`（九宫格）、`MessageToastBG.png`、血条/蓝条/奖杯图标。
- 纯色/代码：图鉴筛选按钮（`Image + Color`，CodexPanel.cs:118-144；`SelectedColor/NormalColor/LockedColor` :20-22）、锁定卡灰化与锁图标 fallback 纯红（:294-300）、扇形布局/拖拽/数字滚动全部代码驱动。

---

## 5. 纯单机化阻塞点（重点）

### 5.1 TapTap SDK 的全部源码调用点

包：`Packages/manifest.json:6-8` —— `com.taptap.sdk.core` / `com.taptap.sdk.login` / `com.taptap.sdk.achievement`（GitHub 源，缓存于 `Library/PackageCache/com.taptap.sdk.*`）。

**using 引用点（4 处）**

- `Assets/Scripts/HotUpdate/Core/GameCore.cs:1` —— `using TapSDK.Login;`（**仅类型引用**：`TapTapAccount account`(GameCore.cs:12) + `SetAccount(TapTapAccount)`(:13)）
- `Assets/Scripts/HotUpdate/Core/TapTapCore.cs:8-10` —— `using TapSDK.Achievement/Core/Login;`（全部 SDK 封装在此文件）
- `Assets/Scripts/HotUpdate/Manager/AchievementManager.cs:4` —— `using TapSDK.Achievement;`
- `Assets/Scripts/HotUpdate/UI/Login/LoginPage.cs:6` —— `using TapSDK.Login;`

**真正触网/云端的调用点**

| 位置 | 调用 | 触发路径 | 离线模式下是否执行 |
|---|---|---|---|
| TapTapCore.cs:71 | `TapTapSDK.Init(coreOptions, otherOptions)` | `TapTapCore.Initialize()` | 否（仅非离线分支调用） |
| TapTapCore.cs:74 | `TapTapAchievement.RegisterCallBack(...)` | `Initialize()` | 否 |
| TapTapCore.cs:87 | `TapTapAchievement.UnRegisterCallBack(...)` | `TapTapCore.OnDestroy()` | 否（无人调 OnDestroy） |
| TapTapCore.cs:119 | `TapTapLogin.Instance.LoginWithScopes(...)` | `TapTapCore.LoginAsync()` | 否 |
| TapTapCore.cs:150 | `TapTapAchievement.Unlock(...)` | `TapTapCore.UnlockAchievement()` | **否（全仓无调用者，死代码）** |
| TapTapCore.cs:160 | `TapTapAchievement.Increment(...)` | `TapTapCore.IncrementAchievement()` | **否（无调用者，死代码）** |
| LoginPage.cs:29 | `TapTapCore.Initialize()` | `LoginPage.Start()` 非离线分支 | 否 |
| LoginPage.cs:37 | `TapTapLogin.Instance.GetCurrentTapAccount()` | `ChackLoginToken()` 非离线分支 | 否 |
| LoginPage.cs:72 | `TapTapCore.LoginAsync(...)` | `OnLoginButtonClicked()` 非离线分支 | 否 |
| **AchievementManager.cs:34** | **`TapTapAchievement.Unlock(config.成就ID)`** | `AchievementManager.TryPurchase()` | **⚠️ 会执行（无 LocalOfflineMode 判断）** |

> **唯一的离线阻塞点**：`AchievementManager.cs:34`。购买成就时无条件调用云端 SDK，是纯单机化下**唯一必然触网的路径**（其余 TapTap 调用都在 `GameCore.LocalOfflineMode` 的非离线分支或死代码里）。

**登录流程入口（`LoginPage.cs`）**

- `Start()` :18 挂按钮；**若 `LocalOfflineMode`（GameCore.cs:8 默认 true）**：:22-27 设提示文本 → `OnLoginSuccess(new TapTapAccount())` → **`return`（在 :29 `TapTapCore.Initialize()` 之前返回，不触网）**。
- `OnLoginButtonClicked()` :53：`isLoggingIn` 已为 true → :55-60 直接 `ResCore.LoadSceneAsync("Home")`；离线分支 :62-66 再次 `OnLoginSuccess` 后 return（不触网）。
- `OnLoginSuccess` :76-90 只做本地：`GameCore.SetAccount` + `SaveManager.Instance.Load()`（本地 JSON）。
- **结论：离线登录路径完整可用，不会半途触网。**

**成就系统：本地 vs 云端**

- 本地链路自洽：`AchievementManager.TryPurchase`(:13-38) → `GameCore.SpendTrophy`(:26) + `GameCore.UnlockAchievement`(:32 → GameCore.cs:63-75 写 `PlayerData.achievementUnlocked`) → `SaveManager.Save`（本地 JSON）。UI 侧 `AechiPanel/AechiItem` 只读本地。
- 云端同步仅 `AchievementManager.cs:34`（活调用）。

### 5.2 改成"纯本地"需要动的文件

**最小改动（仅达成"运行时不触网"）—— 1 个文件**

1. `Assets/Scripts/HotUpdate/Manager/AchievementManager.cs:34`：删除 `TapTapAchievement.Unlock(config.成就ID);`，或改为 `if (!GameCore.LocalOfflineMode) TapTapAchievement.Unlock(...)`；同时移除文件顶部 `using TapSDK.Achievement;`(:4)。

**彻底移除 SDK 依赖（去"联网可能性"与包体积）—— 追加 4 项**

2. 删除 `Assets/Scripts/HotUpdate/Core/TapTapCore.cs`；处理调用者 `LoginPage.cs:29,72`（离线分支已 return，可整段删除非离线登录逻辑）。
3. 清理 using：`GameCore.cs:1`（`TapTapAccount` 类型需替换为自定义空账号类型或直接删除 account 字段/SetAccount）、`LoginPage.cs:6`。
4. **修改 `Assets/Scripts/HotUpdate/HotUpdate.asmdef` 的 references：移除 6 个 TapSDK GUID**（见 1.1 第 1-6 项）——否则删包后编译失败。
5. `Packages/manifest.json:6-8`：移除 `com.taptap.sdk.core` / `com.taptap.sdk.login` / `com.taptap.sdk.achievement` 三个包。

### 5.3 GoveKits Network 模块是否还有活调用

- 模块：`Packages/com.gove.kits/Runtime/Network/**`（Http/HttpEngine.cs、Protocol/.../ClientCore.cs、ServerCore.cs、Rpc/RpcCore.cs、Sync/*）。
- **游戏 Assets 内零调用**：对 `HttpEngine/HttpCore/RpcCore/SyncCore/ClientCore/ServerCore/GoveKits.Runtime.Network` 的全量 grep **全部为空**。
- Packages 内唯一引用点：`Packages/com.gove.kits/Runtime/GoveKitsManager.cs` —— `using GoveKits.Runtime.Network`(:3)、`ProtocolCore.AddResolver`(:33)、`ProtocolCore.ScanAndRegister`(:35)、`SyncCore.Update`(:69)；**这些只是协议注册与时间轮更新，非 I/O**，且真实连接 `ClientCore.ConnectAsync` / `ServerCore.StartAsync` 均被注释（:37-43）。
- `GoveKitsManager` 本身 **不在任何场景、无人 `Instance` 访问 → 死代码**（MonoSingleton 仅在被访问时才创建）。
- **无独立 asmdef**，Network 属于 `GoveKits` 程序集；移除 Network 目录不会破坏 asmdef 结构，但会与 `Runtime/Gen/*Formatter`（Network 消息的 MessagePack 生成器）及 `GoveKitsManager.cs` 产生编译依赖，并牵出 MessagePack。
- **建议：不动 Network（留着不产生联网），性价比最高。**

### 5.4 除 TapTap 外的网络请求代码全量排查

对 `http(s):// / UnityWebRequest / HttpClient / Socket / Application.OpenURL / UnityServices / Analytics / ReportEvent / telemetry / CDN` 的全量 grep：

- 游戏代码**零命中**（唯一 `"CDN"` 出现在注释 `GameCore.cs:6`）。
- 无 Unity Services / Analytics / 遥测上报。
- 无自有 HTTP 客户端（GoveKits Network 未被调用）。
- 持久化全部本地：`PlayerPrefs`（HomePage.cs:54-56、SettingsPanel.cs:40/47/53/60/95、GoveKits LocalizationCore.cs:147-154）+ `SaveCore` JSON（SaveManager.cs:12,18，路径 `"player"`）。

### 5.5 一句话结论

> **要做到"完全离线可玩"，最少只需改 1 个文件**：`Assets/Scripts/HotUpdate/Manager/AchievementManager.cs:34`（去掉无条件的 `TapTapAchievement.Unlock`）。
> 若要**彻底剥离 TapTap SDK**：再改 `TapTapCore.cs`（删）、`LoginPage.cs`、`GameCore.cs`、`HotUpdate.asmdef`（去 6 个 GUID 引用）、`Packages/manifest.json:6-8`。
> `GoveKits Network` 与 `Assets/Scripts/Core/**` 均为**零调用死代码，可安全不管**。

---

## 6. 玩法核心文件清单

> 除 `Assets/Scripts/Boot.cs`（Assembly-CSharp）外，**以下全部位于 HotUpdate 程序集**（`Assets/Scripts/HotUpdate/**`）。

### 6.1 战斗

| 路径 | 职责 | 关键成员 |
|---|---|---|
| `HotUpdate/Manager/BattleManager.cs` | 战斗状态机总控（MonoSingleton） | `enum BattlePhase`(:11)、`Start`(:36)、`RequestPlayCard`(:98)、`RequestEndTurn`(:134)、`CheckBattleEnd`(:149)、`EndBattle`(:168)、`ShowRoguelikeChoice`(:212)、`AdvanceToNextLv`(:225)、`RunEnemyTurnAsync`(:256) |
| `HotUpdate/Character/BaseCharacter.cs` | 角色基类：手牌/牌库/弃牌、法力/护盾/属性、出牌结算、Hook 效果 | `CanUseCard`(:74)、`SpendMana`(:95)、`TakeDamage`(:103)、`UseCard`(:138)、`DrawCards`(:222)、`DiscardCard`(:212)、`StartTurn`(:457)、`HookEffects`(:599) |
| `HotUpdate/Character/Player.cs` | 玩家：手牌上限 8、按 runState 组牌、存牌 | `HandLimit=8`(:5)、`Setup`(:9)、`DrawCards`(:34)、`BuildStarterDeck`(:45)、`SaveCurrentDeck`(:64) |
| `HotUpdate/Character/Enemy.cs` | 敌人：按关卡成长/组牌、启发式出牌 | `Setup`(:7)、`BuildStarterDeck`(:19)、`GetAllowedSeries`(:69)、`TryActOnce`(:89) |
| `HotUpdate/Effect/UnitAttributeEffect.cs` | 伤害/治疗/护盾/法力等效果实现 | `AttackEffect`(:25, 结算 :39-48)、`HurtEffect`(:59, :73-83)、`HealEffect`(:94) 等 |
| `HotUpdate/Effect/BattleEffect.cs` | 出牌相关效果 | `UseCardEffect`(:5) |
| `HotUpdate/Effect/VisualEffect.cs` | 视觉特效（斩击等，逻辑已弱化） | `RedSlashEffect`(:5) 等 |
| `HotUpdate/UI/Battle/HUD.cs` | 战斗 HUD 刷新 | `Update`(:14) |

### 6.2 卡牌 / 配置

| 路径 | 职责 |
|---|---|
| `HotUpdate/Cards/CardConfigData.cs` | CSV 行模型：`[ConfigPath("第九张史莱姆牌-工作表1","csv")]`(:4)，字段 `id,名称,系列,费用,数值1/2/3,描述,趣闻,备注`(:8-17) |
| `HotUpdate/Cards/CardFactory.cs` | 反射注册 161 个卡类（id→Type，名称→id）(:10-26)，`CreateCard(int/string)`(:28/39) |
| `HotUpdate/Cards/Cards/BaseCard.cs` | 卡牌抽象基类：读配置、描述替换、`CanUse/PreUse(扣法力)/OnUse/PostUse(弃牌)`、`LoadCardSprite("Card_{名}")`(:87-91) |
| `HotUpdate/Cards/Cards/Cards_1..8.cs` | 161 张卡的 `OnUse` 实现 |
| `Assets/GameRes/Configs/第九张史莱姆牌-工作表1.csv` | 卡牌表（163 行≈161 卡） |
| `Assets/GameRes/Configs/第九张史莱姆牌-工作表2.csv` | 成就表（200 行），`AchievementConfigData.cs:3` |

### 6.3 牌组 / 进度 / 存档

| 路径 | 职责 |
|---|---|
| `HotUpdate/Core/RunState.cs` | 单局状态：`currentLv`、`playerDeckIds`、`StarterDeckIds={101,101,101,102,102,103}`(:8) |
| `HotUpdate/Core/GameCore.cs` | 全局静态门面：`LocalOfflineMode`(:8)、`account`(:12)、`playerData`(:19)、奖杯(:26-52)、成就(:58-75)、图鉴(:81-99)、`runState`(:105)、`StartNewRun`(:113)、`HasActiveRun`(:139) |
| `HotUpdate/Core/PlayerData.cs` | 存档模型：`trophy/achievementUnlocked/runState/collection` |
| `HotUpdate/Manager/SaveManager.cs` | 本地 JSON 存档（`SaveCore`，路径 `"player"`）(:6,12,18) |
| `HotUpdate/Manager/AchievementManager.cs` | 成就查询/购买（**含唯一活云端调用 :34**） |
| `HotUpdate/UI/Battle/RoguelikeChoicePanel.cs` | 胜利选牌 5 轮循环 |
| `HotUpdate/UI/Battle/BattleResultOverlay.cs` | 失败结算 |

### 6.4 其它基础设施

- `HotUpdate/UI/EventCenter.cs`：静态事件总线（`GameEvents` 常量 + `EventCenter`）。
- `HotUpdate/Manager/AudioManager.cs` / `MessageToastManager.cs` / `EffectManager.cs`（空壳）：MonoSingleton，按需自动创建。
- `HotUpdate/Core/LanguageTable.cs`：纯代码语言表（遍历 TMP 替换），未持久化词典、未见 `InitDefaultEntries` 调用。
- `HotUpdate/Core/TapTapCore.cs`：TapTap 封装（离线化需处理）。
- `Assets/Scripts/Boot.cs`：启动层（Assembly-CSharp），初始化 YooAsset/GoveKits 服务并加载 Login 场景。

---

## 7. 风险与不确定项

### 7.1 会阻碍"重做 UI"的硬耦合（高优先级）

1. **场景名字符串契约**：`"Login"`(Boot.cs:107)、`"Home"`(LoginPage.cs:58 / BattleManager.cs:55 / BattleResultOverlay.cs:41)、`"Battle"`(LevelSelect.cs:47 / BattleManager.cs:49 / BattleResultOverlay.cs:30)。
2. **资源地址 = 文件名**：YooAsset `AddressByFileName`（`Assets/AssetBundleCollectorSetting.asset:36,44,52,60,68,81`）+ `EnableAddressable:1 / SupportExtensionless:1`(:21-22)。故 `"Card_{名}"`(BaseCard.cs:89 等)、`"MessageToast"`(MessageToastManager.cs:17)、音频名（UI/回蓝/出牌/斩击/毒液/回血/抽牌/战斗bgm1/2/标题界面bgm1/2）都是硬编码地址，改名/挪目录即断。
3. **按对象名/层级查找**：`button.name=="OverTurn"`(BattleManager.cs:358)；CodexPanel/RoguelikeChoicePanel 的 `transform.Find("Text (TMP)")`、`"Viewport/Content"` 等（CodexPanel.cs:52-75,251,263；RoguelikeChoicePanel.cs:148-149,259,353）。
4. **场景序列化引用**：HomePage 18 个字段（Home.unity:7288-7307）；CardItem.prefab:517-519（cardImage/cardNameText/costText）。改层级/改名即丢引用。
5. **单例/静态跨场景生命周期**：`GameCore` 全静态、`EventCenter` 静态字典跨场景不清（`ClearAllEvents` :239 无人调用）、`MonoSingleton` 自动创建常驻对象（`Packages/com.gove.kits/Runtime/Core/Singleton/MonoSingleton.cs:38-50,74-84`）。BattleManager 在 Battle 场景、MessageToastManager 只在 Login 场景、AchievementManager 只在 Home 场景，跨场景靠自动创建兜底 → 重构场景时谨防重复实例警告/悬空引用。
6. **手感常量散落**：扇形 `fanAngle=30/radius=200/cardSpacingOffset=-30`（HandCardFanLayout.cs:7-10）、出牌阈值 `playThreshold=0.58` 且基于 `Screen.height`（CardItem.cs:20,103，分辨率敏感）、敌人节奏 `0.5/0.8/0.5`（BattleManager.cs:21-23）。

### 7.2 结构性/资产风险

- **Home 场景 14.1k 行 + 171 个 MonoBehaviour**，是全项目最重的耦合点；其 CanvasScaler 为 800×600 / Match=Width，**无安全区**，横屏挖孔机易裁切。
- **卡图体量 230 MB / 159 张（全部 1086×1448）**，无图集、无压缩合并策略 → APK 体积与内存需评估。
- **游离场景 `Assets/Home.unity`（1241 行）**：位于 Assets 根、不在 GameRes/Scenes，疑似历史遗留，需确认是否影响构建/被误引。
- **Battle 场景内 1 个未知来源 Prefab**（guid `be88b1c2581432749a0171e9bd615db0`）不在仓库内，疑似外部包 Prefab，来源不确定。
- **新增卡牌必须加 C# 类**：`CardFactoryCore` 依赖反射（CardFactory.cs:10-26），仅改 CSV 不会生效。
- **`Assets/Scripts/Core/**`（ArchitectureCore）为死代码**，与 HotUpdate 命名/职责重叠，重做时易被误当现役架构。

### 7.3 不确定项（需人工确认）

- TapTap 包在 Android 上是否有**平台层自动初始化/自动上报**（SDK 内部行为，源码不可见），grep 无法覆盖；建议移除包以彻底消除。
- `UI/UIItem/` 目录下实际不存在 CardItem.cs（`CardItem` 仅在 `UI/Battle/CardItem.cs`），原任务书按 19 个脚本计数，故此处为口径差异之一。
- `LanguageTable` 未调用 `InitDefaultEntries`，`ApplyLanguage` 实际是空表遍历（当前语言切换**不生效**）。
- 任务书所述 "19 个 UI 脚本 / 172 张 PNG" 与实测（**18 个 / 171 张（GameRes 口径）**）存在 1 的偏差，以本文件实测为准。

---

（全文完）
