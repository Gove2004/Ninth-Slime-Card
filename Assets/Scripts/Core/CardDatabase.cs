using System;
using System.Collections.Generic;

namespace Slime.Core
{
    /// <summary>卡池。数据来自 Resources/Configs/cards.csv，纯 C# 解析。</summary>
    public sealed class CardDatabase
    {
        private readonly Dictionary<int, CardDef> byId = new Dictionary<int, CardDef>();
        private readonly List<CardDef> all = new List<CardDef>();
        private readonly Dictionary<CardSeries, List<CardDef>> bySeries = new Dictionary<CardSeries, List<CardDef>>();

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
        }

        public List<CardDef> OfSeries(CardSeries series)
        {
            List<CardDef> bucket;
            return bySeries.TryGetValue(series, out bucket) ? bucket : new List<CardDef>();
        }

        public List<CardDef> OfSeries(IEnumerable<CardSeries> seriesList)
        {
            var set = new HashSet<CardSeries>();
            foreach (var s in seriesList)
            {
                set.Add(s);
            }

            var result = new List<CardDef>();
            for (int i = 0; i < all.Count; i++)
            {
                if (set.Contains(all[i].Series))
                {
                    result.Add(all[i]);
                }
            }

            return result;
        }

        public List<CardDef> OfSeries(CardSeries series, int minCost, int maxCost)
        {
            var result = new List<CardDef>();
            var bucket = OfSeries(series);
            for (int i = 0; i < bucket.Count; i++)
            {
                if (bucket[i].Cost >= minCost && bucket[i].Cost <= maxCost)
                {
                    result.Add(bucket[i]);
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
                if (cells.Length < 9)
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
                    Series = Series.FromName(cells[2]),
                    Cost = ParseInt(cells[3], 0),
                    V1 = ParseInt(cells[4], 0),
                    V2 = ParseInt(cells[5], 0),
                    V3 = ParseInt(cells[6], 0),
                    Raw = cells[7].Trim(),
                    Script = cells[8].Trim()
                };

                if (cells.Length > 9)
                {
                    def.Flavor = cells[9].Trim();
                }

                def.Ops = EffectScript.Parse(def.Script, def.V1, def.V2, def.V3);
                db.Add(def);
            }

            return db;
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

            db.Add(Make(101, "普攻", CardSeries.Starter, 1, 4, "造成[数值1]点伤害", "dmg:v1"));
            db.Add(Make(102, "治疗", CardSeries.Starter, 1, 4, "恢复[数值1]点生命", "heal:v1"));
            db.Add(Make(103, "抽牌", CardSeries.Starter, 0, 1, "抽取[数值1]张牌", "draw:v1"));
            db.Add(Make(105, "强击", CardSeries.Starter, 1, 6, "造成[数值1]点伤害", "dmg:v1"));
            db.Add(Make(106, "防御", CardSeries.Starter, 1, 4, "获得[数值1]点护盾", "shield:v1"));

            return db;
        }

        private static CardDef Make(int id, string name, CardSeries series, int cost, int v1, string text, string script)
        {
            var def = new CardDef
            {
                Id = id,
                Name = name,
                Series = series,
                Cost = cost,
                V1 = v1,
                Raw = text,
                Script = script
            };
            def.Ops = EffectScript.Parse(script, def.V1, def.V2, def.V3);
            return def;
        }
    }

    /// <summary>成就条目。旧版的成就靠奖杯购买解锁，同时记录统计进度。</summary>
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

    /// <summary>起始牌组与奖励池规则。</summary>
    public static class DeckRules
    {
        /// <summary>初始牌组：3 张普攻、2 张治疗、1 张抽牌。</summary>
        public static readonly int[] StarterDeck = { 101, 101, 101, 102, 102, 103 };

        public const int HandLimit = 8;

        /// <summary>
        /// 牌组容量上限。廿七战最多能给到 36 次补强，不设上限牌组会膨胀到 40+ 张，
        /// 每回合只抽 2 张的情况下根本轮不到关键牌——所以满了之后奖励转为「换牌」。
        /// </summary>
        public const int DeckCap = 30;

        /// <summary>奖励卡池：按当前关解锁的系列取牌，费用不超过 3，避免过早出现终结牌。</summary>
        public static List<CardDef> RewardPool(CardDatabase db, int level)
        {
            var unlocked = EnemyRoster.UnlockedSeries(level);
            var pool = db.OfSeries(unlocked);
            var result = new List<CardDef>();
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Cost <= 3)
                {
                    result.Add(pool[i]);
                }
            }

            if (result.Count == 0)
            {
                result.AddRange(pool);
            }

            return result;
        }
    }
}
