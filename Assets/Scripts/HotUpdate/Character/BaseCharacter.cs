


using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseCharacter : MonoBehaviour
{
    // 目标
    public BaseCharacter Target;

    // 基础属性
    public HookField<int> Health = new HookField<int>(100);
    public HookField<int> Mana = new HookField<int>(100);
    public List<BaseCard> HandCards = new List<BaseCard>();  // 手牌

    public void UseCard(BaseCard card)
    {
        if (HandCards.Contains(card))
        {
            // 使用卡牌的逻辑
            // ......
            HandCards.Remove(card);
        }
    }






    #region Trun Logic

    public Action<BaseCharacter> BeforeActionTurn;
    public Action<BaseCharacter> AfterActionTurn;
    public virtual void StartTurn()
    {
        BeforeActionTurn?.Invoke(this);
        // 角色开始回合逻辑
    }
    public virtual void EndTurn()
    {
        // 角色结束回合逻辑
        AfterActionTurn?.Invoke(this);
    }

    #endregion


    
    





}