---
title: 发版流水线（改需求 → 打包 → TapTap 上架）
description: 从用户提需求到 TapTap 商店上线的完整闭环流程。含代码改动与美术/音频/字体素材制作的并行实施与验证、Android 打包、包体上传、槽位绑定、提审与驳回重试。
globs: ["**/Assets/**/*.cs", "**/ProjectSettings/**", "**/Assets/StreamingAssets/**", "**/Assets/Plugins/Android/**", "**/Assets/Resources/**", "**/*.csv", "**/Skills/**/*.md"]
standards-version: 1.1.0
---

# 发版流水线

本项目所有要上 TapTap 的改动都走这一条链路。六个阶段严格按序，每个阶段有明确的**出口条件**——不满足就不要往下走，往回退比事后补救便宜得多。

```
① 接需求  →  ② 分析  →  ③ 实施 + 验证  →  ④ 汇报迭代  →  ⑤ 打包  →  ⑥ 上传提审
                              ↑_______|
```

**③ 是双轨的**：需求可能落在代码上，也可能落在美术、音频、字体、配置表上，两者经常同时出现（加一张新卡 = 改 `cards.csv` + 出新卡面 + 补卡牌逻辑）。两条轨的实施方式和验证手段完全不同，但必须在同一个 ③ 里都走完才进 ⑤。

历史上最容易出事的是三个地方：**③ 验证不充分就打包**（打完发现文案不对，白等一轮审核）、**③ 只验了代码没验素材**（源码改了但资源没进包，或进包了但名字对不上）、**⑤ 用错方式启动 Unity**（构建卡死，看起来在跑其实 clang 全挂着）。

---

## ① 接需求：先把口径对齐

用户的原话常常只覆盖一部分。开工前必须把三件事问清楚，问不出来就先按最保守的假设做，并在汇报时明说假设。

| 要确认的 | 为什么 |
|---|---|
| **改哪里** | 只说「改隐私政策」时，可能指包内弹窗、商店后台字段、或两者都要。这两条路的成本差一个完整审核周期 |
| **改的是什么资源** | 代码 / 美术 / 音频 / 字体 / 配置表。决定 ③ 走哪条验证轨，也决定后面要不要重跑 `AudioCheck`、`fontcheck`、`cardlint` |
| **要不要立刻上线** | 「改了」和「上线了」是两件事。很多需求可以只提交代码、不打包 |
| **动的是不是审核敏感区** | 隐私文案、权限、包名、SDK 这类改动会触发人工复核，必须提前告知风险 |

**反面教材**：用户说「打包」，就只打包，不要顺手讲平衡性分析——那不是他要的。

**素材需求要多问一句**：用户说「加一张卡」时，通常包含三件事——`cards.csv` 加一行、出一张卡面、卡牌逻辑要能跑。只做其中一件等于没做完。反过来，用户说「换个音乐」时，多半只需要替换音频文件 + 同步 `AudioCheck` 的键名列表，不动代码。

---

## ② 分析：先找现状，再定方案

不要凭印象改。任何改动前先做这三步定位：

```bash
# 1. 这段逻辑/文案/资源到底在哪儿
grep -rn "<关键词>" --include="*.cs" --include="*.html" --include="*.json" Assets/
find Assets/Resources -iname "*<资源名>*"

# 2. 线上现在是什么状态（先看真实值，别猜）
taptap-cli app get-app-version --dev-id $DEV --app-id $APP --data '{"version":"<VERSION>"}'

# 3. 代码里的实现和文档/声明/资源是否一致
grep -rn "<权限名>|<SDK名>" Assets/Plugins/Android ProjectSettings
python .workbuddy/tools/fontcheck.py     # 文案字符是否都在字体里
python .workbuddy/tools/cardlint.py      # CSV 描述与效果脚本是否说同一件事
```

第 3 步经常能挖出真问题。例：`AndroidManifest.xml` 里挂着 `READ_PHONE_STATE`，代码里一次都没用——这种「声明了但没用到」的敏感权限是隐私合规检测的必命中项，比漏声明还容易被驳回。

**方案要有优先级**：能只改代码就不动版本号；能只改包内就不动商店资料；能只换资源文件就不动代码；两者都必须改时，明确告诉用户「这次要重走一轮审核」。

---

## ③ 实施 + 验证：双轨并行，验证不到位等于没改

需求可能落在两类东西上，实施与验证方式完全不同：

