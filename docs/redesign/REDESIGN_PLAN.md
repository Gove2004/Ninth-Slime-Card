# 第九张史莱姆牌 · UI / 核心玩法重设计方案与实施计划

> 版本：v1.0（2026-10-01）
> 目标：彻底重做界面、UI 与核心玩法，并完成实际落地。**验收只看最终成果。**

---

## 0. 执行摘要

三条结论决定了整套方案：

1. **Unity MCP 实测不可用** → 放弃"靠 MCP 操作 Unity 本体"的思路，改用**代码驱动 UI**。
2. **现有 UI 大量内联在场景里**（`Home.unity` 433KB、`Battle.unity` 176KB，Prefab 仅 6 个）→ 这是改版最大的阻力，必须先立框架。
3. **HybridCLR 已移除、YooAsset 已是离线随包** → 现在改代码就是改包，编译即生效，迭代成本很低。

因此整体路径定为：**代码驱动 UI 框架 → 屏幕逐个迁移 → 玩法增强 → 美术重制 → 打磨验收**。

---

## 1. 现状基线（已实测）

| 维度 | 现状 |
|---|---|
| 引擎 | Unity 6（HEAD 记录 6000.4.4f1；本机 Editor 为 6000.3.25f1）|
| 平台 | Android + TapTap SDK（Core / Login / Achievement）|
| 资源 | YooAsset 2.3.18，`AutoOfflineMode` → 真机 `OfflinePlayMode`，**完全随包不联网** |
| 热更 | 已移除（HybridCLR 全清），`HotUpdate` 程序集 `autoReferenced:true` 直接进包 |
| 场景 | `Boot` / `Login` / `Home` / `Battle` 四个 |
| UI 脚本 | 19 个（Login 1 / Home 5 / Battle 6 / UIItem 6 / EventCenter）|
| Prefab | 仅 6 个 → **UI 主要内联在场景中** |
| 美术 | `Assets/GameRes/Arts/` 171 张 PNG |
| 数据 | CSV → `ConfigCore` |
| 状态层 | `GameCore` 静态跨场景、`BattleManager` 单例、`EventCenter` 字符串事件 |

**核心痛点**：界面搭在场景里 → 改版必须动场景 YAML；无设计系统（颜色/字号/间距无统一 token）；无多分辨率适配策略；玩法逻辑与 UI 表现耦合。

---

## 2. 关键技术决策

### 2.1 实现路径：代码驱动 UI（Programmatic UI）

**决策：界面由 C# 在运行时构建，而不是在编辑器里拖控件。**

理由：

| 方案 | 结论 |
|---|---|
| Unity MCP 操作编辑器 | ❌ 实测不可用（见 §5.1），且本仓库历史结论为"MCP 不导入/不编译新文件" |
| 手写 `.prefab` / `.unity` YAML | ❌ 极其脆弱：GUID、fileID 互引、Missing Script、fileID 冲突 |
| Unity CLI `-batchmode -executeMethod` | ⚠️ 可行但需先关闭正在运行的 Editor（工程锁），适合批量任务 |
| **代码构建 UI（选中）** | ✅ 编译即生效、可 diff、可回滚、可复现、无资源导入问题 |

### 2.2 目录结构

```
Assets/Scripts/UI/
├── Framework/
│   ├── UIBuilder.cs        声明式构建（链式 API）
│   ├── UIFactory.cs        常用控件（Button/Bar/Card/Toast）
│   ├── UIRouter.cs         屏幕栈与转场
│   ├── UILayer.cs          层级：Background / Screen / Overlay / Toast
│   └── SafeAreaFitter.cs   安全区适配
├── Theme/
│   └── ThemeSO.cs + Theme_Default.asset   颜色/字号/间距/圆角/动效时长
├── Screens/                LoginScreen / HomeScreen / BattleScreen /
│                           RoguelikeScreen / ResultOverlay / PauseOverlay
└── Components/             CardView / StatBar / IconButton / ResourceBar ...
```

**场景只保留**：一个根 `Canvas` + `EventSystem` + `UIRouter` 挂载点。所有界面运行时构建。

### 2.3 数据与表现解耦

