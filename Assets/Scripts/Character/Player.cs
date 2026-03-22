

using UnityEngine;

public class Player : BaseCharacter
{
    public const int HandLimit = 999;  // 解除手牌上限限制

    public Player()
    {
        int difficultyLevel = GameManager.Instance.difficultyLevel;
        health = difficultyLevel switch
        {
            1 => 10UL,
            2 => 6UL,
            _ => 3L
        };
        mana = difficultyLevel >= 3 ? 2UL : 3UL;
        autoManaPerTurn = difficultyLevel >= 3 ? 2UL : 3UL;
    }


    public bool isReady { get; set; } = false;
    private bool isEndingTurn = false;
    protected override int MaxHandSize => HandLimit;

    protected override void Action()
    {
        isReady = true;
    }


    private bool isDead = false;
    public override void ChangeHealth(long amount)
    {
        if (amount >= 0)
        {
            health = SaturatingAdd(health, (ulong)amount);
        }
        else
        {
            ulong damage = amount == long.MinValue ? (ulong)long.MaxValue + 1UL : (ulong)(-amount);
            ulong damageToShield = shiled < damage ? shiled : damage;
            shiled = SaturatingSub(shiled, damageToShield);
            damage = SaturatingSub(damage, damageToShield);
            if (damage > 0)
            {
                health = SaturatingSub(health, damage);
            }
        }
        if (health == 0 && !isDead)
        {
            health = 0;
            isDead = true;
            if ((LastDamageCard != null && LastDamageCard.IsStolenFromOpponent) ||
                (LastDamageDot != null && LastDamageDot.IsStolenFromOpponent))
            {
                EventCenter.Publish("Achievement_KilledByStolenCard", LastDamageCard);
            }
            EventCenter.Publish("PlayerDead", this);
        }
    }





    // UI：调用使用卡牌
    public void UI_PlayCard(BaseCard card)
    {
        if (isReady && Cards.Contains(card) && card.Cost <= mana)
        {
            PlayCard(card);

            EventCenter.Publish("Player_PlayCard", card);
        }
    }


    // UI：调用抽卡
    public void UI_DrawCard()
    {
        if (isReady)
        {
            var card = DrawCard();

            EventCenter.Publish("Player_DrawCard", card);
        }
    }

    // UI：调用结束回合
    public void UI_EndTurn()
    {
        EndTurn();
    }

    public override void EndTurn()
    {
        if (!isReady || isEndingTurn) return;

        isReady = false;
        isEndingTurn = true;

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.StartCoroutine(EndTurnRoutine());
            return;
        }

        isEndingTurn = false;
        base.EndTurn();
    }

    private System.Collections.IEnumerator EndTurnRoutine()
    {
        // 1. 检查是否满足升级条件
        var enemy = BattleManager.Instance.enemy as EnemyBoss;
        if (enemy != null)
        {
            if (enemy.score >= enemy.nextPhaseHealthThreshold)
            {
                Debug.Log($"回合结束结算：当前分数 {enemy.score} >= 阈值 {enemy.nextPhaseHealthThreshold}，触发升级。");
                
                enemy.TriggerPhaseChange();
                
                yield return null; 
                
                while (Time.timeScale == 0f)
                {
                    yield return null;
                }
            }
        }

        // 2. 执行标准回合结束逻辑（回蓝等）
        // 因为在此之前触发了 TriggerPhaseChange，如果升级了，EnemyBoss_PhaseChanged 事件会被触发
        // BattleManager 监听了这个事件并增加了 player.autoManaPerTurn
        // 所以这里调用 EndTurn 时的 ChangeMana 已经是增加了之后的数值
        base.EndTurn();
        isEndingTurn = false;
    }



    
}
