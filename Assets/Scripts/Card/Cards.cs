using System;
using System.Collections.Generic;
using UnityEngine;
using static CardNumberUtil;

public static class CardRuntimeHelper
{
    public static void DiscardAllCards(BaseCharacter user)
    {
        if (user == null || user.Cards.Count == 0) return;
        if (user is Player)
        {
            var discardList = new List<BaseCard>(user.Cards);
            foreach (var card in discardList)
            {
                user.Cards.Remove(card);
                EventCenter.Publish("Player_PlayCard", card);
            }
            return;
        }

        user.Cards.Clear();
    }

    public static void SetHandCost(BaseCharacter character, ulong cost)
    {
        if (character == null) return;
        foreach (var card in character.Cards)
        {
            card.SetCost(cost);
        }
    }

    public static int GetRandomDurationFromValue(int maxDuration)
    {
        if (maxDuration <= 0) return 0;
        return UnityEngine.Random.Range(1, maxDuration + 1);
    }
}

public class 流血 : BaseCard
{
    protected override int id => 1000;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, target, duration, d => d.source.DealDamage(d.target, value), null, () => $"每回合造成{value}点伤害，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}

public class 恢复 : BaseCard
{
    protected override int id => 1001;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, user, duration, d => d.source.ApplyHealthChange(ToLong(value), d.source), null, () => $"每回合恢复{value}点生命，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}

public class 入魔 : BaseCard
{
    protected override int id => 1002;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, user, duration, d => d.source.ChangeMana(ToLong(value)), null, () => $"每回合获得{value}点魔力，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}

public class 抽牌 : BaseCard
{
    protected override int id => 1003;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        int count = ToInt(Value);
        if (duration <= 0 || count <= 0) return;

        Dot dot = null;
        dot = new Dot(user, user, duration, d =>
        {
            for (int i = 0; i < count; i++)
            {
                var card = d.source.DrawCard(0);
                if (card != null && d.source is Player)
                {
                    EventCenter.Publish("Player_DrawCard", card);
                }
            }
        }, null, () => $"每回合抽{count}张牌，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 驱散 : BaseCard
{
    protected override int id => 1007;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (target == null) return;
        int count = ToInt(Value);
        if (count <= 0 || target.dotBar.Count == 0) return;

        int removeCount = Mathf.Min(count, target.dotBar.Count);
        for (int i = 0; i < removeCount; i++)
        {
            if (target.dotBar.Count == 0) break;
            int index = UnityEngine.Random.Range(0, target.dotBar.Count);
            Dot effect = target.dotBar[index];
            target.dotBar.RemoveAt(index);
            effect?.TransferTo(null);
        }
    }
}

public class 吸血 : BaseCard
{
    protected override int id => 1100;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;

        Dot dot = null;
        dot = new Dot(user, target, duration, d =>
        {
            d.source.DealDamage(d.target, value);
            d.source.ApplyHealthChange(ToLong(value), d.source);
        }, null, () => $"每回合造成{value}点伤害并恢复{value}点生命，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 加速 : BaseCard
{
    protected override int id => 1101;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        ulong startDamage = Value;
        ulong growth = 1;

        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            d.source.DealDamage(d.target, startDamage);
            startDamage = BaseCharacter.SaturatingAdd(startDamage, growth);
        }, null, () => $"每回合造成递增伤害，当前{startDamage}点，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 延续 : BaseCard
{
    protected override int id => 1102;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int add = ToInt(Value);
        if (add == 0) return;

        foreach (var dot in user.dotBar)
        {
            dot.duration += add;
        }
    }
}

public class 超频 : BaseCard
{
    protected override int id => 1103;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        ulong factor = Value;
        if (factor <= 1) return;
        user.ApplyOverclock(factor);
    }
}

public class 结算 : BaseCard
{
    protected override int id => 1104;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int times = ToInt(Value);
        user.TriggerDotsTimes(times);
        target?.TriggerDotsTimes(times);
    }
}

public class 急救 : BaseCard
{
    protected override int id => 1105;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        ulong value = Value;
        user.ChangeMana(ToLong(value));
        user.ApplyHealthChange(ToLong(value), user);
    }
}

public class 闪击 : BaseCard
{
    protected override int id => 1106;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        user.DealDamage(target, Value);
    }
}

public class 重奏 : BaseCard
{
    protected override int id => 1110;
    private static bool isResolving;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (isResolving) return;
        int times = ToInt(Value);
        if (times <= 0) return;

        BaseCard previous = user.PreviousPlayedCard;
        if (previous == null) return;
        if (ReferenceEquals(previous, this) || previous is 重奏)
        {
            Debug.LogWarning("重奏无法复制重奏，已跳过以避免递归。");
            return;
        }