| 轨 | 内容 | 验证手段 | 详见 |
|---|---|---|---|
| **代码轨** | C# 逻辑、UI、配置读取、平台配置 | 编译 + 自检 + 解包比对 | 本节 |
| **素材轨** | 卡面 / 立绘 / BGM / 音效 / 字体 / CSV 配置表 | 专项脚本 + 截图目视 | `references/asset-production.md` |

两条轨经常交织：加一张新卡 = CSV 加一行（素材） + 卡面出图（素材） + 效果脚本要能跑（代码）。**只要需求里出现了「美术」「音乐」「音效」「字体」「卡面」「立绘」「图标」这类词，就必须同时走素材轨。**

### 代码轨

#### 改动约定

- **一处改动，多处同步**。隐私文案这类东西通常同时存在于包内 `StreamingAssets` 和商店后台，改一处必须显式检查另一处。存放位置要按「谁采集的」来决定——我方读的（GAID）进第一节，SDK / 引擎读的（Android ID、传感器）单开一节。
- **版本号只能走内存写入**。直接编辑 `ProjectSettings.asset` 的文本对已运行的编辑器无效，构建出来的还是旧版本号：

```csharp
var so = new UnityEditor.SerializedObject(
    UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
so.FindProperty("bundleVersion").stringValue = "6.11.1";
so.FindProperty("AndroidBundleVersionCode").intValue = 13;
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.AssetDatabase.SaveAssets();
```

- **签名口令只在构建进程内设**，不写进 `ProjectSettings`（那是要提交的文件）：

```csharp
PlayerSettings.Android.useCustomKeystore = true;
PlayerSettings.Android.keystorePass = "android";      // Android 官方 debug keystore
PlayerSettings.Android.keyaliasPass = "android";
```

### 素材轨

素材没有编译器和测试兜底，验证要更机械。**每个专项脚本都是防静默失败的最后一道闸**：

```bash
python .workbuddy/tools/fontcheck.py    # 文案用字是否都在字体里（目标：缺字 0）
python .workbuddy/tools/cardlint.py     # CSV 描述 vs 效果脚本是否一致
# DevPanel.AudioCheck()  → 11 个音频是否都能 Resources.Load（目标：缺失 0）
# DevPanel.SelfTest()    → 卡牌数 / 通关 / 战斗 / 音频综合回归
python .workbuddy/tools/shots.py home battle reward   # 改动过的界面截图
```

三条硬约束（踩过坑，详见 `references/asset-production.md`）：

1. **资源命名是硬约束**。卡面必须 `Card_<卡名>.png`（`ArtLib.Card()` 拼的就是这个前缀），立绘必须按 `EnemyDef.Sprite` 命名而非中文名。名字错了 `Resources.Load` **只返回 null 不报错**，表现是「图不见了」而不是报错。
2. **`.meta` 要自己写**。Unity 首次导入生成的 `.meta` 是 `spriteMode: Single`，与项目既有资源（`spriteMode: 2`）不一致，要用 `metas.py` 按模板生成。
3. **旧资源不删，移到 `.workbuddy/backup/<日期>-<描述>/`**。禁 `git rm` / `git mv`。

**素材改动的出口条件**（任一项不过就不要往下走）：

- [ ] 文件在正确目录、命名符合硬约束
- [ ] `.meta` 已生成，`spriteMode` 与既有资源一致
- [ ] 对应自检脚本全绿（`AudioCheck` / `fontcheck` / `cardlint`）
- [ ] 改动过的界面截图目视通过
- [ ] 旧资源已备份，不是删除

### 三层验证（两条轨都要过）

| 层 | 怎么验 | 查什么 |
|---|---|---|
| **语法/结构** | 正则数标签、比对数量、跑 lint 脚本 | `<h2>` 数量、`<ul>`/`</ul>` 配平、GBK 乱码、缺字、CSV 与脚本分叉 |
| **源码语义** | `grep -c` 逐项点检 | 每个新增关键词出现次数、旧表述是否清理干净 |
| **产物本体** | 直接解包 APK 查 | 见下 |

产物验证是这个流程里最有价值的一步——源码改对了不等于打进包里了，素材同理：

