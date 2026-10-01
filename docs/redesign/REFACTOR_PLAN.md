# 《第九张史莱姆牌》彻底重构方案 · v2.0（联机化）

> 日期：2026-10-01
> 状态：**待用户确认 §1 方向反转与 §8 四个待决项后执行**
> 取代：`REDESIGN_PLAN.md`（v1.0/v1.1）中「纯单机化、摘除 TapSDK」的方向
> 参照基线：`AUDIT_BASELINE.md`（本次已复核，结论仍成立）

---

## 0. 一句话结论

本次重构的方向与 2026-10-01 上午的既有方案**完全相反**：

| 维度 | 上午方案（REDESIGN_PLAN v1.1） | 本次指令 | 处理 |
|---|---|---|---|
| TapSDK | **摘除** `com.taptap.sdk.*`，纯本地化 | **只保留**与 TapSDK 相关的**联机**功能 | 方向反转，原 P0-d 作废 |
| 网络 | 零后端、离线可玩 | 以 TapSDK 联机（OnlineBattle）为玩法基座 | 方向反转 |
| 热更 | 保留 YooAsset 离线随包 | **移除全部热更新逻辑** | 范围扩大 |
| 玩法 | 增强现有卡牌 Roguelike | **重新设计并重构全部玩法** | 范围扩大 |
| UI/美术 | 重做 UI | **重做全套美术资源** | 范围扩大 |

---

## 1. ⚠️ 需要你确认的方向反转

上午的方案把 TapSDK 定为「必须摘除的联网风险」，并已规划 `P0-d 纯单机化改造`。
本次指令要求「只保留与 TapSDK 相关的**联机功能**」。

我的理解（**请确认**）：`联机` = TapTap 官方 **OnlineBattle（多人联机）** 模块，
即游戏改为**以 TapSDK 联机对局为核心玩法**的产品，而不是把 TapSDK 降级为「登录 + 成就」两个附属能力。

依据：TapSDK 官方确实提供该模块，包名 `com.taptap.sdk.onlinebattle`（路径 `/OnlineBattle`），
与项目现有 TapSDK 4.10.0 同源，能力包括：房间创建/匹配/加入、`SendCustomMessage` 玩家通信、
确定性随机数、帧同步（高级）。**它当前未安装。**

> 如果你的本意是「保留 TapSDK 的登录/成就，但仍是单机游戏」，那么 §4 玩法设计与 §3 边界需整体下调，
> 请直接说明——那会是另一套（更小）的方案。

---

## 2. 现状梳理（本次实测复核）

### 2.1 工程与运行时

| 项 | 实测值 |
|---|---|
| Unity | ProjectSettings 记录 **6000.3.25f1**；本机 Editor 可用版本 `6000.3.25f1` / `6000.0.59f2` |
| 构建目标 | Android（`Builds/A/*.apk`、`Bundles/Android/**` 已有产物） |
| Unity MCP | **已连通可用**。`read_console` 正常返回；控制台 **0 error / 0 warning** |
| MCP 脚本导入 | ✅ **实测可导入编译新文件**（写入 `__McpProbe.cs` → `refresh_unity` → `.meta` 自动生成 → 反射可解析类型 → 已清理） |
| 热更现状 | HybridCLR **包与代码已完全移除**；`Boot.cs:151` 注释明示「代码热更已移除」 |

> **重要修正**：仓库历史记忆（2026-07-30/31）称「MCP 不导入/不编译新文件、MCP 不适用本工程」。
> 本次实测**已推翻**：`.meta` 正常生成、`Assembly-CSharp.dll` 正常重建、`execute_code` 可反射到
> `Assembly-CSharp` / `HotUpdate` 内的业务类型（`GameCore` / `BattleManager` / `TapTapCore` / `BaseCard`）。
> 因此**「通过 Unity MCP 操控与实现」这一要求在当前环境下是可执行的**。

### 2.2 程序集划分（4 个自建 asmdef）

