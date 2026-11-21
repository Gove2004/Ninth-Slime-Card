using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Slime.Game
{
    /// <summary>
    /// 设置屏：音量、外观、账号信息、存档管理。
    /// 清单式排版：**没有任何面板底**——节标题（暗色小字）→ 内容行 → 一条 2px 细分隔线，
    /// 全部直接落在背景上；行内控件（滑块 / 按钮）自带最小视觉壳。
    /// </summary>
    public sealed class SettingsScreen : Screen
    {
        protected override void Build()
        {
            // 均匀暗幕只压背景不动 UI：设置屏是纯文字清单，背景城堡剪影的亮度会吃掉暗色小字
            var veil = Ui.Solid("Veil", Root, new Color(0f, 0f, 0f, 0.42f));
            veil.raycastTarget = false;

            var title = Ui.Label("Title", Root, "设 置", Theme.H1, Theme.Text, TextAlignmentOptions.Left);
            title.characterSpacing = 4f;
            Ui.Fixed(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(56f, -56f), new Vector2(600f, 74f));

            var underline = Ui.Solid("Underline", Root, Theme.WithAlpha(Theme.Accent, 0.85f));
            Ui.Fixed(underline.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(58f, -146f), new Vector2(132f, 5f));

            var back = Ui.LinkButton("Back", Root, "返 回", Theme.Body, () => App.Pop());
            Ui.Fixed(back.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-72f, -62f), new Vector2(180f, 56f));

            var column = Ui.Node("Column", Root);
            Ui.Stretch(column, 64f, 190f, 64f, 64f);
            Ui.Column(column, 22, new RectOffset(0, 0, 0, 0));

            BuildAudioSection(column);
            Divider(column);
            BuildFestivalSection(column);
            Divider(column);
            BuildAccountSection(column);
            Divider(column);
            BuildDataSection(column);
        }

        /// <summary>节与节之间的 2px 细分隔线。</summary>
        private static void Divider(RectTransform column)
        {
            var line = Ui.Solid("Divider", column, Theme.WithAlpha(Theme.Border, 0.9f));
            Ui.FixedSize(line.gameObject, 0f, 2f);
        }

        /// <summary>节标题：暗色小字 + 加宽字距，只有文字没有框；压在剪影上加描边保可读。</summary>
        private static void SectionHead(RectTransform parent, string title)
        {
            var head = Ui.Node("Head", parent);
            Ui.FixedSize(head.gameObject, 0f, 34f);

            var label = Ui.Label("Title", head, title, Theme.H4, Theme.TextDim, TextAlignmentOptions.Left);
            label.characterSpacing = 5f;
            Ui.Outline(label.gameObject, new Color(0f, 0f, 0f, 0.6f), 2.5f);
            Ui.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
        }

        private void BuildAudioSection(RectTransform parent)
        {
            SectionHead(parent, "音 频");

            BuildSlider(parent, "音乐音量", App.Audio.MusicVolume, v =>
            {
                App.Audio.SetMusicVolume(v);
                App.Audio.PlaySfx("ui_click");
                App.PersistMeta();
            });

            BuildSlider(parent, "音效音量", App.Audio.SfxVolume, v =>
            {
                App.Audio.SetSfxVolume(v);
                App.Audio.PlaySfx("ui_click");
                App.PersistMeta();
            });
        }

        /// <summary>一行音量滑块：细轨 + 圆形滑钮，标签左、数值右、轨道居中。</summary>
        private void BuildSlider(RectTransform parent, string caption, float value, Action<float> onChanged)
        {
            var row = Ui.Node("Row", parent);
            Ui.FixedSize(row.gameObject, 0f, 72f);

            var label = Ui.Label("Caption", row, caption, Theme.H4, Theme.Text, TextAlignmentOptions.Left);
            Ui.Fixed(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(260f, 34f));

            var valueLabel = Ui.Label("Value", row, Mathf.RoundToInt(value * 100f) + "%",
                Theme.H4, Theme.Accent, TextAlignmentOptions.Right);
            Ui.Fixed(valueLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(120f, 34f));

            // 轨道：高 10 的暗色细线（左 300 右 160 之间）
            var track = Ui.Rounded("Track", row, 5, Theme.PanelSunken, Theme.BorderSoft, 1, true);
            var trackRt = track.rectTransform;
            trackRt.anchorMin = new Vector2(0f, 0.5f);
            trackRt.anchorMax = new Vector2(1f, 0.5f);
            trackRt.pivot = new Vector2(0.5f, 0.5f);
            trackRt.offsetMin = new Vector2(300f, -5f);
            trackRt.offsetMax = new Vector2(-160f, 5f);

            var fillArea = Ui.Node("FillArea", track.transform);
            Ui.Stretch(fillArea, 0f, 0f, 0f, 0f);

            var fill = Ui.Rounded("Fill", fillArea, 5, Theme.Accent, Theme.Accent, 0);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = Ui.Node("HandleArea", track.transform);
            Ui.Stretch(handleArea, 0f, -16f, 0f, -16f);

            var handle = Ui.Solid("Handle", handleArea, Theme.Text);
            var handleRt = handle.rectTransform;
            handleRt.anchorMin = new Vector2(0f, 0.5f);
            handleRt.anchorMax = new Vector2(0f, 0.5f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(26f, 26f);

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

        /// <summary>新春装饰偏好：跟随日期 / 常开 / 关闭，点按钮循环切换。</summary>
        private void BuildFestivalSection(RectTransform parent)
        {
            SectionHead(parent, "外 观");

            var row = Ui.Node("Row", parent);
            Ui.FixedSize(row.gameObject, 0f, 56f);

            var info = Ui.Label("Info", row,
                "灯笼春联会在 1 月 20 日至 2 月 28 日自动出现，纯装饰。",
                Theme.Body, Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Outline(info.gameObject, new Color(0f, 0f, 0f, 0.6f), 2.5f);
            Ui.Fixed(info.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(1100f, 34f));

            var meta = App.Meta;
            Button btn = null;
            btn = Ui.TextButton("FestBtn", row, "装饰：" + Festival.PrefName(meta.FestivalPref),
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
                Vector2.zero, new Vector2(400f, 56f));
        }

        private void BuildAccountSection(RectTransform parent)
        {
            SectionHead(parent, "账 号");

            bool logged = App.Tap.IsLoggedIn;

            var row = Ui.Node("Row", parent);
            Ui.FixedSize(row.gameObject, 0f, 44f);

            var state = Ui.Label("State", row,
                logged ? "TapTap 已连接" : "未登录（调试）",
                Theme.H4, logged ? Theme.Accent : Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Fixed(state.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(900f, 38f));

            var nick = Ui.Label("Nick", row,
                string.IsNullOrEmpty(App.Tap.Nickname) ? "未登录" : App.Tap.Nickname,
                Theme.H3, Theme.Text, TextAlignmentOptions.Right);
            Ui.Fixed(nick.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(600f, 40f));

            // 只留玩家自己会关心的一句话。UnionId 与应用数据目录属于排查现场才需要的东西。
            string line = logged
                ? "成就已解锁 " + App.Meta.Unlocked.Count + " / " + App.Achievements.All.Count + "，记录会同步到 TapTap"
                : "成就已解锁 " + App.Meta.Unlocked.Count + " / " + App.Achievements.All.Count + "，未登录时只记在本机";

            var info = Ui.Label("Info", parent, line, Theme.Body, Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Outline(info.gameObject, new Color(0f, 0f, 0f, 0.6f), 2.5f);
            Ui.FixedSize(info.gameObject, 0f, 30f);
        }

        private void BuildDataSection(RectTransform parent)
        {
            SectionHead(parent, "存 档");

            var row = Ui.Node("Row", parent);
            Ui.FixedSize(row.gameObject, 0f, 56f);

            var note = Ui.Label("Text", row,
                "进度只写在本机，换设备或卸载后不会自动恢复。",
                Theme.Body, Theme.TextDim, TextAlignmentOptions.Left);
            Ui.Outline(note.gameObject, new Color(0f, 0f, 0f, 0.6f), 2.5f);
            Ui.Fixed(note.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                Vector2.zero, new Vector2(1200f, 34f));

            var reset = Ui.TextButton("Reset", row, "清除本地存档", Theme.Body,
                Theme.WithAlpha(Theme.Danger, 0.85f), Theme.Text, ConfirmReset);
            Ui.Fixed(reset.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                Vector2.zero, new Vector2(300f, 56f));
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
    }
}
