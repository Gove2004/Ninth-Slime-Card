using System;
using System.Collections.Generic;
using System.Text;

namespace Slime.Core
{
    /// <summary>
    /// 卡牌系列。旧版《第九张史莱姆牌》的全部卡池按八条系列组织，
    /// 系列决定卡面配色、图鉴分组与敌方牌库的解锁进度。
    /// </summary>
    public enum CardSeries
    {
        None = 0,
        Starter = 1,  // 初始
        Sin = 2,      // 七罪
        Blood = 3,    // 血族
        Fortify = 4,  // 坚固
        Tech = 5,     // 科技
        Seed = 6,     // 种子
        Shadow = 7,   // 暗影
        Chrono = 8    // 时序
    }

    public static class Series
    {
        public static readonly CardSeries[] All =
        {
            CardSeries.Starter, CardSeries.Sin, CardSeries.Blood, CardSeries.Fortify,
            CardSeries.Tech, CardSeries.Seed, CardSeries.Shadow, CardSeries.Chrono
        };

        public static string DisplayName(CardSeries s)
        {
            switch (s)
            {
                case CardSeries.Starter: return "初始";
                case CardSeries.Sin: return "七罪";
                case CardSeries.Blood: return "血族";
                case CardSeries.Fortify: return "坚固";
                case CardSeries.Tech: return "科技";
                case CardSeries.Seed: return "种子";
                case CardSeries.Shadow: return "暗影";
                case CardSeries.Chrono: return "时序";
                default: return "无系";
            }
        }

        public static string IdOf(CardSeries s)
        {
            switch (s)
            {
                case CardSeries.Starter: return "starter";
                case CardSeries.Sin: return "sin";
                case CardSeries.Blood: return "blood";
                case CardSeries.Fortify: return "fortify";
                case CardSeries.Tech: return "tech";
                case CardSeries.Seed: return "seed";
                case CardSeries.Shadow: return "shadow";
                case CardSeries.Chrono: return "chrono";
                default: return "none";
            }
        }

        public static CardSeries FromName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return CardSeries.None;
            }

            raw = raw.Trim();
            for (int i = 0; i < All.Length; i++)
            {
                if (DisplayName(All[i]) == raw || IdOf(All[i]) == raw.ToLowerInvariant())
                {
                    return All[i];
                }
            }

