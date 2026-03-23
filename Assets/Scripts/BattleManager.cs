using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleManager : MonoBehaviour
{
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
        var audioMgr = FindObjectOfType<AudioManager>();
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
    private Action onPhaseChangedUnsub;
    private Action onPlayerDeadUnsub;
    private Action onEnemyDeadUnsub;
    private Action onCharacterEndedTurnUnsub;


    public void StartBattle()
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

        EventCenter.Publish("BattleStarted");

        onPhaseChangedUnsub = EventCenter.Register("EnemyBoss_PhaseChanged", (param) =>
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

        onPlayerDeadUnsub = EventCenter.Register("PlayerDead", (param) =>
        {
            var character = param as BaseCharacter;
            EndBattle();
        });
        onEnemyDeadUnsub = EventCenter.Register("EnemyDead", (param) =>
        {
            var character = param as BaseCharacter;
            EndBattle();
        });
        onCharacterEndedTurnUnsub = EventCenter.Register("CharacterEndedTurn", (param) =>
        {
            NextTurn();
        });

        int initialHandCount = Mathf.Clamp(5 - GameManager.Instance.difficultyLevel, 1, 4);
        for (int i = 0; i < initialHandCount; i++)
        {
            var card = player.DrawCard(0);
            if (card != null)
            {
                EventCenter.Publish("Player_DrawCard", card);
            }
        }

        NextTurn();
    }



    private bool isEndingBattle = false;

    public void EndBattle()
    {
        if (isEndingBattle) return;
        StartCoroutine(EndBattleRoutine());
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

        EventCenter.Publish("BattleEnded");

        // 显示游戏结束 UI
        ShowGameOverUI();

        yield return new WaitForSeconds(1.0f);

        // 结算分数
        ulong score = 0;
        if (enemy is EnemyBoss enemyBoss) score = enemyBoss.score;
        if (AchievementManager.Instance != null) AchievementManager.Instance.AddScore(score);
        GameManager.Instance.Save(score);

        // 清理战斗数据
        player = null;
        enemy = null;

        // 隐藏游戏结束 UI
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        // UI跳转
        GameManager.Instance.SwitchSecne(false);
        isEndingBattle = false;
    }

    private void ShowGameOverUI()
    {
        if (gameOverPanel == null)
        {
            var battleUI = FindObjectOfType<BattleUI>();
            Transform parent = battleUI ? battleUI.transform : FindObjectOfType<Canvas>()?.transform;
            
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