沿用现有 `GameCore` / `BattleManager` 作为**状态源**，但引入一层 Presenter：

```
BattleManager(逻辑) → BattleStateChanged(事件) → BattleScreen(纯渲染)
```
UI 不再反向持有战斗逻辑，改用 SO 事件通道（`Assets/ScriptableObjects/Events/` 已有 `GameEventSO` 基础设施）。

---

## 3. 重新设计方案

### 3.1 设计支柱（三条，所有取舍以此裁决）

1. **一眼可读** —— 卡牌战斗信息密度高，必须强对比、大字号关键数值、颜色语义固定（增/减/危险/中性不混用）。
2. **每个回合都有决策** —— 抽牌、出牌、弃牌、留牌每个动作都有明确反馈与期待感；敌人行为可预判。
3. **随包即玩、秒进** —— 无网络等待；冷启动到可操作 < 3 秒。

### 3.2 UI 设计系统

- **色板**：背景四阶（底/卡/浮层/弹窗）、主色、危险、增益、禁用、文本三级（主/次/弱）；全部走 `ThemeSO`
- **字号阶梯**：H1 64 / H2 48 / H3 36 / H4 28 / Body 24 / Caption 20（1080×1920 基准）
- **间距栅格**：8pt 基准（8/16/24/32/48）
- **圆角与阴影**：统一 token，避免每个面板各写一套
- **动效时长**：Fast 120ms / Normal 200ms / Slow 320ms，统一缓动曲线
- **适配**：`CanvasScaler` = ScaleWithScreenSize，参考 1080×1920，Match 0.5；`SafeAreaFitter` 处理刘海/手势条
- **可访问性**：颜色语义不单靠红绿；点击热区 ≥ 88px；字号可调档

### 3.3 屏幕清单（重设计）

| 屏幕 | 重设计要点 |
|---|---|
| **Boot** | 极简：Logo + 细进度条，无多余文案 |
| **Login** | 单主视觉 + TapTap 登录唯一入口（无游客降级，失败留在登录页重试） |
| **Home** | 底部 Tab（对战 / 图鉴 / 成就 / 设置）+ 顶部资源条（奖杯）；替代当前按钮堆叠 |
| **Battle** | 上敌下我、中部战场、底部手牌扇形、右侧能量/回合指示、**顶部敌人意图预告** |
| **Roguelike 选牌** | 3 选 1 大卡位 + 详情浮层 + 跳过；展示卡牌与现有构筑的协同提示 |
| **Overlay 层** | 结算 / 暂停 / Toast 统一走 `UILayer.Overlay`，不再各自实现 |

### 3.4 核心玩法重设计（增强 + 局部重塑）

> 保持"卡牌 Roguelike"内核不变，提升**决策深度**与**反馈密度**。

| 改动 | 说明 | 幅度 |
|---|---|---|
| 敌人意图预告 | 下回合行为图标化（攻击/增益/蓄力），玩家可提前规划 | 中 |
| 能量曲线明确化 | 每回合能量与抽牌数可视化，弃牌堆循环可见 | 小 |
| 连击 / 共鸣 | 连续使用同类卡触发额外效果，强化"构筑感" | 中 |
| 战后构筑路径 | 三选一 + 遗物/被动槽 + 路线选择（普通/精英/事件/商店） | 大 |
| 结算反馈 | 奖杯结算动画、成就解锁 Toast、卡牌升级提示 | 小 |

⚠️ **待确认**：玩法是"增强现有"还是"重塑结构"。默认按上表（增强为主 + 一个大地图路线系统）。

---

## 4. 分阶段实施计划

### Phase 0 · 基线审计与设计冻结
- **目标**：摸清现状代码与数据契约，冻结设计方向
- **交付物**：① 现状审计报告（场景结构 / UI API / 玩法流程 / 风险点）② Design Pillars ③ `ThemeSO` token 表 ④ 各屏幕线框 ⑤ 玩法改动清单
- **验收标准**：审计报告覆盖全部 19 个 UI 脚本与 4 个场景；线框覆盖全部屏幕；**用户确认设计方向**