            return CardSeries.None;
        }

        /// <summary>一句话系列说明，用于图鉴与卡面角标。</summary>
        public static string Blurb(CardSeries s)
        {
            switch (s)
            {
                case CardSeries.Starter: return "基础攻防，稳定的骨架";
                case CardSeries.Sin: return "以代价换取爆发";
                case CardSeries.Blood: return "流血与吸血，越险越强";
                case CardSeries.Fortify: return "护盾与减伤，站桩反击";
                case CardSeries.Tech: return "低费连击与复制";
                case CardSeries.Seed: return "骰子随机，上限极高";
                case CardSeries.Shadow: return "偷取、诅咒与削弱";
                case CardSeries.Chrono: return "持续效果与结算";
                default: return string.Empty;
            }
        }
    }

    // ---------------------------------------------------------------- 效果脚本

    /// <summary>条件判定名。对应旧版卡牌描述里的"若……则……"分支。</summary>
    public enum OpTest
    {
        Always = 0,
        FirstCard,        // 这是本回合打出的第一张牌
        PlayedOther,      // 本回合已打出过其他牌
        PlayedTech,       // 本回合已打出过科技牌
        HalfHp,           // 自身生命不高于一半
        HasShield,        // 自身有护盾
        EnemyHasShield,   // 目标有护盾
        TookDamage,       // 本回合自身受过伤
        HandGte,          // 手牌不少于 A 张
        EnemyManaHigher,  // 敌方魔力高于自己
        EnemyAttackedMe,  // 本回合被敌方攻击过
        HasSeed,          // 手中已有"种子"牌
        NoOtherHand       // 没有其他手牌
    }

    /// <summary>钩子触发时机。对应旧版 BaseCharacter.HookTiming。</summary>
    public enum HookTiming
    {
        None = 0,
        StartTurn,     // 自己的回合开始
        EndTurn,       // 自己的回合结束
        UseCard,       // 自己打出一张牌后
        UseCardTech,   // 自己打出一张科技牌后
        EnemyUseCard,  // 敌方打出一张牌后
        Heal,          // 自己恢复生命后
        Hurt,          // 自己受到伤害后
        DealDamage     // 自己造成伤害后
    }

    /// <summary>
    /// 效果脚本节点。DSL 形如 <c>dmg:v1;if:hasshield|draw:v2|shield:2</c>。
    /// 数值参数支持 <c>v1</c>/<c>v2</c>/<c>v3</c> 占位，解析时替换为卡面数值。
    /// </summary>
    public sealed class CardOp
    {
        public string Kind = string.Empty;
        public int A;
        public int B;
        public int C;
        /// <summary>字符串载荷：系列名、牌号、随机分支列表等。</summary>
        public string Text = string.Empty;
        /// <summary>条件测试名（Kind == "if" 时有效）。</summary>
        public OpTest Test = OpTest.Always;
        public int TestValue;
        /// <summary>条件成立分支。</summary>
        public List<CardOp> Then;
        /// <summary>条件不成立分支，可为 null。</summary>
        public List<CardOp> Else;
        /// <summary>钩子时机（Kind == "hook" 时有效）。</summary>
        public HookTiming Timing = HookTiming.None;
        /// <summary>钩子载荷。</summary>
        public List<CardOp> Hook;
        public int HookTurns = 1;
        /// <summary>生成的牌本回合免费。</summary>
        public bool Free;

        public bool IsIf { get { return Kind == "if"; } }
        public bool IsHook { get { return Kind == "hook"; } }
    }

    /// <summary>效果脚本解析器。纯字符串处理，不依赖引擎。</summary>
    public static class EffectScript
    {
        public static List<CardOp> Parse(string spec, int v1, int v2, int v3)
        {
            var list = new List<CardOp>();
            if (string.IsNullOrEmpty(spec))
            {
                return list;
            }

            var parts = spec.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                var op = ParseOp(parts[i], v1, v2, v3);
                if (op != null)
                {
                    list.Add(op);
                }
            }

            return list;
        }

        private static List<CardOp> ParsePlus(string spec, int v1, int v2, int v3)
        {
            var list = new List<CardOp>();
            if (string.IsNullOrEmpty(spec))
            {
                return list;
            }

            var parts = spec.Split('+');
            for (int i = 0; i < parts.Length; i++)
            {
                var op = ParseOp(parts[i], v1, v2, v3);
                if (op != null)
                {
                    list.Add(op);
                }
            }

            return list;
        }

        private static CardOp ParseOp(string raw, int v1, int v2, int v3)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            raw = raw.Trim();
            if (raw.Length == 0)
            {
                return null;
            }

            var op = new CardOp();
            string body = raw;
            var bar = raw.IndexOf('|');
            if (bar >= 0)
            {
                body = raw.Substring(0, bar);
            }

            var seg = body.Split(':');
            op.Kind = seg[0].Trim();

            if (op.Kind == "if")
            {
                // if : test | then | else?
                var all = raw.Split('|');
                string testSpec = all.Length > 1 ? all[1] : "always";
                string thenSpec = all.Length > 2 ? all[2] : string.Empty;
                string elseSpec = all.Length > 3 ? all[3] : string.Empty;

                op.Test = ParseTest(testSpec, out op.TestValue);
                op.Then = ParsePlus(thenSpec, v1, v2, v3);
                op.Else = elseSpec.Length > 0 ? ParsePlus(elseSpec, v1, v2, v3) : null;
                return op;
            }

            if (op.Kind == "hook")
            {
                // hook : timing | ops | turns
                var all = raw.Split('|');
                op.Timing = ParseTiming(all.Length > 1 ? all[1] : string.Empty);
                op.Hook = ParsePlus(all.Length > 2 ? all[2] : string.Empty, v1, v2, v3);
                op.HookTurns = all.Length > 3 ? Arg(all[3], v1, v2, v3, 1) : 1;
                if (op.HookTurns < 1)
                {
                    op.HookTurns = 1;
                }

                return op;
            }

            if (seg.Length > 1)
            {
                op.A = Arg(seg[1], v1, v2, v3, 0);
            }

            if (seg.Length > 2)
            {
                op.B = Arg(seg[2], v1, v2, v3, 0);
            }

            if (seg.Length > 3)
            {
                op.C = Arg(seg[3], v1, v2, v3, 0);
            }

            // 字符串载荷：系列名 / 牌号等出现在 A 位无法解析时
            if (seg.Length > 1 && IsNamed(op.Kind))
            {
                op.Text = seg[1].Trim();
            }

            if (seg.Length > 3)
            {
                op.Free = seg[3].Trim() == "free";
            }
            else if (seg.Length > 2 && op.Kind == "addrandom")
            {
                op.Free = seg[2].Trim() == "free";
            }

            return op;
        }

        /// <summary>这些 op 的第一个参数是名字而不是数字（系列名 / 随机分支表 / 弃牌模式）。</summary>
        private static bool IsNamed(string kind)
        {
            return kind == "addrandom" || kind == "randadd" || kind == "randpick"
                   || kind == "randpick4" || kind == "discardall";
        }

        private static int Arg(string raw, int v1, int v2, int v3, int fallback)
        {
            if (raw == null)
            {
                return fallback;
            }

            string t = raw.Trim();
            switch (t)
            {
                case "v1": return v1;
                case "v2": return v2;
                case "v3": return v3;
                case "": return fallback;
            }

            int value;
            return int.TryParse(t, out value) ? value : fallback;
        }

        private static OpTest ParseTest(string raw, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(raw))
            {
                return OpTest.Always;
            }

            var seg = raw.Trim().Split(':');
            string name = seg[0].Trim();
            if (seg.Length > 1)
            {
                int.TryParse(seg[1].Trim(), out value);
            }

            switch (name)
            {
                case "firstcard": return OpTest.FirstCard;
                case "playedother": return OpTest.PlayedOther;
                case "playedtech": return OpTest.PlayedTech;
                case "halfhp": return OpTest.HalfHp;
                case "hasshield": return OpTest.HasShield;
                case "enemyhasshield": return OpTest.EnemyHasShield;
                case "tookdamage": return OpTest.TookDamage;
                case "handgte": return OpTest.HandGte;
                case "enemymanahigher": return OpTest.EnemyManaHigher;
                case "enemyattackedme": return OpTest.EnemyAttackedMe;
                case "hasseed": return OpTest.HasSeed;
                case "nohand": return OpTest.NoOtherHand;
                default: return OpTest.Always;
            }
        }

        private static HookTiming ParseTiming(string raw)
        {
            switch ((raw ?? string.Empty).Trim())
            {
                case "StartTurn": return HookTiming.StartTurn;
                case "EndTurn": return HookTiming.EndTurn;
                case "UseCard": return HookTiming.UseCard;
                case "UseCardTech": return HookTiming.UseCardTech;
                case "EnemyUseCard": return HookTiming.EnemyUseCard;
                case "Heal": return HookTiming.Heal;
                case "Hurt": return HookTiming.Hurt;
                case "DealDamage": return HookTiming.DealDamage;
                default: return HookTiming.None;
            }
        }
    }

    // ---------------------------------------------------------------- 卡牌定义

    /// <summary>静态卡牌定义。来自 cards.csv。</summary>
    public sealed class CardDef
    {
        public int Id;
        public string Name = string.Empty;
        public CardSeries Series = CardSeries.Starter;
        public int Cost;
        public int V1;
        public int V2;
        public int V3;
        public string Raw = string.Empty;
        public string Flavor = string.Empty;
        public string Script = string.Empty;
        public List<CardOp> Ops = new List<CardOp>();

        /// <summary>把 [数值1]/[数值2]/[数值3]/[数值]/[费用] 占位替换成实际数字。</summary>
        public string Text()
        {
            return RenderText(Raw, Cost, V1, V2, V3);
        }

        public static string RenderText(string raw, int cost, int v1, int v2, int v3)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            return raw
                .Replace("[费用]", cost.ToString())
                .Replace("[数值1]", v1.ToString())
                .Replace("[数值2]", v2.ToString())
                .Replace("[数值3]", v3.ToString())
                .Replace("[数值]", v1.ToString());
        }

        public bool HasSeries(CardSeries s)
        {
            return Series == s;
        }
    }

    // ---------------------------------------------------------------- 敌人定义

    /// <summary>敌人模板。旧版按等级线性成长，这里额外给每个阶段一个名字与形象。</summary>
    public sealed class EnemyDef
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string Title = string.Empty;
        public int Seed;
        public CardSeries Accent = CardSeries.Blood;
    }

    /// <summary>
    /// 赛程结构：3 幕 × 3 关 × 3 战 = 27 场战斗。
    /// 每一关仍由一位系列宗师镇守，但要连打三场：先锋战 → 中坚战 → 宗师战，
    /// 第三场才是关底，强度最高、奖励翻倍。
    /// </summary>
    public static class EnemyRoster
    {
        /// <summary>幕数。</summary>
        public const int ActsCount = 3;
        /// <summary>每幕关数。</summary>
        public const int LevelsPerAct = 3;
        /// <summary>每关战斗场次：先锋 / 中坚 / 宗师。</summary>
        public const int StagesPerLevel = 3;

        /// <summary>赛程总关数：八系宗师 + 第九关最终头目。打穿即通关。</summary>
        public const int FinalLevel = 9;

        /// <summary>赛程总战斗数 = 3 × 3 × 3 = 27。</summary>
        public const int FinalBattle = ActsCount * LevelsPerAct * StagesPerLevel;

        public static readonly string[] ActNames =
        {
            "第一幕 · 浸染", "第二幕 · 锈蚀", "第三幕 · 终局"
        };

        public static readonly string[] StageNames =
        {
            "先锋战", "中坚战", "宗师战"
        };

        /// <summary>把战序夹到 1..27。</summary>
        public static int ClampBattle(int battle)
        {
            if (battle < 1)
            {
                return 1;
            }

            return battle > FinalBattle ? FinalBattle : battle;
        }

        /// <summary>战序 → 关号（1..9）。</summary>
        public static int LevelOf(int battle)
        {
            return (ClampBattle(battle) - 1) / StagesPerLevel + 1;
        }

        /// <summary>战序 → 关内场次（1 先锋 / 2 中坚 / 3 宗师）。</summary>
        public static int StageOf(int battle)
        {
            return (ClampBattle(battle) - 1) % StagesPerLevel + 1;
        }

        /// <summary>战序 → 幕号（1..3）。</summary>
        public static int ActOf(int battle)
        {
            return (LevelOf(battle) - 1) / LevelsPerAct + 1;
        }

        /// <summary>是否是关底宗师战（奖励翻倍）。</summary>
        public static bool IsMasterStage(int battle)
        {
            return StageOf(battle) == StagesPerLevel;
        }

        /// <summary>战序在关内的相对位置描述，例如「第 3 关 · 中坚战」。</summary>
        public static string Describe(int battle)
        {
            return "第 " + LevelOf(battle) + " 关 · " + StageNames[StageOf(battle) - 1];
        }

        // ---------------------------------------------------------- 强度曲线
        // 放在这里而不是战斗会话内部：主界面要预告下一战的敌情，两边必须用同一套公式。
        //
        // 标定依据：玩家的出牌吞吐被「每回合抽 2 张」锁死，牌组从 6 张涨到 30 张之后
        // 单张牌的质量会被稀释。所以敌人这边不能一味堆量——生命与回魔都要比线性更缓，
        // 否则中后段会变成玩家还没抽到能打的牌就已经被打死。

        /// <summary>玩家生命：10 → 29，长赛程需要更多容错。</summary>
        public static int PlayerHpFor(int battle)
        {
            return 10 + (ClampBattle(battle) - 1) * 3 / 4;
        }

        /// <summary>敌人生命：10 → 44；关底宗师战额外 +25%，把"关底"这件事做出来。</summary>
        public static int EnemyHpFor(int battle)
        {
            int b = ClampBattle(battle);
            int hp = 10 + (b - 1) * 4 / 3;
            return IsMasterStage(b) ? hp * 5 / 4 : hp;
        }

        /// <summary>敌人回魔：1 → 3。敌人一出牌就是一整串，回魔每 +1 都是质变，必须压住。</summary>
        public static int EnemyManaRegenFor(int battle)
        {
            return 1 + (ClampBattle(battle) - 1) / 13;
        }

        /// <summary>敌人牌库张数：5 → 18，越到后面手牌越不会枯。</summary>
        public static int EnemyDeckSizeFor(int battle)
        {
            return 5 + (ClampBattle(battle) - 1) / 2;
        }

        public static readonly EnemyDef[] All =
        {
            new EnemyDef { Id = "e_slime", Name = "野性黏体", Title = "第一关 · 湿润的走廊", Seed = 11, Accent = CardSeries.Starter },
            new EnemyDef { Id = "e_sinner", Name = "七罪回响", Title = "第二关 · 罪业的回声", Seed = 23, Accent = CardSeries.Sin },
            new EnemyDef { Id = "e_vampire", Name = "血裔术士", Title = "第三关 · 猩红的祭坛", Seed = 37, Accent = CardSeries.Blood },
            new EnemyDef { Id = "e_bastion", Name = "顽石守卫", Title = "第四关 · 不可撼动", Seed = 53, Accent = CardSeries.Fortify },
            new EnemyDef { Id = "e_engine", Name = "演算核心", Title = "第五关 · 无尽循环", Seed = 71, Accent = CardSeries.Tech },
            new EnemyDef { Id = "e_dice", Name = "骰胎赌徒", Title = "第六关 · 概率深渊", Seed = 89, Accent = CardSeries.Seed },
            new EnemyDef { Id = "e_shade", Name = "窃影", Title = "第七关 · 你的倒影", Seed = 101, Accent = CardSeries.Shadow },
            new EnemyDef { Id = "e_chrono", Name = "时序守望", Title = "第八关 · 回合的尽头", Seed = 131, Accent = CardSeries.Chrono },
            new EnemyDef { Id = "e_ninth", Name = "第九张史莱姆牌", Title = "终关 · 牌堆尽头的传说", Seed = 271, Accent = CardSeries.Starter }
        };

        public static EnemyDef ForLevel(int level)
        {
            if (level < 1)
            {
                level = 1;
            }

            // 赛程制：超出终关就停在 Boss（正常流程不会发生，Boss 被击败后即通关重开）。
            int index = System.Math.Min(level - 1, All.Length - 1);
            return All[index];
        }

        /// <summary>按战序取守关宗师（同一关的三场共用一位宗师）。</summary>
        public static EnemyDef ForBattle(int battle)
        {
            return ForLevel(LevelOf(battle));
        }

        /// <summary>
        /// 按关号解锁的系列数：每一关解锁一个新系列，第九关时八系全开。
        /// 宗师镇守的系列正好是这一关新解锁的那个——敌人变强，你的选择也变多。
        /// </summary>
        public static List<CardSeries> UnlockedSeries(int level)
        {
            var list = new List<CardSeries>();
            int count = level < 1 ? 1 : level;
            if (count > Series.All.Length)
            {
                count = Series.All.Length;
            }

            for (int i = 0; i < count; i++)
            {
                list.Add(Series.All[i]);
            }

            if (list.Count == 0)
            {
                list.Add(CardSeries.Starter);
            }

            return list;
        }

        /// <summary>按战序解锁的系列（同一关三场共享）。</summary>
        public static List<CardSeries> UnlockedSeriesForBattle(int battle)
        {
            return UnlockedSeries(LevelOf(battle));
        }
    }

    // ---------------------------------------------------------------- 工具

    /// <summary>确定性随机数（xorshift）。可复现，便于调试与录像。</summary>
    public sealed class DeterministicRng
    {
        private uint state;

        public DeterministicRng(int seed)
        {
            uint s = seed == 0 ? 0x9E3779B9u : (uint)seed;
            state = s == 0u ? 1u : s;
        }

        public int Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state & 0x7FFFFFFF);
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            return minInclusive + Next() % (maxExclusive - minInclusive);
        }

        public void Shuffle<T>(List<T> list)
        {
            if (list == null)
            {
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                int j = Range(i, list.Count);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }

    /// <summary>极简 CSV 解析：支持双引号包裹与转义双引号，纯 C# 无引擎依赖。</summary>
    public static class CsvTable
    {
        public static List<string[]> Parse(string text)
        {
            var rows = new List<string[]>();
            if (string.IsNullOrEmpty(text))
            {
                return rows;
            }

            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        row.Add(field.ToString());
                        field.Length = 0;
                        break;
                    case '\r':
                        break;
                    case '\n':
                        row.Add(field.ToString());
                        field.Length = 0;
                        rows.Add(row.ToArray());
                        row.Clear();
                        break;
                    default:
                        field.Append(c);
                        break;
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }

            return rows;
        }
    }
}
