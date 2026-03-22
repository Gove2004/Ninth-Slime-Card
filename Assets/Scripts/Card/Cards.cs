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
        if (Duration <= 0) return;
        Dot dot = null;
        Action<ulong, BaseCharacter> handler = (amount, victim) =>
        {
            if (dot == null || dot.source == null) return;
            if (!BaseCharacter.TryEnterNestedTrigger(dot.sourceCard, out BaseCard previousContext)) return;
            try
            {
                dot.source.ApplyHealthChange(ToLong(amount), dot.source);
            }
            finally
            {
                BaseCharacter.ExitNestedTrigger(dot.sourceCard, previousContext);
            }
        };

        user.DamageDealt += handler;
        dot = new Dot(user, target, Duration, d => { }, d =>
        {
            if (d.source != null) d.source.DamageDealt -= handler;
        }, () => $"造成伤害后恢复等量生命，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldSource != null) oldSource.DamageDealt -= handler;
            if (d.source != null) d.source.DamageDealt += handler;
        });
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
        ulong growth = BaseCharacter.SaturatingMultiply(Value, 2);

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
        user.ApplyHealthChange(ToLong(BaseCharacter.SaturatingMultiply(value, 3)), user);
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
    private BaseCard mirroredCard;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (mirroredCard == null) return;
        mirroredCard.Execute(user, target);
    }

    public void TransformInto(BaseCard sourceCard)
    {
        if (sourceCard == null) return;
        BaseCard effectiveSource = sourceCard;
        if (effectiveSource is 重奏 reprise)
        {
            BaseCard repriseMirror = reprise.GetMirroredCard();
            if (repriseMirror == null) return;
            effectiveSource = repriseMirror;
        }

        BaseCard nextMirror = CardFactory.GetThisCard(effectiveSource.Name);
        if (nextMirror == null) return;

        nextMirror.SetCost(effectiveSource.Cost);
        nextMirror.SetValue(effectiveSource.Value);
        nextMirror.SetDuration(effectiveSource.Duration);
        if (effectiveSource.IsStolenFromOpponent) nextMirror.MarkStolenFromOpponent();

        mirroredCard = nextMirror;
        SetCost(nextMirror.Cost);
        SetValue(nextMirror.Value);
        SetDuration(nextMirror.Duration);
    }

    public BaseCard GetMirroredCard()
    {
        return mirroredCard;
    }

    public override string GetDisplayName()
    {
        if (mirroredCard == null) return Name;
        return $"{mirroredCard.Name}·重奏";
    }

    public override string GetDynamicDescription()
    {
        if (mirroredCard == null) return base.GetDynamicDescription();
        return $"{mirroredCard.GetDynamicDescription()}\n重奏";
    }

    public override ulong GetDisplayCost()
    {
        if (mirroredCard == null) return Cost;
        return mirroredCard.Cost;
    }

    public override string GetDisplayImagePath()
    {
        if (mirroredCard == null) return ImagePath;
        return mirroredCard.ImagePath;
    }

    public override bool IsMirageCard => mirroredCard != null;
}

public class 七罪 : BaseCard
{
    protected override int id => 1200;
    private static readonly string[] SinNames = { "暴怒", "傲慢", "嫉妒", "贪婪", "懒惰", "色欲", "暴食" };

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        int gainCount = ToInt(Value);
        int initialGain = Mathf.Min(gainCount, SinNames.Length);
        for (int i = 0; i < initialGain; i++)
        {
            var initialCard = CardFactory.GetThisCard(SinNames[i]);
            if (initialCard != null) user.GainCard(initialCard);
        }

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
        ulong value = Value;
        if (value == 0) return;
        user.DealDamage(target, value);
        float selfDamageProbability = Mathf.Clamp01(ToFloat(value) / 100f);
        if (UnityEngine.Random.value < selfDamageProbability)
        {
            user.DealDamage(user, value);
        }
    }
}

public class 傲慢 : BaseCard
{
    protected override int id => 1202;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Mathf.Max(1, Duration);
        int remainingHits = Mathf.Max(0, ToInt(Value));
        if (remainingHits <= 0) return;

        Dot dot = null;
        Action<ulong, BaseCharacter> handler = (amount, source) =>
        {
            if (dot == null || dot.source == null) return;
            if (remainingHits <= 0 || amount == 0) return;
            if (!BaseCharacter.TryEnterNestedTrigger(dot.sourceCard, out BaseCard previousContext)) return;
            try
            {
                dot.source.ApplyHealthChange(ToLong(amount), dot.source);
                remainingHits--;
            }
            finally
            {
                BaseCharacter.ExitNestedTrigger(dot.sourceCard, previousContext);
            }
        };