| asmdef | 名称 | 内容 | autoReferenced |
|---|---|---|---|
| `Assets/Scripts/HotUpdate/HotUpdate.asmdef` | `HotUpdate` | 全部玩法/UI，约 **7,900 LOC** | true |
| `Assets/Scripts/Core/ArchitectureCore.asmdef` | `NinthsSlime.ArchitectureCore` | 死代码（仅 `UnitBehaviour` 被继承） | true |
| `Assets/ScriptableObjects/ScriptableObjects.asmdef` | `NinthsSlime.ScriptableObjects` | 死代码（零引用） | true |
| `Assets/Scripts/Boot.cs` | 落入 `Assembly-CSharp` | 唯一启动层脚本（263 行） | — |

`HotUpdate` 直接引用了 **6 个 TapSDK 程序集**（Core/Mobile ×2、Achievement/Mobile ×2、Login/Mobile ×2）。

### 2.3 场景

| 场景 | 行数 | 内联对象 | 加载方式 |
|---|---|---|---|
| `Boot.unity` | 1,244 | 13 | **Build Settings（唯一）** |
| `Login.unity` | 980 | 8 | YooAsset 地址 |
| `Home.unity` | **14,135** | 134 GO / 171 MB | YooAsset 地址 |
| `Battle.unity` | 5,729 | 47 GO / 72 MB | YooAsset 地址 |
| `Assets/Home.unity` | 1,241 | — | **游离遗留** |

- 全仓仅 **6 个 Prefab**（AechiItem / Button / CardItem / ChoiceItem / Level / MessageToast）
  → **UI 主体内联在场景 YAML 中**，是本次重构最大的工程阻力。
- CanvasScaler 四场景一致：`800×600`、`Match=Width`；**全仓无安全区适配**。

### 2.4 玩法与数据

- **战斗内核**：`BattleManager`(447) + `BaseCharacter`(633) + `UnitAttributeEffect`(586) + `Player`/`Enemy`
- **卡牌**：`BaseCard` 抽象基类 + `Cards_1..8.cs`（161 张卡），`CardFactory` 反射注册
- **配置**：`第九张史莱姆牌-工作表1.csv`（161 卡）、`工作表2.csv`（199 条成就）→ GoveKits `ConfigCore`
- **进度**：`RunState`（`currentLv` + `playerDeckIds`）单机 Roguelike 循环
- **状态层**：`GameCore` 全静态跨场景 + `EventCenter` 字符串事件总线

### 2.5 美术与音频（体积是硬问题）

| 目录 | 数量 | 体积 | 备注 |
|---|---|---|---|
| `Arts/Images/Card` | **159 PNG** | **230.49 MB** | 全部 1086×1448，与卡名严格对应 |
| `Arts/Images/UI` | 7 | 4.12 MB | 含 2 张 `Generated Image …` 乱名垃圾 |
| `Arts/Images/Character` | 2 | 2.23 MB | |
| `Arts/Images/Level` / `BG` | 2 / 1 | 2.09 MB | |
| `Arts/Audios` | 4 mp3 + 6 ogg | 22 MB | 中文文件名，按名加载 |
| `Arts/Fonts` | Alibaba PuHuiTi 2.0 | 20 MB | ttf + TMP asset |
| **合计** | 171 PNG | **≈239 MB** | **无图集、无九宫格（仅 Panel.png 1 张）** |

### 2.6 依赖清单

- **保留价值高**：DOTween（表现动画）、UniTask（异步）、TextMeshPro、URP 2D、InputSystem、GoveKits（本地包）
- **GoveKits 可复用模块**：`Core/{Singleton,Log,Time,Random,Pool,Scene,Event}`、`Storage/{Config,Save,Res}`、`Unit/{Unit,Attribute,Ability,Mark,Reaction}`
- **GoveKits 无关模块**：`Network/**`（HttpEngine / Rpc / Sync，游戏侧零调用，`GoveKitsManager` 本身也不在场景中，属死代码）
- **待定**：YooAsset 2.3.18、TapSDK core/login/achievement 4.10.0

