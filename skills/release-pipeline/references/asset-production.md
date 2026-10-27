# 素材制作参考

美术、音频、字体三类资源的生产与验证。需求不总是「改代码」——很多时候用户要的是一张新卡面、一段新 BGM、或者修一批显示成方框的字。这类改动同样要走完发版链路，而且**验证方式和代码完全不同**。

## 资源总览

| 类别 | 位置 | 数量 | 规格 |
|---|---|---|---|
| 卡面 | `Assets/Resources/Art/Cards/Card_<卡名>.png` | 144 | 384×512 调色板 PNG |
| 怪物立绘 | `Assets/Resources/Art/Monsters/<sprite>.png` | 30 | 512×512 调色板 PNG |
| 背景 / 主角 / 图标 | `Assets/Resources/Art/{Bg,Char,Icon}/` | 少量 | — |
| BGM | `Assets/Resources/Audios/Music/` | 4 | `title1 title2 battle1 battle2` |
| 音效 | `Assets/Resources/Audios/Sound/` | 7 | `ui draw play slash mana heal venom` |
| 字体 | `Assets/Resources/Fonts/` | 主图集 + fallback | Alibaba PuHuiTi 2.0 |
| 配置表 | `Assets/Resources/Configs/{cards,achievements}.csv` | 144 卡 / 48 成就 | 生成脚本产出 |

**程序化是兜底。** 整个 UI 由 `SpriteForge` 在运行时按令牌生成（圆角、渐变、描边、进度条、光晕），没有外部贴图依赖。真图缺位时自动回退，所以缺一张卡面不会崩，只会少一点辨识度。

---

## 一、美术

### 生成管线（`.workbuddy/artgen/`）

| 文件 | 职责 |
|---|---|
| `gen.py` | 批量出图。`gen.py cards` / `gen.py monsters`，支持 `--only` `--force` `--jobs` `--dry` |
| `artpipe.py` | PNG 解码/编码、调色板量化、洪泛抠图、缩放、裁切 |
| `metas.py` | 批量写 Unity `.meta`（按 `hero.png.meta` 模板） |
| `sheet.py` | 拼联系表，人工过图用 |
| `styleboard.py` | 生成风格板，用于锁定画风 |
| `manifest_cards.json` / `manifest_monsters.json` | 生成清单（卡面清单与 `cards.csv` 的 144 条逐一对齐） |

`gen.py` 走 `POST /images/generations` 直连，超时 240s + 退避重试 6 次。**不要走 `agnes_media.py image` 那个 CLI**：它 HTTP 超时写死 60s，排队一满必然 `read operation timed out`，而且 `--timeout` 参数没接进去，传了也无效。

缓存按 prompt 哈希存在 `cache*/`，重复跑不烧额度。改 prompt 才会重新出图。

### 三个必须遵守的经验

**① 图生图会模仿参考图的「内容」，不只是风格。**

这是最贵的一课，卡面风格被推翻三次：

| 尝试 | 参考图 | 结果 |
|---|---|---|
| 1 | 怪物立绘 `dark_knight` | 144 张全变成同一个黑甲骑士 |
| 2 | 8 只怪拼的样张 | 仍是骑士专美：火球卡里出现蝙蝠人，雷击卡里出现蘑菇+史莱姆 |
| 3 | 程序生成的抽象风格板 | 画风对了，但卡面偏写实厚涂，与立绘的粗墨线卡通割裂 |
| 4 | **`hero.png`（标准粗墨线史莱姆）** | ✅ 风格统一 |

想锁风格又不被内容污染，正确做法是拿「与目标同画风、但主体是通用吉祥物」的图当锚点。本项目就是 `hero.png` 那只史莱姆，顺带让所有卡面多出一个统一主角，和游戏名也贴。

**② 提示词不能自相矛盾。**

「禁止人物」+「a warrior swinging a sword」是打架的，模型只会随机取舍。必须把清单里的主语本身换成史莱姆执行：`a cute round blue slime hero swinging a short glowing sword`。144 张卡里有 39 张的 `subj` 因此重写。

**③ 立绘用洋红键控 + 洪泛抠图，不是色差判定。**

生成时要求纯 `#FF00FF` 平背景，本地抠图。三个坑：

1. **只按颜色距离判定不够**。主体投在洋红上的阴影是「被压暗的洋红」，与基准色距离很远，抠不掉。→ 改为**从画布四边向内洪泛**，判据是「与边框连通」而不是单纯色差。阴影天然连着背景，主体内部暗部不会误伤。
2. 洪泛后边缘留 1~2px 亮粉反光带 → 按「距透明区的圈数」逐层放宽阈值（18/34/52）做腐蚀，再邻域平均补回。
3. `trim_fit_square` 按 alpha 包围盒裁空白 → 居中放正方形 → 缩放，保证 30 只怪在 UI 里视觉大小一致。

### 命名是硬约束