        user.DamageTaken += handler;
        dot = new Dot(user, user, duration, d => { }, d =>
        {
            if (d.source != null) d.source.DamageTaken -= handler;
        }, () => $"剩余免疫{remainingHits}次，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldSource != null) oldSource.DamageTaken -= handler;
            if (d.source != null) d.source.DamageTaken += handler;
        });

        user.dotBar.Add(dot);
    }
}

public class 嫉妒 : BaseCard
{
    protected override int id => 1203;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (target == null) return;
        ulong maxSteal = Value;
        ulong actualSteal = user.mana < maxSteal ? user.mana : maxSteal;
        if (actualSteal == 0) return;
        user.ChangeMana(NegToLong(actualSteal));

        for (ulong i = 0; i < actualSteal; i++)
        {
            List<int> available = new();
            if (target.Cards.Count > 0) available.Add(0);
            if (target.mana > 0) available.Add(1);
            if (target.dotBar.Count > 0) available.Add(2);
            if (target.health > 0) available.Add(3);
            if (available.Count == 0) break;

            int stealType = available[UnityEngine.Random.Range(0, available.Count)];
            if (stealType == 0)
            {
                int index = UnityEngine.Random.Range(0, target.Cards.Count);
                BaseCard stolen = target.Cards[index];
                stolen.MarkStolenFromOpponent();
                target.RemoveCard(stolen);
                user.GainCard(stolen);
                continue;
            }

            if (stealType == 1)
            {
                target.ChangeMana(-1);
                user.ChangeMana(1);
                continue;
            }

            if (stealType == 2)
            {
                int index = UnityEngine.Random.Range(0, target.dotBar.Count);
                Dot stolen = target.dotBar[index];
                target.dotBar.RemoveAt(index);
                stolen.TransferTo(user);
                stolen.MarkStolenFromOpponent();
                user.dotBar.Add(stolen);
                continue;
            }

            target.ApplyHealthChange(-1, user);
            user.ApplyHealthChange(1, user);
        }
    }
}

public class 贪婪 : BaseCard
{
    protected override int id => 1204;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int maxDiscard = Mathf.Max(0, ToInt(Value));
        int discardCount = Mathf.Min(maxDiscard, user.Cards.Count);
        if (discardCount <= 0) return;
        for (int i = 0; i < discardCount; i++)
        {
            if (user.Cards.Count == 0) break;
            BaseCard card = user.Cards[0];
            user.RemoveCard(card);
            if (user is Player) EventCenter.Publish("Player_PlayCard", card);
        }
        user.ChangeMana(discardCount);
        user.DealDamage(target, (ulong)discardCount);
    }
}

public class 懒惰 : BaseCard
{
    protected override int id => 1205;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        ulong spend = user.mana < Value ? user.mana : Value;
        if (spend == 0)
        {
            user.EndTurn();
            return;
        }

        user.ChangeMana(NegToLong(spend));
        int drawCount = ToInt(spend);
        for (int i = 0; i < drawCount; i++)
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
        int addDuration = ToInt(Value);
        if (addDuration <= 0 || BattleManager.Instance == null) return;
        var player = BattleManager.Instance.player;
        var enemy = BattleManager.Instance.enemy;
        if (player == null || enemy == null) return;

        List<Dot> allDots = new();
        allDots.AddRange(player.dotBar);
        allDots.AddRange(enemy.dotBar);
        if (allDots.Count == 0) return;

        Dot randomDot = allDots[UnityEngine.Random.Range(0, allDots.Count)];
        if (randomDot != null)
        {
            randomDot.duration += addDuration;
        }
    }
}

public class 暴食 : BaseCard
{
    protected override int id => 1207;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        ulong value = Value;
        int eatCount = Mathf.Min(ToInt(value), user.Cards.Count);
        if (eatCount <= 0) return;
        for (int i = 0; i < eatCount; i++)
        {
            BaseCard card = user.Cards[0];
            user.RemoveCard(card);
            if (user is Player) EventCenter.Publish("Player_PlayCard", card);
            user.ApplyHealthChange(ToLong(value), user);
        }
    }
}