---

## 3. 保留 / 删除边界（含处理方式与风险等级）

### 3.1 ✅ 保留（KEEP）

| 模块 | 处理 | 理由 |
|---|---|---|
| `TapSDK.Core` | 保留 + 升级到 4.11.0（联机同源） | 联机前置依赖 |
| `TapSDK.Login` | 保留，重构调用入口 | 联机需先登录拿身份 |
| `TapSDK.Achievement` | 保留，**降级为可选**（对局结算后异步上报，失败不阻断） | TapTap 侧成就展示 |
| `Assets/TapSDK/**/link.xml` | 保留并新增 OnlineBattle 版 | IL2CPP 裁剪保护 |
| `TapTapCore.cs` | **重写**为 `TapOnlineService` 的登录子模块 | 核心复用点：`Initialize` / `LoginAsync` / 客户端凭据 |
| GoveKits 基础模块 | 保留 | 配置/存档/日志/时间/随机/对象池/单例 |
| DOTween / UniTask / TMP | 保留 | 表现层与异步基础设施 |
| URP 2D + `Assets/Settings/**` | 保留（已修好，勿再动） | 渲染管线 |
| CSV → `ConfigCore` 数据驱动管线 | 保留并扩展 | 卡牌/成就数据可配 |
| `Alibaba_PuHuiTi_2.0` 字体 | 保留 | 中文显示 |
| `Arts/Audios/Sound`（179 KB） | 保留 + 规范化命名 | 音效可复用 |
| `Assets/Editor/{AndroidSdkPathBootstrap, Il2CppModuleChecker}` | 保留（已修过 CS0122） | 构建工具链 |
| `rules/` `skills/` `CLAUDE.md` | 保留并按新架构更新 | AI 协作上下文 |
| `.workbuddy/` | 保留 | 工程数据目录 |

### 3.2 ❌ 删除（DELETE）

| 模块 | 处理方式 | 风险 |
|---|---|---|
| HybridCLR 残留（`Assets/HybridCLRGenerate/**`、AOT 引用、link 片段） | 直接删（若存在） | 低 |
| **YooAsset 全套**：`com.tuyoogame.yooasset` 包、`Assets/AssetBundleCollectorSetting.asset`、`Assets/StreamingAssets/yoo/**`、`Bundles/**` | 删包 + 归档资源 | **中**（需确认，见 §8-Q4） |
| `Assets/Scripts/HotUpdate/**` 的**命名与 asmdef 边界** | 拆解重排（见 §4.1） | 中 |
| `Assets/Scripts/Core/**`（ArchitectureCore 死代码） | 整目录删 | 低（零引用，仅 `UnitBehaviour` 被继承） |
| `Assets/ScriptableObjects/**`（零引用） | 整目录删 | 低 |
| `EventCenter.cs` 字符串事件总线 | 删，改用类型安全事件 | 低 |
| 单机 Roguelike：`RunState`、`RoguelikeChoicePanel`、`Enemy` 关卡成长、选牌循环 | 删（玩法重做） | 中 |
| 旧战斗内核：`BattleManager`、`BaseCharacter`、`Effect/*`、`Player`、`Enemy` | 删（结算内核重写） | **高**（复用其设计经验，代码不保留） |
| 全部旧 UI 脚本（18 个）+ 场景内联 UI 节点 | 删（UI 重做） | **高**（Home.unity 14k 行） |
| `Assets/Home.unity`（游离遗留） | 删 | 低 |
| `Arts/Images/Card/**`（159 张 / **230 MB**） | **归档**到 `.workbuddy/backup/`，不直接删 | **高**（见 §8-Q3） |
| `Arts/Images/UI/` 中 2 张 `Generated Image …` 垃圾 | 归档 | 低 |
| `mcp-server/**`（旧 Cursor 时代的本地 MCP，与 com.coplaydev.unity-mcp 无关） | 归档 | 低 |
| `LanguageTable.cs`（`InitDefaultEntries` 从未调用，语言切换实际不生效） | 删，重做本地化 | 低 |
| `Bundles/**`、`Builds/A/**`、`Temp/**`、`Logs/**` | 归档/清 | 低 |
| `HomePage` 的 18 个序列化字段与 `"OverTurn"` 名字查找等硬耦合 | 随 UI 重做消失 | — |

