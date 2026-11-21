using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Slime.Game
{
    /// <summary>设置屏：音量、账号信息、存档管理。</summary>
    public sealed class SettingsScreen : Screen
    {
        protected override void Build()
        {
            BuildHeader("设置", null, "返回");
            var body = BuildBody(142f);

            var column = Ui.Node("Column", body);
            // 纵向居中：上下留白等分，避免内容全挤在上半屏。
            // body 高 890（1080 - 头部 142 - 上下边距），四节内容收紧到 868 以内。
            Ui.Fixed(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1180f, 890f));
            Ui.Column(column, 16, new RectOffset(0, 0, 0, 0));

            BuildAudioSection(column);
            Spacer(column);
            BuildFestivalSection(column);
            Spacer(column);
            BuildAccountSection(column);
            Spacer(column);
            BuildDataSection(column);
        }

        /// <summary>节与节之间的额外空隙：Ui.Column 的统一 spacing 分不出「组内」和「组间」。</summary>
        private static void Spacer(RectTransform column)
        {
            var node = Ui.Node("Gap", column);
            var le = node.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 12f;
        }

        /// <summary>新春装饰偏好：跟随日期 / 常开 / 关闭，点按钮循环切换。</summary>
        private void BuildFestivalSection(RectTransform parent)
        {
            BuildSectionHeader(parent, "外观", "新春装饰");

            var panel = Ui.Rounded("Festival", parent, Theme.RadiusL, Theme.WithAlpha(Theme.Panel, 0.96f), Theme.Border, 1);
            Ui.FixedSize(panel.gameObject, 1180f, 136f);

            var info = Ui.Label("Info", panel.transform,
                "灯笼春联会在 1 月 20 日至 2 月 28 日自动出现。纯装饰，不影响玩法。",
                Theme.Body, Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(info.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(26f, 0f), new Vector2(700f, 40f));

            BuildFestivalButton(panel.transform);
        }

        private void BuildFestivalButton(Transform panel)
        {
            var meta = App.Meta;
            Button btn = null;
            btn = Ui.TextButton("FestBtn", panel, "装饰：" + Festival.PrefName(meta.FestivalPref),
                Theme.Body, Theme.PanelRaised, Theme.Text, () =>
                {
                    meta.FestivalPref = (meta.FestivalPref + 1) % 3;
                    App.Audio.PlaySfx("ui_click");
                    App.PersistMeta();
                    // 只改文字不重建节点：Destroy 当帧不生效，同帧重建会新旧按钮叠在一起
                    var label = btn.GetComponentInChildren<TMPro.TMP_Text>();
                    if (label != null)
                    {
                        label.text = "装饰：" + Festival.PrefName(meta.FestivalPref);
                    }
                });
            Ui.Fixed(btn.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-26f, 0f), new Vector2(340f, 62f));
        }

        private void BuildSectionHeader(RectTransform parent, string title, string caption)
        {
            var head = Ui.Node("SectionHead", parent);
            Ui.FixedSize(head.gameObject, 1180f, 36f);

            var mark = Ui.Solid("Mark", head, Theme.Accent);
            Ui.Fixed(mark.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(6f, 26f));

            var label = Ui.Label("Title", head, title, Theme.H4, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(200f, 34f));

            // 补充说明紧贴标题，不做整行右对齐，也不留大间距：
            // 三个分节标题都是两字，说明挪到 104 就正好跟在后面；
            // 再往右会落在背景那轮月亮上，灰字压亮圆，等于没写。
            var sub = Ui.Label("Cap", head, caption, Theme.Caption, Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(sub.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(104f, 0f), new Vector2(700f, 28f));
        }

        private void BuildAudioSection(RectTransform parent)
        {
            BuildSectionHeader(parent, "音频", "即时生效");

            var panel = Ui.Rounded("Audio", parent, Theme.RadiusL, Theme.WithAlpha(Theme.Panel, 0.96f), Theme.Border, 1);
            Ui.FixedSize(panel.gameObject, 1180f, 220f);

            BuildSlider(panel.transform, 0, "音乐音量", "标题与战斗背景音乐", App.Audio.MusicVolume, v =>
            {
                App.Audio.SetMusicVolume(v);
                App.Audio.PlaySfx("ui_click");
                App.PersistMeta();
            });

            var divider = Ui.Solid("Divider", panel.transform, Theme.BorderSoft);
            var dividerRt = divider.rectTransform;
            dividerRt.anchorMin = new Vector2(0f, 0.5f);
            dividerRt.anchorMax = new Vector2(1f, 0.5f);
            dividerRt.pivot = new Vector2(0.5f, 0.5f);
            dividerRt.offsetMin = new Vector2(26f, -0.5f);
            dividerRt.offsetMax = new Vector2(-26f, 0.5f);

            BuildSlider(panel.transform, 110, "音效音量", "出牌、爆发与界面反馈音", App.Audio.SfxVolume, v =>
            {
                App.Audio.SetSfxVolume(v);
                App.Audio.PlaySfx("ui_click");
                App.PersistMeta();
            });
        }

        private void BuildAccountSection(RectTransform parent)
        {
            BuildSectionHeader(parent, "账号", string.Empty);

            var panel = Ui.Rounded("Account", parent, Theme.RadiusL, Theme.WithAlpha(Theme.Panel, 0.96f), Theme.Border, 1);
            Ui.FixedSize(panel.gameObject, 1180f, 136f);

            bool logged = App.Tap.IsLoggedIn;

            var stateChip = Ui.Rounded("State", panel.transform, Theme.RadiusS,
                logged ? Theme.WithAlpha(Theme.AccentDeep, 0.45f) : Theme.WithAlpha(Theme.PanelSunken, 1f),
                logged ? Theme.WithAlpha(Theme.Accent, 0.75f) : Theme.BorderSoft, 1);
            Ui.Fixed(stateChip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -22f), new Vector2(210f, 40f));

            var stateText = Ui.Label("StateText", stateChip.transform,
                logged ? "TapTap 已连接" : "未登录（调试）", Theme.Body,
                logged ? Theme.Accent : Theme.TextDim, TextAlignmentOptions.Center);
            Ui.Stretch(stateText.rectTransform, 0f, 0f, 0f, 0f);

            var nick = Ui.Label("Nick", panel.transform,
                string.IsNullOrEmpty(App.Tap.Nickname) ? "未登录" : App.Tap.Nickname,
                Theme.H3, Theme.Text, TextAlignmentOptions.Right);
            Ui.Fixed(nick.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -22f), new Vector2(600f, 40f));

            // 只留玩家自己会关心的一句话。UnionId 与应用数据目录属于排查现场才需要的东西，
            // 摆在设置里除了让面板像一张日志表，没有任何用处。
            string line = logged
                ? "成就已解锁 " + App.Meta.Unlocked.Count + " / " + App.Achievements.All.Count + "，记录会同步到 TapTap"
                : "成就已解锁 " + App.Meta.Unlocked.Count + " / " + App.Achievements.All.Count + "，未登录时只记在本机";

            var info = Ui.Label("Info", panel.transform, line, Theme.Body, Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(info.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 30f), new Vector2(1120f, 34f));
        }

        private void BuildDataSection(RectTransform parent)
        {
            BuildSectionHeader(parent, "存档", "v" + Application.version);

            var row = Ui.Node("DataRow", parent);
            Ui.FixedSize(row.gameObject, 1180f, 84f);
            var layout = Ui.Row(row, 16, new RectOffset(0, 0, 0, 0));
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var note = Ui.Rounded("Note", row, Theme.RadiusL, Theme.WithAlpha(Theme.PanelSunken, 1f), Theme.BorderSoft, 1);
            Ui.FixedSize(note.gameObject, 830f, 92f);

            var noteText = Ui.Label("Text", note.transform,
                "进度只写在本机，换设备或卸载后不会自动恢复。",
                Theme.Body, Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(noteText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(770f, 40f));

            var reset = Ui.TextButton("Reset", row, "清除本地存档", Theme.Body, Theme.WithAlpha(Theme.Danger, 0.85f), Theme.Text, ConfirmReset);
            Ui.FixedSize(reset.gameObject, 334f, 68f);
        }

        private void ConfirmReset()
        {
            var dialog = Ui.Rounded("Dialog", Root, Theme.RadiusL, Theme.WithAlpha(Theme.PanelRaised, 0.99f), Theme.Border, 1);
            Ui.Fixed(dialog.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 300f));

            var text = Ui.Label("Text", dialog.transform, "确定清除全部本地存档？\n奖杯、图鉴与成就记录都会丢失。", Theme.Body, Theme.Text, TextAlignmentOptions.Center);
            Ui.Fixed(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(560f, 120f));

            var yes = Ui.TextButton("Yes", dialog.transform, "确认清除", Theme.Body, Theme.Danger, Theme.Text, () =>
            {
                SaveService.DeleteFile();
                var meta = App.Director.Meta;
                meta.Trophy = 0;
                meta.BestChapter = 1;
                meta.RunsPlayed = 0;
                meta.RunsCleared = 0;
                meta.Wins = 0;
                meta.Losses = 0;
                meta.DiscoveredCards.Clear();
                meta.Unlocked.Clear();
                meta.Counters.Clear();
                meta.Flags.Clear();
                meta.ClearRun();
                App.Director.StartNewRun();
                App.PersistMeta();
                UnityEngine.Object.Destroy(dialog.gameObject);
                App.Toast("存档已清除");
                App.Replace(new SettingsScreen());
            });
            Ui.Fixed(yes.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-130f, 28f), new Vector2(220f, 66f));

            var no = Ui.TextButton("No", dialog.transform, "取消", Theme.Body, Theme.Panel, Theme.TextDim,
                () => UnityEngine.Object.Destroy(dialog.gameObject));
            Ui.Fixed(no.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(130f, 28f), new Vector2(220f, 66f));
        }

        /// <summary>一行音量滑块。topOffset 为行在面板内的纵向偏移。</summary>
        private void BuildSlider(Transform parent, float topOffset, string caption, string hint, float value, Action<float> onChanged)
        {
            var rowRt = Ui.Node("Row", parent);
            Ui.Fixed(rowRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -topOffset), new Vector2(1120f, 110f));

            var label = Ui.Label("Caption", rowRt, caption, Theme.Body, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 18f), new Vector2(320f, 30f));

            var hintLabel = Ui.Label("Hint", rowRt, hint, Theme.Caption, Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(hintLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, -16f), new Vector2(460f, 26f));

            var valueLabel = Ui.Label("Value", rowRt, Mathf.RoundToInt(value * 100f) + "%", Theme.H4, Theme.Accent, TextAlignmentOptions.Right);
            Ui.Fixed(valueLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(120f, 34f));

            // 轨道：从 500 到 740（右侧留出数值位置）
            var track = Ui.Rounded("Track", rowRt, 6, Theme.PanelSunken, Theme.Border, 1, true);
            var trackRt = track.rectTransform;
            trackRt.anchorMin = new Vector2(0f, 0.5f);
            trackRt.anchorMax = new Vector2(1f, 0.5f);
            trackRt.pivot = new Vector2(0.5f, 0.5f);
            trackRt.offsetMin = new Vector2(500f, -13f);
            trackRt.offsetMax = new Vector2(-150f, 13f);

            var fillArea = Ui.Node("FillArea", track.transform);
            Ui.Stretch(fillArea, 0f, 0f, 0f, 0f);

            var fill = Ui.Rounded("Fill", fillArea, 6, Theme.Accent, Theme.Accent, 0);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = Ui.Node("HandleArea", track.transform);
            Ui.Stretch(handleArea, 0f, -13f, 0f, -13f);

            var handle = Ui.Rounded("Handle", handleArea, 13, Theme.Text, Theme.Lighten(Theme.Accent, 0.3f), 3);
            var handleRt = handle.rectTransform;
            handleRt.anchorMin = new Vector2(0f, 0.5f);
            handleRt.anchorMax = new Vector2(0f, 0.5f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(30f, 0f);

            var slider = track.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;

            slider.onValueChanged.AddListener(v =>
            {
                valueLabel.text = Mathf.RoundToInt(v * 100f) + "%";
                onChanged(v);
            });

            slider.value = value;
            valueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
        }
    }
}