```python
import zipfile, re, glob, os
z = zipfile.ZipFile("Builds/A/<包名>.apk")
names = set(z.namelist())

# 1. 文案是否真的进包（StreamingAssets 保持原名）
h = z.read('assets/privacy-policy-template.html').decode('utf-8-sig', errors='replace')
for kw in ['GAID', 'Android ID', '传感器']:
    print(kw, h.count(kw))

# 2. 素材是否真的进包 —— 用 .meta 的 guid 反查
#    Resources 下的贴图会被打成 assets/bin/Data/<guid>，文件名不保留，
#    所以不能按 "Card_" 搜（搜不到）。guid 是唯一可靠线索。
hit = total = 0
for m in glob.glob('Assets/Resources/Art/Cards/*.png.meta'):
    g = re.search(r'^guid: (\w+)', open(m, encoding='utf-8', errors='replace').read(), re.M).group(1)
    total += 1
    if 'assets/bin/Data/' + g in names: hit += 1
print(f'卡面命中 {hit}/{total}')      # 应等于 144

# 3. 实际权限（AndroidManifest 是二进制 axml，用 utf-16-le 粗解）
s = z.read('AndroidManifest.xml').decode('utf-16-le', errors='ignore')
print(sorted(set(re.findall(r'android\.permission\.[A-Z_]+', s))))
```

**第 2 条容易白忙一场**：`z.namelist()` 里搜不到任何 `Card_` 前缀的名字，因为 Resources 资源进包后被重命名为 GUID。按文件名搜会得出「素材没进包」的错误结论，实际是搜法不对。当前包 144/144 全部命中。

版本号则用 aapt2 读，比解二进制清单靠谱：

```bash
"$UNITY/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/<ver>/aapt2.exe" \
  dump badging "Builds/A/<包名>.apk" | head -3
```

---

## ④ 汇报 + 迭代

汇报不是「我改完了」，要给出**可核对的证据**和**还没做的事**。

必须包含：
1. 改了什么（文件路径 + 具体变化的语义，不是 diff 转述）
2. 怎么验证的（贴出验证输出，别只说「已验证」）
3. 顺带发现的问题（以及改没改、为什么）
4. 遗留项 / 需要用户决策的分支

**素材类改动的汇报要额外给「看得到的东西」**：截图路径、资源数量、包体大小变化。用户没法从 diff 里看出立绘好不好看，必须让他直接看图。代码改动可以说「逻辑验证通过」，素材改动不行——只有目视才算数。

**迭代时的纪律**：用户说「继续」就继续当前方向，不要重复问已经答过的问题；用户纠正了口径就立刻切，不要辩解。中途发现原方案错了（比如日志看错了文件），主动承认并给出纠正后的事实。

素材迭代天然是多轮：卡面风格被推翻三次才定下来是常态，不是失败。每次重做前先明确「这次换的是参考图、提示词、还是画风」，避免原地打转。

---

## ⑤ 打包

### 启动方式决定成败

**必须以前台受控方式启动**，绝不能用 `nohup ... &` 或 `setsid`：

```bash
cd "<项目根>" && rm -f .workbuddy/build_<ver>.log && \
  ("<UNITY_EXE>" -batchmode -quit -projectPath "<项目根>" \
     -executeMethod Slime.DevTools.CliBuild.Android \
     -logFile "<项目根>/.workbuddy/build_<ver>.log" > /dev/null 2>&1; \
   echo "EXIT=$?" >> .workbuddy/build_<ver>.status)
```

放进 `run_in_background: true` 的 Bash 调用里执行。这样跑约 3 分钟完成。

脱离受控派生链的 Unity，其 bee 派发的 clang 子进程会被 Job Object 限制——**进程在，但线程 Suspended、CPU 恒为 0、内存卡在 27MB**，构建永远停在最后几十个目标上。同样的 clang 命令行手动跑 0.2 秒就出结果，问题在派发链而不在编译器。

### 看哪个日志

| 场景 | 日志位置 |
|---|---|
| 编辑器**已有实例**在跑，再 `-logFile` 启动 | `-logFile` 指定的文件**不会写入**，要看 `%LOCALAPPDATA%\Unity\Editor\Editor.log` |
| 全新 batchmode 实例 | `-logFile` 指定的文件 |

**看错日志会导致完全相反的结论**。曾经连续多轮判定「构建失败」，实际旧实例日志里全是历史 Failure，新实例日志写着 `Build Finished, Result: Success`。判断前先确认日志的修改时间是不是本次的。

### 出口条件

构建成功必须同时满足：