### Phase 1 · UI 框架落地
- **目标**：`UIBuilder` / `ThemeSO` / `UIRouter` / `UILayer` / `SafeAreaFitter` 可用
- **交付物**：框架代码 + `Theme_Default.asset` + **样板屏 `SettingsScreen`**（迁移现有设置页验证框架）
- **验收标准**：① Unity 编译零错误 ② 新增一个同类屏幕 ≤ 100 行 ③ 在 1080×1920 / 2400×1080 / 带刘海三档下不破版 ④ 样板屏功能与旧版等价

### Phase 2 · 核心屏幕重做
- **目标**：Login / Home / Battle / Roguelike / Overlay 全部迁移到新框架
- **交付物**：各 `*Screen.cs` + 场景瘦身（清空内联 UI，只留根节点）+ 迁移对照表
- **验收标准**：① 4 个场景可空场景启动全流程跑通 ② 旧 UI 脚本无残留引用 ③ 场景文件体积显著下降（如 `Home.unity` 433KB → < 30KB）

### Phase 3 · 玩法增强
- **目标**：意图预告、连击/共鸣、构筑路径、结算反馈
- **交付物**：玩法代码 + 数值配置（CSV/ConfigData）+ 平衡表
- **验收标准**：① 连续 3 局无阻断/卡死 ② 敌人意图与实际行为一致（自动化校验）③ 核心循环 ≥ 10 分钟仍需决策 ④ 用户试玩通过

### Phase 4 · 美术重制与接入
- **目标**：统一视觉风格
- **交付物**：UI 图标集 / 背景 / 卡面框 / 特效图 + 导入设置（尺寸、压缩、九宫格）+ 资源清单
- **验收标准**：① 风格统一（同一色板与描边语言）② 无拉伸/模糊 ③ 包体增量 ≤ 阈值 ④ 所有图有明确导入预设

### Phase 5 · 打磨与验收
- **目标**：动效、音效、性能、真机
- **交付物**：真机 APK + 验收报告（性能/包体/启动耗时）+ 已知问题清单
- **验收标准**：① 低端机 ≥ 30fps ② 冷启动到可操作 < 3s ③ 断网可玩（离线包验证）④ 无 Missing Script / 无 Console 报错

---

## 5. 美术生成与 Unity 操作的协调方案

### 5.1 Unity 操作：实测结论与三档方案

**实测（2026-10-01）**：
- `netstat` 中 **无 `127.0.0.1:8090` 监听记录**
- `curl` 各端点（`/`、`/mcp`、`/sse`、`/health`）均返回 **HTTP 502**，body 为 `upstream connect failed: 目标计算机积极拒绝 (os error 10061)` —— 即网关在转发，但 **Unity 侧没有 MCP 服务在应答**
- `mcp__unity-mcp` 工具检索不到可用端点

**结论：MCP 通路当前是断的。** 要用它，必须在 Unity 里手动启动桥接：`Window → MCP for Unity → Start/Connect`（并确认监听端口为 8090）。即便启动，本仓库历史经验表明该会话**不会为新文件触发 AssetDatabase 导入/编译**。

**三档方案（按优先级）**：

| 方案 | 做法 | 适用 |
|---|---|---|
| **A（默认）** | 我写 C#，Unity 聚焦时自动导入编译。不动预制体/场景 YAML | 全部常规开发 |
| **B** | Unity CLI `-batchmode -quit -executeMethod` + Editor 脚本，批量生成预制体/设置导入参数 | 需要"真 Unity 操作"的批量任务；**需先关闭运行中的 Editor**（工程锁） |
| **C** | 我写 Editor 菜单脚本（如 `Tools/重建 UI`），你在 Unity 里点一下 | 需要你来点确认的破坏性操作 |

**关于 MCP**：如果你愿意在 Unity 里启动桥接并把端口/行为确认为可用，我可以改为 MCP 路径；否则默认走 A。**这是我唯一需要你动手的地方。**

### 5.2 美术生成

前提：你此前明确说过"我生成的画很丑，不要主动画图"。本次你提到"可能需要重新生成美术资源"，所以按下面口径执行：

**策略：程序化优先 → 缺口再生成**

