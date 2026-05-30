# Todo

## 奖杯方案
- [x] 新增局外货币"奖杯"
- [x] 奖杯数据接入存档
- [x] 在 Login 后加载奖杯数据
- [x] 在 Home 界面右上角显示当前奖杯数量
- [x] 战斗胜利后按玩家完成的 Level 等级发放奖杯
- [x] 增加奖杯结算与显示反馈
- [x] 预留奖杯消耗入口
- [x] 支持后续使用奖杯购买旧版成就

## 成就系统
- [x] 成就配置表接入 ConfigCore（AchievementConfigData）
- [x] 成就已购买状态接入存档（PlayerData.achievementUnlocked）
- [x] AchievementManager 实现购买逻辑
- [x] 购买成功后同步 TapTap（Unlock）
- [x] AechiPanel 展示所有成就
- [x] AechiItem 显示名称、描述、进度条、步数、购买按钮
- [x] 奖杯不足时提示
- [x] 已解锁成就显示 Toggle 勾选，隐藏购买按钮
- [x] Home 场景接线（AechiPanel 绑定 + HomePage 按钮）

## 局内循环（Roguelike 战斗循环）
- [x] 创建 RunState 数据结构（Lv + 牌组 ID 列表）
- [x] RunState 接入 GameCore 和 SaveManager（持久化存档）
- [x] Player.BuildStarterDeck 从 runState 加载牌组
- [x] Enemy.BuildStarterDeck 按 Lv 从全牌池随机生成（保证攻击牌占比）
- [x] Enemy.Setup 按 Lv 缩放 HP（+5/级）和法力恢复（基础 1，每 3 级 +1）
- [x] Player.Setup 按 Lv 缩放 MaxHP（+1/级）
- [x] BattleManager 胜利后保存牌组 → 显示肉鸽选牌面板
- [x] BattleManager 失败后重置 runState → 显示失败面板
- [x] BattleResultOverlay 失败时重开按钮开始新游戏
- [x] 创建 RoguelikeChoicePanel（5 轮选择：加牌 3 选 1 / 删牌 / 跳过）
- [x] LevelSelect 改为开始/继续游戏
- [x] RoguelikeChoicePanel 选牌显示卡图 + 正确描述
- [x] 敌人逐张出牌（每张间隔 0.5 秒 + 显示牌名）
- [x] 每回合抽 2 张牌（原 1 张）
- [x] 初始手牌 1 张（原 5 张）
- [x] MessageToast 消息队列（0.5 秒间隔显示，防重叠）
- [x] RoguelikeChoicePanel UI 层级阻挡修复
- [ ] 在 Battle 场景中搭建 RoguelikeChoicePanel UI + ChoiceCardPrefab
- [ ] Home 场景中绑定 LevelSelect 的显示（开始/继续 Lv.X）

## 下一步计划
- Home 图鉴面板（CodexPanel，展示卡牌收集）
- Home 设置面板（音量、语言等基础设置）
- 音效/特效/动效（战斗和 UI 的音效、视觉特效、动效打磨）
