using System;
using UnityEngine;


public class 抽牌 : BaseCard
{
    protected override int id => 1000;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int count = Value;
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
        int value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, target, duration, d => user.DealDamage(target, value), null, () => $"每回合对敌人造成{value}点伤害，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 恢复 : BaseCard
{
    protected override int id => 1002;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        int value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, user, duration, d => user.ApplyHealthChange(value, user), null, () => $"每回合恢复{value}点生命，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}



public class 入魔 : BaseCard
{
    protected override int id => 1003;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        int value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, user, duration, d => d.target.ChangeMana(value), null, () => $"每回合额外获得{value}点魔力，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 贪婪 : BaseCard
{
    protected override int id => 1004;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        int drawCount = Mathf.Max(0, Value);
        Dot dot = null;
        dot = new Dot(user, user, Duration, d =>
        {
            for (int i = 0; i < drawCount; i++)
            {
                var card = user.DrawCard(0);
                if (card != null && user is Player)
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
        int value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, target, duration, d =>
        {
            user.DealDamage(target, value);
            user.ApplyHealthChange(value * 2, user);
        }, null, () => $"每回合对敌人造成{value}点伤害并恢复{value * 2}点生命，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 爆发 : BaseCard
{
    protected override int id => 1006;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int manaSpent = user.mana;
        if (manaSpent <= 0) return;
        user.ChangeMana(-manaSpent);
        int damage = manaSpent * Value;
        int duration = manaSpent;
        Dot dot = null;
        dot = new Dot(user, target, duration, d => user.DealDamage(target, damage), null, () => $"每回合造成{damage}点伤害，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 第7张牌 : BaseCard
{
    protected override int id => 1007;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        int value = Value;
        if (duration <= 0) return;
        if (value < 0) value = 0;
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
                int damage = value * 11;
                if (UnityEngine.Random.value < 0.77f)
                {
                    damage *= 2;
                    if (UnityEngine.Random.value < 0.77f)
                    {
                        damage = Mathf.Max(0, value / 7);
                    }
                }
                user.DealDamage(target, damage);
            }
            else if (effectIndex == 1)
            {
                user.SetImmuneThisTurn(true);
            }
            else if (effectIndex == 2)
            {
                int stealCount = Mathf.Max(0, value);
                for (int i = 0; i < stealCount; i++)
                {
                    bool stolen = false;
                    for (int attempt = 0; attempt < 3 && !stolen; attempt++)
                    {
                        int pick = UnityEngine.Random.Range(0, 3);
                        if (pick == 0)
                        {
                            if (target != null && target.Cards.Count > 0)
                            {
                                int index = UnityEngine.Random.Range(0, target.Cards.Count);
                BaseCard stolenCard = target.Cards[index];
                stolenCard.MarkStolenFromOpponent();
                target.RemoveCard(stolenCard);
                user.GainCard(stolenCard);
                stolen = true;
                            }
                        }
                        else if (pick == 1)
                        {
                            if (target != null && target.dotBar.Count > 0)
                            {
                                int index = UnityEngine.Random.Range(0, target.dotBar.Count);
                                Dot stolenDot = target.dotBar[index];
                                target.dotBar.RemoveAt(index);
                                stolenDot.source = user;
                                stolenDot.target = user;
                                stolenDot.MarkStolenFromOpponent();
                                user.dotBar.Add(stolenDot);
                                stolen = true;
                            }
                        }
                        else
                        {
                            if (target != null && target.mana > 0)
                            {
                                target.ChangeMana(-1);
                                user.ChangeMana(1);
                                stolen = true;
                            }
                        }
                    }
                    if (!stolen) break;
                }
            }
            else if (effectIndex == 3)
            {
                user.ChangeMana(value);
                if (user.Cards.Count > 0)
                {
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
                }
            }
            else if (effectIndex == 4)
            {
                int count = Mathf.Max(0, value);
                for (int i = 0; i < count; i++)
                {
                    user.GainRandomCard();
                }
                user.EndTurn();
            }
            else if (effectIndex == 5)
            {
                int add = Mathf.Max(0, value);
                if (add > 0)
                {
                    foreach (var otherDot in user.dotBar)
                    {
                        if (otherDot != d)
                        {
                            otherDot.duration += add;
                        }
                    }
                }
            }
            else
            {
                int heal = value * 11;
                if (UnityEngine.Random.value < 0.07f)
                {
                    heal = Mathf.Max(0, value / 7);
                }
                user.ApplyHealthChange(heal, user);
            }
        }, null, () => $"每回合随机触发一种“七宗罪”效果，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 上三角 : BaseCard
{
    protected override int id => 1008;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        int baseValue = Value;
        int timer = 0;
        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            timer++;
            int damage = Mathf.Max(0, baseValue * timer);
            user.DealDamage(target, damage);
        }, null, () => $"每回合对敌人造成{baseValue * (timer + 1)}点伤害(下回合+{baseValue})，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 下三角 : BaseCard
{
    protected override int id => 1009;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        int damage = Value;
        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            user.DealDamage(target, damage);
            damage = Mathf.Max(0, damage / 2);
        }, null, () => $"每回合对敌人造成{damage}点伤害并使此伤害值减半，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 延续 : BaseCard
{
    protected override int id => 1010;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int add = Value;
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
        int factor = Value;
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
        int stealCount = Mathf.Max(0, Value);
        if (stealCount == 0) return;
            Dot dot = null;
            dot = new Dot(user, user, Duration, d =>
        {
            if (target == null || target.Cards.Count == 0) return;
            int count = Mathf.Min(stealCount, target.Cards.Count);
            for (int i = 0; i < count; i++)
            {
                int index = UnityEngine.Random.Range(0, target.Cards.Count);
                BaseCard stolen = target.Cards[index];
                stolen.MarkStolenFromOpponent();
                target.RemoveCard(stolen);
                user.GainCard(stolen);
                if (target.Cards.Count == 0) break;
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
        int times = Mathf.Max(0, Value);
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
        int value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, target, duration, d => user.DealDamage(target, value), null, () => $"每回合对敌人造成{value}点伤害，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 彻底疯狂 : BaseCard
{
    protected override int id => 1015;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        int duration = Duration;
        int value = Value;
        if (duration <= 0) return;
        int actualDuration = UnityEngine.Random.Range(1, duration + 1);
        Dot dot = null;
        dot = new Dot(user, target, actualDuration, d =>
        {
            int damage = UnityEngine.Random.Range(0, value + 1);
            user.DealDamage(target, damage);
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
        int value = Value;
        Dot dot = null;
        dot = new Dot(user, user, duration + delay, d =>
            {
                if (delay > 0)
                {
                    delay--;
                    return;
                }
                user.ApplyHealthChange(value, user);
            }, null, () => delay > 0 ? $"{delay}回合后开始，每回合恢复{value}点生命，持续{duration}回合" : $"每回合恢复{value}点生命，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 急救 : BaseCard
{
    protected override int id => 1017;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        user.ChangeMana(Value);
        user.ApplyHealthChange(Value * 20, user);
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
        for (int i = 0; i < Value; i++)
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
        int value = Value;
        if (duration <= 0) return;
            Dot dot = null;
            dot = new Dot(user, target, duration, d =>
        {
            if (UnityEngine.Random.Range(0, 3) == 0)
            {
                user.DealDamage(target, value);
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
        int value = Value;
        if (duration <= 0) return;
            Dot dot = null;
            dot = new Dot(user, user, duration, d =>
            {
                if (UnityEngine.Random.Range(0, 3) == 0)
                {
                    user.ApplyHealthChange(value, user);
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
        int value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, user, duration, d =>
        {
            if (UnityEngine.Random.Range(0, 3) == 0)
            {
                user.ChangeMana(value);
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
        int maxCount = Mathf.Max(0, Value);
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
        float multiplier = Mathf.Max(0, Value);
        target.SetDamageTakenMultiplier(multiplier);
        Dot dot = null;
        dot = new Dot(user, target, Duration, d =>
        {
            target.SetDamageTakenMultiplier(multiplier);
        }, d =>
        {
            target.SetDamageTakenMultiplier(1f);
        }, () => $"敌方受到的伤害变为{multiplier}倍，剩余{dot.duration}回合");
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
        int stealCount = Mathf.Max(0, Value);
        if (stealCount == 0) return;
        Dot dot = null;
        dot = new Dot(user, user, Duration, d =>
        {
            if (target == null || target.dotBar.Count == 0) return;
            int count = Mathf.Min(stealCount, target.dotBar.Count);
            for (int i = 0; i < count; i++)
            {
                if (target.dotBar.Count == 0) break;
                int index = UnityEngine.Random.Range(0, target.dotBar.Count);
                Dot stolen = target.dotBar[index];
                target.dotBar.RemoveAt(index);
                stolen.source = user;
                stolen.target = user;
                stolen.MarkStolenFromOpponent();
                user.dotBar.Add(stolen);
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
        int value = Value;
        if (duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, user, duration, d =>
        {
            if (target == null) return;
            if (target.mana <= 0) return;
            target.ChangeMana(-value);
            user.ChangeMana(value);
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
        int value = Value;
        if (duration <= 0) return;
        Action<int, BaseCharacter> handler = (amount, source) =>
        {
            if (source != user) return;
            user.ChangeMana(value);
        };
        user.DamageTaken += handler;
        Dot dot = null;
        dot = new Dot(user, user, duration, d => { }, d => user.DamageTaken -= handler, () => $"你对自己造成伤害后获得{value}点魔力，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 献祭 : BaseCard
{
    private static int SacrificeBonus = 0;
    protected override int id => 1029;

    private int CurrentDamage => Value + SacrificeBonus;

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
        int baseValue = Value;
        if (Duration <= 0) return;
        Dot dot = null;
        dot = new Dot(user, user, Duration, d =>
        {
            int damage = baseValue + SacrificeBonus;
            user.DealDamage(user, damage);
            if (target != null) user.DealDamage(target, damage);
            SacrificeBonus += 10;
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
        int value = Value;
        if (duration <= 0) return;
        int drawCount = Mathf.Max(0, value / 10);
        Dot dot = null;
        dot = new Dot(user, user, duration, d =>
        {
            user.ApplyHealthChange(-value, user);
            for (int i = 0; i < drawCount; i++)
            {
                var card = user.DrawCard(0);
                if (card != null && user is Player)
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
        Action<int, BaseCharacter> handler = (amount, source) =>
        {
            if (source != user) return;
            user.ApplyHealthChange(amount, user);
            if (target != null) user.DealDamage(target, amount);
        };
        user.DamageTaken += handler;
        Dot dot = null;
        dot = new Dot(user, user, Duration, d => { }, d => user.DamageTaken -= handler, () => $"你对自己造成伤害后恢复等量生命并对敌人造成等量伤害，剩余{dot.duration}回合");
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
                user.SetImmuneSelfDamage(false);
                return;
            }
            user.SetImmuneSelfDamage(true);
        }, null, () => $"使你无法对自己造成伤害，剩余{dot.duration}回合");
        user.dotBar.Add(dot);
    }
}


public class 吸血 : BaseCard
{
    protected override int id => 1033;

    public override void Execute(BaseCharacter user, BaseCharacter target)
    {
        if (Duration <= 0) return;
        Action<int, BaseCharacter> handler = (amount, victim) =>
        {
            user.ApplyHealthChange(amount, user);
        };
        user.DamageDealt += handler;
        Dot dot = null;
        dot = new Dot(user, user, Duration, d => { }, d => user.DamageDealt -= handler, () => $"你造成的伤害会为你自身恢复等量生命，剩余{dot.duration}回合");
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
