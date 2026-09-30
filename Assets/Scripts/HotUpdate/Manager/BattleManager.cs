using System.Collections;
using System.Collections.Generic;
using GoveKits.Runtime.Core;
using GoveKits.Runtime.Storage;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BattleManager : MonoSingleton<BattleManager>
{
    public enum BattlePhase
    {
        None,
        Initializing,
        PlayerTurn,
        Resolving,
        EnemyTurn,
        BattleEnded,
    }

    [SerializeField] private float enemyTurnStartDelay = 0.5f;
    [SerializeField] private float enemyActionDelay = 0.8f;
    [SerializeField] private float enemyTurnEndDelay = 0.5f;

    public bool isBattleActive { get; private set; }
    public Player Player { get; private set; }
    public Enemy Enemy { get; private set; }
    public int turnCount { get; private set; }
    public BattlePhase Phase { get; private set; } = BattlePhase.None;

    private CardContainer cardContainer;
    private Button endTurnButton;
    private Coroutine enemyTurnCoroutine;
    private BattleResultOverlay resultOverlay;

    public void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindSceneReferences();
        FindEndTurnButton();
        EnsureResultOverlay();
        // 直接在 Start 后开始战斗，避免队列竞态
        StartBattleImmediately();
    }

    public void RestartBattleScene()
    {
        PrepareForSceneTransition();
        ResCore.LoadSceneAsync("Battle");
    }

    public void ReturnHome()
    {
        PrepareForSceneTransition();
        ResCore.LoadSceneAsync("Home");
    }

    // === 关键修复：直接开始，无单帧延迟，且事件订阅在 Setup 之后 ===
    private void StartBattleImmediately()
    {
        BindSceneReferences();

        if (Player == null || Enemy == null)
        {
            Debug.LogError("[BattleManager] Player or Enemy not found in scene!");
            return;
        }

        int lv = GameCore.runState?.currentLv ?? 1;
        Debug.Log($"Starting battle at Lv.{lv}");

        Phase = BattlePhase.Initializing;
        isBattleActive = true;
        turnCount = 0;

        Player.Setup();
        Enemy.Setup();

        Player.Target = Enemy;
        Enemy.Target = Player;

        // ✅ 先 Setup 对象，再订阅事件，避免错过 Setup 触发的事件
        Player.OnHandChanged -= OnPlayerHandChanged;
        Player.OnHandChanged += OnPlayerHandChanged;
        Player.OnStatsChanged -= OnCharacterStatsChanged;
        Player.OnStatsChanged += OnCharacterStatsChanged;
        Enemy.OnStatsChanged -= OnCharacterStatsChanged;
        Enemy.OnStatsChanged += OnCharacterStatsChanged;

        RefreshHand();
        StartPlayerTurn();
        MessageToastManager.Instance.ShowMessage($"Lv.{lv} 战斗开始！");

        string bgmName = Random.value < 0.5f ? "战斗bgm1" : "战斗bgm2";
        AudioManager.Instance.PlayBGM(bgmName);
    }

    public bool RequestPlayCard(BaseCard card)
    {
        if (!isBattleActive || Phase != BattlePhase.PlayerTurn)
        {
            return false;
        }

        if (Player == null || Enemy == null || !Player.CanUseCard(card, Enemy))
        {
            return false;
        }

        Phase = BattlePhase.Resolving;
        RefreshHand();

        bool success = Player.UseCard(card, Enemy);
        if (!success)
        {
            Phase = BattlePhase.PlayerTurn;
            RefreshHand();
            return false;
        }

        MessageToastManager.Instance.ShowMessage($"使用了卡牌：{card.Name}");
        CheckBattleEnd();

        if (isBattleActive)
        {
            Phase = BattlePhase.PlayerTurn;
            RefreshHand();
        }

        UpdateEndTurnButton();
        return true;
    }

    public void RequestEndTurn()
    {
        if (!isBattleActive || Phase != BattlePhase.PlayerTurn)
        {
            return;
        }

        Player.EndTurn();
        if (enemyTurnCoroutine != null)
        {
            StopCoroutine(enemyTurnCoroutine);
        }
        enemyTurnCoroutine = StartCoroutine(RunEnemyTurnAsync());
    }

    public void CheckBattleEnd()
    {
        if (!isBattleActive)
        {
            return;
        }

        if (Player != null && Player.IsDead)
        {
            EndBattle(false);
            return;
        }

        if (Enemy != null && Enemy.IsDead)
        {
            EndBattle(true);
        }
    }

    public void EndBattle(bool playerWon)
    {
        if (!isBattleActive) return;

        isBattleActive = false;
        Phase = BattlePhase.BattleEnded;

        if (enemyTurnCoroutine != null)
        {
            StopCoroutine(enemyTurnCoroutine);
            enemyTurnCoroutine = null;
        }

        UpdateEndTurnButton();
        RefreshHand();

        if (playerWon)
        {
            int trophyReward = CalculateTrophyReward();
            if (trophyReward > 0)
            {
                GameCore.AddTrophy(trophyReward);
                MessageToastManager.Instance.ShowMessage($"战斗胜利！获得 {trophyReward} 奖杯");
            }
            else
            {
                MessageToastManager.Instance.ShowMessage("战斗胜利！");
            }

            Player.SaveCurrentDeck();
            ShowRoguelikeChoice();
        }
        else
        {
            MessageToastManager.Instance.ShowMessage("战斗失败！");
            GameCore.StartNewRun();
            EnsureResultOverlay();
            if (resultOverlay != null)
            {
                resultOverlay.Show(false);
            }
        }
    }

    private void ShowRoguelikeChoice()
    {
        RoguelikeChoicePanel panel = FindFirstObjectByType<RoguelikeChoicePanel>(FindObjectsInactive.Include);
        if (panel == null)
        {
            Debug.LogWarning("[BattleManager] RoguelikeChoicePanel not found, advancing to next Lv directly");
            AdvanceToNextLv();
            return;
        }

        panel.Show(AdvanceToNextLv);
    }

    public void AdvanceToNextLv()
    {
        if (GameCore.runState != null)
        {
            GameCore.runState.currentLv++;
            GameCore.SaveRunState();
        }

        RestartBattleScene();
    }

    private int CalculateTrophyReward()
    {
        return GameCore.runState?.currentLv ?? 1;
    }

    private void StartPlayerTurn()
    {
        if (!isBattleActive)
        {
            return;
        }

        turnCount++;
        Phase = BattlePhase.PlayerTurn;
        Player.StartTurn();
        RefreshHand();
        UpdateEndTurnButton();
        MessageToastManager.Instance.ShowMessage($"第 {turnCount} 回合");
    }

    private IEnumerator RunEnemyTurnAsync()
    {
        if (!isBattleActive)
        {
            yield break;
        }

        Phase = BattlePhase.EnemyTurn;
        RefreshHand();
        UpdateEndTurnButton();
        MessageToastManager.Instance.ShowMessage("敌方回合");

        yield return new WaitForSeconds(enemyTurnStartDelay);
        if (!isBattleActive) yield break;

        Enemy.StartTurn();

        int cardsPlayed = 0;
        const int maxCardsPerTurn = 10;
        while (isBattleActive && cardsPlayed < maxCardsPerTurn)
        {
            string cardName = Enemy.TryActOnce();
            if (cardName == null)
            {
                break;
            }

            cardsPlayed++;
            MessageToastManager.Instance.ShowMessage($"敌人使用了：{cardName}");
            RefreshHand();
            CheckBattleEnd();
            if (!isBattleActive) yield break;

            yield return new WaitForSeconds(0.5f);
            if (!isBattleActive) yield break;
        }

        if (cardsPlayed == 0)
        {
            MessageToastManager.Instance.ShowMessage("敌人没有可执行动作");
        }

        yield return new WaitForSeconds(enemyTurnEndDelay);
        if (!isBattleActive) yield break;

        Enemy.EndTurn();
        CheckBattleEnd();
        if (isBattleActive)
        {
            StartPlayerTurn();
        }

        enemyTurnCoroutine = null;
    }

    private void OnPlayerHandChanged(BaseCharacter character)
    {
        RefreshHand();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Battle")
        {
            cardContainer = null;
            endTurnButton = null;
            resultOverlay = null;
            return;
        }

        FindEndTurnButton();
        EnsureResultOverlay();
        // 场景加载后直接开始，无额外等待
        StartBattleImmediately();
    }

    private void OnCharacterStatsChanged(BaseCharacter character)
    {
        CheckBattleEnd();
    }

    private void RefreshHand()
    {
        if (cardContainer == null)
        {
            cardContainer = Object.FindFirstObjectByType<CardContainer>();
        }

        if (cardContainer == null || Player == null)
        {
            return;
        }

        bool interactable = isBattleActive && Phase == BattlePhase.PlayerTurn;
        cardContainer.RefreshHand(Player.HandCards, interactable);
    }

    private void FindEndTurnButton()
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            if (button != null && button.name == "OverTurn")
            {
                endTurnButton = button;
                endTurnButton.onClick.RemoveListener(RequestEndTurn);
                endTurnButton.onClick.AddListener(AudioManager.Instance.PlayOnClick(RequestEndTurn));
                break;
            }
        }

        UpdateEndTurnButton();
    }

    private void UpdateEndTurnButton()
    {
        if (endTurnButton != null)
        {
            endTurnButton.interactable = isBattleActive && Phase == BattlePhase.PlayerTurn;
        }
    }

    private void PrepareForSceneTransition()
    {
        isBattleActive = false;
        Phase = BattlePhase.None;

        if (enemyTurnCoroutine != null)
        {
            StopCoroutine(enemyTurnCoroutine);
            enemyTurnCoroutine = null;
        }

        UpdateEndTurnButton();
        UnsubscribeCharacterEvents();
    }

    private void BindSceneReferences()
    {
        Player = Object.FindFirstObjectByType<Player>();
        Enemy = Object.FindFirstObjectByType<Enemy>();
        cardContainer = Object.FindFirstObjectByType<CardContainer>();
        resultOverlay = Object.FindFirstObjectByType<BattleResultOverlay>(FindObjectsInactive.Include);
    }

    private void UnsubscribeCharacterEvents()
    {
        if (Player != null)
        {
            Player.OnHandChanged -= OnPlayerHandChanged;
            Player.OnStatsChanged -= OnCharacterStatsChanged;
        }

        if (Enemy != null)
        {
            Enemy.OnStatsChanged -= OnCharacterStatsChanged;
        }
    }

    private void EnsureResultOverlay()
    {
        if (resultOverlay != null)
        {
            return;
        }

        resultOverlay = Object.FindFirstObjectByType<BattleResultOverlay>(FindObjectsInactive.Include);
        if (resultOverlay != null)
        {
            return;
        }

        Debug.LogError("BattleResultOverlay not found in the scene. Please ensure it is present.");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (enemyTurnCoroutine != null)
        {
            StopCoroutine(enemyTurnCoroutine);
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeCharacterEvents();

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(RequestEndTurn);
        }
    }
}