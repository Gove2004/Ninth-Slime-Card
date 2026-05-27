using System.Collections;
using GoveKits.Runtime.Core;
using UnityEngine;
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
        Player = Object.FindFirstObjectByType<Player>();
        Enemy = Object.FindFirstObjectByType<Enemy>();
        cardContainer = Object.FindFirstObjectByType<CardContainer>();
        FindEndTurnButton();
        EnsureResultOverlay();
        StartBattle();
    }

    public void StartBattle()
    {
        string levelName = GameCore.currentLevelName;
        Debug.Log($"Starting battle at level: {levelName}");

        Phase = BattlePhase.Initializing;
        isBattleActive = true;
        turnCount = 0;

        Player.Setup();
        Enemy.Setup();

        Player.Target = Enemy;
        Enemy.Target = Player;

        Player.OnHandChanged += OnPlayerHandChanged;
        Player.OnStatsChanged += OnCharacterStatsChanged;
        Enemy.OnStatsChanged += OnCharacterStatsChanged;

        RefreshHand();
        StartPlayerTurn();
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
        MessageToastManager.Instance.ShowMessage(playerWon ? "战斗胜利！" : "战斗失败！");
        EnsureResultOverlay();
        if (resultOverlay != null)
        {
            resultOverlay.Show(playerWon);
        }
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
        yield return new WaitForSeconds(enemyActionDelay);
        if (!isBattleActive) yield break;

        bool acted = Enemy.TryAct();
        MessageToastManager.Instance.ShowMessage(acted ? "敌人行动了" : "敌人没有可执行动作");

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
                endTurnButton.onClick.AddListener(RequestEndTurn);
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

    private void EnsureResultOverlay()
    {
        if (resultOverlay != null)
        {
            return;
        }

        resultOverlay = Object.FindFirstObjectByType<BattleResultOverlay>();
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

        if (Player != null)
        {
            Player.OnHandChanged -= OnPlayerHandChanged;
            Player.OnStatsChanged -= OnCharacterStatsChanged;
        }

        if (Enemy != null)
        {
            Enemy.OnStatsChanged -= OnCharacterStatsChanged;
        }

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(RequestEndTurn);
        }
    }
}
