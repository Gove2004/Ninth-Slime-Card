using System;
using UnityEngine;
using static CardNumberUtil;


public class 抽牌 : BaseCard
{
    protected override int id => 1000;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int count = ToInt(Value);
        if (count <= 0) return;
        for (int i = 0; i < count; i++)
        {
            var card = user.DrawCard(0);
            if (card != null && user is Player)
            {
                EventCenter.Publish("Player_DrawCard", card);
                if (card.Name == "抽牌")
                {
                    EventCenter.Publish("Achievement_DrawDrawCard", card);
                }
            }
        }
    }
}



public class 流血 : BaseCard
{
    protected override int id => 1001;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, target, duration, d => d.source.DealDamage(d.target, value), null, () => $"每回合对敌人造成{value}点伤害，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 恢复 : BaseCard
{
    protected override int id => 1002;

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
    protected override int id => 1003;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, user, duration, d => d.target.ChangeMana(ToLong(value)), null, () => $"每回合额外获得{value}点魔力，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 贪婪 : BaseCard
{
    protected override int id => 1004;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        int drawCount = ToInt(Value);
        Dot dot = null;
        dot = new Dot(user, user, Duration, d =>
        {
            for (int i = 0; i < drawCount; i++)
            {
                var card = d.source.DrawCard(0);
                if (card != null && d.source is Player)
                {
                    EventCenter.Publish("Player_DrawCard", card);
                }
            }
        }, null, () => $"每回合抽{drawCount}张牌，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}

public class 吸取 : BaseCard
{
    protected override int id => 1005;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, target, duration, d =>
        {
            d.source.DealDamage(d.target, value);
            d.source.ApplyHealthChange(ToLong(BaseCharacter.SaturatingMultiply(value, 2)), d.source);
        }, null, () => $"每回合对敌人造成{value}点伤害并恢复{BaseCharacter.SaturatingMultiply(value, 2)}点生命，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 爆发 : BaseCard
{
    protected override int id => 1006;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        ulong manaSpent = user.mana;
        if (manaSpent <= 0) return;
        user.ChangeMana(NegToLong(manaSpent));
        ulong damage = BaseCharacter.SaturatingMultiply(manaSpent, Value);
        int duration = ToInt(manaSpent);
        Dot dot = null;
        dot = new Dot(user, target, duration, d => d.source.DealDamage(d.target, damage), null, () => $"每回合造成{damage}点伤害，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 第7张牌 : BaseCard
{
    protected override int id => 1007;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        var triggeredEffects = new System.Collections.Generic.HashSet<int>();
        bool reportedAll = false;
        Dot dot = null;
        dot = new Dot(user, target, duration, d =>
        {
            int effectIndex = UnityEngine.Random.Range(0, 7);
            triggeredEffects.Add(effectIndex);
            if (!reportedAll && triggeredEffects.Count == 7 && user is Player)
            {
                reportedAll = true;
                EventCenter.Publish("Achievement_SevenSinsAllEffects", this);
            }
            if (effectIndex == 0)
            {
                ulong damage = BaseCharacter.SaturatingMultiply(value, 11);
                if (UnityEngine.Random.value < 0.77f)
                {
                    damage = BaseCharacter.SaturatingMultiply(damage, 2);
                    if (UnityEngine.Random.value < 0.77f)
                    {
                        damage = value / 7;
                    }
                }
                d.source.DealDamage(d.target, damage);
            }
            else if (effectIndex == 1)
            {
                d.source.SetImmuneThisTurn(true);
            }
            else if (effectIndex == 2)
            {
                int stealCount = ToInt(value);
                for (int i = 0; i < stealCount; i++)
                {
                    bool stolen = false;
                    for (int attempt = 0; attempt < 3 && !stolen; attempt++)
                    {
                        int pick = UnityEngine.Random.Range(0, 3);
                        if (pick == 0)
                        {
                            if (d.target != null && d.target.Cards.Count > 0)
                            {
                                int index = UnityEngine.Random.Range(0, d.target.Cards.Count);
                BaseCard stolenCard = d.target.Cards[index];
                stolenCard.MarkStolenFromOpponent();
                d.target.RemoveCard(stolenCard);
                d.source.GainCard(stolenCard);
                stolen = true;
                            }
                        }
                        else if (pick == 1)
                        {
                            if (d.target != null && d.target.dotBar.Count > 0)
                            {
                                int index = UnityEngine.Random.Range(0, d.target.dotBar.Count);
                                Dot stolenDot = d.target.dotBar[index];
                                d.target.dotBar.RemoveAt(index);
                                stolenDot.TransferTo(d.source);
                                stolenDot.MarkStolenFromOpponent();
                                d.source.dotBar.Add(stolenDot);
                                stolen = true;
                            }
                        }
                        else
                        {
                            if (d.target != null && d.target.mana > 0)
                            {
                                d.target.ChangeMana(-1);
                                d.source.ChangeMana(1);
                                stolen = true;
                            }
                        }
                    }
                    if (!stolen) break;
                }
            }
            else if (effectIndex == 3)
            {
                d.source.ChangeMana(ToLong(value));
                if (d.source.Cards.Count > 0)
                {
                    if (d.source is Player)
                    {
                        var discardList = new System.Collections.Generic.List<BaseCard>(d.source.Cards);
                        foreach (var card in discardList)
                        {
                            d.source.Cards.Remove(card);
                            EventCenter.Publish("Player_PlayCard", card);
                        }
                    }
                    else
                    {
                        d.source.Cards.Clear();
                    }
                }
            }
            else if (effectIndex == 4)
            {
                int count = ToInt(value);
                for (int i = 0; i < count; i++)
                {
                    d.source.GainRandomCard();
                }
                d.source.EndTurn();
            }
            else if (effectIndex == 5)
            {
                int add = ToInt(value);
                if (add > 0)
                {
                    foreach (var otherDot in d.source.dotBar)
                    {
                        if (otherDot == d) continue;
                        if (otherDot.sourceCard is 第7张牌) continue;
                        otherDot.duration += add;
                    }
                }
            }
            else
            {
                ulong heal = BaseCharacter.SaturatingMultiply(value, 11);
                if (UnityEngine.Random.value < 0.07f)
                {
                    heal = value / 7;
                }
                d.source.ApplyHealthChange(ToLong(heal), d.source);
            }
        }, null, () =>
        {
            ulong majorValue = BaseCharacter.SaturatingMultiply(value, 11);
            ulong minorValue = value / 7;
            return $"每回合随机触发一种“七宗罪”效果，剩余{dot.duration}回合\n七种效果分别为：1.暴怒，造成{majorValue}点伤害，此伤害有77％翻倍，若翻倍，此伤害有77％概率被重置为{minorValue}。2.傲慢，无敌一回合。3.嫉妒，随机偷取敌方总计{value}个牌、dot、或魔力。4.贪婪，获得{value}点魔力值并弃掉所有手牌。5.懒惰，随机获得{value}张牌并跳过当前回合。6.色欲，使你所有其他非“七宗罪”dot的持续时间延长{value}回合。7.暴食，恢复{majorValue}点生命值，有7％的概率只回复{minorValue}点生命值";
        });
        user.dotBar.Add(dot);
    }
}


public class 上三角 : BaseCard
{
    protected override int id => 1008;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        ulong baseValue = Value;
        int timer = 0;
        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            timer++;
            ulong damage = BaseCharacter.SaturatingMultiply(baseValue, (ulong)Math.Max(0, timer));
            d.source.DealDamage(d.target, damage);
        }, null, () => $"每回合对敌人造成{BaseCharacter.SaturatingMultiply(baseValue, (ulong)Math.Max(0, timer + 1))}点伤害(下回合+{baseValue})，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 下三角 : BaseCard
{
    protected override int id => 1009;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        ulong damage = Value;
        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            d.source.DealDamage(d.target, damage);
            damage /= 2;
        }, null, () => $"每回合对敌人造成{damage}点伤害并使此伤害值减半，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 延续 : BaseCard
{
    protected override int id => 1010;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int add = ToInt(Value);
        if (add == 0) return;
        foreach (var dot in user.dotBar)
        {
            dot.duration += add;
        }
        foreach (var card in user.Cards)
        {
            card.AddDuration(add);
        }
    }
}


public class 超频 : BaseCard
{
    protected override int id => 1011;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        ulong factor = Value;
        if (factor <= 1) return;
        user.ApplyOverclock(factor);
    }
}


public class 偷窃 : BaseCard
{
    protected override int id => 1012;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        int stealCount = ToInt(Value);
        if (stealCount == 0) return;
            Dot dot = null;
            dot = new Dot(user, target, Duration, d =>
        {
            if (d.target == null || d.target.Cards.Count == 0) return;
            int count = Mathf.Min(stealCount, d.target.Cards.Count);
            for (int i = 0; i < count; i++)
            {
                int index = UnityEngine.Random.Range(0, d.target.Cards.Count);
                BaseCard stolen = d.target.Cards[index];
                stolen.MarkStolenFromOpponent();
                d.target.RemoveCard(stolen);
                d.source.GainCard(stolen);
                if (d.target.Cards.Count == 0) break;
            }
            }, null, () => $"每回合随机偷取敌人的{stealCount}张卡牌，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 结算 : BaseCard
{
    protected override int id => 1013;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int times = ToInt(Value);
        for (int i = 0; i < times; i++)
        {
            user.TriggerDotsOnce();
            target?.TriggerDotsOnce();
        }
    }
}


public class 疯狂 : BaseCard
{
    protected override int id => 1014;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, target, duration, d => d.source.DealDamage(d.target, value), null, () => $"每回合对敌人造成{value}点伤害，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 彻底疯狂 : BaseCard
{
    protected override int id => 1015;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        int actualDuration = UnityEngine.Random.Range(1, duration + 1);
        Dot dot = null;
        dot = new Dot(user, target, actualDuration, d =>
        {
            ulong damage = (ulong)UnityEngine.Random.Range(0, ToRandomUpperExclusive(value));
            d.source.DealDamage(d.target, damage);
        }, null, () => $"每回合造成0~{value}点随机伤害，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 增援未来 : BaseCard
{
    protected override int id => 1016;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int delay = 3;
        int duration = Duration;
        ulong value = Value;
        Dot dot = null;
        dot = new Dot(user, user, duration + delay, d =>
            {
                if (delay > 0)
                {
                    delay--;
                    return;
                }
                d.source.ApplyHealthChange(ToLong(value), d.source);
            }, null, () => delay > 0 ? $"{delay}回合后开始，每回合恢复{value}点生命，持续{duration}回合" : $"每回合恢复{value}点生命，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 急救 : BaseCard
{
    protected override int id => 1017;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        user.ChangeMana(ToLong(Value));
        user.ApplyHealthChange(ToLong(BaseCharacter.SaturatingMultiply(Value, 20)), user);
    }
}


public class 闪击 : BaseCard
{
    protected override int id => 1018;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        user.DealDamage(target, Value);
    }
}


public class 随机种子 : BaseCard
{
    protected override int id => 1019;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int count = ToInt(Value);
        for (int i = 0; i < count; i++)
        {
            user.GainRandomCard();
        }
    }
}


public class 攻击彩票 : BaseCard
{
    protected override int id => 1020;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
            Dot dot = null;
            dot = new Dot(user, target, duration, d =>
        {
            if (UnityEngine.Random.Range(0, 3) == 0)
            {
                d.source.DealDamage(d.target, value);
            }
        }, null, () => $"每回合有三分之一的概率造成{value}点伤害，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 生命彩票 : BaseCard
{
    protected override int id => 1021;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
            Dot dot = null;
            dot = new Dot(user, user, duration, d =>
            {
                if (UnityEngine.Random.Range(0, 3) == 0)
                {
                    d.source.ApplyHealthChange(ToLong(value), d.source);
                }
            }, null, () => $"每回合有三分之一的概率回复{value}点生命，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 魔力彩票 : BaseCard
{
    protected override int id => 1022;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, user, duration, d =>
        {
            if (UnityEngine.Random.Range(0, 3) == 0)
            {
                d.source.ChangeMana(ToLong(value));
            }
        }, null, () => $"每回合有三分之一的概率额外回复{value}点魔力，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 随机彩票 : BaseCard
{
    protected override int id => 1023;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int maxCount = ToInt(Value);
        int count = UnityEngine.Random.Range(0, maxCount + 1);
        if (count == 0) return;
        string[] names = user is Player
            ? new[] { "攻击彩票", "生命彩票", "魔力彩票" }
            : new[] { "攻击彩票", "魔力彩票" };
        for (int i = 0; i < count; i++)
        {
            string name = names[UnityEngine.Random.Range(0, names.Length)];
            user.GainCard(CardFactory.GetThisCard(name));
        }
    }
}


public class 破甲 : BaseCard
{
    protected override int id => 1024;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (target == null) return;
        if (Duration <= 0) return;
        float multiplier = Mathf.Max(0f, ToFloat(Value));
        target.SetDamageTakenMultiplier(multiplier);
        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            d.target.SetDamageTakenMultiplier(multiplier);
        }, d =>
        {
            d.target.SetDamageTakenMultiplier(1f);
        }, () => $"敌方受到的伤害变为{multiplier}倍，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldTarget != null) oldTarget.SetDamageTakenMultiplier(1f);
            if (d.target != null) d.target.SetDamageTakenMultiplier(multiplier);
        });
        user.dotBar.Add(dot);
    }
}


public class 羽化飞升 : BaseCard
{
    protected override int id => 1025;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int discardCount = user.Cards.Count;
        if (discardCount <= 0) return;
        if (user is Player)
        {
            var discardList = new System.Collections.Generic.List<BaseCard>(user.Cards);
            foreach (var card in discardList)
            {
                user.Cards.Remove(card);
                EventCenter.Publish("Player_PlayCard", card);
            }
        }
        else
        {
            user.Cards.Clear();
        }
        user.ChangeMana(discardCount);
    }
}


public class 偷dot : BaseCard
{
    protected override int id => 1026;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        int stealCount = ToInt(Value);
        if (stealCount == 0) return;
        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            if (d.target == null || d.target.dotBar.Count == 0) return;
            int count = Mathf.Min(stealCount, d.target.dotBar.Count);
            for (int i = 0; i < count; i++)
            {
                if (d.target.dotBar.Count == 0) break;
                int index = UnityEngine.Random.Range(0, d.target.dotBar.Count);
                Dot stolen = d.target.dotBar[index];
                d.target.dotBar.RemoveAt(index);
                stolen.TransferTo(d.source);
                stolen.MarkStolenFromOpponent();
                d.source.dotBar.Add(stolen);
            }
        }, null, () => $"每回合随机偷取对方的{stealCount}个持续效果，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 偷魔 : BaseCard
{
    protected override int id => 1027;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, target, duration, d =>
        {
            if (d.target == null) return;
            if (d.target.mana <= 0) return;
            d.target.ChangeMana(NegToLong(value));
            d.source.ChangeMana(ToLong(value));
        }, null, () => $"每回合偷取敌人{value}点魔力，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 苦修 : BaseCard
{
    protected override int id => 1028;

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
        }, () => $"你对自己造成伤害后获得{value}点魔力，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldSource != null) oldSource.DamageTaken -= handler;
            if (d.source != null) d.source.DamageTaken += handler;
        });
        user.dotBar.Add(dot);
    }
}


public class 献祭 : BaseCard
{
    private static ulong SacrificeBonus = 0;
    protected override int id => 1029;

    public static void ResetSacrificeBonus()
    {
        SacrificeBonus = 0;
    }

    private ulong CurrentDamage => BaseCharacter.SaturatingAdd(Value, SacrificeBonus);

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
        ulong baseValue = Value;
        if (Duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, user, Duration, d =>
        {
            ulong damage = BaseCharacter.SaturatingAdd(baseValue, SacrificeBonus);
            d.source.DealDamage(d.source, damage);
            if (d.target != null) d.source.DealDamage(d.target, damage);
            SacrificeBonus = BaseCharacter.SaturatingAdd(SacrificeBonus, 10);
        }, null, () => $"每回合对双方造成{CurrentDamage}点伤害(并使献祭永久+10)，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 卖血 : BaseCard
{
    protected override int id => 1030;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        ulong value = Value;
        if (duration <= 0) return;
        int drawCount = ToInt(value / 10);
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
        }, null, () => $"每回合对自己造成{value}点伤害并抽{drawCount}张牌，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 反伤 : BaseCard
{
    protected override int id => 1031;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        Dot dot = null;
        Action<ulong, BaseCharacter> handler = (amount, source) =>
        {
            if (dot == null || dot.source == null) return;
            if (source != dot.source) return;
            dot.source.ApplyHealthChange(ToLong(amount), dot.source);
            if (dot.target != null && dot.target != dot.source) dot.source.DealDamage(dot.target, amount);
        };
        user.DamageTaken += handler;
        dot = new Dot(user, user, Duration, d => { }, d =>
        {
            if (d.source != null) d.source.DamageTaken -= handler;
        }, () => $"你对自己造成伤害后恢复等量生命并对敌人造成等量伤害，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldSource != null) oldSource.DamageTaken -= handler;
            if (d.source != null) d.source.DamageTaken += handler;
        });
        user.dotBar.Add(dot);
    }
}


public class 逃避 : BaseCard
{
    protected override int id => 1032;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        user.SetImmuneSelfDamage(true);
        Dot dot = null;
        dot = new Dot(user, user, Duration + 1, d =>
        {
            if (d.duration <= 1)
            {
                d.source.SetImmuneSelfDamage(false);
                return;
            }
            d.source.SetImmuneSelfDamage(true);
        }, d =>
        {
            if (d.source != null) d.source.SetImmuneSelfDamage(false);
        }, () => $"使你无法对自己造成伤害，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldSource != null) oldSource.SetImmuneSelfDamage(false);
            if (d.source != null) d.source.SetImmuneSelfDamage(true);
        });
        user.dotBar.Add(dot);
    }
}


public class 吸血 : BaseCard
{
    protected override int id => 1033;

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
        }, () => $"你造成的伤害会为你自身恢复等量生命，剩余{dot.duration}回合", (d, oldSource, oldTarget) =>
        {
            if (oldSource != null) oldSource.DamageDealt -= handler;
            if (d.source != null) d.source.DamageDealt += handler;
        });
        user.dotBar.Add(dot);
    }
}


public class 制衡 : BaseCard
{
    protected override int id => 1034;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (user.Cards.Count <= 0) return;
        var discardList = new System.Collections.Generic.List<BaseCard>(user.Cards);
        discardList.Remove(this);
        int discardCount = discardList.Count;
        if (discardCount <= 0) return;
        if (user is Player)
        {
            foreach (var card in discardList)
            {
                user.Cards.Remove(card);
                EventCenter.Publish("Player_PlayCard", card);
            }
        }
        else
        {
            foreach (var card in discardList)
            {
                user.Cards.Remove(card);
            }
        }
        for (int i = 0; i < discardCount; i++)
        {
            user.GainRandomCard();
        }
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