- **卡面必须带 `Card_` 前缀**。`ArtLib.Card()` 读的是 `"Art/Cards/Card_" + cardName`，少个前缀就是 null。`gen.py` 的 `run()` 因此有 `prefix` 参数。
- **立绘按 `EnemyDef.Sprite` 命名**，不是按中文名。改怪名不用改图，改 `Sprite` 字段就要换图。

### `.meta` 要自己写

Unity 才会生成，但默认是 `spriteMode: Single` 且没有 `spriteSheet` 条目，与项目既有图（`spriteMode: 2` / Multiple）不一致。用 `metas.py` 按 `hero.png.meta` 模板批量生成：

```bash
python .workbuddy/artgen/metas.py cards
python .workbuddy/artgen/metas.py monsters
python .workbuddy/artgen/metas.py all
```

跑完 diff 确认结构一致。

### 旧资源处置

**不删，移到 `.workbuddy/backup/<日期>-<描述>/`。** 例：159 张旧卡面（其中 150 张已不在现卡池）整体移到 `.workbuddy/backup/2026-10-25-cards-old/`。

同时遵守 git 安全纪律：禁 `git rm` / `git mv` / `git checkout` / `git restore`，移动一律用 bash `mv`。

---

## 二、音频

### 来源

4 首 BGM 的 ID3 里带 `WOAS` 字段指向 `suno.com/song/<uuid>`——AI 生成的曲子。音效来源不统一（部分带 Adobe XMP，部分是 libsndfile 转码的 ogg）。

**加新音频前先确认版权出处**。AI 生成曲在商用授权上有边界，TapTap 审核不查这一项，但属于法务风险而非技术风险，需要用户自己判断。

### 路径坑（曾导致全游戏无声）

`AudioService` 的 `Root = "Audios/"`，而资源实际在 `Audios/Music/` 和 `Audios/Sound/` 两个子目录下。于是 `Resources.Load<AudioClip>("Audios/ui")` 必然返回 null，**静默 return**，全工程 23 处音频调用全部落空。

`Resources.Load` 找不到**只返回 null 不报错**——这是本项目的头号长期坑，所有 `Resources.Load` 都要配 `Debug.LogWarning`。

现行写法：

```csharp
private const string Root = "Audios/";
private const string MusicDir = "Music/";
private const string SoundDir = "Sound/";

private AudioClip Get(string dir, string name)
{
    // 先查子目录，再兜底查 Audios 根下（避免以后挪文件又静默失声）
    clip = Resources.Load<AudioClip>(Root + dir + name);
    if (clip == null) clip = Resources.Load<AudioClip>(Root + name);
    if (clip == null)
        Debug.LogWarning("[Audio] 找不到音频资源：" + key + "（已尝试 …）");
}
```

### 音量只在一个地方控制

```csharp
private void ApplyVolume() { music.volume = MusicVolume; sfx.volume = SfxVolume; }

public void PlaySfx(string name)
{
    // 音量已由 sfx.volume 统一控制，这里不再重复乘一次
    sfx.PlayOneShot(clip);
}
```

`PlayOneShot(clip, volume)` 会再乘一次，实际听到的是 `0.8² = 0.64`。只用 source 的 `volume`。

两个 `AudioSource` 都要显式 `spatialBlend = 0`（2D），否则会被当成 3D 源随距离衰减。

### 验证

`DevPanel.AudioCheck()`（并入 `SelfTest()`，`Jump("audio")` 单独跑）：

- 逐个键 `Resources.Load` 验证 11 个音频，报时长/采样率
- 列出运行时 listener 与音源状态
- **新增音频必须同步补进 `musicKeys` / `sfxKeys` 列表**，否则自检覆盖不到

判断「是否真的在播」看 `AudioSource.time` 是否推进，**不要只看 `isPlaying`**——Editor 的 Mute Audio 会误导。

---

## 三、字体

### 结构

| 资源 | 模式 | 作用 |
|---|---|---|
| `Alibaba_PuHuiTi_2.asset` | 静态子集 | 覆盖常用字，固定字形 |
| `Alibaba_PuHuiTi_2_Fallback.asset` | Dynamic（`m_AtlasPopulationMode: 1`） | 缺字时从源 TTF 现场烘焙 |

主资源挂了 `fallbackFontAssetTable` 指向 fallback。两者合起来才是可用字形集。

**动态图集里暂时没有，不等于渲染不出来**——只查 `.asset` 会把 `「」×√` 这类明明能显示的字报成缺字。`fontcheck.py` 专门处理了这个：同时读图集字符表和源 TTF 的字形表。

### PuHuiTi 已知缺字

```
没有：✓ ✔ ☑ ▶ ✗
有：  √ ● ○ ◆ ◇ ■ □ ★ ☆ • ※ ⊙ ◎ → ← ↑ ↓ 〓 【】 〈〉 《》 『』 × ÷ ± § † ‡ ≠ ≤ ≥ ∞
```