> **删除纪律（强制）**：本仓库历史上有过两次因 git 操作导致 `assets/` 目录整体丢失的事故。
> 因此：① 所有删除**先移动到 `.workbuddy/backup/<日期>/`**，不 `rm`；
> ② 批量操作前先 `dangerouslyDisableSandbox` 记录文件数与体积基线；
> ③ 禁用 `git mv` / `git rm` / `git checkout` / `git restore` 等会重建工作区的命令；
> ④ 每批次后比对文件清单。**230 MB 卡图与 22 MB 音频的处置必须逐条确认。**

### 3.3 ⚠️ 改造（TRANSFORM）

| 模块 | 改造方向 |
|---|---|
| `Boot.cs` | 去掉 YooAsset 包流程；变为「初始化服务 → 登录 → 进大厅」精简启动器 |
| `Boot.unity` | 去除进度条/更新文案，改为 Logo + 极简进度点 |
| 场景加载 | `ResCore.LoadSceneAsync`（YooAsset 地址）→ 标准 Build Settings 场景索引加载，**全部场景入 Build Settings** |
| 卡牌数据 | CSV 字段保留（`id/名称/系列/费用/数值1-3/描述`），**数值全部按 PvP 重调**，新增 `速度` 列 |
| 成就数据 | CSV 保留，触发条件从「奖杯购买」改为「对局达成」 |
| 音频 | 规范化英文命名，改为 `AudioService` 显式引用而非地址字符串 |
| 美术管线 | 引入 **SpriteAtlas** + 统一导入预设（压缩格式/尺寸/九宫格） |

---

## 4. 重构方案

### 4.1 目标架构

**分层与依赖方向（单向，禁止反向引用）**

```
Slime.Presentation  ──▶ Slime.Online ──▶ Slime.Core
       │                     │               │
       └─────────────────────┴───────────────┘
                        Slime.App（组装根）
```

| 程序集 | 内容 | 关键约束 |
|---|---|---|
| `Slime.Core` | 战斗状态机、结算器、卡牌定义、确定性随机、协议模型 | **`noEngineReferences: true`** — 纯 C#，可在 EditMode 纯测 |
| `Slime.Online` | TapSDK 联机封装、房间服务、消息收发、权威校验 | 唯一允许引用 TapSDK 的程序集 |
| `Slime.Presentation` | UI 框架、屏幕、组件、表现层动效 | 不直接引用 TapSDK |
| `Slime.App` | Boot、服务定位、场景路由 | 组装根，唯一引用全部 |

**目录结构**

```
Assets/
├── Settings/                          # 渲染/输入（保留）
├── GameRes/
│   ├── Scenes/{Boot, Lobby, Battle}.unity    ← 全部进 Build Settings
│   ├── Configs/*.csv
│   ├── Prefabs/{UI, Battle}
│   └── Arts/{UI, Icons, Cards, Characters, BG, Fonts, Audios}
└── Scripts/
    ├── Core/          (Slime.Core)          纯 C#，零 UnityEngine
    │   ├── Battle/    BattleState, TurnResolver, EffectResolver, StateHash
    │   ├── Cards/     CardDef, ICardEffect, CardDatabase, CardPool
    │   ├── Rng/       DeterministicRng
    │   └── Protocol/  BattleMessage, CommitPacket（双端同构序列化）
    ├── Online/        (Slime.Online)        TapSDK 联机封装
    │   ├── TapOnlineService.cs   连接/生命周期
    │   ├── BattleEventHandler.cs ITapBattleEventHandler 实现
    │   ├── RoomService.cs        创建/匹配/加入/准备/踢人
    │   └── NetBattleSession.cs   提交/广播/种子/哈希校验/掉线
    ├── Presentation/  (Slime.Presentation)
    │   ├── Framework/ ScreenStack, UIBuilder, ThemeSO, SafeAreaFitter
    │   ├── Screens/   LobbyScreen, BattleScreen, ResultScreen, SettingsScreen
    │   └── Components/ CardView, HpBar, EnergyPips, TurnTimer, PlayerPlate
    ├── Services/      SaveService, AudioService, AchievementService, LocalizationService
    └── App/           (Slime.App) Boot, AppRoot, ServiceLocator, SceneRouter
└── Tests/
    ├── EditMode/      (Slime.Core.Tests)  结算/协议/随机 纯逻辑测试
    └── PlayMode/      (Slime.Tests)       流程冒烟
```

