# 第九张史莱姆牌 (Ninth Slime Card)

Unity 6 卡牌 Roguelike 对战游戏。

## 核心玩法

- **Roguelike 战斗循环**：Lv.1 → 击败敌人 → 肉鸽选牌（5 轮：加牌 3 选 1 / 删牌 / 跳过）→ Lv.2 → ... → 失败则进度重置
- **161 张卡牌**：8 个系列（初始、七罪、血族、坚固、科技、种子、暗影、时序）
- **敌人按 Lv 缩放**：HP +5/级，法力恢复每 3 级 +1，牌组规模和系列范围随等级扩展
- **玩家成长**：MaxHP +1/级，每回合抽 2 张牌、恢复 2 点法力

## 局外系统

- **奖杯**：战斗胜利按 Lv 获得，用于购买成就
- **成就**：48 条旧版成就，消耗奖杯购买解锁，同步 TapTap
- **存档**：PlayerData 持久化奖杯、成就状态、局内进度（Lv + 牌组）

## 技术栈

- Unity 6 (`6000.0.59f2`)
- HybridCLR 热更新
- YooAsset 资源管理
- GoveKits 运行时框架（`MonoSingleton`、`ConfigCore`、`SaveCore`、`ResCore`、`UnitEffect`）
- TapTap SDK（登录 + 成就）
- TextMeshPro UI

## 场景流程

```
Boot → Login → Home → Battle ↔ Home
```

- `Boot`：初始化 GoveKits、YooAsset、加载热更新程序集
- `Login`：TapTap 登录，加载存档
- `Home`：开始/继续游戏、成就面板、奖杯显示
- `Battle`：Roguelike 战斗循环
