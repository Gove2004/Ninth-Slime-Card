using System.Collections;
using UnityEngine;


public class EnemyBoss : BaseCharacter
{
    private const int InitialThinkMinMs = 350;
    private const int InitialThinkMaxMs = 900;
    private const int ActionGapMinMs = 220;
    private const int ActionGapMaxMs = 650;
    private const string EnemyPlayAnimationTag = "play";
    private const string EnemyDrawAnimationTag = "draw";
    private Coroutine aiRoutine;
    private System.Action animationCompletedUnlisten;
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
        if (aiRoutine != null && BattleManager.Instance != null)
        {
            BattleManager.Instance.StopCoroutine(aiRoutine);
        }
        aiRoutine = null;
        animationCompletedUnlisten?.Invoke();
        animationCompletedUnlisten = null;
    }

    // 这里是敌人的行动逻辑
    protected override void Action()
    {
        Stop();
        if (BattleManager.Instance != null)
        {
            aiRoutine = BattleManager.Instance.StartCoroutine(AIActionRoutine());
        }
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

    public void SetScore(ulong value)
    {
        score = value;
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
            ulong totalDamage = amount == long.MinValue ? (ulong)long.MaxValue + 1UL : (ulong)(-amount);
            ulong damageToShield = shiled < totalDamage ? shiled : totalDamage;
            shiled = SaturatingSub(shiled, damageToShield);
            ulong damageToHealth = SaturatingSub(totalDamage, damageToShield);

            if (totalDamage > 0)
            {
                score = SaturatingAdd(score, totalDamage);
                if (!IsEndlessMode)
                {
                    health = SaturatingSub(health, damageToHealth);
                    if (health == 0 && !isDead)
                    {
                        health = 0;
                        isDead = true;
                        EventCenter.Publish(GameEvents.EnemyDefeated, new CharacterEventContext(this));
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
        EventCenter.Publish(GameEvents.EnemyBossPhaseChanged, new EnemyBossPhaseChangedEventContext(this, phase));  // 提前发布事件，让玩家先知道阶段提升了，可能会有一些反应措施

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

    }


    private static ulong GetThresholdForPhase(int phase)
    {
        if (phase <= 1) return 10;
        ulong current = 10;
        ulong delta = 5;
        for (int i = 2; i <= phase; i++)
        {
            current = SaturatingAdd(current, delta);
            delta = SaturatingAdd(delta, 1);
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

    private IEnumerator WaitRandomSecondsRoutine(int min = ActionGapMinMs, int max = ActionGapMaxMs)
    {
        float settlementScale = GameSettings.GetSettlementDelayScale();
        float scale = GetAISpeedScale() * settlementScale;
        int minFloor = Mathf.Max(20, Mathf.RoundToInt(120f * settlementScale));
        int maxGapFloor = Mathf.Max(12, Mathf.RoundToInt(40f * settlementScale));
        int scaledMin = Mathf.Max(minFloor, Mathf.RoundToInt(min * scale));
        int scaledMax = Mathf.Max(scaledMin + maxGapFloor, Mathf.RoundToInt(max * scale));
        int delay = Random.Range(scaledMin, scaledMax);
        float duration = delay / 1000f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!IsInTurn) yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private float GetEnemyAnimationScale()
    {
        float manaFactor = Mathf.InverseLerp(3f, 12f, ClampToFloat(mana));
        float handFactor = Mathf.InverseLerp(3f, 10f, Cards.Count);
        float t = Mathf.Clamp01(Mathf.Max(manaFactor, handFactor));
        float aiScale = Mathf.Lerp(1f, 0.35f, t);
        return aiScale * GameSettings.GetSettlementDelayScale();
    }

    private float GetExpectedAnimationDurationSeconds(string animationTag)
    {
        float scale = GetEnemyAnimationScale();
        if (animationTag == EnemyPlayAnimationTag)
        {
            float expandDuration = Mathf.Max(0.1f, 0.33f * scale);
            float holdDuration = Mathf.Max(0.08f, 0.33f * scale);
            float collapseDuration = Mathf.Max(0.1f, 0.33f * scale);
            return expandDuration + holdDuration + collapseDuration;
        }

        float rotateDuration = Mathf.Max(0.12f, 0.5f * scale);
        return rotateDuration;
    }

    private bool CanTakeAnyAction()
    {
        if (AllowDraw && mana > 0)
        {
            return true;
        }

        if (AllowPlay)
        {
            foreach (var card in Cards)
            {
                if (card.Cost <= mana)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private IEnumerator WaitForAnimationCompletionRoutine(string animationTag)
    {
        bool completed = false;
        animationCompletedUnlisten?.Invoke();
        animationCompletedUnlisten = EventCenter.Register<EnemyAnimationEventContext>(GameEvents.EnemyActionAnimationCompleted, context =>
        {
            if (context.Tag == animationTag)
            {
                completed = true;
            }
        });

        float timeout = Mathf.Max(0.2f, GetExpectedAnimationDurationSeconds(animationTag) + 0.35f);
        float elapsed = 0f;
        while (!completed && elapsed < timeout)
        {
            if (!IsInTurn) yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        animationCompletedUnlisten?.Invoke();
        animationCompletedUnlisten = null;
    }

    private IEnumerator WaitForAnimationThenGapRoutine(string animationTag)
    {
        yield return WaitForAnimationCompletionRoutine(animationTag);
        if (!IsInTurn) yield break;

        if (!CanTakeAnyAction())
        {
            EndTurn();
            yield break;
        }

        yield return WaitRandomSecondsRoutine(ActionGapMinMs, ActionGapMaxMs);
    }


    private IEnumerator AIActionRoutine()
    {
        yield return WaitRandomSecondsRoutine(InitialThinkMinMs, InitialThinkMaxMs);
        if (!IsInTurn)
        {
            aiRoutine = null;
            yield break;
        }

        while (IsInTurn)
        {
            if (Random.value < 0.1f)
            {
                EndTurn();
                aiRoutine = null;
                yield break;
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

                    EventCenter.Publish(GameEvents.EnemyCardPlayed, new CardEventContext(this, playable));

                    yield return WaitForAnimationThenGapRoutine(EnemyPlayAnimationTag);
                    if (!IsInTurn)
                    {
                        aiRoutine = null;
                        yield break;
                    }
                    continue;
                }
            }

            if (AllowDraw && mana > 0)
            {
                var card = DrawCard();
                if (card != null)
                {
                    EventCenter.Publish(GameEvents.EnemyCardDrawn, new CardEventContext(this, card));

                    yield return WaitForAnimationThenGapRoutine(EnemyDrawAnimationTag);
                    if (!IsInTurn)
                    {
                        aiRoutine = null;
                        yield break;
                    }
                    continue;
                }
            }
        }

        if (IsInTurn)
        {
            EndTurn();
        }

        aiRoutine = null;
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        Stop();
    }
}