**关键收益**：消除 `GameCore` 静态上帝类、消除 `MonoSingleton` 隐式跨场景、消除字符串事件总线、
消除字符串场景/资源地址加载、**玩法内核可脱离 Unity 单测**。

### 4.2 热更新逻辑的移除范围（逐项）

| 热更相关 | 现状 | 处理 |
|---|---|---|
| HybridCLR 包 | 已移除 | 无需动作（确认无残留目录） |
| HybridCLR 生成物 / AOT 泛型引用 / link.xml | 无 `HybridCLRGenerate` 目录 | 确认即删 |
| 热更程序集边界 `HotUpdate.asmdef` | 仍存在 | **拆解**为 §4.1 的 4 个普通程序集 |
| YooAsset 资源包更新流程 | `Boot.cs` 走 `PackageWorkflowAsync` | **移除**，`Boot` 不再有更新阶段与进度回调 |
| YooAsset 场景地址加载 `ResCore.LoadSceneAsync("Home")` | 字符串契约 | **移除**，改用 Build Settings 场景 |
| YooAsset 资源地址加载 `LoadAssetSync("Card_{名}")` | 字符串契约 | **移除**，改用显式引用 / `Resources` |
| `AssetBundleCollectorSetting.asset` / `StreamingAssets/yoo` / `Bundles/` | 存在 | **移除** |
| `AudioManager` 的 `"Audio/{name}"` 地址加载 | 存在 | 移除，改显式引用 |
| `Assets/Editor/YooAssetQuickBuild.cs` | 存在 | 移除 |

**结论**：本项目「热更新」的实质残留 = **YooAsset 资源热更管线 + 伪热更程序集划分**。二者全部拆除后，
编译即生效、场景进 Build Settings、资源直接引用，与「无热更」语义完全一致。

### 4.3 新玩法设计：《第九张史莱姆牌 · 联机》

> 保留 IP 与「史莱姆 + 卡牌」题材（数据与美术资产可部分复用），**重做规则内核**。

**定位**：移动端 **1v1 同时出牌回合制卡牌对决**，单局 60–120 秒。

**为什么适配 TapSDK 联机（这是选型的决定性依据）**

TapSDK OnlineBattle 的通信模型是「房间 + 自定义消息」，**不是**帧同步（帧同步是标注为高级的可选项），
且有 **15 次/秒**的 `SendCustomMessage` 频率上限。因此：

| 玩法形态 | 适配度 |
|---|---|
| 实时动作 / 格斗 / 射击 | ❌ 需帧同步，频率受限，移动网络抖动敏感 |
| **回合制 / 卡牌 / 棋类** | ✅ 官方文档明确列为适用场景；消息量极低 |
| 我方设计：**同时暗牌 + 揭示结算** | ✅✅ **天然隐藏 RTT**——等待时间即"思考时间"，网络延迟不表现为卡顿 |

**设计支柱**

1. **双端确定性** — 同一输入必得同一结果（房主权威 + 确定性种子 + 状态哈希校验）
2. **每回合都是一次读心** — 同时暗牌，猜对手、留后手、骗反制
3. **60 秒进一局、两分钟见胜负** — 无等待、无加载、无匹配长尾