public class 种子 : BaseCard
{
    protected override int id => 1300;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int seed = Guid.NewGuid().GetHashCode();
        UnityEngine.Random.InitState(seed);
        if (IsPrime(Mathf.Abs(seed)))
        {
            user.ChangeMana(999);
        }
    }

    private static bool IsPrime(int value)
    {
        if (value <= 1) return false;
        if (value == 2) return true;
        if ((value & 1) == 0) return false;
        int limit = (int)Math.Sqrt(value);
        for (int i = 3; i <= limit; i += 2)
        {
            if (value % i == 0) return false;
        }
        return true;
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
        int maxDuration = Duration;

        Dot dot = null;
        dot = new Dot(user, target, duration, d =>
        {
            ulong damage = (ulong)UnityEngine.Random.Range(0, ToRandomUpperExclusive(upper));
            d.source.DealDamage(d.target, damage);
        }, null, () => $"每回合随机造成0~{upper}点伤害，持续1~{maxDuration}回合，剩余{dot.duration}回合");

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
        int maxDuration = Duration;

        Dot dot = null;
        dot = new Dot(user, user, duration, d =>
        {
            ulong heal = (ulong)UnityEngine.Random.Range(0, ToRandomUpperExclusive(upper));
            d.source.ApplyHealthChange(ToLong(heal), d.source);
        }, null, () => $"每回合随机恢复0~{upper}点生命，持续1~{maxDuration}回合，剩余{dot.duration}回合");

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
        int maxDuration = Duration;

        Dot dot = null;
        dot = new Dot(user, user, duration, d =>
        {
            ulong gain = (ulong)UnityEngine.Random.Range(0, ToRandomUpperExclusive(upper));
            d.source.ChangeMana(ToLong(gain));
        }, null, () => $"每回合随机获得0~{upper}点魔力，持续1~{maxDuration}回合，剩余{dot.duration}回合");

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
        if (p <= 0f) return;
        int safety = 64;

        while (safety-- > 0)
        {
            if (UnityEngine.Random.value > p)
            {
                break;
            }
            string name = RandomSeriesNames[UnityEngine.Random.Range(0, RandomSeriesNames.Length)];
            var card = CardFactory.GetThisCard(name);
            if (card != null) user.GainCard(card);
        }
    }
}

public class 轮盘 : BaseCard
{
    protected override int id => 1305;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        ulong hitValue = BaseCharacter.SaturatingMultiply(Value, 6);
        ulong selfDamage = Value / 6;

        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            if (UnityEngine.Random.Range(0, 6) == 0)
            {
                d.source.DealDamage(d.target, hitValue);
                return;
            }

            d.source.DealDamage(d.source, selfDamage);
        }, null, () => $"1/6概率对敌造成{hitValue}伤害，否则对自己造成{selfDamage}伤害，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
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
            if (!BaseCharacter.TryEnterNestedTrigger(dot.sourceCard, out BaseCard previousContext)) return;
            try
            {
                dot.source.ChangeMana(ToLong(value));
            }
            finally
            {
                BaseCharacter.ExitNestedTrigger(dot.sourceCard, previousContext);
            }
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
        BaseCharacter effectTarget = target ?? user.Target;
        if (effectTarget == null && BattleManager.Instance != null)
        {
            effectTarget = user == BattleManager.Instance.player ? BattleManager.Instance.enemy : BattleManager.Instance.player;
        }

        Dot dot = null;
        dot = new Dot(user, effectTarget, Duration, d =>
        {
            ulong damage = BaseCharacter.SaturatingAdd(baseValue, sacrificeBonus);
            d.source.DealDamage(d.source, damage);
            if (d.target != null && !ReferenceEquals(d.target, d.source)) d.source.DealDamage(d.target, damage);
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
        int duration = Duration;
        ulong value = Value;
        int drawCount = ToInt(value / 2);
        if (duration <= 0) return;

        Dot dot = null;
        dot = new Dot(user, user, duration, d =>
        {
            d.source.ApplyHealthChange(NegToLong(value), d.source);
            for (int i = 0; i < drawCount; i++)
            {
                var card = d.source.DrawCard(0);
                if (card != null && d.source is Player)
                {
                    EventCenter.Publish("Player_DrawCard", card);
                }
            }
        }, null, () => $"每回合失{value}点生命并抽{drawCount}张牌，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
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
            if (dot.target == null) return;
            if (!BaseCharacter.TryEnterNestedTrigger(dot.sourceCard, out BaseCard previousContext)) return;
            try
            {
                dot.source.DealReflectDamage(dot.target, amount);
            }
            finally
            {
                BaseCharacter.ExitNestedTrigger(dot.sourceCard, previousContext);
            }
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
        BaseCharacter effectTarget = target ?? user?.Target;
        if (effectTarget == null && BattleManager.Instance != null)
        {
            effectTarget = user == BattleManager.Instance.player ? BattleManager.Instance.enemy : BattleManager.Instance.player;
        }

        Dot dot = null;
        Action<ulong> handler = amount =>
        {
            if (dot == null || dot.source == null || dot.target == null) return;
            if (amount == 0) return;
            if (!BaseCharacter.TryEnterNestedTrigger(dot.sourceCard, out BaseCard previousContext)) return;
            try
            {
                dot.source.DealDamage(dot.target, amount);
            }
            finally
            {
                BaseCharacter.ExitNestedTrigger(dot.sourceCard, previousContext);
            }
        };

        user.HealTaken += handler;
        dot = new Dot(user, effectTarget, Duration, d => { }, d =>
        {
            if (d.source != null) d.source.HealTaken -= handler;
        }, () => $"恢复生命时造成等量伤害，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldSource != null) oldSource.HealTaken -= handler;
            if (d.source != null) d.source.HealTaken += handler;
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
            ulong stolen = d.target.mana < value ? d.target.mana : value;
            if (stolen == 0) return;
            d.target.ChangeMana(NegToLong(stolen));
            d.source.ChangeMana(ToLong(stolen));
        }, null, () => $"每回合偷取{value}点魔力，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 偷月 : BaseCard
{
    protected override int id => 1502;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (target == null || Duration <= 0) return;
        int count = ToInt(Value);
        if (count <= 0) return;

        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            if (d.target == null || d.target.dotBar.Count == 0) return;
            int stealCount = Mathf.Min(count, d.target.dotBar.Count);
            for (int i = 0; i < stealCount; i++)
            {
                if (d.target.dotBar.Count == 0) break;
                int index = UnityEngine.Random.Range(0, d.target.dotBar.Count);
                Dot stolen = d.target.dotBar[index];
                d.target.dotBar.RemoveAt(index);
                stolen.TransferTo(d.source);
                stolen.MarkStolenFromOpponent();
                d.source.dotBar.Add(stolen);
            }
        }, null, () => $"每回合偷取{count}个效果，剩余{dot.duration}回合");

        user.dotBar.Add(dot);
    }
}