1. **纯代码图形**（优先）：圆角矩形、渐变、描边、进度条、图标底衬 —— 用 `UIBuilder` 直接画，零美术资源、零导入问题、体积极小。**现代扁平 UI 的 80% 可以这样完成。**
2. **生成图像**（仅限高容错的）：UI 图标、卡面框、纯色/渐变背景纹理。**不出角色立绘、不出复杂插画。**
3. **你来提供**：如果需要高品质卡牌插画/角色立绘，建议你提供或指定来源。

**交接规范**（保证生成物能无痛接入）：
- 命名：`ui_ico_{name}.png` / `card_frame_{rarity}.png` / `bg_{scene}.png`
- 尺寸：图标 128×128、卡面框 512×768、背景 1080×1920
- 导入：统一由 Editor 脚本（方案 B/C）批量设置 TextureType / 压缩 / 九宫格，不手工配
- 落盘：`Assets/GameRes/Arts/UI/{icons,frames,backgrounds}/`

### 5.3 协调流程（每阶段循环）

```
我：产出设计 + 代码 + 资源清单
 ↓
你（仅必要时）：① 启动 MCP 桥接 或 ② 在 Unity 里点一个菜单项
 ↓
Unity：导入 → 编译 → 运行
 ↓
我：读 Console 报错（通过你贴 或 MCP 若可用）→ 修复 → 循环
 ↓
阶段验收门 → 进入下一阶段
```

---

## 6. 风险与对策

| 风险 | 影响 | 对策 |
|---|---|---|
| 场景内联 UI 无法平滑迁移 | 旧界面引用丢失 | Phase 2 采用"新屏幕并行构建 → 切换入口 → 删除旧代码"，不做原地改造 |
| 序列化引用断裂（Missing Script） | 场景报错 | 代码驱动 UI 天然规避；迁移后用脚本扫描场景残留组件 |
| 玩法改动破坏存档兼容 | 玩家进度丢失 | `PlayerData` / `RunState` 加版本号 + 迁移函数 |
| 生成美术风格不统一 | 观感廉价 | 先定 token 与描边/圆角语言，再生成；统一后处理 |
| 工程版本回退（6000.4.4f1 → 6000.3.25f1） | 协作方无法打开 | 提交前与用户确认统一版本 |
| 大改动缺少验证手段 | 回归失控 | 每阶段设验收门；关键流程加自动化检查（意图一致性、流程可达性） |

---

## 7. 待用户确认项

1. **Unity 操作路径**：是否愿意在 Unity 里启动 MCP 桥接？否则默认走方案 A（代码驱动，无需你操作）。
2. **玩法改动幅度**：按 §3.4 的"增强为主 + 地图路线系统"，还是只做纯表现层重做？
3. **美术口径**：是否认可"程序化优先 + 只生成图标/背景"的策略？
4. **工程版本**：统一到 6000.4.4f1 还是 6000.3.25f1？

---

## 附：执行节奏

- Phase 0 可立即开始（基线审计已在进行）
- Phase 1 起每个阶段结束设**验收门**，未达标不进下一阶段
- 全程不 commit，除非用户明确要求

---

# v1.1 更新（2026-10-01 11:25）

## A. Unity MCP：**已打通并验证可用**（推翻 v1.0 结论）

用户已在 Unity 里启动 MCP 桥接。实测：

```
POST http://127.0.0.1:8090/mcp
initialize → 200，serverInfo = mcp-for-unity-server v3.4.7
tools/list → 26 个工具
manage_editor{telemetry_ping} → {"success":true,"message":"telemetry ping queued"}
read_console{count:8}        → 返回 8 条 Unity 真实控制台日志
```

**可用工具**（节选）：`manage_gameobject` / `manage_scene` / `manage_prefabs` / `manage_asset` /
`manage_components` / `manage_material` / `manage_graphics` / `manage_physics` / `manage_camera` /
`manage_build` / `manage_editor` / `manage_packages` / `refresh_unity` / `read_console` /
`execute_menu_item` / `batch_execute` / `create_script` / `manage_script` / `script_apply_edits` /
`apply_text_edits` / `validate_script` / `find_gameobjects` / `find_in_file` / `get_sha` /
`set_active_instance` / `manage_tools`

