using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleManager : MonoBehaviour
{
    private const int SideNone = 0;
    private const int SidePlayer = 1;
    private const int SideEnemy = 2;
    // 简单的单例模式
    public static BattleManager Instance;
    public bool IsGMMode = false;

    // 手动指定角色位置，用于 DamageEffectManager
    public Transform playerTransformRef;
    public Transform enemyTransformRef;

    [Header("UI References")]
    public GameObject gameOverPanel; // 在 Inspector 中引用，或者自动生成

    [Header("Audio Resources")]
    public AudioClip drawClip;
    public AudioClip playClip;
    public AudioClip damageClip;
    public AudioClip playerDamageClip;
    public AudioClip enemyDamageClip;
    public AudioClip healClip;
    public AudioClip manaClip;
    private bool lastIsGMMode;

    void Awake()
    {
        Instance = this;
        lastIsGMMode = !IsGMMode;
        EnsureGMToolState();

        // 自动添加伤害特效管理器
        if (GetComponent<DamageEffectManager>() == null)
        {
            var dem = gameObject.AddComponent<DamageEffectManager>();
            // 自动注入引用
            dem.playerTransform = playerTransformRef;
            dem.enemyTransform = enemyTransformRef;
        }
        else
        {
            var dem = GetComponent<DamageEffectManager>();
            dem.playerTransform = playerTransformRef;
            dem.enemyTransform = enemyTransformRef;
        }

        // 自动添加回合镜头专注（UI偏移）控制器
        var focusController = GetComponent<TurnFocusCameraUI>();
        if (focusController == null)
        {
            focusController = gameObject.AddComponent<TurnFocusCameraUI>();
        }
        focusController.BindTargets(playerTransformRef, enemyTransformRef);

        // 自动添加音频管理器
        var audioMgr = FindFirstObjectByType<AudioManager>();
        if (audioMgr == null)
        {
            var audioObj = new GameObject("AudioManager");
            audioMgr = audioObj.AddComponent<AudioManager>();
        }

        // 注入音频资源
        if (audioMgr != null)
        {
            if (drawClip != null) audioMgr.drawClip = drawClip;
            if (playClip != null) audioMgr.playClip = playClip;
            if (damageClip != null) audioMgr.damageClip = damageClip;
            if (playerDamageClip != null) audioMgr.playerDamageClip = playerDamageClip;
            if (enemyDamageClip != null) audioMgr.enemyDamageClip = enemyDamageClip;
            if (healClip != null) audioMgr.healClip = healClip;
            if (manaClip != null) audioMgr.manaClip = manaClip;
            
            Debug.Log($"[BattleManager] Injecting clips to AudioManager. ManaClip: {manaClip}");
            
            audioMgr.RegisterClips();
        }
    }

    private void Update()
    {
        if (lastIsGMMode != IsGMMode)
        {
            EnsureGMToolState();
        }
    }

    private void EnsureGMToolState()
    {
        if (IsGMMode)
        {
            var allGMTools = FindObjectsByType<GMTool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allGMTools.Length; i++)
            {
                var tool = allGMTools[i];
                if (tool != null && tool.gameObject != gameObject)
                {
                    Destroy(tool);
                }
            }

            var gmTool = GetComponent<GMTool>();
            if (gmTool == null)
            {
                gameObject.AddComponent<GMTool>();
            }
        }
        else
        {
            var allGMTools = FindObjectsByType<GMTool>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allGMTools.Length; i++)
            {
                if (allGMTools[i] != null)
                {
                    Destroy(allGMTools[i]);
                }
            }
        }
        lastIsGMMode = IsGMMode;
    }


    public BaseCharacter player;
    public BaseCharacter enemy;
    private int currentTurn = 1;
    public bool IsPlayerTurn()
    {
        return currentTurn % 2 == 1;
    }

    private BattleEventContext CreateBattleEventContext()
    {
        return new BattleEventContext(this, player, enemy, currentTurn);
    }

    private Action onPhaseChangedUnsub;
    private Action onPlayerDeadUnsub;
    private Action onEnemyDeadUnsub;
    private Action onCharacterEndedTurnUnsub;


    public void StartBattle()
    {
        StartBattle(true);
    }

    public void StartBattle(bool autoStartTurns)
    {
        Debug.Log("战斗开始！");
        EnsureGMToolState();

        StopAllCoroutines();
        player?.OnBattleEnd();
        enemy?.OnBattleEnd();
        onPhaseChangedUnsub?.Invoke();
        onPlayerDeadUnsub?.Invoke();
        onEnemyDeadUnsub?.Invoke();
        onCharacterEndedTurnUnsub?.Invoke();
        Time.timeScale = 1f;
        EnemyBoss.AllowPlay = true;
        EnemyBoss.AllowDraw = true;
        var gmTool = GetComponent<GMTool>();
        if (gmTool != null) gmTool.ResetEnemyAIFlags();

        currentTurn = 0;
        isEndingBattle = false;

        CardFactory.ResetPlayerDeck();
        CardFactory.ResetEnemyDeck();
        献祭.ResetSacrificeBonus();
        激光.ResetGlobalState();

        player = new Player();
        enemy = new EnemyBoss();
        BaseCard.ResetOverclock();

        player.Target = enemy;
        enemy.Target = player;

        player.ChangeHealth(0);  // 触发UI更新
        enemy.ChangeHealth(0);  // 触发UI更新

        player.NotifyHandChanged();
        EventCenter.Publish(GameEvents.BattleStarted, CreateBattleEventContext());

        RegisterBattleEvents();

        if (!autoStartTurns) return;

        int initialHandCount = Mathf.Clamp(5 - GameManager.Instance.difficultyLevel, 1, 4);
        for (int i = 0; i < initialHandCount; i++)
        {
            player.DrawCard(0);
        }

        NextTurn();
    }

    private void RegisterBattleEvents()
    {
        onPhaseChangedUnsub = EventCenter.Register<EnemyBossPhaseChangedEventContext>(GameEvents.EnemyBossPhaseChanged, context =>
        {
            if (player != null)
            {
                if (player.autoManaPerTurn < 10)
                {
                    player.autoManaPerTurn++;
                    Debug.Log($"阶段提升，玩家每回合自动回蓝增加至: {player.autoManaPerTurn}");
                }
            }
        });

        onPlayerDeadUnsub = EventCenter.Register<CharacterEventContext>(GameEvents.PlayerDefeated, context =>
        {
            EndBattle(true);
        });
        onEnemyDeadUnsub = EventCenter.Register<CharacterEventContext>(GameEvents.EnemyDefeated, context =>
        {
            EndBattle(true);
        });
        onCharacterEndedTurnUnsub = EventCenter.Register<CharacterEventContext>(GameEvents.CharacterTurnEnded, context =>
        {
            NextTurn();
        });
    }



    private bool isEndingBattle = false;
    private bool deleteCurrentSaveOnEnd;

    public void EndBattle()
    {
        EndBattle(false);
    }

    public void EndBattle(bool deleteCurrentSave)
    {
        if (isEndingBattle) return;
        deleteCurrentSaveOnEnd = deleteCurrentSave;
        StartCoroutine(EndBattleRoutine());
    }

    public void ExitToMainMenuWithoutSettlement()
    {
        StopAllCoroutines();
        player?.OnBattleEnd();
        enemy?.OnBattleEnd();
        onPhaseChangedUnsub?.Invoke();
        onPlayerDeadUnsub?.Invoke();
        onEnemyDeadUnsub?.Invoke();
        onCharacterEndedTurnUnsub?.Invoke();
        Time.timeScale = 1f;
        if (player != null) player.AbortTurn();
        if (enemy != null) enemy.AbortTurn();
        EventCenter.Publish(GameEvents.BattleEnded, CreateBattleEventContext());
        player = null;
        enemy = null;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        GameManager.Instance.SwitchSecne(false);
        isEndingBattle = false;
    }

    private IEnumerator EndBattleRoutine()
    {
        isEndingBattle = true;
        Debug.Log("战斗结束。");

        player?.OnBattleEnd();
        enemy?.OnBattleEnd();
        onPhaseChangedUnsub?.Invoke();
        onPlayerDeadUnsub?.Invoke();
        onEnemyDeadUnsub?.Invoke();
        onCharacterEndedTurnUnsub?.Invoke();
        Time.timeScale = 1f;

        if (player != null) player.AbortTurn();
        if (enemy != null) enemy.AbortTurn();

        EventCenter.Publish(GameEvents.BattleEnded, CreateBattleEventContext());

        // 显示游戏结束 UI
        ShowGameOverUI();

        yield return new WaitForSeconds(1.0f);

        // 结算分数
        ulong score = 0;
        if (enemy is EnemyBoss enemyBoss) score = enemyBoss.score;
        GameManager.Instance.Save(score);

        // 清理战斗数据
        player = null;
        enemy = null;

        // 隐藏游戏结束 UI
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // UI跳转
        GameManager.Instance.SwitchSecne(false);
        isEndingBattle = false;
        if (deleteCurrentSaveOnEnd && BattleSessionSaveService.Instance != null)
        {
            BattleSessionSaveService.Instance.DeleteCurrentSlotIfAny();
        }
        deleteCurrentSaveOnEnd = false;
    }

    private void ShowGameOverUI()
    {
        if (gameOverPanel == null)
        {
            var battleUI = FindFirstObjectByType<BattleUI>();
            Transform parent = battleUI ? battleUI.transform : FindFirstObjectByType<Canvas>()?.transform;
            
            if (parent != null)
            {
                var go = new GameObject("GameOverPanel");
                go.transform.SetParent(parent, false);
                gameOverPanel = go;

                // BG
                var img = go.AddComponent<Image>();
                img.color = new Color(0, 0, 0, 0.8f);
                var rect = img.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                // Text
                var textObj = new GameObject("Text");
                textObj.transform.SetParent(go.transform, false);
                var text = textObj.AddComponent<TextMeshProUGUI>();
                text.text = "游戏结束";
                text.fontSize = 100;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                
                var textRect = text.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                
                if (battleUI != null && battleUI.turnText != null)
                {
                    text.font = battleUI.turnText.font;
                }
            }
        }
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
        }
    }


    public void NextTurn()
    {
        if (player == null || enemy == null) return; // Add null check
        currentTurn++;

        Debug.Log($"第{currentTurn}回合开始");

        // 轮流行动
        if (currentTurn % 2 == 1)
        {
            StartCoroutine(player.StartTurnRoutine());
        }
        else
        {
            StartCoroutine(enemy.StartTurnRoutine());
        }
    }

    public BattleSnapshotData CaptureBattleSnapshot()
    {
        if (player == null || enemy == null) return null;
        var data = new BattleSnapshotData
        {
            difficultyLevel = GameManager.Instance != null ? GameManager.Instance.difficultyLevel : 1,
            currentTurn = currentTurn,
            playerIsInTurn = player.IsInTurn,
            enemyIsInTurn = enemy.IsInTurn,
            player = CaptureCharacter(player),
            enemy = CaptureCharacter(enemy),
            playerDeck = CaptureCardList(CardFactory.GetPlayerDeck()),
            enemyDeck = CaptureCardList(CardFactory.GetDeckSnapshot(enemy)),
            playerDots = CaptureDotList(player.dotBar),
            enemyDots = CaptureDotList(enemy.dotBar),
            sacrificeBonus = 献祭.GetSacrificeBonus(),
            laserPlayerBonusDamage = 激光.GetPlayerBonusDamage(),
            laserEnemyBonusDamage = 激光.GetEnemyBonusDamage()
        };

        if (enemy is EnemyBoss enemyBoss)
        {
            data.enemyPhase = enemyBoss.phase;
            data.enemyNextPhaseHealthThreshold = enemyBoss.nextPhaseHealthThreshold;
            data.enemyScore = enemyBoss.score;
            data.rougeDamageTier = enemyBoss.phase;
            data.currentTotalDamage = enemyBoss.score;
        }

        return data;
    }

    public void RestoreBattleFromSnapshot(BattleSnapshotData snapshot)
    {
        if (snapshot == null) return;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetDiff(snapshot.difficultyLevel);
        }

        player?.OnBattleEnd();
        enemy?.OnBattleEnd();

        player = new Player();
        enemy = new EnemyBoss();
        player.Target = enemy;
        enemy.Target = player;

        ApplyCharacterSnapshot(player, snapshot.player);
        ApplyCharacterSnapshot(enemy, snapshot.enemy);

        bool playerInTurn = snapshot.playerIsInTurn;
        bool enemyInTurn = snapshot.enemyIsInTurn;
        if (!playerInTurn && !enemyInTurn && snapshot.currentTurn > 0)
        {
            playerInTurn = snapshot.currentTurn % 2 == 1;
            enemyInTurn = !playerInTurn;
        }
        if (playerInTurn && enemyInTurn)
        {
            playerInTurn = snapshot.currentTurn % 2 == 1;
            enemyInTurn = !playerInTurn;
        }
        player.RestoreTurnState(playerInTurn);
        enemy.RestoreTurnState(enemyInTurn);
        if (player is Player playerCharacter && !playerInTurn)
        {
            playerCharacter.isReady = false;
        }

        献祭.SetSacrificeBonus(snapshot.sacrificeBonus);
        激光.SetGlobalState(snapshot.laserPlayerBonusDamage, snapshot.laserEnemyBonusDamage);

        if (enemy is EnemyBoss enemyBoss)
        {
            int phase = snapshot.enemyPhase > 0 ? snapshot.enemyPhase : snapshot.rougeDamageTier;
            enemyBoss.phase = Mathf.Max(1, phase);
            enemyBoss.nextPhaseHealthThreshold = snapshot.enemyNextPhaseHealthThreshold;
            ulong score = snapshot.enemyScore > 0 ? snapshot.enemyScore : snapshot.currentTotalDamage;
            enemyBoss.SetScore(score);
        }

        player.dotBar.Clear();
        enemy.dotBar.Clear();
        RestoreDotList(snapshot.playerDots, player);
        RestoreDotList(snapshot.enemyDots, enemy);

        ApplyCharacterSnapshot(player, snapshot.player);
        ApplyCharacterSnapshot(enemy, snapshot.enemy);
        RestoreDeck(CardFactory.GetPlayerDeck(), snapshot.playerDeck);
        RestoreDeck(GetEnemyDeckReference(), snapshot.enemyDeck);

        player.RestoreTurnState(playerInTurn);
        enemy.RestoreTurnState(enemyInTurn);
        if (player is Player restoredPlayer && !playerInTurn)
        {
            restoredPlayer.isReady = false;
        }
        currentTurn = Mathf.Max(1, snapshot.currentTurn);
        EventCenter.Publish(GameEvents.BattleStarted, CreateBattleEventContext());
    }

    private SavedCharacterData CaptureCharacter(BaseCharacter character)
    {
        var data = new SavedCharacterData
        {
            health = character.health,
            mana = character.mana,
            shiled = character.shiled,
            autoManaPerTurn = character.autoManaPerTurn,
            cards = CaptureCardList(character.Cards)
        };

        if (character is Player playerCharacter)
        {
            data.isPlayerReady = playerCharacter.isReady;
        }

        return data;
    }

    private List<SavedCardData> CaptureCardList(List<BaseCard> cards)
    {
        var result = new List<SavedCardData>();
        if (cards == null) return result;

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            if (card == null) continue;
            var cardData = CaptureCard(card);
            result.Add(cardData);
        }

        return result;
    }

    private List<SavedDotData> CaptureDotList(List<Dot> dots)
    {
        var result = new List<SavedDotData>();
        if (dots == null) return result;

        for (int i = 0; i < dots.Count; i++)
        {
            var dot = dots[i];
            if (dot == null) continue;
            var saved = new SavedDotData
            {
                sourceSide = ResolveCharacterSide(dot.source),
                targetSide = ResolveCharacterSide(dot.target),
                duration = dot.duration,
                isStolenFromOpponent = dot.IsStolenFromOpponent,
                sourceCard = CaptureCard(dot.sourceCard)
            };
            result.Add(saved);
        }

        return result;
    }

    private SavedCardData CaptureCard(BaseCard card)
    {
        if (card == null) return null;
        var saved = new SavedCardData
        {
            name = card.Name,
            typeName = card.GetType().AssemblyQualifiedName,
            cost = card.Cost,
            value = card.Value,
            duration = card.Duration,
            isStolenFromOpponent = card.IsStolenFromOpponent
        };

        if (card is 重奏 reprise)
        {
            saved.mirroredCard = CaptureCard(reprise.GetMirroredCard());
        }

        return saved;
    }

    private void ApplyCharacterSnapshot(BaseCharacter character, SavedCharacterData data)
    {
        if (character == null || data == null) return;

        character.health = data.health;
        character.mana = data.mana;
        character.shiled = data.shiled;
        character.autoManaPerTurn = data.autoManaPerTurn;
        character.Cards.Clear();

        if (data.cards != null)
        {
            for (int i = 0; i < data.cards.Count; i++)
            {
                var savedCard = data.cards[i];
                var card = CreateCardFromSaved(savedCard);
                if (card == null) continue;
                card.SetOwningCharacter(character);
                character.Cards.Add(card);
            }
        }

        if (character is Player playerCharacter)
        {
            playerCharacter.isReady = data.isPlayerReady;
        }

        character.NotifyHandChanged();
    }

    private BaseCard CreateCardFromSaved(SavedCardData saved)
    {
        if (saved == null) return null;

        BaseCard card = null;
        if (!string.IsNullOrEmpty(saved.name))
        {
            card = CardFactory.GetThisCard(saved.name);
        }

        if (card == null && !string.IsNullOrEmpty(saved.typeName))
        {
            var cardType = Type.GetType(saved.typeName);
            if (cardType != null && typeof(BaseCard).IsAssignableFrom(cardType))
            {
                card = Activator.CreateInstance(cardType) as BaseCard;
            }
        }

        if (card == null) return null;

        ApplySavedCardState(card, saved);
        return card;
    }

    private void ApplySavedCardState(BaseCard card, SavedCardData saved)
    {
        if (card == null || saved == null) return;

        if (card is 重奏 reprise && saved.mirroredCard != null)
        {
            var mirroredCard = CreateCardFromSaved(saved.mirroredCard);
            if (mirroredCard != null)
            {
                reprise.TransformInto(mirroredCard);
            }
        }

        card.SetCost(saved.cost);
        card.SetValue(saved.value);
        card.SetDuration(saved.duration);
        if (saved.isStolenFromOpponent) card.MarkStolenFromOpponent();
    }

    private void RestoreDeck(List<BaseCard> deck, List<SavedCardData> savedDeck)
    {
        if (deck == null) return;
        deck.Clear();
        if (savedDeck == null) return;

        for (int i = 0; i < savedDeck.Count; i++)
        {
            var card = CreateCardFromSaved(savedDeck[i]);
            if (card != null) deck.Add(card);
        }
    }

    private void RestoreDotList(List<SavedDotData> savedDots, BaseCharacter owner)
    {
        if (savedDots == null || owner == null) return;

        for (int i = 0; i < savedDots.Count; i++)
        {
            var saved = savedDots[i];
            if (saved == null || saved.sourceCard == null) continue;

            var source = ResolveCharacterFromSide(saved.sourceSide, owner);
            var target = ResolveCharacterFromSide(saved.targetSide, source != null ? source.Target : owner.Target);
            if (source == null) source = owner;
            if (target == null) target = source.Target;
            if (source == null) continue;

            var card = CreateCardFromSaved(saved.sourceCard);
            if (card == null) continue;
            card.SetOwningCharacter(source);
            if (saved.isStolenFromOpponent) card.MarkStolenFromOpponent();

            int beforeCount = source.dotBar.Count;
            card.Execute(source, target);
            if (source.dotBar.Count <= beforeCount) continue;

            var createdDot = source.dotBar[source.dotBar.Count - 1];
            if (createdDot == null) continue;
            createdDot.duration = Mathf.Max(0, saved.duration);
            createdDot.sourceCard = card;
            if (saved.isStolenFromOpponent) createdDot.MarkStolenFromOpponent();

            if (!ReferenceEquals(owner, source))
            {
                source.dotBar.Remove(createdDot);
                owner.dotBar.Add(createdDot);
            }
        }
    }

    private int ResolveCharacterSide(BaseCharacter character)
    {
        if (character == null) return SideNone;
        if (ReferenceEquals(character, player) || character is Player) return SidePlayer;
        if (ReferenceEquals(character, enemy) || character is EnemyBoss) return SideEnemy;
        return SideNone;
    }

    private BaseCharacter ResolveCharacterFromSide(int side, BaseCharacter fallback)
    {
        if (side == SidePlayer) return player;
        if (side == SideEnemy) return enemy;
        return fallback;
    }

    private List<BaseCard> GetEnemyDeckReference()
    {
        if (enemy == null) return new List<BaseCard>();
        var snapshot = new List<BaseCard>();
        var enemyDeckType = typeof(CardFactory).GetField("enemyDeck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (enemyDeckType != null)
        {
            var value = enemyDeckType.GetValue(null) as List<BaseCard>;
            if (value != null) return value;
        }
        return snapshot;
    }


    // 注释掉临时代码

    // void Start()
    // {
    //     StartBattle();
    // }

    // void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.Space))
    //     {
    //         StartBattle();
    //     }
    // }

    void OnDestroy()
    {
        onPhaseChangedUnsub?.Invoke();
        onPlayerDeadUnsub?.Invoke();
        onEnemyDeadUnsub?.Invoke();
        onCharacterEndedTurnUnsub?.Invoke();
        
        // 游戏退出或重新加载时，清理所有静态事件，防止引用已销毁的对象
        EventCenter.ClearAllEvents();
    }
}
