using System;
using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>
    /// 卡池。数据来自 Resources/Configs/cards.csv，纯 C# 解析。
    ///
    /// CSV 列序：
    /// id,名称,种类,系列,费用,数值1,数值2,数值3,描述,效果,升级1,升级2,升级3,目标,稀有度,趣闻
    /// </summary>
    public sealed class CardDatabase
    {
        private readonly Dictionary<int, CardDef> byId = new Dictionary<int, CardDef>();
        private readonly List<CardDef> all = new List<CardDef>();
        private readonly Dictionary<CardSeries, List<CardDef>> bySeries = new Dictionary<CardSeries, List<CardDef>>();
        private readonly Dictionary<CardKind, List<CardDef>> byKind = new Dictionary<CardKind, List<CardDef>>();

        public IReadOnlyList<CardDef> All { get { return all; } }

        public int Count { get { return all.Count; } }

        public CardDef Get(int id)
        {
            CardDef def;
            return byId.TryGetValue(id, out def) ? def : null;
        }

        public void Add(CardDef def)
        {
            if (def == null || byId.ContainsKey(def.Id))
            {
                return;
            }

            byId[def.Id] = def;
            all.Add(def);

            List<CardDef> bucket;
            if (!bySeries.TryGetValue(def.Series, out bucket))
            {
                bucket = new List<CardDef>();
                bySeries[def.Series] = bucket;
            }

            bucket.Add(def);

            if (!byKind.TryGetValue(def.Kind, out bucket))
            {
                bucket = new List<CardDef>();
                byKind[def.Kind] = bucket;
            }

            bucket.Add(def);
        }

        public List<CardDef> OfSeries(CardSeries series)
        {
            List<CardDef> bucket;
            return bySeries.TryGetValue(series, out bucket) ? new List<CardDef>(bucket) : new List<CardDef>();
        }

        public List<CardDef> OfKind(CardKind kind)
        {
            List<CardDef> bucket;
            return byKind.TryGetValue(kind, out bucket) ? new List<CardDef>(bucket) : new List<CardDef>();
        }

        /// <summary>按稀有度筛选。</summary>
        public List<CardDef> OfRarity(int maxRarity)
        {
            var result = new List<CardDef>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Rarity <= maxRarity)
                {
                    result.Add(all[i]);
                }
            }

            return result;
        }

        public static CardDatabase FromCsv(string csv)
        {
            var db = new CardDatabase();
            var rows = CsvTable.Parse(csv);

            for (int i = 0; i < rows.Count; i++)
            {
                var cells = rows[i];
                if (cells.Length < 10)
                {
                    continue;
                }

                int id;
                if (!int.TryParse(cells[0].Trim(), out id))
                {
                    continue; // 表头或空行
                }

                var def = new CardDef
                {
                    Id = id,
                    Name = cells[1].Trim(),
                    Kind = ParseKind(cells[2]),
                    Series = Series.FromName(cells[3]),
                    Cost = ParseInt(cells[4], 0),
                    V1 = ParseInt(cells[5], 0),
                    V2 = ParseInt(cells[6], 0),
                    V3 = ParseInt(cells[7], 0),
                    Raw = cells[8].Trim(),
                    Script = cells[9].Trim()
                };

                if (cells.Length > 10) def.UpV1 = ParseInt(cells[10], 0);
                if (cells.Length > 11) def.UpV2 = ParseInt(cells[11], 0);
                if (cells.Length > 12) def.UpV3 = ParseInt(cells[12], 0);
                if (cells.Length > 13) def.Target = ParseTarget(cells[13]);
                if (cells.Length > 14) def.Rarity = ParseInt(cells[14], 0);
                if (cells.Length > 15) def.Flavor = cells[15].Trim();

                def.Ops = EffectScript.Parse(def.Script, def.V1, def.V2, def.V3);
                db.Add(def);
            }

            return db;
        }

        private static CardKind ParseKind(string raw)
        {
            switch ((raw ?? string.Empty).Trim())
            {
                case "攻击": case "attack": return CardKind.Attack;
                case "技能": case "skill": return CardKind.Skill;
                case "咒文": case "spell": return CardKind.Spell;
                case "能力": case "power": return CardKind.Power;
                default: return CardKind.Attack;
            }
        }

        private static CardTarget ParseTarget(string raw)
        {
            switch ((raw ?? string.Empty).Trim())
            {
                case "自身": case "self": return CardTarget.Self;
                case "全体": case "all": return CardTarget.AllEnemies;
                case "无": case "none": return CardTarget.None;
                default: return CardTarget.Enemy;
            }
        }

        private static int ParseInt(string raw, int fallback)
        {
            int value;
            return int.TryParse((raw ?? string.Empty).Trim(), out value) ? value : fallback;
        }

        /// <summary>内置兜底卡池：CSV 缺失时保证游戏仍能开局。</summary>
        public static CardDatabase CreateFallback()
        {
            var db = new CardDatabase();

            db.Add(Make(1, "斩击", CardKind.Attack, CardSeries.Strike, 1, 6, 3, "造成[数值1]点伤害", "dmg:v1"));
            db.Add(Make(2, "格挡", CardKind.Skill, CardSeries.Light, 1, 5, 3, "获得[数值1]点护盾", "shield:v1"));
            db.Add(Make(3, "火球", CardKind.Spell, CardSeries.Fire, 1, 7, 3, "造成[数值1]点伤害", "spdmg:v1"));
            db.Add(Make(4, "治疗", CardKind.Spell, CardSeries.Light, 1, 6, 3, "恢复[数值1]点生命", "heal:v1"));
            db.Add(Make(5, "冥想", CardKind.Skill, CardSeries.Arcane, 0, 1, 0, "抽[数值1]张牌", "draw:v1"));

            return db;
        }

        private static CardDef Make(int id, string name, CardKind kind, CardSeries series, int cost,
            int v1, int up1, string text, string script)
        {
            var def = new CardDef
            {
                Id = id,
                Name = name,
                Kind = kind,
                Series = series,
                Cost = cost,
                V1 = v1,
                UpV1 = up1,
                Raw = text,
                Script = script
            };
            def.Ops = EffectScript.Parse(script, def.V1, def.V2, def.V3);
            return def;
        }
    }

    /// <summary>起始牌组与奖励规则。</summary>
    public static class DeckRules
    {
        /// <summary>
        /// 初始牌组：5 斩击 + 4 格挡 + 1 火球（10 张）。
        /// 必须留防御牌——全是攻击牌的话，玩家从第一场战斗起就没有「打还是挡」的选择，
        /// 格挡机制整章都用不上，后面再给盾牌也建立不起节奏感。
        /// </summary>
        public static readonly int[] StarterDeck = { 1, 1, 1, 1, 1, 129, 129, 129, 129, 33 };

        /// <summary>牌组上限，超过后奖励转为「换牌」。</summary>
        public const int DeckCap = 40;

        public const int StarterDeckSize = 10;

        public static List<CardInstance> BuildStarter(CardDatabase db)
        {
            var list = new List<CardInstance>();
            for (int i = 0; i < StarterDeck.Length; i++)
            {
                var def = db != null ? db.Get(StarterDeck[i]) : null;
                if (def != null)
                {
                    list.Add(CardInstance.Wrap(def));
                }
            }

            if (list.Count == 0 && db != null)
            {
                for (int i = 0; i < db.All.Count && list.Count < 10; i++)
                {
                    list.Add(CardInstance.Wrap(db.All[i]));
                }
            }

            return list;
        }

        /// <summary>
        /// 奖励池：按章节开放稀有度，避免前期就拿到终结牌。
        /// 自由混池——不按职业筛，只按稀有度与系列分布。
        /// </summary>
        public static List<CardDef> RewardPool(CardDatabase db, int chapter, bool elite)
        {
            int maxRarity = elite ? 2 : Math.Min(2, Math.Max(0, chapter - 1));
            var pool = new List<CardDef>();
            for (int i = 0; i < db.All.Count; i++)
            {
                var def = db.All[i];
                if (def.Rarity <= maxRarity && def.Kind != CardKind.Power)
                {
                    pool.Add(def);
                }
            }

            if (pool.Count == 0)
            {
                pool.AddRange(db.All);
            }

            return pool;
        }
    }

    /// <summary>成就条目。</summary>
    public sealed class AchievementDef
    {
        public int Order;
        public string Name = string.Empty;
        public string Id = string.Empty;
        public string Text = string.Empty;
        public long Threshold;
        public int TrophyCost;
        /// <summary>counter:score / counter:draw / ... / flag</summary>
        public string Metric = "flag";

        public bool IsCounter { get { return Metric.StartsWith("counter:", StringComparison.Ordinal); } }

        public string CounterKey { get { return IsCounter ? Metric.Substring(8) : string.Empty; } }
    }

    public sealed class AchievementDatabase
    {
        private readonly List<AchievementDef> all = new List<AchievementDef>();

        public IReadOnlyList<AchievementDef> All { get { return all; } }

        public static AchievementDatabase FromCsv(string csv)
        {
            var db = new AchievementDatabase();
            var rows = CsvTable.Parse(csv);

            for (int i = 0; i < rows.Count; i++)
            {
                var cells = rows[i];
                if (cells.Length < 6)
                {
                    continue;
                }

                int order;
                if (!int.TryParse(cells[0].Trim(), out order))
                {
                    continue;
                }

                var def = new AchievementDef
                {
                    Order = order,
                    Name = cells[1].Trim(),
                    Id = cells[2].Trim(),
                    Text = cells[3].Trim()
                };

                long threshold;
                long.TryParse(cells[4].Trim(), out threshold);
                def.Threshold = threshold;

                int cost;
                int.TryParse(cells[5].Trim(), out cost);
                def.TrophyCost = cost;

                if (cells.Length > 6 && cells[6].Trim().Length > 0)
                {
                    def.Metric = cells[6].Trim();
                }

                if (def.Id.Length > 0)
                {
                    db.all.Add(def);
                }
            }

            return db;
        }
    }
}