**对局规则（v1 初版，数值待平衡）**

```
双方初始：HP 30，牌库 20 张，起手 5 张，能量上限 1（每回合 +1，上限 +2/回合）

回合流程：
 1) 双方同时从手牌暗选 1 张（可「蓄力」跳过）
 2) 双方提交 → {round, slot, seed, hash}
 3) 揭示，按【速度】降序结算（快3 / 中2 / 慢1）
 4) 同速时由「回合种子」决定先后（确定性）
 5) 各抽 1 张，进入下一回合

IP 记忆点 ——「第九张」机制：
 单局中每方打出的第 9 张牌触发【史莱姆觉醒】，本局永久强化（三选一，写入状态哈希）

防拖延：
 第 8 回合起进入【坍缩】，每回合双方各扣 2 HP 且递增

胜负：一方 HP ≤ 0；或 15 回合后 HP 高者胜（平局按剩余手牌数）
```

**卡池重做**

- 现有 8 系列 `初始 / 七罪 / 血族 / 坚固 / 科技 / 种子 / 暗影 / 时序` **保留为 8 大流派**
- 卡池从 161 张**精简重排到 60–80 张**——原卡池含大量为「单机 AI 关卡成长」设计的冗余卡
- 全部数值按 PvP 重调；**移除**为单机设计的「对敌 Lv 缩放」逻辑
- CSV 新增 `速度` 列；`CardFactory` 的反射注册机制改为显式注册表（可 AOT 安全、可测试）

**联机流程（TapSDK OnlineBattle 落地映射）**

```
Login(TapTap) ─▶ TapBattleClient.Initialize(handler) ─▶ Connect() ─▶ 保存 myPlayerId
                                    │
                              LobbyScreen
                     ┌──────────────┴──────────────┐
              快速匹配 MatchRoom            创建/加入房间
              (maxPlayerCount=2,          CreateRoom / GetRoomList / JoinRoom
               matchParams: 段位)                    │
                     └──────────────┬──────────────┘
                        UpdatePlayerCustomStatus("ready")  ×2
                        房主 UpdateRoomProperties(规则/卡池)
                        房主广播 {"t":"start", seed} ─▶ BattleScreen
                                    │
                     每回合：SendCustomMessage({t:"commit", r, slot, seed, h})
                     双端执行同一个 TurnResolver.Resolve(state, a, b, seed)
                     比对 StateHash ── 连续 2 次不一致 ─▶ 房主下发全量快照重同步
                                    │
                     结束 ─▶ LeaveRoom ─▶ ResultScreen ─▶ 奖杯/成就上报 ─▶ Lobby
```

**关键同步设计（重要，避免踩坑）**

- **只传意图，不传状态**。每回合消息体 ≤ 200 字节，远低于 2048 字节上限
- **房主权威**：房主每回合生成 `seed` 并随自己的提交广播；非房主校验 `seed` 序号连续
- **状态哈希**：结算后对 `(hp, energy, handCount, deckTopHash, round, awakeFlag)` 求哈希并随下一回合提交附带
- **频率**：每回合 ≤ 3 条消息（本回合提交 + 哈希确认 + 下回合种子），对比 15/s 上限余量巨大
- **掉线处理**：`OnPlayerOffline` → 5 秒宽限；超时判负，记录为「对手掉线胜」
- **错误处理**：监听 `OnBattleServiceError`；`OnDisconnected` 网络异常时按官方建议重连
- **本地立即反馈**：提交后本地立刻播放"已锁定"表现，不等待对端

**单机陪练（必做，非可选）**

因为 `Slime.Core` 是纯 C#、与网络解耦，**AI 只是另一个 `CommitSource` 实现**。
因此同一套内核可以跑：
- 联机 PvP（对端提交来自网络）
- 单机陪练 AI（对端提交来自本地 AI）
- EditMode 自动化回归（对端提交来自脚本/录制的对局回放）