        isResolving = true;
        try
        {
            for (int i = 0; i < times; i++)
            {
                var oldContext = BaseCharacter.ActiveCardContext;
                BaseCharacter.ActiveCardContext = previous;
                previous.Execute(user, target);
                BaseCharacter.ActiveCardContext = oldContext;
            }
        }
        finally
        {
            isResolving = false;
        }
    }
}

public class 七罪 : BaseCard
{
    protected override int id => 1200;
    private static readonly string[] SinNames = { "暴怒", "傲慢", "嫉妒", "贪婪", "懒惰", "色欲", "暴食" };

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;

        Dot dot = null;
        dot = new Dot(user, user, Duration, d =>
        {
            string randomName = SinNames[UnityEngine.Random.Range(0, SinNames.Length)];
            var card = CardFactory.GetThisCard(randomName);
            if (card != null) d.source.GainCard(card);
        }, null, () => $"每回合获得1张原罪卡，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 暴怒 : BaseCard
{
    protected override int id => 1201;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        ulong value = Value;

        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            d.source.DealDamage(d.target, value);
            if (UnityEngine.Random.value < 0.77f)
            {
                d.source.DealDamage(d.source, value);
            }
        }, null, () => $"每回合造成{value}点伤害，并有77%概率反噬，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 傲慢 : BaseCard
{
    protected override int id => 1202;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        user.ChangeMana(NegToLong(user.mana));
        user.SetImmuneThisTurn(true);

        int duration = Mathf.Max(1, Duration);
        Dot dot = null;
        dot = new Dot(user, user, duration, d => d.source.SetImmuneThisTurn(true), d =>
        {
            if (d.source != null) d.source.SetImmuneThisTurn(false);
        }, () => $"免疫伤害，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 嫉妒 : BaseCard
{
    protected override int id => 1203;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (target == null) return;

        var userDots = new List<Dot>(user.dotBar);
        var targetDots = new List<Dot>(target.dotBar);

        user.dotBar.Clear();
        target.dotBar.Clear();

        foreach (var dot in userDots)
        {
            dot.TransferTo(target);
            target.dotBar.Add(dot);
        }

        foreach (var dot in targetDots)
        {
            dot.TransferTo(user);
            user.dotBar.Add(dot);
        }
    }
}

public class 贪婪 : BaseCard
{
    protected override int id => 1204;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int discardCount = user.Cards.Count;
        if (discardCount <= 0) return;
        CardRuntimeHelper.DiscardAllCards(user);
        user.ChangeMana(ToLong((ulong)(discardCount * 2)));
    }
}

public class 懒惰 : BaseCard
{
    protected override int id => 1205;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        for (int i = 0; i < 7; i++)
        {
            var card = user.DrawCard(0);
            if (card != null && user is Player)
            {
                EventCenter.Publish("Player_DrawCard", card);
            }
        }
        user.EndTurn();
    }
}

public class 色欲 : BaseCard
{
    protected override int id => 1206;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (BattleManager.Instance == null) return;
        var player = BattleManager.Instance.player;
        var enemy = BattleManager.Instance.enemy;
        if (player == null || enemy == null) return;

        foreach (var dot in player.dotBar)
        {
            dot.duration = 7;
        }

        foreach (var dot in enemy.dotBar)
        {
            dot.duration = 7;
        }
    }
}

public class 暴食 : BaseCard
{
    protected override int id => 1207;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        ulong value = Value;

        user.SetDamageTakenMultiplier(2f);
        Dot dot = null;
        dot = new Dot(user, user, Duration, d =>
        {
            d.source.ApplyHealthChange(ToLong(value), d.source);
            d.source.SetDamageTakenMultiplier(2f);
        }, d =>
        {
            if (d.source != null) d.source.SetDamageTakenMultiplier(1f);
        }, () => $"每回合恢复{value}点生命且受伤翻倍，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldSource != null) oldSource.SetDamageTakenMultiplier(1f);
            if (d.source != null) d.source.SetDamageTakenMultiplier(2f);
        });

        user.dotBar.Add(dot);
    }
}

public class 种子 : BaseCard
{
    protected override int id => 1300;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int seed = Guid.NewGuid().GetHashCode();
        UnityEngine.Random.InitState(seed);
    }
}

public class 骰子 : BaseCard
{
    protected override int id => 1301;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = CardRuntimeHelper.GetRandomDurationFromValue(Duration);
        if (duration <= 0) return;
        ulong upper = Value;

