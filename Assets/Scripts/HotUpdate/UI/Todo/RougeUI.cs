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

    public Button okButton;
    public Button cancelButton;


    // 选择的卡牌
    private BaseCard selectedCard;
    // 要替换的序号
    private int replaceIndex;
    private static int activePanelCount;
    public static bool IsPanelOpen => activePanelCount > 0;

    void Start()
    {
        okButton.onClick.AddListener(() =>
        {
            CardFactory.ReplaceCardInPlayerDeck(selectedCard, replaceIndex);
            var player = BattleManager.Instance != null ? BattleManager.Instance.player as Player : null;
            if (player != null && selectedCard != null)
            {
                var handCard = CardFactory.GetThisCard(selectedCard.Name);
                player.GainCard(handCard != null ? handCard : selectedCard);
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


        this.gameObject.SetActive(false);

        EventCenter.Register<EnemyBossPhaseChangedEventContext>(GameEvents.EnemyBossPhaseChanged, _ =>
        {
            Debug.Log("阶段改变，显示换牌界面");
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


    void Update()
    {
        okButton.interactable = selectedCard != null && replaceIndex != -1;
    }



    private void Init()
    {
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

        // 显示玩家牌组
        var playerDeck = CardFactory.GetPlayerDeck();
        for (int i = 0; i < playerDeck.Count; i++)
        {
            BaseCard cardData = playerDeck[i];
            playerCardButtons[i].SetData(cardData);
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
    }

    private void SelectReplaceIndex(int index)
    {
        replaceIndex = index;
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
        replaceIndex = -1;
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
    }

    private void HideAndReset()
    {
        Time.timeScale = 1f;
        ClearSelectionState();
        this.gameObject.SetActive(false);
    }

}