这一条同时解决了「联机功能在 Editor/单机上难以调试」的问题，是本次架构设计的核心取舍。

### 4.4 美术重设计方案

**视觉方向：「牌桌剧场」暗色扁平矢量 + 高对比**

- **色板**：8 流派各一主色（沿用 8 系列语义）+ 统一中性阶；全部收敛为 `ThemeSO` token
- **圆角/描边语言**：统一 `8 / 12 / 16` 圆角与 1px 描边，全 UI 一致
- **卡面**：程序化构建 = 九宫格卡框（每流派一张）+ 系列色带 + 费用/速度角标 + 中心插画位 + 描述区
- **动效**：卡牌入手上浮、出牌锁定/揭示翻面、伤害数字、觉醒演出，统一时长 token（120/200/320ms）

**技术指标（相对现状的量化目标）**

| 指标 | 现状 | 目标 |
|---|---|---|
| 卡图体积 | 230.49 MB / 159 张 | **< 25 MB** |
| 单张卡图尺寸 | 1086×1448 | 512×720（ASTC 6×6 / ETC2） |
| 图集 | 无（0 个） | 卡框/图标按流派合批 |
| 九宫格 | 1 张 | 卡框 + 面板 ≥ 8 张 |
| UI 位图依赖 | 大量 PNG | 圆角/渐变/分割线改 **程序化 SDF**，仅图标用 PNG |

> **产出策略分三档，需你选（见 §8-Q3）**：
> **A. 程序化优先（推荐）**：卡框/面板/背景/图标全部由 shader + 代码绘制与生成脚本产出，
>   可复现、可 diff、体积最小，且**完全由 MCP 可控**。
> **B. 程序化 + 少量生成图**：A 为主，卡牌中心小插画/角色/图标用图像生成补充（不出复杂大插画）。
> **C. 全套生成图**：159 张卡图全部重新生成。**我不建议**——成本高、风格一致性难保证，
>   且你此前明确表示过我生成的画不好看。

---

## 5. 实施步骤（Phase，每阶段设验收门）

| Phase | 目标 | 主要交付物 | 验收门 |
|---|---|---|---|
| **P0** 冻结与清理 | 方向冻结、备份、死代码清除 | 基线备份快照；`ArchitectureCore` / `ScriptableObjects` / 游离场景 / `mcp-server` / 缓存归档 | 控制台 0 error；备份清单可核对 |
| **P1** 拆热更 | 去 YooAsset 与伪热更边界 | 4 个新程序集 + 目录重排；`Boot` 精简；4 场景全部进 Build Settings；资源改显式引用 | **空场景启动全流程可跑通**；全仓 grep 无 YooAsset / `ResCore.LoadSceneAsync` 残留 |
| **P2** 打通联机 | TapSDK 联机链路可用 | `com.taptap.sdk.onlinebattle` 接入；`Login → Connect → CreateRoom → 准备 → 进入 Battle` 最小闭环；`link.xml` | **两台设备/两账号可同房**；`OnCustomMessageReceived` 双向收发实测；后台已开通联机服务 |
| **P3** 内核 | 玩法内核落地且可测 | `Slime.Core` 全套 + CSV 新卡池 + AI 陪练 + EditMode 测试 | **确定性测试通过**：同一 `(state, a, b, seed)` 1000 次结果与哈希恒定；对局 AI 自对弈 100 局无死锁 |
| **P4** 联机对战 | 权威结算与异常处理 | `NetBattleSession` 提交/种子/哈希/重同步/掉线判负 | 双端对局 20 局，哈希零分歧；断网/重连/掉线场景各有正确降级 |
| **P5** 表现层 | 全部屏幕重做 | `ScreenStack` + `ThemeSO` + Lobby/Battle/Result/Settings；场景瘦身 | 1080×1920 / 2400×1080 / 带刘海三档不破版；`Battle.unity` 从 5.7k 行降到 < 400 行 |
| **P6** 美术 | 全套视觉资产 | 程序化卡框/面板/背景 + 图标集 + 导入预设 + 图集 | 卡图体积 < 25 MB；风格统一（同色板同描边）；无拉伸模糊 |
| **P7** 打磨验收 | 真机与性能 | APK + 验收报告 | 低端机 ≥ 30fps；冷启动到可操作 < 3s；无 Missing Script；无 Console 报错 |

