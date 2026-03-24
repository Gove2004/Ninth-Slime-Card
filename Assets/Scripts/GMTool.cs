using UnityEngine;




public class GMTool : MonoBehaviour
{
    private const int WindowId = 9527;
    private static readonly Rect DragArea = new Rect(0, 0, 10000, 24);
    private string healthDelta = "0";
    private string manaDelta = "0";
    private string cardId = "1000";
    private string cardName = "重奏";
    private string message = "";
    private bool enemyAllowPlay = true;
    private bool enemyAllowDraw = true;
    private bool enemyToggleInitialized = false;
    private bool showPanel = true;
    private Rect windowRect = new Rect(10, 10, 260, 470);
    private const int FullHeight = 470;
    private const int CollapsedHeight = 70;

    public void ResetEnemyAIFlags()
    {
        enemyAllowPlay = true;
        enemyAllowDraw = true;
        enemyToggleInitialized = false;
        EnemyBoss.AllowPlay = true;
        EnemyBoss.AllowDraw = true;
    }
#if UNITY_EDITOR
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            PlayerPrefs.DeleteAll();
            if (GameManager.Instance != null) GameManager.Instance.Load();
            message = "存档已清空";
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;
        var battleManager = BattleManager.Instance;
        if (battleManager == null) return;
        if (battleManager.gameObject != gameObject)
        {
            Destroy(this);
            return;
        }
        windowRect.height = showPanel ? FullHeight : CollapsedHeight;
        bool oldEnabled = GUI.enabled;
        Color oldColor = GUI.color;
        try
        {
            GUI.enabled = true;
            GUI.color = Color.white;
            windowRect = GUILayout.Window(WindowId, windowRect, DrawWindow, "GM");
        }
        catch (System.Exception ex)
        {
            message = $"GM窗口异常: {ex.Message}";
        }
        finally
        {
            GUI.enabled = oldEnabled;
            GUI.color = oldColor;
        }
    }

    private void DrawWindow(int windowId)
    {
        try
        {
            if (!Application.isPlaying) return;
            var battleManager = BattleManager.Instance;
            if (battleManager == null) return;
            var player = battleManager.player;
            var enemy = battleManager.enemy as EnemyBoss;

            if (GUILayout.Button(showPanel ? "隐藏" : "显示"))
            {
                showPanel = !showPanel;
            }
            if (!showPanel)
            {
                return;
            }

            if (player == null)
            {
                GUILayout.Label("等待战斗开始...");
                if (!string.IsNullOrEmpty(message))
                {
                    GUILayout.Label(message);
                }
                return;
            }

            GUILayout.Label("玩家生命值调整(可负数)");
            healthDelta = GUILayout.TextField(healthDelta);
            if (GUILayout.Button("应用生命值"))
            {
                if (long.TryParse(healthDelta, out var value))
                {
                    player.ApplyHealthChange(value, player);
                    message = $"生命值调整 {value}";
                }
                else
                {
                    message = "生命值输入无效";
                }
            }

            GUILayout.Label("玩家法力值调整(可负数)");
            manaDelta = GUILayout.TextField(manaDelta);
            if (GUILayout.Button("应用法力值"))
            {
                if (long.TryParse(manaDelta, out var value))
                {
                    player.ChangeMana(value);
                    message = $"法力值调整 {value}";
                }
                else
                {
                    message = "法力值输入无效";
                }
            }

            GUILayout.Label("获取指定ID卡牌");
            cardId = GUILayout.TextField(cardId);
            if (GUILayout.Button("获取卡牌"))
            {
                if (int.TryParse(cardId, out var cardIdValue))
                {
                    var data = CardDatabase.GetCardData(cardIdValue);
                    if (data != null)
                    {
                        var card = CardFactory.GetThisCard(data.name);
                        if (card != null)
                        {
                            player.GainCard(card);
                            message = $"已获得卡牌 {data.name}";
                        }
                        else
                        {
                            message = "未找到卡牌类";
                        }
                    }
                }
                else
                {
                    message = "卡牌ID输入无效";
                }
            }

            GUILayout.Label("获取指定名称卡牌");
            cardName = GUILayout.TextField(cardName);
            if (GUILayout.Button("按名称获取卡牌"))
            {
                string trimmedName = string.IsNullOrWhiteSpace(cardName) ? string.Empty : cardName.Trim();
                if (string.IsNullOrEmpty(trimmedName))
                {
                    message = "卡牌名称不能为空";
                }
                else
                {
                    var card = CardFactory.GetThisCard(trimmedName);
                    if (card != null)
                    {
                        player.GainCard(card);
                        message = $"已获得卡牌 {card.Name}";
                    }
                    else
                    {
                        message = $"未找到名称为 {trimmedName} 的卡牌";
                    }
                }
            }

            if (enemy != null)
            {
                if (!enemyToggleInitialized)
                {
                    enemyAllowPlay = EnemyBoss.AllowPlay;
                    enemyAllowDraw = EnemyBoss.AllowDraw;
                    enemyToggleInitialized = true;
                }
                GUILayout.Label("敌人AI开关");
                GUILayout.BeginHorizontal();
                enemyAllowPlay = GUILayout.Toggle(enemyAllowPlay, "用牌");
                enemyAllowDraw = GUILayout.Toggle(enemyAllowDraw, "抽牌");
                GUILayout.EndHorizontal();
                EnemyBoss.AllowPlay = enemyAllowPlay;
                EnemyBoss.AllowDraw = enemyAllowDraw;
            }

            if (!string.IsNullOrEmpty(message))
            {
                GUILayout.Label(message);
            }

            if (GUILayout.Button("清空存档"))
            {
                PlayerPrefs.DeleteAll();
                if (GameManager.Instance != null) GameManager.Instance.Load();
                message = "存档已清空";
            }

            if (GUILayout.Button("测试成就弹窗"))
            {
                AchievementToast.EnsureInstance();
                EventCenter.Publish(AchievementManager.AchievementUnlockedEvent, new AchievementManager.AchievementUnlockedInfo
                {
                    id = "gm_test",
                    name = "测试成就",
                    description = "这是GM触发的成就弹窗"
                });
                message = "已触发成就弹窗测试";
            }
        }
        catch (System.Exception ex)
        {
            message = $"GM窗口异常: {ex.Message}";
        }
        finally
        {
            GUI.DragWindow(DragArea);
        }
    }
#endif
}

