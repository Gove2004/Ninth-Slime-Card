

using UnityEngine;

public class Player : BaseCharacter
{
    public const int HandLimit = 10;

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
    public bool IsJailed => jailedTurnsRemaining > 0;
    public bool CanUseTurnActions => isReady && !IsJailed;
    private bool isEndingTurn = false;
    private int jailedTurnsRemaining;
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
            EventCenter.Publish(GameEvents.PlayerDefeated, new CharacterEventContext(this));
        }
    }





    // UI：调用使用卡牌
    public void UI_PlayCard(BaseCard card)
    {
        if (isReady && IsJailed)
        {
            if (DamageEffectManager.Instance != null && DamageEffectManager.Instance.playerTransform != null)
            {
                DamageEffectManager.Instance.ShowFloatingText(DamageEffectManager.Instance.playerTransform, "你已因罪入狱，只能结束回合", Color.gray);
            }
            return;
        }

        if (CanUseTurnActions && Cards.Contains(card) && card.Cost <= mana)
        {
            PlayCard(card);
        }
    }


    // UI：调用抽卡
    public void UI_DrawCard()
    {
        if (CanUseTurnActions)
        {
            DrawCard();
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
        if (jailedTurnsRemaining > 0)
        {
            jailedTurnsRemaining--;
        }
        isEndingTurn = false;
    }

    public void SendToJail(int turns = 1)
    {
        if (turns <= 0) return;
        jailedTurnsRemaining = Mathf.Max(jailedTurnsRemaining, turns);
    }

    public void RestoreJailState(int turns)
    {
        jailedTurnsRemaining = Mathf.Max(0, turns);
    }

    public int GetJailTurnsRemaining()
    {
        return jailedTurnsRemaining;
    }



    
}