**接入方式**：该服务未注册为 WorkBuddy 连接器，走**原生 JSON-RPC 直连**。
已封装助手脚本：`.workbuddy/tools/mcp_unity.sh`（`list` / `schema <tool>` / `call <tool> '<json>'`）。

**修正后的实现路径（组合拳）**：

| 用途 | 手段 |
|---|---|
| UI 构建 | **代码驱动**（`UIBuilder`）—— 可 diff/回滚，不产生场景与 prefab 膨胀 |
| 编译验证 / 调试 | **MCP** `read_console` + `refresh_unity` |
| 运行验证 | **MCP** `manage_editor{play/stop}` |
| 资产导入设置 / 包管理 / 构建 | **MCP** `manage_asset` / `manage_packages` / `manage_build` |
| 批量预制体/场景操作 | **MCP** `manage_prefabs` / `manage_scene` / `batch_execute` |

**风险**：本机同时运行 3 个 `Unity.exe`，需用 `set_active_instance` 确认操作目标实例。

## B. 新增产品约束：**纯单机运行，不依赖任何服务器或后端**

依赖盘点结果：

| 依赖 | 现状 | 处理 |
|---|---|---|
| YooAsset | `AutoOfflineMode` → 真机 `OfflinePlayMode` | ✅ 已合规，保留 |
| HybridCLR | 已移除 | ✅ 已合规 |
| 存档 | `SaveCore` + `JsonSerializer`，本地 | ✅ 已合规 |
| 本地化 | `LocalizationCore`，本地配置 | ✅ 已合规 |
| **TapTap SDK** | 云端登录 + 成就同步（`TapTapCore.cs`：`LoginWithScopes` / `Achievement.Unlock` / `Increment`） | ❌ **须处理** |
| **GoveKits Network** | `Network/Http/HttpEngine.cs`（UnityWebRequest）+ `Network/Protocol/{Session/Rpc/Sync}`（ClientCore/ServerCore/RpcCore），当前**未被调用**（`GoveKitsManager.cs` 里是注释） | ⚠️ 死代码，建议移除或隔离 |

**决定**：
1. `GameCore.LocalOfflineMode` 已是 `true`（`GameCore.cs:8`），`LoginPage.cs:22/62` 已有离线分支 → **固化该路径**，移除可用 TapTap 云端的分支。
2. `TapTapCore.cs` 的云端调用（登录 / 成就同步）改为**纯本地**：本地玩家档案 + 本地成就存储（`PlayerData.achievementUnlocked` 已有）。
3. 视最终形态决定是否摘除 `com.taptap.sdk.*` 包依赖；**默认摘除**，以彻底满足"不依赖后端"。
4. GoveKits `Network` 模块：确认无调用后移除（共享包，需谨慎）。

## C. ⚠️ 当前硬阻塞：**工程编译不过**（必须先解）

`read_console` 实测：

```
Library\PackageCache\com.unity.2d.common@edfe77495b67\Runtime\InternalBridge\InternalEngineBridge.cs(51,55):
  error CS1501: No overload for method 'IsGPUSkinningEnabled' takes 1 arguments
Library\PackageCache\com.unity.2d.common@edfe77495b67\Runtime\InternalBridge\InternalEngineBridge.cs(66,48):
  error CS0117: 'SpriteRendererDataAccessExtensions' does not contain a definition for
                'SetBatchBoneTransformIndexAndLocalAABBArray'
Missing types referenced from component UniversalRenderPipelineGlobalSettings on game object ...
Default Renderer is missing, make sure there is a Renderer assigned as the default
  on the current Universal RP asset:UniversalRP   (×3)
```

**根因**：`ProjectSettings/ProjectVersion.txt` 为 **6000.4.4f1**，但本机编辑器是 **6000.3.25f1**；
`Library/PackageCache` 是按旧版本解析缓存的，`com.unity.2d.common` 与当前编辑器 API 不匹配。
URP 资产还额外缺 Default Renderer。

**修复选项（已作废，见下方实际结论）**：
- ~~C1 把项目统一回 6000.4.4f1~~ → **本机根本没装 6000.4.x**（只有 `D:\Unity Editor\6000.3.25f1` 与 `6000.0.59f2`），选项不成立
- ~~C2 删除 PackageCache 重解析~~ / ~~C3 两版都留~~ → 见下方已执行的精确修复

