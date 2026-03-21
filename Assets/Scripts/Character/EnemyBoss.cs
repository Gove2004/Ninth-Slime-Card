using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;


public class EnemyBoss : BaseCharacter
{
    private const int InitialThinkMinMs = 350;
    private const int InitialThinkMaxMs = 900;
    private const int ActionGapMinMs = 220;
    private const int ActionGapMaxMs = 650;
    private CancellationTokenSource _cts;
    private bool isDead = false;
    public ulong score { get; private set; }
    public bool IsEndlessMode => GameManager.Instance != null && GameManager.Instance.difficultyLevel >= 4;
    public static ulong GetTargetHealth(int difficultyLevel)
    {
        return difficultyLevel switch
        {
            1 => 9999,
            2 => 9999_9999,
            3 => 9999_9999_9999,
            _ => ulong.MaxValue  // 无尽模式Boss生命值极高，基本上不可能被击败，玩家的目标是尽可能地得分
        };
    }

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
        health = GetTargetHealth(GameManager.Instance.difficultyLevel);
        mana = (ulong)GameManager.Instance.difficultyLevel;
        autoManaPerTurn = (ulong)GameManager.Instance.difficultyLevel;
        score = 0;
    }


    public int phase = 1;  // 当前阶段
    public ulong nextPhaseHealthThreshold = GetThresholdForPhase(1);  // 下一阶段的生命阈值
    public override void ChangeHealth(long amount)
    {
        if (amount > 0)
        {
            shiled = SaturatingAdd(shiled, (ulong)amount);
        }
        else
        {
            ulong damage = amount == long.MinValue ? (ulong)long.MaxValue + 1UL : (ulong)(-amount);
            ulong damageToShield = shiled < damage ? shiled : damage;
            shiled = SaturatingSub(shiled, damageToShield);
            damage = SaturatingSub(damage, damageToShield);

            if (damage > 0)
            {
                score = SaturatingAdd(score, damage);
                if (!IsEndlessMode)
                {
                    health = SaturatingSub(health, damage);
                    if (health == 0 && !isDead)
                    {
                        health = 0;
                        isDead = true;
                        EventCenter.Publish("EnemyDead", this);
                    }
                }
            }
        }
    }
    
    public void TriggerPhaseChange()
    {
        // 进入下一阶段
        phase++;

        // 超过界定后Boss成长加速
        autoManaPerTurn = SaturatingAdd(autoManaPerTurn, (ulong)((phase / 10) + 1));
        Debug.Log($"魔力回复 +{(int)(phase/10)}");

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


    private static ulong GetThresholdForPhase(int phase)
    {
        if (phase <= 1) return 5;
        if (phase == 2) return 8;
        ulong prev = 5;
        ulong current = 8;
        for (int i = 3; i <= phase; i++)
        {
            ulong next = SaturatingAdd(prev, current);
            prev = current;
            current = next;
        }
        return current;
    }



    private float GetAISpeedScale()
    {
        float manaFactor = Mathf.InverseLerp(3f, 12f, ClampToFloat(mana));
        float handFactor = Mathf.InverseLerp(3f, 10f, Cards.Count);
        float t = Mathf.Clamp01(Mathf.Max(manaFactor, handFactor));
        return Mathf.Lerp(0.55f, 0.22f, t);
    }

    private static float ClampToFloat(ulong value)
    {
        if (value >= (ulong)int.MaxValue) return int.MaxValue;
        return value;
    }

    private async Task WaitRandomSeconds(CancellationToken token, int min = ActionGapMinMs, int max = ActionGapMaxMs)
    {
        float scale = GetAISpeedScale();
        int scaledMin = Mathf.Max(120, Mathf.RoundToInt(min * scale));
        int scaledMax = Mathf.Max(scaledMin + 40, Mathf.RoundToInt(max * scale));
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
        await WaitRandomSeconds(token, InitialThinkMinMs, InitialThinkMaxMs);
        
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

                    await WaitRandomSeconds(token, ActionGapMinMs, ActionGapMaxMs);
                    continue;
                }
            }

            if (AllowDraw && mana > 0)
            {
                var card = DrawCard();

                EventCenter.Publish("Enemy_DrewCard", card);

                await WaitRandomSeconds(token, ActionGapMinMs, ActionGapMaxMs);
                continue;
            }

            EndTurn();
            return;
        }
    }
}