        Dot dot = null;
        dot = new Dot(user, target, duration, d =>
        {
            ulong damage = (ulong)UnityEngine.Random.Range(0, ToRandomUpperExclusive(upper));
            d.source.DealDamage(d.target, damage);
        }, null, () => $"每回合随机造成0~{upper}点伤害，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 命签 : BaseCard
{
    protected override int id => 1302;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = CardRuntimeHelper.GetRandomDurationFromValue(Duration);
        if (duration <= 0) return;
        ulong upper = Value;

        Dot dot = null;
        dot = new Dot(user, user, duration, d =>
        {
            ulong heal = (ulong)UnityEngine.Random.Range(0, ToRandomUpperExclusive(upper));
            d.source.ApplyHealthChange(ToLong(heal), d.source);
        }, null, () => $"每回合随机恢复0~{upper}点生命，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 运势 : BaseCard
{
    protected override int id => 1303;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = CardRuntimeHelper.GetRandomDurationFromValue(Duration);
        if (duration <= 0) return;
        ulong upper = Value;

        Dot dot = null;
        dot = new Dot(user, user, duration, d =>
        {
            ulong gain = (ulong)UnityEngine.Random.Range(0, ToRandomUpperExclusive(upper));
            d.source.ChangeMana(ToLong(gain));
        }, null, () => $"每回合随机获得0~{upper}点魔力，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 赌徒 : BaseCard
{
    protected override int id => 1304;
    private static readonly string[] RandomSeriesNames = { "种子", "骰子", "命签", "运势", "赌徒", "轮盘" };

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        ulong value = Value;
        float p = value <= 0 ? 0f : (float)value / (value + 1f);
        int safety = 64;

        while (safety-- > 0)
        {
            string name = RandomSeriesNames[UnityEngine.Random.Range(0, RandomSeriesNames.Length)];
            var card = CardFactory.GetThisCard(name);
            if (card != null) user.GainCard(card);

            if (UnityEngine.Random.value > p)
            {
                break;
            }
        }
    }
}

public class 轮盘 : BaseCard
{
    protected override int id => 1305;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (UnityEngine.Random.Range(0, 6) == 0)
        {
            user.DealDamage(target, Value);
            return;
        }

        user.DealDamage(user, 1);
    }
}

public class 苦修 : BaseCard
{
    protected override int id => 1400;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;

        Dot dot = null;
        Action<ulong, BaseCharacter> handler = (amount, source) =>
        {
            if (dot == null || dot.source == null) return;
            if (source != dot.source) return;
            dot.source.ChangeMana(ToLong(value));
        };

        user.DamageTaken += handler;
        dot = new Dot(user, user, duration, d => { }, d =>
        {
            if (d.source != null) d.source.DamageTaken -= handler;
        }, () => $"自伤后获得{value}点魔力，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldSource != null) oldSource.DamageTaken -= handler;
            if (d.source != null) d.source.DamageTaken += handler;
        });

        user.dotBar.Add(dot);
    }
}

public class 献祭 : BaseCard
{
    private static ulong sacrificeBonus = 0;
    protected override int id => 1401;

    public static void ResetSacrificeBonus()
    {
        sacrificeBonus = 0;
    }

    private ulong CurrentDamage => BaseCharacter.SaturatingAdd(Value, sacrificeBonus);

    public override string GetDynamicDescription()
    {
        if (string.IsNullOrEmpty(Description)) return Description;
        return Description
            .Replace("[费用]", Cost.ToString())
            .Replace("[数值]", CurrentDamage.ToString())
            .Replace("[持续时间]", Duration.ToString());
    }

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        ulong baseValue = Value;

        Dot dot = null;
        dot = new Dot(user, user, Duration, d =>
        {
            ulong damage = BaseCharacter.SaturatingAdd(baseValue, sacrificeBonus);
            d.source.DealDamage(d.source, damage);
            if (d.target != null) d.source.DealDamage(d.target, damage);
            sacrificeBonus = BaseCharacter.SaturatingAdd(sacrificeBonus, 1);
        }, null, () => $"每回合对双方造成{CurrentDamage}点伤害（献祭永久+1），剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 卖血 : BaseCard
{
    protected override int id => 1402;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        user.ApplyHealthChange(NegToLong(Value), user);
        var card = user.DrawCard(0);
        if (card != null && user is Player)
        {
            EventCenter.Publish("Player_DrawCard", card);
        }
    }
}

public class 反伤 : BaseCard
{
    protected override int id => 1403;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;

        Dot dot = null;
        Action<ulong, BaseCharacter> handler = (amount, source) =>
        {
            if (dot == null || dot.source == null) return;
            if (BaseCharacter.IsReflectDamageContext) return;
            if (dot.target != null) dot.source.DealReflectDamage(dot.target, amount);
        };

        user.DamageTaken += handler;
        dot = new Dot(user, target, Duration, d => { }, d =>
        {
            if (d.source != null) d.source.DamageTaken -= handler;
        }, () => $"受伤反弹等量伤害，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldSource != null) oldSource.DamageTaken -= handler;
            if (d.source != null) d.source.DamageTaken += handler;
        });

        user.dotBar.Add(dot);
    }
}

public class 血契 : BaseCard
{
    protected override int id => 1404;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;

