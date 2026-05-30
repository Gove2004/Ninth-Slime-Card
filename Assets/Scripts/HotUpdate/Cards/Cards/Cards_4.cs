using GoveKits.Runtime.Unit;
using UnityEngine;

public class 硬化 : BaseCard
{
    protected override int id => 401;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int duration = Mathf.Max(1, Value3 == 0 ? 1 : Value3);
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定减免, Value1, duration, user).Apply(user);
    }
}

public class 反伤 : BaseCard
{
    protected override int id => 402;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int duration = Value3 > 0 ? Value3 : 1;
        user.AddHookEffect(BaseCharacter.HookTiming.WhenHurt, () =>
        {
            if (user.CurrentSourceCharacter == null || user.CurrentSourceCharacter == user)
            {
                return GainManaEffect.Create().Setup(0, user);
            }

            return HurtEffect.Create().Setup(Value1, user, user.CurrentSourceCharacter).ApplyWrapper(user.CurrentSourceCharacter);
        }, duration);
    }
}

public class 钻石 : BaseCard
{
    protected override int id => 403;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害百分比提升, 1, 1, user).Apply(user);
    }
}

public class 磐岩 : BaseCard
{
    protected override int id => 404;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target)
    {
        return user;
    }

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value1);
    }
}

public class 破甲 : BaseCard
{
    protected override int id => 405;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int damage = Value1;
        if (target.GetShield() > 0)
        {
            damage *= 2;
        }

        AttackEffect.Create().Setup(damage, user, target).Apply(target);
    }
}

public class 石刺 : BaseCard
{
    protected override int id => 406;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value2);
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
    }
}

public class 守势 : BaseCard
{
    protected override int id => 407;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value1);
    }
}

public class 厚甲 : BaseCard
{
    protected override int id => 408;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value1);
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定减免, Value2, Value3, user).Apply(user);
    }
}

public class 镇压 : BaseCard
{
    protected override int id => 409;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int damage = Value1;
        if (user.GetShield() > 0)
        {
            DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
        }
        AttackEffect.Create().Setup(damage, user, target).Apply(target);
    }
}

public class 拒敌 : BaseCard
{
    protected override int id => 410;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenHurt, () => HurtEffect.Create().Setup(Value1, user, user.CurrentSourceCharacter), Value3);
    }
}

public class 铁壁 : BaseCard
{
    protected override int id => 411;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定减免, Value1, Value3, user).Apply(user);
    }
}

public class 震荡 : BaseCard
{
    protected override int id => 412;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
        if (target.GetShield() > 0)
        {
            target.SetMana(Mathf.Max(0, target.GetMana() - Value2));
        }
    }
}

public class 守财 : BaseCard
{
    protected override int id => 413;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        if (user.GetShield() > 0)
        {
            GainManaEffect.Create().Setup(Value1, user).Apply(user);
            DrawCardsEffect.Create().Setup(Value2, user).Apply(user);
        }
        else
        {
            user.GainShield(2);
        }
    }
}

public class 格挡 : BaseCard
{
    protected override int id => 414;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value1);
    }
}

public class 盾击 : BaseCard
{
    protected override int id => 415;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value2);
        AttackEffect.Create().Setup(Value1, user, target).Apply(target);
    }
}

public class 固守 : BaseCard
{
    protected override int id => 416;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定减免, Value1, Mathf.Max(1, Value3), user).Apply(user);
    }
}

public class 震甲 : BaseCard
{
    protected override int id => 417;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value1);
        if (user.LastDamageSourceThisTurn == target)
        {
            HurtEffect.Create().Setup(Value2, user, target).Apply(target);
        }
    }
}

public class 崩甲 : BaseCard
{
    protected override int id => 418;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        int damage = target.GetShield() > 0 ? Value2 : Value1;
        AttackEffect.Create().Setup(damage, user, target).Apply(target);
    }
}

public class 壁垒 : BaseCard
{
    protected override int id => 419;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value1);
    }
}

public class 稳压 : BaseCard
{
    protected override int id => 420;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.GainShield(Value1);
        if (user.GetShield() > 0)
        {
            TemporaryAttributeEffect.Create().Setup(StaticString.属性.伤害固定减免, Value2, 1, user).Apply(user);
        }
    }
}

public class 反震 : BaseCard
{
    protected override int id => 421;

    public override BaseCharacter ResolveTarget(BaseCharacter user, BaseCharacter target) => user;

    public override void OnUse(BaseCharacter user, BaseCharacter target)
    {
        user.AddHookEffect(BaseCharacter.HookTiming.WhenHurt, () => CallbackEffect.Create().Setup(() =>
        {
            if (user.LastDamageSourceThisTurn != null)
            {
                HurtEffect.Create().Setup(Value1, user, user.LastDamageSourceThisTurn).Apply(user.LastDamageSourceThisTurn);
            }
        }), 1);
    }
}

internal static class EffectCardExtensions
{
    public static DummyApplyEffect ApplyWrapper(this HurtEffect effect, BaseCharacter target)
    {
        return DummyApplyEffect.Create().Setup(() => effect.Apply(target));
    }
}

public class DummyApplyEffect : UnitEffect<DummyApplyEffect>
{
    private System.Action action;

    public DummyApplyEffect Setup(System.Action value)
    {
        action = value;
        return this;
    }

    public override void OnApply<TUnit>(TUnit target)
    {
        action?.Invoke();
    }

    public override void OnRecycle()
    {
        action = null;
    }
}
