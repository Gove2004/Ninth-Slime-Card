using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class BattleUI : MonoBehaviour
{
    private const float ActionStateRefreshInterval = 0.1f;
    // UI按钮引用
    public Button drawCardButton;
    public Button playCardButton;
    public Button endTurnButton;
    public Button exitButton;

    public TextMeshProUGUI turnText;

    private TextMeshProUGUI drawCardButtonText;
    private TextMeshProUGUI endTurnButtonText;
    private ulong lastDrawCost = ulong.MaxValue;
    private ulong lastAutoMana = 0;
    private bool hasLastAutoMana = false;
    private float nextActionStateRefreshTime;

    public CardList cardList;

    void Start()
    {
        EnsureReferences();

        if (drawCardButton != null) drawCardButton.onClick.AddListener(OnDrawCardClicked);
        if (playCardButton != null) playCardButton.onClick.AddListener(OnPlayCardClicked);
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);

        if (drawCardButton != null) drawCardButtonText = drawCardButton.GetComponentInChildren<TextMeshProUGUI>();
        if (endTurnButton != null) endTurnButtonText = endTurnButton.GetComponentInChildren<TextMeshProUGUI>();

        EventCenter.Register<CharacterEventContext>(GameEvents.CharacterTurnStarted, _ => ShowNewTurnInfo());
        EventCenter.Register<BattleEventContext>(GameEvents.BattleStarted, _ => 
        {
            if (cardList != null) cardList.SyncFromBattleHand();
            RefreshActionState(true);
        });

        RefreshActionState(true);
    }

    private void EnsureReferences()
    {
        var buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            if (button == null) continue;
            var buttonName = button.gameObject.name;
            if (drawCardButton == null && (buttonName == "DrawCard" || buttonName == "抽牌"))
            {
                drawCardButton = button;
            }
            else if (playCardButton == null && (buttonName == "PlayCard" || buttonName == "出牌"))
            {
                playCardButton = button;
            }
            else if (endTurnButton == null && (buttonName == "EndTurn" || buttonName == "结束回合"))
            {
                endTurnButton = button;
            }
            else if (exitButton == null && (buttonName == "Exit" || buttonName == "ExitButton" || buttonName == "退出"))
            {
                exitButton = button;
            }
        }
    }


    Sequence showTurnInfoSequence;
    private void ShowNewTurnInfo()
    {
        if (turnText != null)
        {
            if (BattleManager.Instance.IsPlayerTurn())
            {
                turnText.gameObject.SetActive(true);

                turnText.text = "你的回合";
                turnText.color = Color.green;

                if (showTurnInfoSequence != null && showTurnInfoSequence.IsActive())
                {
                    showTurnInfoSequence.Restart();
                }
                else
                {
                    turnText.transform.localScale = Vector3.one * 1.5f;
                    showTurnInfoSequence = DOTween.Sequence();
                    showTurnInfoSequence.Append(turnText.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));

                    showTurnInfoSequence.AppendInterval(1.5f); // 显示1.5秒后淡出
                    showTurnInfoSequence.Append(turnText.DOFade(0f, 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
                    {
                        turnText.gameObject.SetActive(false);
                        turnText.color = new Color(turnText.color.r, turnText.color.g, turnText.color.b, 1f); // 重置透明度
                    }));
                }

            }
            else
            {
                turnText.text = "敌人回合";
                turnText.color = Color.red;
                turnText.gameObject.SetActive(true);

                if (showTurnInfoSequence != null && showTurnInfoSequence.IsActive())
                {
                    showTurnInfoSequence.Restart();
                }
                else
                {
                    turnText.transform.localScale = Vector3.one * 1.5f;
                    showTurnInfoSequence = DOTween.Sequence();
                    showTurnInfoSequence.Append(turnText.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack));

                    showTurnInfoSequence.AppendInterval(1.5f); // 显示1.5秒后淡出
                    showTurnInfoSequence.Append(turnText.DOFade(0f, 0.5f).SetEase(Ease.OutQuad).OnComplete(() =>
                    {
                        turnText.gameObject.SetActive(false);
                        turnText.color = new Color(turnText.color.r, turnText.color.g, turnText.color.b, 1f); // 重置透明度
                    }));
                }
            }
        }
    }


    void Update()
    {
        RefreshActionState(false);

        if (GMTool.IsTextInputActive)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.A) && CanUseDraw())
        {
            OnDrawCardClicked();
        }
        if (Input.GetKeyDown(KeyCode.W) && CanUsePlay())
        {
            OnPlayCardClicked();
        }
        if (Input.GetKeyDown(KeyCode.D) && CanUseEndTurn())
        {
            OnEndTurnClicked();
        }
    }

    private void OnDrawCardClicked()
    {
        if (!CanUseDraw()) return;
        ((Player)BattleManager.Instance.player).UI_DrawCard();
        RefreshActionState(true);
    }

    private void OnPlayCardClicked()
    {
        if (!CanUsePlay()) return;
        ((Player)BattleManager.Instance.player).UI_PlayCard(cardList.selectedCard);
        RefreshActionState(true);
    }

    private void OnEndTurnClicked()
    {
        if (!CanUseEndTurn()) return;
        ((Player)BattleManager.Instance.player).UI_EndTurn();
        RefreshActionState(true);
    }

    private void OnExitClicked()
    {
        if (BattleManager.Instance == null || !BattleManager.Instance.IsPlayerTurn()) return;
        if (GameManager.Instance == null) return;
        GameManager.Instance.ExitBattleWithAutoSave();
    }

    public void OnExitButtonClicked()
    {
        OnExitClicked();
    }

    private void RefreshActionState(bool immediate)
    {
        if (!immediate && Time.unscaledTime < nextActionStateRefreshTime)
        {
            return;
        }

        nextActionStateRefreshTime = Time.unscaledTime + ActionStateRefreshInterval;

        if ((Player)BattleManager.Instance?.player != null)
        {
            Player player = (Player)BattleManager.Instance.player;
            bool isPlayerTurn = player.isReady;
            bool canUseTurnActions = player.CanUseTurnActions;
            ulong drawCost = player.GetCurrentDrawCardCost();
            if (drawCardButton != null) drawCardButton.interactable = canUseTurnActions && player.mana >= drawCost && player.Cards.Count < Player.HandLimit;
            if (playCardButton != null) playCardButton.interactable = canUseTurnActions && cardList.AblePlay;
            if (endTurnButton != null) endTurnButton.interactable = isPlayerTurn;
            bool showExitButton = isPlayerTurn;
            if (exitButton != null && exitButton.gameObject.activeSelf != showExitButton) exitButton.gameObject.SetActive(showExitButton);

            if (drawCardButtonText != null && lastDrawCost != drawCost)
            {
                lastDrawCost = drawCost;
                drawCardButtonText.text = $"抽牌（A）\n(消耗{drawCost}魔力)";
            }

            if (!hasLastAutoMana || player.autoManaPerTurn != lastAutoMana)
            {
                lastAutoMana = player.autoManaPerTurn;
                hasLastAutoMana = true;
                if (endTurnButtonText != null)
                {
                    endTurnButtonText.text = $"结束（D）\n(回复{lastAutoMana}魔力)";
                }
            }
        }
    }

    private bool CanUseDraw()
    {
        return drawCardButton != null && drawCardButton.interactable;
    }

    private bool CanUsePlay()
    {
        return playCardButton != null && playCardButton.interactable;
    }

    private bool CanUseEndTurn()
    {
        return endTurnButton != null && endTurnButton.interactable;
    }
}
