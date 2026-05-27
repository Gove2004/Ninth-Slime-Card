using DG.Tweening;
using GoveKits.Runtime.Unit;
using UnityEngine;

public class RedSlashEffect : UnitEffect<RedSlashEffect>
{
    public override void OnApply<TUnit>(TUnit target)
    {
        var character = target as BaseCharacter;
        if (character != null)
        {
            SpriteRenderer render = character.GetComponent<SpriteRenderer>();
            render.DOColor(Color.red, 0.1f).SetLoops(2, LoopType.Yoyo);
        }
    }

    public override void OnRecycle()
    {
        
    }
}

public class GreenSlashEffect : UnitEffect<GreenSlashEffect>
{
    public override void OnApply<TUnit>(TUnit target)
    {
        var character = target as BaseCharacter;
        if (character != null)
        {
            SpriteRenderer render = character.GetComponent<SpriteRenderer>();
            render.DOColor(Color.green, 0.1f).SetLoops(2, LoopType.Yoyo);
        }
    }

    public override void OnRecycle()
    {
        
    }
}



public class GraySlashEffect : UnitEffect<GraySlashEffect>
{
    public override void OnApply<TUnit>(TUnit target)
    {
        var character = target as BaseCharacter;
        if (character != null)
        {
            SpriteRenderer render = character.GetComponent<SpriteRenderer>();
            render.DOColor(Color.gray, 0.1f).SetLoops(2, LoopType.Yoyo);
        }
    }

    public override void OnRecycle()
    {
        
    }
}

