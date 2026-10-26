using System;
using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>
    /// 怪物谱系。按 DQ 的经典分组：史莱姆 / 野兽 / 魔物 / 亡灵 / 龙 / 人形。
    /// 战斗强度按「章节 + 层级」缩放，同名怪物在后期只是强化版。
    /// </summary>
    public static class EnemyRoster
    {
        public const int TotalChapters = 4;
        /// <summary>每章地图层数（含 Boss 层）。</summary>
        public const int FloorsPerChapter = 12;

        public static readonly string[] ChapterNames =
        {
            "第一章 · 起始之村", "第二章 · 森林与洞窟", "第三章 · 魔王的城堡", "第四章 · 混沌深渊"
        };

        public static readonly string[] ChapterSubtitles =
        {
            "史莱姆出没的草原", "越深的林线，越浓的魔力", "龙与巫王盘踞的要塞", "规则开始崩坏的地方"
        };

        // ---------------------------------------------------------- 数值缩放

        /// <summary>
        /// 第 N 章的玩家生命上限。
        ///
        /// 这个数不是「一场战斗能挨几下」，而是「一整章能挨几下」：
        /// 一章要打 7~9 场、每场掉 8~15 血，中间还有两三处篝火回 60%。
        /// 按 80 血算，一章净损耗上限约 80 + 2×48 = 176，而实际损耗常在 150 以上，
        /// 也就是稍微走错就必死。抬到 100 起，留出让玩家犯错的空间。
        /// </summary>
        public static int PlayerHpForChapter(int chapter)
        {
            switch (ClampChapter(chapter))
            {
                case 1: return 100;
                case 2: return 115;
                case 3: return 130;
                default: return 150;
            }
        }

        /// <summary>
        /// 怪物生命。baseHp 就是「该章节一场普通战应有的血量」，已含章节差异，
        /// 这里只按层数做小幅递进（同章后段略硬）。
        ///
        /// 定这条曲线的依据是玩家的每回合输出：能量 3→4→5→6 配合牌组从 10 张长到 40 张，
        /// 每回合能打掉的血量逐章抬升。血量必须跟着抬，否则普通战会退化成两回合速通，
        /// 玩家的血量永远满格，篝火与商店就都失去意义了。
        /// </summary>
        public static int ScaleHp(int baseHp, int chapter, int floor, bool elite)
        {
            int hp = baseHp + floor / 2;
            if (elite)
            {
                hp = hp * 5 / 4;
            }

            return hp;
        }

        /// <summary>
        /// 怪物伤害随章节成长。
        /// 定这条曲线的硬约束是「Boss 战可解」：玩家护甲与回复有限，
        /// 承伤回合数 = 玩家血量 / 敌方每回合输出，必须大于击杀所需回合数。
        /// 所以单段伤害压得比卡面低，靠「多段 + 状态」制造压力而不是靠白值堆死。
        ///
        /// 章节系数必须跟上玩家血量：生命上限是 100/115/130/150，
        /// 如果伤害只按「+1/章」线性走，第四章的敌人对玩家的相对威胁会比第一章还低——
        /// 全局数值曲线就是反的，越往后越安全。
        /// </summary>
        public static int ScaleDamage(int baseDamage, int chapter, int floor, bool elite)
        {
            int ch = ClampChapter(chapter);
            // 100/115/130/150：与 PlayerHpForChapter 同向。
            // 关键是「按百分比长」而不是「+1/章」——后者四章下来只涨 3 点，
            // 而玩家血量涨了 50，敌人相对威胁反而在下降，全局曲线是反的。
            int pct = 100 + (ch - 1) * 17;
            int value = baseDamage * pct / 100 + floor / 5;
            if (elite)
            {
                value = value * 5 / 4;
            }

            return Math.Max(1, value);
        }

        public static int ClampChapter(int chapter)
        {
            if (chapter < 1)
            {
                return 1;
            }

            return chapter > TotalChapters ? TotalChapters : chapter;
        }

        // ---------------------------------------------------------- 怪物名录

        private static Dictionary<string, EnemyDef> cache;
        private static List<EnemyDef> ordered;

        /// <summary>全部怪物模板（按定义顺序，用于图鉴与生成）。</summary>
        public static IReadOnlyList<EnemyDef> All
        {
            get
            {
                BuildAll();
                return ordered;
            }
        }

        public static EnemyDef Default
        {
            get { return Get("slime_blue"); }
        }

        public static EnemyDef Get(string id)
        {
            var table = BuildAll();
            EnemyDef def;
            return table.TryGetValue(id ?? string.Empty, out def) ? def : table["slime_blue"];
        }

        /// <summary>某章节的普通怪物池。</summary>
        public static List<EnemyDef> NormalPool(int chapter)
        {
            var result = new List<EnemyDef>();
            int target = ClampChapter(chapter);
            var all = All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (!e.IsElite && !e.IsBoss && e.Chapter == target)
                {
                    result.Add(e);
                }
            }

            if (result.Count == 0)
            {
                result.Add(Default);
            }

            return result;
        }

        /// <summary>
        /// 某章节的精英池。只取本章的精英——跨章借用会把第三章 100HP 的怪扔给第一章
        /// 只有 10 张基础牌的玩家，那是无法取胜的。
        /// </summary>
        public static List<EnemyDef> ElitePool(int chapter)
        {
            var result = new List<EnemyDef>();
            int target = ClampChapter(chapter);
            var all = All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e.IsElite && e.Chapter == target)
                {
                    result.Add(e);
                }
            }

            if (result.Count == 0)
            {
                // 兜底：取不超过本章的最近精英，避免空池崩溃
                int best = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].IsElite && all[i].Chapter <= target && all[i].Chapter > best)
                    {
                        best = all[i].Chapter;
                    }
                }

                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].IsElite && all[i].Chapter == best)
                    {
                        result.Add(all[i]);
                    }
                }
            }

            return result;
        }

        public static EnemyDef BossOf(int chapter)
        {
            switch (ClampChapter(chapter))
            {
                case 1: return Get("boss_king_slime");
                case 2: return Get("boss_treant");
                case 3: return Get("boss_dragon_lord");
                default: return Get("boss_chaos_seed");
            }
        }

        /// <summary>怪物所属章节。Generated 时写入 EnemyDef.Chapter，升章后仍能正确归属。</summary>
        public static int ChapterOf(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return 1;
            }

            var def = All;
            for (int i = 0; i < def.Count; i++)
            {
                if (string.Equals(def[i].Id, id, StringComparison.Ordinal))
                {
                    return ClampChapter(def[i].Chapter);
                }
            }

            return 1;
        }

        private static Dictionary<string, EnemyDef> BuildAll()
        {
            if (cache != null)
            {
                return cache;
            }

            var list = new List<EnemyDef>();

            // ==================== 第一章：草原与洞窟 ====================
            list.Add(Make("slime_blue", "蓝色史莱姆", "史莱姆", "最常见的小怪，一碰就碎",
                1, 52, "slime_blue",
                Move("撞击", 4)));

            list.Add(Make("slime_red", "红色史莱姆", "史莱姆", "比蓝色的更烫手",
                1, 48, "slime_red",
                Move("热撞", 5),
                Move("粘液", intKind: IntentKind.Debuff, status: StatusKind.Weak, stacks: 1, weight: 4)));

            list.Add(Make("slime_metal", "金属史莱姆", "史莱姆", "皮糙肉厚，掉落经验极高",
                1, 74, "slime_metal",
                Move("硬撞", 3)));

            list.Add(Make("bat_cave", "洞窟蝙蝠", "野兽", "成群出现，专挑眼睛",
                1, 44, "bat",
                Move("撕咬", 3, hits: 2)));

            list.Add(Make("wolf_grey", "灰狼", "野兽", "饥饿使它的咬合力翻倍",
                1, 60, "wolf",
                Move("撕咬", 6),
                Move("低吼", intKind: IntentKind.Buff, status: StatusKind.Strength, stacks: 2, weight: 3)));

            list.Add(Make("goblin", "哥布林", "人形", "会偷东西，也会喊帮手",
                1, 56, "goblin",
                Move("短刀", 5),
                Move("投石", 3, intKind: IntentKind.AttackDebuff, status: StatusKind.Vulnerable, stacks: 1, weight: 5)));

            list.Add(WithStatus(Elite("elite_slime_king_small", "史莱姆王（幼体）", "史莱姆", "分裂前的小王，已有些许神性",
                1, 58, "slime_king",
                Move("吞噬", 9),
                Move("分裂再生", intKind: IntentKind.Heal, heal: 8, weight: 4),
                Move("黏液喷射", intKind: IntentKind.Debuff, status: StatusKind.Frail, stacks: 2, weight: 4)),
                StatusKind.Regen, 2));

            list.Add(WithStatus(Elite("elite_goblin_chief", "哥布林首领", "人形", "戴着捡来的王冠，手下有十几个",
                1, 55, "goblin_chief",
                Move("铁锤", 10),
                Move("呼朋引伴", intKind: IntentKind.Buff, status: StatusKind.Strength, stacks: 2, weight: 5),
                Move("抢掠", intKind: IntentKind.Debuff, status: StatusKind.Weak, stacks: 2, weight: 4)),
                StatusKind.Enrage, 1));

            // ==================== 第二章：森林与洞窟深处 ====================
            list.Add(Make("c2_mushroom", "毒蘑菇", "魔物", "孢子一散，整片林子都在咳",
                2, 88, "mushroom",
                Move("孢子", intKind: IntentKind.Debuff, status: StatusKind.Poison, stacks: 3, weight: 8),
                Move("菌盖冲撞", 5)));

            list.Add(Make("c2_spider", "洞穴蜘蛛", "野兽", "结网者，动作极快",
                2, 82, "spider",
                Move("毒牙", 4, intKind: IntentKind.AttackDebuff, status: StatusKind.Poison, stacks: 2),
                Move("结网", intKind: IntentKind.Debuff, status: StatusKind.Weak, stacks: 2, weight: 4)));

            list.Add(Make("c2_skeleton", "骷髅兵", "亡灵", "骨头架子，打不散",
                2, 94, "skeleton",
                Move("骨剑", 6),
                Move("重组", intKind: IntentKind.Defend, shield: 8, weight: 4)));

            list.Add(Make("c2_ghost", "游魂", "亡灵", "穿墙而过，实体攻击对它效果有限",
                2, 76, "ghost",
                Move("吸取生命", 5, intKind: IntentKind.AttackDebuff, status: StatusKind.Weak, stacks: 1),
                Move("恐惧", intKind: IntentKind.Debuff, status: StatusKind.Blind, stacks: 2, weight: 5)));

            list.Add(Make("c2_golem_moss", "苔石傀儡", "魔物", "走得慢，但每一拳都很重",
                2, 118, "golem",
                Move("岩石拳", 7),
                Move("硬化", intKind: IntentKind.Defend, shield: 10, weight: 5)));

            // 古树人的威胁来自「回血 + 荆棘」的叠加——两者同时生效时，
            // 玩家每回合的净输出会被吃掉一大截，战斗拖长到玩家先被磨死。
            // 现在把回血挂上长冷却，让它是「偶尔喘口气」而不是「每回合都在回」。
            list.Add(WithStatus(Elite("elite_treant", "古树人", "魔物", "林子的守卫，根须能贯穿大地",
                2, 78, "treant",
                Move("根须缠绕", 10, intKind: IntentKind.AttackDebuff, status: StatusKind.Freeze, stacks: 1),
                Move("光合作用", intKind: IntentKind.Heal, heal: 4, weight: 1, cooldown: 4),
                Move("树皮护体", intKind: IntentKind.Defend, shield: 8, weight: 3)),
                StatusKind.Thorns, 2));

            list.Add(WithStatus(Elite("elite_spider_queen", "蛛后 · 阿拉克涅", "野兽", "洞窟深处织网的祖母",
                2, 108, "spider_queen",
                Move("毒牙连刺", 6, hits: 2, intKind: IntentKind.AttackDebuff, status: StatusKind.Poison, stacks: 2),
                Move("结网", intKind: IntentKind.Debuff, status: StatusKind.Weak, stacks: 2, weight: 5),
                Move("产卵", intKind: IntentKind.Buff, status: StatusKind.Strength, stacks: 2, weight: 3)),
                StatusKind.Regen, 3));

            // ==================== 第三章：魔王城 ====================
            list.Add(Make("c3_dark_knight", "黑暗骑士", "人形", "曾经是勇者，如今只剩盔甲里的怨念",
                3, 138, "dark_knight",
                Move("魔剑", 8),
                Move("诅咒斩", 6, intKind: IntentKind.AttackDebuff, status: StatusKind.Vulnerable, stacks: 2, weight: 6)));

            // 石化凝视原本直接给 1 层眩晕 = 玩家整回合不能动。
            // 普通怪不该有「剥夺一个回合」的能力——玩家没有任何反制手段，
            // 抽到就是纯粹的运气损失。改为冻结：只削一层，代价可控。
            list.Add(Make("c3_basilisk", "石化蜥蜴", "野兽", "被它盯上就会僵硬",
                3, 118, "basilisk",
                Move("石化凝视", intKind: IntentKind.Debuff, status: StatusKind.Freeze, stacks: 1, weight: 3, cooldown: 3),
                Move("毒尾", 6, intKind: IntentKind.AttackDebuff, status: StatusKind.Poison, stacks: 2)));

            list.Add(Make("c3_bomb_creeper", "炸弹岩", "魔物", "越靠近越危险",
                3, 104, "bomb",
                Move("蓄爆", intKind: IntentKind.Buff, status: StatusKind.Enrage, stacks: 3, weight: 6),
                Move("自爆", 13, cooldown: 3)));

            list.Add(Make("c3_wraith", "怨灵法师", "亡灵", "念咒的时候，整间屋子都冷了",
                3, 110, "wraith",
                Move("暗影箭", 7),
                Move("吸魔", intKind: IntentKind.AttackDebuff, status: StatusKind.Frail, stacks: 2, weight: 5)));

            list.Add(WithStatus(Elite("elite_minotaur", "牛头人守卫", "魔物", "守住大门的最后一道锁",
                3, 124, "minotaur",
                Move("重斧", 11),
                Move("践踏", 7, hits: 2),
                Move("战吼", intKind: IntentKind.Buff, status: StatusKind.Strength, stacks: 3, weight: 4)),
                StatusKind.Thorns, 2));

            list.Add(WithStatus(Elite("elite_lich", "巫妖 · 摩甘", "亡灵", "龙王的谋士，早就不需要身体了",
                3, 134, "lich",
                Move("死亡一指", 12, intKind: IntentKind.AttackDebuff, status: StatusKind.Frail, stacks: 2),
                Move("亡灵召唤", intKind: IntentKind.Buff, status: StatusKind.Strength, stacks: 3, weight: 5),
                Move("灵魂汲取", intKind: IntentKind.Heal, heal: 12, weight: 4),
                Move("诅咒", intKind: IntentKind.Debuff, status: StatusKind.Vulnerable, stacks: 2, weight: 4)),
                StatusKind.RegenAura, 3));

            // ==================== 第四章：混沌深渊 ====================
            list.Add(Make("c4_chaos_blob", "混沌黏块", "史莱姆", "没有固定形状，也没有固定弱点",
                4, 176, "chaos_blob",
                Move("不定形撞击", 9),
                Move("随机变形", intKind: IntentKind.Buff, status: StatusKind.Strength, stacks: 2, weight: 5)));

            list.Add(Make("c4_void_servant", "虚空仆从", "亡灵", "从深渊缝隙里爬出来",
                4, 162, "void_servant",
                Move("虚空爪", 9),
                Move("侵蚀", intKind: IntentKind.Debuff, status: StatusKind.Burn, stacks: 4, weight: 5)));

            list.Add(Make("c4_abyss_wyrm", "深渊蠕虫", "龙", "龙与虫的界线在它身上模糊了",
                4, 185, "abyss_wyrm",
                Move("尾扫", 7, hits: 2),
                Move("腐化吐息", 5, intKind: IntentKind.AttackDebuff, status: StatusKind.Poison, stacks: 3, weight: 5)));

            list.Add(WithStatus(Elite("elite_fallen_hero", "堕落勇者", "人形", "他走到过这里，然后留下了",
                4, 158, "fallen_hero",
                Move("圣剑", 12),
                Move("勇者之怒", 7, hits: 2),
                Move("祈祷", intKind: IntentKind.Heal, heal: 12, weight: 3),
                Move("绝望", intKind: IntentKind.Debuff, status: StatusKind.Weak, stacks: 3, weight: 3)),
                StatusKind.Strength, 2));

            // 第四章精英里最硬的一只：20 点护盾的「空间壁垒」每回合都可能来一次，
            // 等于白送两回合血。护盾压到 14、湮灭 16→14，
            // 让它的难点落在「磨」上而不是「一波打死」。
            list.Add(WithStatus(Elite("elite_void_titan", "虚空巨像", "魔物", "深渊自己站起来的样子",
                4, 158, "void_titan",
                Move("崩界拳", 11),
                Move("虚空回响", 6, hits: 3),
                Move("空间壁垒", intKind: IntentKind.Defend, shield: 14, weight: 4),
                Move("湮灭", 14, weight: 4, cooldown: 3)),
                StatusKind.Barrier, 4));

            // ==================== Boss ====================
            // Boss 的可解性约束：玩家承伤回合数 = 当前生命 / 敌方每回合输出，
            // 这个值必须大于击杀所需回合数。第一章单动、第二章单动但更硬、
            // 第三四章双动——双动把输出翻倍，所以单段伤害必须同比砍半。
            //
            // 第一章 Boss 是教学关：单次行动、无回复、伤害低，让新玩家第一次就能摸到章节推进的手感。
            list.Add(WithStatus(BossOneAction("boss_king_slime", "史莱姆之王", "史莱姆", "草原上的王，身体里吞过整个村子",
                1, 78, "king_slime",
                Move("王者重压", 7),
                Move("群史莱姆来袭", 3, hits: 3),
                Move("王冠威压", intKind: IntentKind.Debuff, status: StatusKind.Weak, stacks: 2, weight: 3)),
                StatusKind.Enrage, 1));

            // 第二章 Boss：靠「厚壳 + 回血」拖延，但壳可以被打破。
            // 单次行动，让玩家有足够回合数读懂「防御回合不开火」的节奏。
            list.Add(WithStatus(BossOneAction("boss_treant", "森之古王", "魔物", "整片森林是它的身体",
                2, 150, "treant_king",
                Move("万根穿刺", 6, hits: 2),
                Move("年轮壁垒", intKind: IntentKind.Defend, shield: 10, weight: 4),
                Move("光合作用", intKind: IntentKind.Heal, heal: 5, weight: 2, cooldown: 3),
                Move("枯萎之息", intKind: IntentKind.Debuff, status: StatusKind.Weak, stacks: 2, weight: 4)),
                StatusKind.Thorns, 2));

            list.Add(WithStatus(Boss("boss_dragon_lord", "龙王 · 瓦尔哈拉", "龙", "魔王城里最高的一把椅子",
                3, 145, "dragon_lord",
                Move("龙焰", 6, intKind: IntentKind.AttackDebuff, status: StatusKind.Burn, stacks: 2),
                Move("爪击", 4, hits: 3),
                Move("龙鳞", intKind: IntentKind.Defend, shield: 16, weight: 4),
                Move("龙威", intKind: IntentKind.Buff, status: StatusKind.Strength, stacks: 3, weight: 3)),
                StatusKind.Enrage, 2));

            // 第四章 Boss：靠「规则崩坏」制造失控感，而不是靠白值硬碾。
            // 它每回合行动两次，两次都可能落在攻击上——单回合最坏 23 点，
            // 而玩家此时每回合输出约 28 点、血 150。双方都在 5 回合内完成击杀，
            // 谁先一步全看抽牌运气，这种「擦肩而过」的胜负没有可玩性。
            // 血量从 178 压到 160，把玩家的输出窗口从 5 回合扩到 6 回合。
            list.Add(WithStatus(Boss("boss_chaos_seed", "混沌之种", "魔物", "规则在这里失效，包括你自己的",
                4, 160, "chaos_seed",
                Move("混沌冲击", 4, hits: 2),
                Move("规则崩坏", intKind: IntentKind.Debuff, status: StatusKind.Blind, stacks: 2, weight: 6),
                Move("虚空吞噬", 7, weight: 3, cooldown: 3),
                Move("重构", intKind: IntentKind.Heal, heal: 8, weight: 4)),
                StatusKind.Enrage, 3));

            cache = new Dictionary<string, EnemyDef>();
            ordered = new List<EnemyDef>();
            for (int i = 0; i < list.Count; i++)
            {
                cache[list[i].Id] = list[i];
                ordered.Add(list[i]);
            }

            return cache;
        }

        // ---------------------------------------------------------- 构造助手

        /// <summary>普通怪：无额外特性。</summary>
        private static EnemyDef Make(string id, string name, string family, string blurb,
            int chapter, int hp, string sprite, params EnemyMove[] moves)
        {
            return Build(id, name, family, blurb, chapter, hp, sprite, 1, false, false, null, moves);
        }

        /// <summary>精英怪：带起始状态。</summary>
        private static EnemyDef Elite(string id, string name, string family, string blurb,
            int chapter, int hp, string sprite, params EnemyMove[] moves)
        {
            return Build(id, name, family, blurb, chapter, hp, sprite, 1, true, false, null, moves);
        }

        /// <summary>Boss：每回合行动两次。</summary>
        private static EnemyDef Boss(string id, string name, string family, string blurb,
            int chapter, int hp, string sprite, params EnemyMove[] moves)
        {
            return Build(id, name, family, blurb, chapter, hp, sprite, 2, false, true, null, moves);
        }

        /// <summary>第一章 Boss：单次行动的教学关。</summary>
        private static EnemyDef BossOneAction(string id, string name, string family, string blurb,
            int chapter, int hp, string sprite, params EnemyMove[] moves)
        {
            return Build(id, name, family, blurb, chapter, hp, sprite, 1, false, true, null, moves);
        }

        private static EnemyDef Build(string id, string name, string family, string blurb,
            int chapter, int hp, string sprite, int actionsPerTurn, bool elite, bool boss,
            KeyValuePair<StatusKind, int>[] startStatuses, EnemyMove[] moves)
        {
            var def = new EnemyDef
            {
                Id = id,
                Name = name,
                Family = family,
                Blurb = blurb,
                Hp = hp,
                Sprite = sprite,
                Chapter = ClampChapter(chapter),
                IsElite = elite,
                IsBoss = boss,
                ActionsPerTurn = actionsPerTurn,
                Accent = AccentOf(family),
                Seed = StableSeed(id)
            };

            if (startStatuses != null)
            {
                for (int i = 0; i < startStatuses.Length; i++)
                {
                    def.StartStatuses[startStatuses[i].Key] = startStatuses[i].Value;
                }
            }

            if (moves != null)
            {
                for (int i = 0; i < moves.Length; i++)
                {
                    if (moves[i] != null)
                    {
                        def.Moves.Add(moves[i]);
                    }
                }
            }

            return def;
        }

        /// <summary>给怪物附加一个开场状态（链式写法）。</summary>
        private static EnemyDef WithStatus(EnemyDef def, StatusKind kind, int stacks)
        {
            def.StartStatuses[kind] = stacks;
            return def;
        }

        /// <summary>DQ 的六个谱系各有固定的视觉色调，图鉴与战场据此染色。</summary>
        private static CardSeries AccentOf(string family)
        {
            switch (family)
            {
                case "史莱姆": return CardSeries.Arcane;
                case "野兽": return CardSeries.Blade;
                case "魔物": return CardSeries.Dark;
                case "亡灵": return CardSeries.Ice;
                case "龙": return CardSeries.Fire;
                case "人形": return CardSeries.Light;
                default: return CardSeries.Strike;
            }
        }

        /// <summary>由 id 求稳定种子。同一只怪每局长得一样，换机器也不变。</summary>
        private static int StableSeed(string id)
        {
            unchecked
            {
                // FNV-1a 的 32 位偏移基准值超出 int 范围，必须显式转换，
                // 否则字面量会被推断成 uint，赋值给 int 直接编译不过。
                int hash = unchecked((int)2166136261u);
                for (int i = 0; i < id.Length; i++)
                {
                    hash = (hash ^ id[i]) * 16777619;
                }

                return (int)((uint)hash & 0x7FFFFFFF);
            }
        }

        // 命名参数辅助（让数据表读起来接近配置）
        private static EnemyMove Move(string label, int damage = 0,
            int hits = 1, int shield = 0, int heal = 0,
            IntentKind intKind = IntentKind.Attack,
            StatusKind status = StatusKind.None, int stacks = 0,
            int weight = 10, int cooldown = 1)
        {
            return new EnemyMove
            {
                Label = label,
                Kind = intKind,
                Damage = damage,
                Hits = hits,
                Shield = shield,
                Heal = heal,
                Status = status,
                StatusStacks = stacks,
                Weight = weight,
                Cooldown = cooldown
            };
        }
    }
}