**执行纪律**
- 全程 **禁用 `git mv` / `git rm` / `git checkout` / `git restore`**（历史事故根因），移动文件用 bash `mv`
- 每个 Phase 结束跑一次「文件清单 + 体积」基线比对
- 不 commit，除非你明确要求

---

## 6. 风险与对策

| 风险 | 等级 | 对策 |
|---|---|---|
| **OnlineBattle 服务未在 TapTap 后台开通** | **高** | P2 前置动作：先在开发者后台开通「联机」服务；不通则整个玩法方向需回退（§8-Q1 必须先定） |
| **Editor 内联机不可用**（SDK 常需真机/移动端） | **高** | Core 纯 C# + AI 陪练 → **开发期不依赖网络**；联机验证放真机，P3 可在 Editor 完成 |
| **单账号无法自测双端** | 中 | 需要第二个 TapTap 账号 + 第二台设备（或同机双开需确认 SDK 支持） |
| 230 MB 卡图处置不当导致资产丢失 | **高** | 只归档不删除；先记基线；分批 ≤ 10 项；每批后比对 |
| Home.unity 14k 行内联 UI 迁移 | 中 | **不做原地改造**：新屏幕并行构建 → 切换入口 → 删旧场景，走"替换"而非"迁移" |
| 玩法重做导致存档不兼容 | 低 | 玩法全换，`PlayerData` 直接升版本并重置（无历史包袱） |
| MCP 误操作到非目标 Unity 实例 | 中 | 每次操作前 `set_active_instance` 确认；破坏性操作先 `read_console` + 截图确认 |
| 美术风格不统一 | 中 | 先冻结 `ThemeSO` token 与描边/圆角语言，再批量产出 |
| 包体与内存（现 239 MB 美术） | 中 | P6 用图集 + ASTC 压缩；P7 出包体报告 |

---

## 7. 与既有方案的关系

- `AUDIT_BASELINE.md`：**继续有效**（现状事实全部复核通过）
- `REDESIGN_PLAN.md` v1.0/v1.1：**§A（MCP 可用性）、§C（编译阻塞修复）继续有效**；
  **§B（纯单机化 + 摘除 TapSDK）作废**，由本文件的 §4.2/§4.3 取代
- 本文件为**当前唯一有效的实施方案**

---

## 8. 待你确认（阻塞执行）

| # | 问题 | 我的建议 |
|---|---|---|
| **Q1** | 「联机功能」是否指 **TapSDK OnlineBattle 多人联机**（游戏改为联机对局产品）？还是仅指「保留 TapSDK 登录/成就，游戏仍单机」？ | 按**联机对局**理解并已按此设计，但这是方向性判断，**必须确认** |
| **Q2** | 是否允许**安装** `com.taptap.sdk.onlinebattle`（并可能把 TapSDK 从 4.10.0 升到 4.11.0）？你的规则是「安装必须先经你同意」 | 需要安装；建议同时确认升级版本 |
| **Q3** | 159 张卡图（230 MB）如何处置？美术产出走 A（程序化）/ B（程序化+少量生成）/ C（全生成）哪一档？ | 卡图**归档不删**；走 **A 或 B**，不建议 C |
| **Q4** | 「移除全部热更新逻辑」是否包含**拆除 YooAsset 资源热更管线**（场景改 Build Settings、资源改显式引用）？ | 建议**一并拆除**，这才是彻底无热更 |
| **Q5**（次要） | TapTap 开发者后台是否已为该项目**开通「联机」服务**？ | P2 的前置条件，需你确认 |

---

（方案完）
