using UnityEngine;
using UnityEngine.UI;

public class RougeUI : MonoBehaviour
{
    // 常量
    private const int selNums = 3;
    private const int replaceNums = 10;


    public GameObject cardButtonPrefab;

    public Transform cardsParent;
    private CardButton[] cardButtons = new CardButton[selNums];
    public Transform playerCardsParent;
    private CardButton[] playerCardButtons = new CardButton[replaceNums];
    public GameObject oldTipObject;
    public GameObject playerListObject;

    public Button okButton;
    public Button cancelButton;


    // 选择的卡牌
    private BaseCard selectedCard;
    private static int activePanelCount;
    public static bool IsPanelOpen => activePanelCount > 0;

    void Start()
    {
        okButton.onClick.AddListener(() =>
        {
            selectedCard = CardRuntimeHelper.PreparePlayerAcquiredCard(selectedCard);
            CardFactory.AddCardToPlayerDeck(CardRuntimeHelper.CloneCardState(selectedCard) ?? selectedCard);
            var player = BattleManager.Instance != null ? BattleManager.Instance.player as Player : null;
            if (player != null && selectedCard != null)
            {
                var handCard = CardRuntimeHelper.CloneCardState(selectedCard);
                player.GainCard(handCard ?? selectedCard);
            }
            HideAndReset();
        });
        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(() =>
            {
                HideAndReset();
            });
        }


        Init();
        HideReplacementUi();


        this.gameObject.SetActive(false);

        EventCenter.Register<EnemyBossPhaseChangedEventContext>(GameEvents.EnemyBossPhaseChanged, _ =>
        {
            this.gameObject.SetActive(true);
            Show();
        });
    }

    private void OnEnable()
    {
        activePanelCount++;
    }

    private void OnDisable()
    {
        activePanelCount = Mathf.Max(0, activePanelCount - 1);
    }


    private void UpdateOkButton()
    {
        if (okButton != null)
        {
            okButton.interactable = selectedCard != null;
        }
    }



    private void Init()
    {
        EnsureOptionalReferences();

        // 初始化卡牌按钮
        for (int i = 0; i < selNums; i++)
        {
            int index = i;  // 需要一个局部变量来捕获当前的i值
            cardButtons[i] = Instantiate(cardButtonPrefab, cardsParent).GetComponent<CardButton>();
            cardButtons[i].SetAction(() =>
            {
                SelectCard(index);
            });
        }
        for (int i = 0; i < replaceNums; i++)
        {
            if (playerCardsParent == null) break;
            playerCardButtons[i] = Instantiate(cardButtonPrefab, playerCardsParent).GetComponent<CardButton>();
            int index = i;  // 需要一个局部变量来捕获当前的i值
            playerCardButtons[i].SetAction(() =>
            {
                SelectReplaceIndex(index);
            });
        }
    }


    public void Show()
    {
        Time.timeScale = 0f;
        ClearSelectionState();
        HideReplacementUi();
        UpdateOkButton();

        // 随机三个卡牌
        System.Collections.Generic.HashSet<string> selectedNames = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < cardButtons.Length; i++)
        {
            BaseCard cardData = null;
            int maxAttempts = 50;
            do
            {
                cardData = CardFactory.GetRandomCard();
                maxAttempts--;
            } while (cardData != null && selectedNames.Contains(cardData.Name) && maxAttempts > 0);

            if (cardData != null)
            {
                selectedNames.Add(cardData.Name);
                cardButtons[i].SetData(cardData);
            }
        }
    }

    private void SelectCard(int index)
    {
        selectedCard = cardButtons[index].cardData;
        for (int i = 0; i < cardButtons.Length; i++)
        {
            if (cardButtons[i] != null)
            {
                cardButtons[i].Deselected();
            }
        }
        if (cardButtons[index] != null)
        {
            cardButtons[index].Selected();
        }
        UpdateOkButton();
    }

    private void SelectReplaceIndex(int index)
    {
        for (int i = 0; i < playerCardButtons.Length; i++)
        {
            if (playerCardButtons[i] != null)
            {
                playerCardButtons[i].Deselected();
            }
        }
        if (playerCardButtons[index] != null)
        {
            playerCardButtons[index].Selected();
        }
    }

    private void ClearSelectionState()
    {
        selectedCard = null;
        for (int i = 0; i < cardButtons.Length; i++)
        {
            if (cardButtons[i] != null)
            {
                cardButtons[i].Deselected();
            }
        }
        for (int i = 0; i < playerCardButtons.Length; i++)
        {
            if (playerCardButtons[i] != null)
            {
                playerCardButtons[i].Deselected();
            }
        }
        UpdateOkButton();
    }

    private void HideAndReset()
    {
        Time.timeScale = 1f;
        ClearSelectionState();
        this.gameObject.SetActive(false);
    }

    private void HideReplacementUi()
    {
        if (oldTipObject != null) oldTipObject.SetActive(false);
        if (playerListObject != null) playerListObject.SetActive(false);
        if (playerCardsParent != null) playerCardsParent.gameObject.SetActive(false);
    }

    private void EnsureOptionalReferences()
    {
        if (oldTipObject == null)
        {
            Transform child = transform.Find("OldTip");
            if (child != null) oldTipObject = child.gameObject;
        }

        if (playerListObject == null)
        {
            Transform child = transform.Find("PlayerList");
            if (child != null) playerListObject = child.gameObject;
        }
    }

}
