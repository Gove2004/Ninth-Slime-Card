using System;

public class Dot
{
    public BaseCharacter source;
    public BaseCharacter target;
    public int duration;
    public BaseCard sourceCard;
    public bool IsStolenFromOpponent { get; private set; }
    private readonly Action<Dot> onTick;
    private readonly Action<Dot> onExpire;
    private readonly Action<Dot, BaseCharacter, BaseCharacter> onOwnershipChanged;

        public readonly Func<string> description;  // 用于界面显示，实时生成

        public Dot(BaseCharacter s, BaseCharacter t, int duration, Action<Dot> onTick, Action<Dot> onExpire = null, Func<string> description = null, Action<Dot, BaseCharacter, BaseCharacter> onOwnershipChanged = null)
        {
            source = s;
            target = t;
            this.duration = duration;
            this.onTick = onTick;
            this.onExpire = onExpire;
            this.onOwnershipChanged = onOwnershipChanged;
            sourceCard = BaseCharacter.ActiveCardContext;

            this.description = description ?? (() => "");
        }

    public void Apply()
    {
        var previousCardContext = BaseCharacter.ActiveCardContext;
        var previousDotContext = BaseCharacter.ActiveDotContext;
        BaseCharacter.ActiveCardContext = sourceCard;
        BaseCharacter.ActiveDotContext = this;
        onTick?.Invoke(this);
        BaseCharacter.ActiveCardContext = previousCardContext;
        BaseCharacter.ActiveDotContext = previousDotContext;
        duration--;
        if (duration <= 0)
        {
            onExpire?.Invoke(this);
            if (source != null)
            {
                source.dotBar.Remove(this);
            }
            else if (target != null)
            {
                target.dotBar.Remove(this);
            }
        }
    }

    public void MarkStolenFromOpponent()
    {
        IsStolenFromOpponent = true;
    }

    public void TransferTo(BaseCharacter newSource)
    {
        BaseCharacter oldSource = source;
        BaseCharacter oldTarget = target;
        source = newSource;
        if (oldTarget == oldSource)
        {
            target = newSource;
        }
        else if (newSource != null && BattleManager.Instance != null)
        {
            if (newSource == BattleManager.Instance.player) target = BattleManager.Instance.enemy;
            else if (newSource == BattleManager.Instance.enemy) target = BattleManager.Instance.player;
        }
        onOwnershipChanged?.Invoke(this, oldSource, oldTarget);
    }
}