### ✅ P0-a 已解决（2026-10-01）

**根因**：`ProjectSettings/ProjectVersion.txt` 已被改回 6000.3.25f1，但 `Packages/packages-lock.json`
仍是 **ProjectVersion = 6000.4.4f1 时生成**的，把一批 **声明 `"unity":"6000.4"` 的包**钉死：
`2d.animation 14.0.3 / 2d.common 13.0.2 / 2d.psdimporter 13.0.2 / 2d.spriteshape 14.0.1 /
2d.tilemap.extras 7.0.1 / 2d.aseprite 4.0.1`。另有一个 **被 vendor 进仓库的 6000.4 专属包**
`Packages/com.unity.2d.tooling`（`"unity":"6000.4"`，强制 `2d.common 13.0.2`），以 embedded 形式
成为 2d.common 被顶到 13.0.2 的直接原因。

**为什么 URP 也被拖垮**：编辑器内置的 `Unity.RenderPipelines.Universal.Editor.asmdef:60` 引用了
`com.unity.2d.common`。2d.common 编译失败 ⇒ URP Editor 程序集连带失败 ⇒ 报出
`Missing types referenced from UniversalRenderPipelineGlobalSettings` 与
`Default Renderer is missing`（**下游症状，不是独立问题**）。

**执行的最小修复**：
1. `Packages/manifest.json`：移除 `com.unity.2d.tooling`（file: 行）；`com.unity.render-pipelines.universal`
   `17.4.0 → 17.3.0`（编辑器内置版）。**其余包一律不动**——实测它们声明的 `unity` 字段都是
   `2021.3 / 2022.3 / 6000.0`，本来就兼容。
2. 把 embedded 的 `Packages/com.unity.2d.tooling/` 与过期的 `Packages/packages-lock.json`
   移到 `.workbuddy/backup/`（**用 mv 不用 rm**，本环境有 safe-delete 拦截）。
3. 让 UPM 重新解析 → `[Package Manager] Done resolving packages in 71.82s` + `Lock file was created`
   + `Registered 77 packages`。结果：`2d.common 13.0.2 → 12.0.4`、`2d.tooling → registry 1.0.4`、
   `URP = 17.3.0 (builtin)`，旧缓存目录被自动清理。
4. **URP Default Renderer 的二次修复（关键）**：重新解析后引用仍是 null。用 `execute_code` 实测确认
   `m_RendererDataList[0] == NULL`，而 `LoadAssetAtPath("Assets/Settings/Renderer2D.asset")` 正常。
   结论：**资产文件本身没坏**（磁盘 GUID 一直是对的），是包崩坏期间 `Renderer2DData` 类型不可用，
   Unity 加载时把该引用解析成 null 并缓存住了。重新导入无效，**用 API 重新指派并 SaveAssets 才修复**。

**顺带修掉的独立编译错误**：`Assets/Editor/Il2CppModuleChecker.cs:93` CS0122
（`ShowInstallDialog()` 是 private 却被 `Il2CppModuleBuildGuard` 调用）；
同时把里面硬编码的 `6000.4.4f1` 改为动态读取 `Application.unityVersion`。

**验收（MCP 实测）**：控制台 **0 error / 0 warning**；域重载后复验
`m_RendererDataList = len 1, [0] = Renderer2D [Renderer2DData]` 稳定。

**新增工具**：`.workbuddy/tools/mcp_raw.py`（Python 版 MCP 调用，避免 bash JSON 转义问题）；
`mcp_unity.sh` 的 `call` 子命令原有 bug（`${3:-{}}` 被 bash 解析成多一个 `}`）已修复。

## D. 更新后的阶段顺序

原 Phase 0–5 不变。P0-a / P0-b 已完成：

```
P0-a  解除编译阻塞                                    ✅ 已完成
P0-b  基线审计（代码 + 后端依赖测绘）                  ✅ 已完成 → docs/redesign/AUDIT_BASELINE.md
P0-c  设计冻结                                        ← 进行中
P0-d  纯单机化改造（TapTap 云端调用摘除）              ← 下一步
P1    UI 框架 + 样板屏
...
```
