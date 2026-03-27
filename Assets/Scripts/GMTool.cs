using UnityEngine;




public class GMTool : MonoBehaviour
{
    private const int WindowId = 9527;
    private static readonly Rect DragArea = new Rect(0, 0, 10000, 24);
    private const string HealthDeltaFieldName = "GMTool_HealthDelta";
    private const string ManaDeltaFieldName = "GMTool_ManaDelta";
    private const string CardIdFieldName = "GMTool_CardId";
    private const string CardNameFieldName = "GMTool_CardName";
    private string healthDelta = "0";
    private string manaDelta = "0";
    private string cardId = "1000";
    private string cardName = "重奏";
    private string message = "";
    private bool enemyAllowPlay = true;
    private bool enemyAllowDraw = true;
    private bool enemyToggleInitialized = false;
    private bool showPanel = true;
    private bool wantsImeInput;
    private IMECompositionMode appliedImeCompositionMode = IMECompositionMode.Auto;
    private Vector2 imeCursorScreenPosition;
    private Rect windowRect = new Rect(10, 10, 340, 620);
    private const int FullHeight = 620;
    private const int CollapsedHeight = 70;
    public static bool IsTextInputActive { get; private set; }

    public void ResetEnemyAIFlags()
    {
        enemyAllowPlay = true;
        enemyAllowDraw = true;
        enemyToggleInitialized = false;
        EnemyBoss.AllowPlay = true;
        EnemyBoss.AllowDraw = true;
    }

    private void OnDisable()
    {
        ReleaseTextInput();
    }

    private void OnDestroy()
    {
        ReleaseTextInput();
    }
#if UNITY_EDITOR
    private void Update()
    {
        ApplyImeCompositionMode();
        if (IsTextInputActive)
        {
            return;
        }

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
            if (Event.current.type == EventType.Repaint)
            {
                wantsImeInput = IsImeFieldControl(GUI.GetNameOfFocusedControl());
                IsTextInputActive = wantsImeInput;
            }
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
            var achievementManager = AchievementManager.Instance;

            if (GUILayout.Button(showPanel ? "隐藏" : "显示"))
            {
                showPanel = !showPanel;
                if (!showPanel)
                {
                    GUI.FocusControl(string.Empty);
                    wantsImeInput = false;
                    IsTextInputActive = false;
                }
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

            if (achievementManager != null)
            {
                GUILayout.Label("玩家累计统计");
                GUILayout.BeginVertical("box");
                GUILayout.Label($"累计造成伤害：{FormatStatValue(achievementManager.TotalScore)}");
                GUILayout.Label($"累计恢复血量：{FormatStatValue(achievementManager.TotalHeal)}");
                GUILayout.Label($"累计抽牌：{FormatStatValue(achievementManager.TotalDraw)}");
                GUILayout.Label($"累计出牌：{FormatStatValue(achievementManager.TotalPlay)}");
                GUILayout.Label($"累计获得魔力：{FormatStatValue(achievementManager.TotalMana)}");
                GUILayout.EndVertical();
            }

            GUILayout.Label("玩家生命值调整(可负数)");
            healthDelta = DrawImeTextField(HealthDeltaFieldName, healthDelta);
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
            manaDelta = DrawImeTextField(ManaDeltaFieldName, manaDelta);
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
            cardId = DrawImeTextField(CardIdFieldName, cardId);
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
            cardName = DrawImeTextField(CardNameFieldName, cardName);
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
                EventCenter.Publish(GameEvents.AchievementUnlocked, new AchievementManager.AchievementUnlockedInfo
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

    private string DrawImeTextField(string controlName, string value)
    {
        GUI.SetNextControlName(controlName);
        string result = GUILayout.TextField(value);
        if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() == controlName)
        {
            Rect rect = GUILayoutUtility.GetLastRect();
            imeCursorScreenPosition = GUIUtility.GUIToScreenPoint(new Vector2(rect.xMin + 6f, rect.yMax - 6f));
            wantsImeInput = true;
            IsTextInputActive = true;
        }
        return result;
    }

#endif

    private void ApplyImeCompositionMode()
    {
        IMECompositionMode targetMode = wantsImeInput ? IMECompositionMode.On : IMECompositionMode.Auto;
        if (appliedImeCompositionMode != targetMode)
        {
            Input.imeCompositionMode = targetMode;
            appliedImeCompositionMode = targetMode;
        }

        if (wantsImeInput)
        {
            Input.compositionCursorPos = imeCursorScreenPosition;
        }
    }

    private void ReleaseTextInput()
    {
        wantsImeInput = false;
        IsTextInputActive = false;
        appliedImeCompositionMode = IMECompositionMode.Auto;
        Input.imeCompositionMode = IMECompositionMode.Auto;
    }

    private static bool IsImeFieldControl(string controlName)
    {
        return controlName == HealthDeltaFieldName
               || controlName == ManaDeltaFieldName
               || controlName == CardIdFieldName
               || controlName == CardNameFieldName;
    }

    private static string FormatStatValue(ulong value)
    {
        return value.ToString("N0");
    }
}