        Dot dot = null;
        Action<ulong, BaseCharacter> handler = (amount, victim) =>
        {
            if (dot == null || dot.source == null) return;
            dot.source.ApplyHealthChange(ToLong(amount), dot.source);
        };

        user.DamageDealt += handler;
        dot = new Dot(user, user, Duration, d => { }, d =>
        {
            if (d.source != null) d.source.DamageDealt -= handler;
        }, () => $"造成伤害时吸血，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldSource != null) oldSource.DamageDealt -= handler;
            if (d.source != null) d.source.DamageDealt += handler;
        });

        user.dotBar.Add(dot);
    }
}

public class 偷窃 : BaseCard
{
    protected override int id => 1500;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0 || target == null) return;
        int stealCount = ToInt(Value);
        if (stealCount <= 0) return;

        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            if (d.target == null || d.target.Cards.Count == 0) return;
            int count = Mathf.Min(stealCount, d.target.Cards.Count);
            for (int i = 0; i < count; i++)
            {
                if (d.target.Cards.Count == 0) break;
                int index = UnityEngine.Random.Range(0, d.target.Cards.Count);
                BaseCard stolen = d.target.Cards[index];
                stolen.MarkStolenFromOpponent();
                d.target.RemoveCard(stolen);
                d.source.GainCard(stolen);
            }
        }, null, () => $"每回合偷取{stealCount}张卡牌，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 偷魔 : BaseCard
{
    protected override int id => 1501;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0 || target == null) return;

        Dot dot = null;
        dot = new Dot(user, target, duration, d =>
        {
            if (d.target == null || d.target.mana <= 0) return;
            d.target.ChangeMana(NegToLong(value));
            d.source.ChangeMana(ToLong(value));
        }, null, () => $"每回合偷取{value}点魔力，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 偷梁 : BaseCard
{
    protected override int id => 1502;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (target == null) return;
        int count = ToInt(Value);
        if (count <= 0) return;

        var positiveDots = new List<Dot>();
        foreach (var dot in target.dotBar)
        {
            if (dot.target == target) positiveDots.Add(dot);
        }

        if (positiveDots.Count == 0) return;

        int stealCount = Mathf.Min(count, positiveDots.Count);
        for (int i = 0; i < stealCount; i++)
        {
            if (positiveDots.Count == 0) break;
            int index = UnityEngine.Random.Range(0, positiveDots.Count);
            Dot stolen = positiveDots[index];
            positiveDots.RemoveAt(index);
            target.dotBar.Remove(stolen);
            stolen.TransferTo(user);
            stolen.MarkStolenFromOpponent();
            user.dotBar.Add(stolen);
        }
    }
}

public class 未来 : BaseCard
{
    protected override int id => 1503;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int delay = Duration;
        int sustain = Duration;
        ulong value = Value;
        if (delay <= 0 || sustain <= 0) return;

        int total = delay + sustain;
        Dot dot = null;
        dot = new Dot(user, user, total, d =>
        {
            if (delay > 0)
            {
                delay--;
                return;
            }
            d.source.ApplyHealthChange(ToLong(value), d.source);
        }, null, () => delay > 0 ? $"{delay}回合后开始恢复" : $"每回合恢复{value}点生命，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 破甲 : BaseCard
{
    protected override int id => 1504;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (target == null || Duration <= 0) return;

        float multiplier = Mathf.Max(0f, ToFloat(Value));
        target.SetDamageTakenMultiplier(multiplier);

        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            d.target.SetDamageTakenMultiplier(multiplier);
        }, d =>
        {
            d.target.SetDamageTakenMultiplier(1f);
        }, () => $"敌方受伤变为{multiplier}倍，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldTarget != null) oldTarget.SetDamageTakenMultiplier(1f);
            if (d.target != null) d.target.SetDamageTakenMultiplier(multiplier);
        });

        user.dotBar.Add(dot);
    }
}

public class 羽化 : BaseCard
{
    protected override int id => 1505;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int discardCount = user.Cards.Count;
        if (discardCount <= 0) return;

        CardRuntimeHelper.DiscardAllCards(user);
        user.ChangeMana(discardCount);
    }
}