UI 里表示「已完成」用 `√`（U+221A），不要用 `✓`（U+2713）。

### 检查

```bash
python .workbuddy/tools/fontcheck.py    # 当前报缺字 0
```

它把工程里所有运行时文案（CSV 配置 + C# 字符串字面量）用到的字符，和字体图集的字符表比对，列出会被渲染成方框的字。**新增文案后必跑**，这是最容易漏的验证。

---

## 四、配置表

`cards.csv` / `achievements.csv` 由 `.workbuddy/tools/gen_cards_v2.py` 与 `gen_achievements.py` 生成，不要手改 CSV——下次重跑就被覆盖。

### 描述与脚本必须一致

`cards.csv` 里每一行都有「描述」和「效果脚本」两列。**脚本是唯一真相，描述只是给人看的字**。两者一旦分叉，玩家读到的就是假的。

```bash
python .workbuddy/tools/cardlint.py      # 只报问题
python .workbuddy/tools/cardlint.py -v   # 连通过项一起列
```

它按 `STATUS_WORDS` 表检查：描述里出现「中毒」，脚本里就必须有 `poison` op，等等。

---

## 五、素材类改动的验证清单

代码有编译器和测试兜底，素材没有。所以素材的验证要更机械、更依赖脚本。

| 类别 | 验证方式 | 目标 |
|---|---|---|
| 卡面 | `ArtLib.Card()` 能否取到 + 截图目视 | 144 张全部非 null，命名与 `cards.csv` 一一对应 |
| 立绘 | `DevPanel.SelfTest()` + 截图 | 30 只怪各有其图，不共用 |
| 音频 | `DevPanel.AudioCheck()` | 11/11 加载成功，缺失 0 |
| 字体 | `fontcheck.py` | 缺字 0 |
| 配置表 | `cardlint.py` + `coretest smoke` | 描述与脚本一致 |
| 整体 | `shots.py <界面名>...` | 每个改动过的界面截一张，人工看 |

### 解包核对（最容易搞错的一步）

想确认素材真的进了 APK，**不能按文件名搜**。Resources 下的资源进包后被重命名为 GUID，`z.namelist()` 里一个 `Card_` 都找不到。必须从 `.meta` 读出 guid 再反查：

```python
import zipfile, re, glob
z = zipfile.ZipFile("Builds/A/第九张史莱姆牌.apk")
names = set(z.namelist())

hit = total = 0
for m in glob.glob('Assets/Resources/Art/Cards/*.png.meta'):
    g = re.search(r'^guid: (\w+)', open(m, encoding='utf-8', errors='replace').read(), re.M).group(1)
    total += 1
    if 'assets/bin/Data/' + g in names:
        hit += 1
print(f'卡面命中 {hit}/{total}')
```

当前包 `144/144` 全部命中，说明 144 张卡面都在包里。同样的方法可用于立绘（`Art/Monsters/*.png.meta`）和音频（`Audios/**/*.mp3.meta`）。

**按文件名搜会得出「素材没进包」的错误结论**，实际只是搜法不对。这个坑踩过一次。

### 截图

```bash
python .workbuddy/tools/shots.py home battle reward
python .workbuddy/tools/shots.py --prefix r7 home battle   # 输出名加前缀
```

一次会话内连续跳转 + 截图多个界面，产物落 `Assets/Screenshots/`。`DevPanel.Jump(key)` 可直达任意界面：`map` / `treasure` / `rest` / `shop` / `event` / `battle-elite` / `battle-boss` / `codex-mech` / `audio` / `reward-preview`。

### 出口条件

- [ ] 资源文件在正确目录、命名符合硬约束（`Card_` 前缀 / `Sprite` 字段）
- [ ] `.meta` 已生成，`spriteMode` 与既有资源一致
- [ ] 对应的自检脚本全绿（`AudioCheck` / `fontcheck` / `cardlint`）
- [ ] 改动过的界面截图目视通过
- [ ] 旧资源已移到 `.workbuddy/backup/`，不是删除

---

## 六、素材改动对包体的影响

| 项 | 影响 |
|---|---|
| 卡面 144 张 × ~52KB | ≈ 7 MB |
| 立绘 30 张 × ~53KB | ≈ 1.6 MB |
| BGM 4 首 | ≈ 22 MB（最大的单项） |
| APK 总体 | ≈ 91.6 MB |

**BGM 占了近四分之一。** 如果要压包体，优先考虑 BGM 码率而不是卡面——卡面已经量化到调色板 PNG，再压视觉损失明显；BGM 从 320kbps 降到 128kbps 听感几乎无差，能省 15 MB 左右。

历史参考：重构前有 159 张全尺寸卡图（1086×1448，共 230 MB）且无图集，当前 144 张 384×512 只有约 7 MB——**尺寸和量化比压缩算法更决定体积**。
