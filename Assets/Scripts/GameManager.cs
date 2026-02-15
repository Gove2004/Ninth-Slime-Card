using UnityEngine;

public class GameManager : MonoBehaviour
{
    // 简单的单例模式
    public static GameManager Instance;
    void Awake()
    {
        Instance = this;

        if (AchievementManager.Instance == null)
        {
            var go = new GameObject("AchievementManager");
            go.AddComponent<AchievementManager>();
        }

        Load(); // 加载存档
    }


    void Start()
    {
        
        SwitchSecne(false); // 默认进入主界面
    }

    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
    }

    # region 存档

    public int maxScore { get; private set; } = 0;

    public void Save(int score)
    {
        Debug.Log($"Saving score: {score}, current maxScore: {maxScore}");
        if (score > maxScore)
        {
            maxScore = score;
            PlayerPrefs.SetInt("MaxScore", maxScore);
            PlayerPrefs.Save();
        }
    }


    public void Load()
    {
        maxScore = PlayerPrefs.GetInt("MaxScore", 0);
    }

    # endregion



    #region 状态

    private bool IsBattle = false;
    public GameObject battleUI;
    public GameObject mainUI;

    public void SwitchSecne(bool isBattle)
    {
        // 这里只涉及两个场景的切换
        // 并且直接用MAin挡住Battle

        IsBattle = isBattle;

        if (IsBattle) // 进入Battle
        {
            // battleUI.gameObject.SetActive(true);
            mainUI.gameObject.SetActive(false);

            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayBattleBGM();

            BattleManager.Instance.StartBattle();
        }
        else  // 回到主界面
        {
            // battleUI.gameObject.SetActive(false);
            mainUI.gameObject.SetActive(true);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayTitleBGM();
            }
            else
            {
                Debug.LogWarning("[GameManager] AudioManager.Instance is null when switching to Title!");
                // Try to find it manually in case Awake hasn't set Instance yet (unlikely in Start, but possible)
                var am = FindObjectOfType<AudioManager>();
                if (am != null)
                {
                    am.PlayTitleBGM();
                }
                else
                {
                     Debug.LogWarning("[GameManager] AudioManager not found in scene. Creating one.");
                     var go = new GameObject("AudioManager");
                     go.AddComponent<AudioManager>().PlayTitleBGM();
                }
            }
        }
    }

    #endregion


    #region 难度
    // 没有采用配置表
    // 建议全局搜索 "GameManager.Instance.difficultyLevel" 查看用法
    // 忘了用枚举，直接用整数了，1-3分别代表简单、困难、地狱
    public int difficultyLevel { get; private set; } = 1;  // 默认难度为1
    
    public void SetDiff(int diff)
    {
        
        difficultyLevel = diff;
    }
    #endregion
}