public class 制衡 : BaseCard
{
    protected override int id => 1506;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int discardCount = user.Cards.Count;
        if (discardCount <= 0) return;

        CardRuntimeHelper.DiscardAllCards(user);
        for (int i = 0; i < discardCount; i++)
        {
            user.GainRandomCard();
        }
    }
}

public class 诅咒 : BaseCard
{
    protected override int id => 1508;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (target == null || Duration <= 0) return;
        ulong value = Value;

        Dot dot = null;
        Action<object> handler = obj =>
        {
            if (dot == null || dot.target == null) return;
            if (!ReferenceEquals(obj, dot.target)) return;
            dot.source.DealDamage(dot.target, value);
        };

        EventCenter.Register("Character_PlayCardExecuted", handler);
        dot = new Dot(user, target, Duration, d => { }, d =>
        {
            EventCenter.Unregister("Character_PlayCardExecuted", handler);
        }, () => $"敌方每出牌受到{value}点伤害，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            EventCenter.Unregister("Character_PlayCardExecuted", handler);
            EventCenter.Register("Character_PlayCardExecuted", handler);
        });

        user.dotBar.Add(dot);
    }
}

public class 镜像 : BaseCard
{
    protected override int id => 1700;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        ulong value = Value;

        Dot dot = null;
        Action<object> handler = obj =>
        {
            if (dot == null || dot.source == null || dot.target == null) return;
            if (!ReferenceEquals(obj, dot.source)) return;
            dot.source.DealDamage(dot.target, value);
        };

        EventCenter.Register("Character_PlayCardExecuted", handler);
        dot = new Dot(user, target, Duration, d => { }, d =>
        {
            EventCenter.Unregister("Character_PlayCardExecuted", handler);
        }, () => $"每次出牌自动造成{value}点伤害，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            EventCenter.Unregister("Character_PlayCardExecuted", handler);
            EventCenter.Register("Character_PlayCardExecuted", handler);
        });

        user.dotBar.Add(dot);
    }
}

public class 激光 : BaseCard
{
    protected override int id => 1702;
    private static ulong globalBonusDamage = 0;
    private static ulong globalExtraCost = 0;

    public static void ResetGlobalState()
    {
        globalBonusDamage = 0;
        globalExtraCost = 0;
    }

    public 激光()
    {
        if (globalExtraCost > 0)
        {
            AddCost(globalExtraCost);
        }
    }

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        ulong damage = BaseCharacter.SaturatingAdd(1, globalBonusDamage);
        user.DealDamage(target, damage);

        globalBonusDamage = BaseCharacter.SaturatingAdd(globalBonusDamage, Value);
        globalExtraCost = BaseCharacter.SaturatingAdd(globalExtraCost, 1);
    }
}

public class 黑洞 : BaseCard
{
    protected override int id => 1800;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (BattleManager.Instance == null) return;
        CardRuntimeHelper.DiscardAllCards(BattleManager.Instance.player);
        CardRuntimeHelper.DiscardAllCards(BattleManager.Instance.enemy);
    }
}

public class 奇点 : BaseCard
{
    protected override int id => 1801;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (BattleManager.Instance == null || Duration <= 0) return;
        ulong fixedCost = Value;

        CardRuntimeHelper.SetHandCost(BattleManager.Instance.player, fixedCost);
        CardRuntimeHelper.SetHandCost(BattleManager.Instance.enemy, fixedCost);

        Dot dot = null;
        dot = new Dot(user, user, Duration, d =>
        {
            if (BattleManager.Instance == null) return;
            CardRuntimeHelper.SetHandCost(BattleManager.Instance.player, fixedCost);
            CardRuntimeHelper.SetHandCost(BattleManager.Instance.enemy, fixedCost);
        }, null, () => $"双方手牌费用固定为{fixedCost}，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public static class CardNumberUtil
{
    public static int ToInt(ulong value)
    {
        return value >= int.MaxValue ? int.MaxValue : (int)value;
    }

    public static long ToLong(ulong value)
    {
        return value >= (ulong)long.MaxValue ? long.MaxValue : (long)value;
    }

    public static long NegToLong(ulong value)
    {
        return value >= (ulong)long.MaxValue ? -long.MaxValue : -(long)value;
    }

    public static float ToFloat(ulong value)
    {
        return value >= (ulong)int.MaxValue ? int.MaxValue : value;
    }

    public static int ToRandomUpperExclusive(ulong inclusiveUpper)
    {
        if (inclusiveUpper >= int.MaxValue) return int.MaxValue;
        return (int)inclusiveUpper + 1;
    }
}