- [ ] `.status` 里有 `EXIT=0`
- [ ] 日志有 `[CliBuild] result=Succeeded`
- [ ] APK 时间戳是本次构建的
- [ ] aapt2 读出的 versionCode / versionName 是目标值
- [ ] 三项产物验证通过（文案 / 权限 / 素材）
- [ ] 素材类改动：包内能查到新增资源，或装包后截图确认

任何一项不过就停下来修，不要带着疑问上传。

---

## ⑥ TapTap 上传

固定五步，每步都要读写校验。详细命令见 `references/taptap-cli.md`。

```
upload-apk  →  list-packages 取 package_id  →  select-package 绑 main 槽
            →  precheck-app-review  →  submit-app-review
```

### 铁律

1. **先 `--dry-run` 看请求体**，确认 URL、参数、body 都对，再带 `--yes` 真跑。
2. **写操作一律带 `--idempotency-key`**，格式 `<操作>-<版本>-<日期>`，重试不会产生重复副作用。
3. **每个写操作后立刻读回**。`select-package` 返回 ok 不等于绑定生效，要用 `list-packages` 确认 `current_bindings`。
4. **核对 MD5**。本地文件的 md5 必须和后台包体一致，否则上传的是旧包。
5. **`save-changes` 必须带 `expected`**（当前严格值），不要拿 `force=true` 当 422 的自动兜底。
6. **改商店资料前必须有草稿版本**。线上版本状态不允许直接改，需先 `create-draft`。

### 审核驳回怎么办

```bash
taptap-cli app get-app-version --dev-id $DEV --app-id $APP --data '{"version":"<VERSION>"}'
```

看 `logs` 里的 `review_rejected` 及 `reason`，`review_approved` + `published` 则是成功。

被驳回通常是包体问题，处理循环是：**补代码 → 升版本号 → 重新打包 → 上传 → 绑槽 → 重提审**。版本号必须升，否则同 versionCode 可能被拒。

### 常见驳回原因

| 原因 | 根因 | 修法 |
|---|---|---|
| 安装包隐私合规检测未通过 | 声明与实采不符 / 有敏感权限未声明 | 补文案 + 删无用敏感权限，见 `references/privacy-compliance.md` |
| 资质不全 | 版号、软著、ICP 等 `pending_upload` | 需要用户提供材料，无法自动化 |
| 资料字段缺必填 | `config_status = warning` | CLI 看不到具体字段，需后台确认 |

---

## 一页速查

```bash
# 状态
taptap-cli app get-app-version   --data '{"version":"<V>"}'        # 版本 + 审核日志
taptap-cli app list-packages      --data '{"package_types":[]}'     # 槽位与绑定
taptap-cli app analyze-app-status                                  # 资料完整度 / 资质缺口
taptap-cli app precheck-app-review --data '{"release_schedule":{"kind":"immediate"}}'

# 写
taptap-cli upload-apk "<apk>" --idempotency-key upload-<v>-<d> --yes
taptap-cli app select-package  --data @sel.json --idempotency-key bind-<v>-<d> --yes
taptap-cli app submit-app-review --data '{"release_schedule":{"kind":"immediate"}}' \
  --idempotency-key submit-<v>-<d> --yes

# 排障
taptap-cli schema <服务> <命令>      # 查参数契约
taptap-cli skills read <skill-name>  # 查官方操作文档
```

## 参考

- `references/asset-production.md` — 美术 / 音频 / 字体 / 配置表的制作管线与验证
- `references/taptap-cli.md` — CLI 全命令用法、参数契约、踩坑
- `references/build-android.md` — 打包排障、日志定位、DAG 手动补编译
- `references/privacy-compliance.md` — 隐私合规自查清单

## 素材改动速查

```bash
# 生成（有缓存，重复跑不烧额度）
python .workbuddy/artgen/gen.py cards --only 斩击,重斩
python .workbuddy/artgen/gen.py monsters --only bat            # 立绘
python .workbuddy/artgen/metas.py all                              # 批量写 .meta
python .workbuddy/artgen/sheet.py                              # 拼联系表人工过图

# 校验（改完必跑，防静默失败）
python .workbuddy/tools/fontcheck.py        # 缺字，目标 0
python .workbuddy/tools/cardlint.py         # CSV 描述 vs 脚本
python .workbuddy/tools/shots.py home battle reward   # 界面截图

# 配置表重生成（不要手改 CSV）
python .workbuddy/tools/gen_cards_v2.py
python .workbuddy/tools/gen_achievements.py
```