public class 未来 : BaseCard
{
    protected override int id => 1503;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int delay = 3;
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
            var card = user.DrawCard(0);
            if (card != null && user is Player)
            {
                EventCenter.Publish("Player_DrawCard", card);
            }
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
            if (!BaseCharacter.TryEnterNestedTrigger(dot.sourceCard, out BaseCard previousContext)) return;
            try
            {
                dot.source.DealDamage(dot.target, value);
            }
            finally
            {
                BaseCharacter.ExitNestedTrigger(dot.sourceCard, previousContext);
            }
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
            if (!BaseCharacter.TryEnterNestedTrigger(dot.sourceCard, out BaseCard previousContext)) return;
            try
            {
                dot.source.DealDamage(dot.target, value);
            }
            finally
            {
                BaseCharacter.ExitNestedTrigger(dot.sourceCard, previousContext);
            }
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
    private static ulong playerBonusDamage = 0;
    private static ulong enemyBonusDamage = 0;

    public static void ResetGlobalState()
    {
        playerBonusDamage = 0;
        enemyBonusDamage = 0;
    }

    private static bool IsPlayerCharacter(BaseCharacter character)
    {
        if (character == null) return true;
        if (character is Player) return true;
        if (character is EnemyBoss) return false;
        if (BattleManager.Instance == null) return true;
        if (ReferenceEquals(character, BattleManager.Instance.player)) return true;
        if (ReferenceEquals(character, BattleManager.Instance.enemy)) return false;
        return true;
    }

    private static ulong GetBonusForCharacter(BaseCharacter character)
    {
        return IsPlayerCharacter(character) ? playerBonusDamage : enemyBonusDamage;
    }

    private static void IncreaseBonusForCharacter(BaseCharacter character)
    {
        if (IsPlayerCharacter(character))
        {
            playerBonusDamage = BaseCharacter.SaturatingAdd(playerBonusDamage, 1);
            return;
        }
        enemyBonusDamage = BaseCharacter.SaturatingAdd(enemyBonusDamage, 1);
    }

    private ulong GetDisplayDamage()
    {
        return BaseCharacter.SaturatingAdd(Value, GetBonusForCharacter(OwningCharacter));
    }

    public override string GetDynamicDescription()
    {
        if (string.IsNullOrEmpty(Description)) return Description;
        return Description
            .Replace("[费用]", Cost.ToString())
            .Replace("[数值]", GetDisplayDamage().ToString())
            .Replace("[持续时间]", Duration.ToString());
    }

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        ulong damage = BaseCharacter.SaturatingAdd(Value, GetBonusForCharacter(user));
        user.DealDamage(target, damage);
        IncreaseBonusForCharacter(user);
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
        if (BattleManager.Instance == null) return;
        BattleManager.Instance.player.dotBar.Clear();
        BattleManager.Instance.enemy.dotBar.Clear();
    }
}

public class 时域 : BaseCard
{
    protected override int id => 1802;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (user == null) return;

        BaseCharacter effectTarget = target ?? user.Target;
        if (effectTarget == null && BattleManager.Instance != null)
        {
            effectTarget = user == BattleManager.Instance.player ? BattleManager.Instance.enemy : BattleManager.Instance.player;
        }

        ulong spentMana = user.mana;
        if (spentMana == 0) return;

        user.ChangeMana(NegToLong(spentMana));
        effectTarget?.ChangeMana(NegToLong(spentMana));
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
