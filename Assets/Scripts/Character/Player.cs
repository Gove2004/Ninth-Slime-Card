

using UnityEngine;

public class Player : BaseCharacter
{
    public const int HandLimit = 8;

    public Player()
    {
        // 初始化玩家属性
        health = 50 * (4 - GameManager.Instance.difficultyLevel);  // 难度系数影响玩家初始生命
        mana = GameManager.Instance.difficultyLevel == 3 ? 2 : 3;  // 最难模式下，只有2
        autoManaPerTurn = GameManager.Instance.difficultyLevel == 3 ? 2 : 3;  // 最难模式下，每回合增加2
    }


    public bool isReady { get; set; } = false;
    protected override int MaxHandSize => HandLimit;

    protected override void Action()
    {
        isReady = true;
    }


    private bool isDead = false;
    public override void ChangeHealth(int amount)
    {
        if (amount >= 0)
        {
            health += amount;
        }
        else
        {
            int damageToShield = UnityEngine.Mathf.Min(shiled, -amount);
            shiled -= damageToShield;
            amount += damageToShield;
            if (amount < 0)
            {
                health += amount;
            }
        }
        if (health <= 0 && !isDead)
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
        if (isReady)
        {
            isReady = false;
            // 启动协程来处理结束回合逻辑（包括等待 RougeUI）
            BattleManager.Instance.StartCoroutine(EndTurnRoutine());
        }
    }

    private System.Collections.IEnumerator EndTurnRoutine()
    {
        // 1. 检查是否满足升级条件
        var enemy = BattleManager.Instance.enemy as EnemyBoss;
        if (enemy != null)
        {
            // 循环检测，直到不再满足升级条件
            while (enemy.health >= enemy.nextPhaseHealthThreshold)
            {
                Debug.Log($"回合结束结算：当前分数 {enemy.health} >= 阈值 {enemy.nextPhaseHealthThreshold}，触发升级。");
                
                // 触发升级事件
                enemy.TriggerPhaseChange();
                
                // 等待 RougeUI 显示并完成选择
                // RougeUI 在 TriggerPhaseChange 触发的事件中会设置 Time.timeScale = 0
                yield return null; 
                
                // 等待直到游戏恢复正常（RougeUI 关闭）
                while (Time.timeScale == 0f)
                {
                    yield return null;
                }
                
                // 选择完成后，循环继续，再次检查当前分数是否还高于新的阈值
            }
        }

        // 2. 执行标准回合结束逻辑（回蓝等）
        // 因为在此之前触发了 TriggerPhaseChange，如果升级了，EnemyBoss_PhaseChanged 事件会被触发
        // BattleManager 监听了这个事件并增加了 player.autoManaPerTurn
        // 所以这里调用 EndTurn 时的 ChangeMana 已经是增加了之后的数值
        base.EndTurn();
    }



    
}
