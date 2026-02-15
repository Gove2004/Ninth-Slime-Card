using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


public class EnemyBoss : BaseCharacter
{
    private CancellationTokenSource _cts;

    public override void OnBattleEnd()
    {
        Stop();
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    // 这里是敌人的行动逻辑
    protected override void Action()
    {
        Stop(); // Cancel previous task if any
        _cts = new CancellationTokenSource();
        _ = AIAction(_cts.Token);
    }

    public static bool AllowPlay = true;
    public static bool AllowDraw = true;

    public EnemyBoss()
    {
        // 初始化敌人属性
        health = 0;
        mana = GameManager.Instance.difficultyLevel;
        autoManaPerTurn = GameManager.Instance.difficultyLevel;
    }


    public int phase = 1;  // 当前阶段
    public int nextPhaseHealthThreshold = GetThresholdForPhase(1);  // 下一阶段的生命阈值
    public override void ChangeHealth(int amount)
    {
        // 如果是治疗效果，转为护盾
        if (amount > 0)
        {
            shiled += amount;
        }
        else
        {
            // 先扣护盾
            int damageToShield = Mathf.Min(shiled, -amount);
            shiled -= damageToShield;
            amount += damageToShield; // 减去被护盾吸收的伤害

            // 如果还有剩余伤害，扣血
            if (amount < 0)
            {
                health -= amount;  // Boss的生命是得分
                
                // 移除实时检测阶段变化的逻辑
                // if (health >= nextPhaseHealthThreshold) ...
            }
        }
    }
    
    public void TriggerPhaseChange()
    {
        // 进入下一阶段
        phase++;

        autoManaPerTurn++;  // 每个阶段增加自动法力值

        EventCenter.Publish("EnemyBoss_PhaseChanged", phase);  // 提前发布事件，让玩家先知道阶段提升了，可能会有一些反应措施

        nextPhaseHealthThreshold = GetThresholdForPhase(phase);  // 更新下一阶段的生命阈值
        // 增加阶段提示
        // Transform targetTransform = null;
        // if (DamageEffectManager.Instance != null)
        // {
        //         // Determine transform based on character type
        //         // A bit hacky since BaseCharacter doesn't know about Transforms directly usually
        //         // But we can check against BattleManager instance
        //     if (this == BattleManager.Instance.player) targetTransform = DamageEffectManager.Instance.playerTransform;
        //     else if (this == BattleManager.Instance.enemy) targetTransform = DamageEffectManager.Instance.enemyTransform;
        // }
        // DamageEffectManager.Instance.ShowFloatingText(targetTransform, $"第 {phase} 阶段 : {nextPhaseHealthThreshold}\nBoss每回合魔力恢复+1", Color.red);


        CardFactory.AddRandomCardToEnemyDeck();

        Debug.Log($"进入阶段 {phase}，下一阶段阈值 {nextPhaseHealthThreshold}");
    }


    private static int GetThresholdForPhase(int phase)
    {
        if (phase <= 1) return 30;
        if (phase == 2) return 50;
        int prev = 30;
        int current = 50;
        for (int i = 3; i <= phase; i++)
        {
            int next = prev + current;
            prev = current;
            current = next;
        }
        return current;
    }



    private float GetAISpeedScale()
    {
        float manaFactor = Mathf.InverseLerp(3f, 12f, mana);
        float handFactor = Mathf.InverseLerp(3f, 10f, Cards.Count);
        float t = Mathf.Clamp01(Mathf.Max(manaFactor, handFactor));
        return Mathf.Lerp(1f, 0.35f, t);
    }

    private async Task WaitRandomSeconds(CancellationToken token, int min = 1000, int max = 3000)
    {
        float scale = GetAISpeedScale();
        int scaledMin = Mathf.Max(200, Mathf.RoundToInt(min * scale));
        int scaledMax = Mathf.Max(scaledMin + 50, Mathf.RoundToInt(max * scale));
        int delay = Random.Range(scaledMin, scaledMax);
        float duration = delay / 1000f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (token.IsCancellationRequested) return;

            if (Time.timeScale > 0)
            {
                elapsed += Time.deltaTime;
            }
            await Task.Yield();
        }
    }


    private async Task AIAction(CancellationToken token)
    {
        await WaitRandomSeconds(token);
        
        while (!token.IsCancellationRequested)
        {
            // 有10%概率直接结束回合, 这是负面的， 假装很智能的样子
            if (Random.value < 0.1f)
            {
                EndTurn();
                return;
            }


            if (AllowPlay)
            {
                BaseCard playable = null;
                foreach (var card in Cards)
                {
                    if (card.Cost <= mana)
                    {
                        playable = card;
                        break;
                    }
                }
                if (playable != null)
                {
                    PlayCard(playable);

                    EventCenter.Publish("Enemy_PlayedCard", playable);

                    await WaitRandomSeconds(token);
                    continue;
                }
            }

            if (AllowDraw && mana > 0)
            {
                var card = DrawCard();

                EventCenter.Publish("Enemy_DrewCard", card);

                await WaitRandomSeconds(token);
                continue;
            }

            EndTurn();
            return;
        }
    }
}
