


using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseCharacter : MonoBehaviour
{
    // 目标
    public BaseCharacter Target;

    // 基础属性
    public HookField<int> Health = new HookField<int>(10);
    public HookField<int> MaxHealth = new HookField<int>(10);
    public float HealthPercent => (float)Health.Value / MaxHealth.Value;

    public HookField<int> Mana = new HookField<int>(0);
    public HookField<int> ManaRecovery = new HookField<int>(2);


    public List<BaseCard> HandCards = new List<BaseCard>();  // 手牌
    public List<BaseCard> DeckCards = new List<BaseCard>();  // 牌库


    public void UseCard(BaseCard card)
    {
        if (HandCards.Contains(card))
        {
            card.Use(this, Target);
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
        Mana.Change(x => x + ManaRecovery.Value); // 回合开始恢复法力
    }
    public virtual void EndTurn()
    {
        // 角色结束回合逻辑
        AfterActionTurn?.Invoke(this);
    }

    #endregion


    
    
    public virtual void Setup()
    {
        // 角色初始化逻辑
        // ......
    }




}