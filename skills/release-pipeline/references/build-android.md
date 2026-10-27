# Android 打包排障

本项目 Android Release 包的构建链路与所有已知坑。

## 环境

| 项 | 值 |
|---|---|
| Unity | `6000.3.25f1`，路径 `D:\Unity Editor\6000.3.25f1\Editor\Unity.exe` |
| 渲染 | URP 2D |
| 脚本后端 | IL2CPP + Gradle |
| 签名 | Android 官方 debug keystore（`~/.android/debug.keystore`，口令 `android` / `androiddebugkey`） |
| 输出 | `Builds/A/第九张史莱姆牌.apk`（约 91.6 MB / 87.4 MiB，构建约 2.5 分钟） |
| MCP | `http://127.0.0.1:8090/mcp` |

程序集：`Slime.Core` / `Slime.Game` / `Slime.Editor`。

## 两条构建入口

### 1. MCP（编辑器在跑时）

```bash
python .workbuddy/tools/build_android.py
```

通过 `execute_code` 让编辑器执行构建。**有两个限制**：

- 构建期间主线程被占，MCP 会拒绝响应；调用可能 30 秒就返回 `success:false`，但**这不代表 Unity 退出**，构建仍在继续。轮询等 MCP 恢复后再读结果。
- 某些情况下 bee 派发的 clang 子进程会挂起（见下），MCP 这条路就走不通。

### 2. batchmode（推荐，也用于 CI）

```bash
"<UNITY_EXE>" -batchmode -quit \
  -projectPath "<项目根>" \
  -executeMethod Slime.DevTools.CliBuild.Android \
  -logFile "<项目根>/.workbuddy/build_<ver>.log"
```

`CliBuild.Android` 会在构建进程内设好签名口令，不污染 `ProjectSettings`。

**启动方式必须受控**：

```bash
rm -f .workbuddy/build_<ver>.log && \
  ("<UNITY_EXE>" -batchmode -quit -projectPath "<项目根>" \
     -executeMethod Slime.DevTools.CliBuild.Android \
     -logFile "<项目根>/.workbuddy/build_<ver>.log" > /dev/null 2>&1; \
   echo "EXIT=$?" >> .workbuddy/build_<ver>.status)
```

放进 `run_in_background: true`。**不要用 `nohup ... &`、`setsid`、`disown`。**

---

## 坑一：clang 子进程挂起（最耗时的一个）

### 症状

- 构建停在 IL2CPP 编译的最后几十个目标上（典型值 `2021/2035`），长时间不动
- `tasklist` 能看到 clang 进程，但
  - **CPU 恒为 0**
  - **内存卡在 27 MB**
  - 线程状态 **Suspended**
- 用 DAG 里完全相同的命令行手动跑，**0.2 秒**就产出有效 ELF

### 根因

从后台 shell / 非受控进程树启动的 Unity，其 bee 派发的 clang 子进程继承了 Job Object 限制，**创建即挂起**。问题在进程派发链，不在编译器、不在代码、不在缓存。

### 曾经试过但**无效**的处理

- 用 DAG json 定位卡住的节点，手动 shell 执行编译命令补齐 `.o` → 0.2 秒成功产出，但构建继续推进后又在下一批挂起
- 杀进程重启构建 → 同样位置复现
- 清 `Library/Bee` / `Library/Il2cppBuildCache` → 无关

### 唯一有效

**让 Unity 由用户手动启动（或以前台受控方式派生）**，一次通过。

### 如果确实需要手动补编译

DAG 文件：`Library/Bee/Playerf0c8cdd4.dag.json`。节点含 `DisplayName` / `Annotation` / `Action` / `Inputs`。找到挂起节点的 `Action` 命令，直接在 shell 里跑即可产出 `.o`。这只能救急，不能替代正确启动方式。

---

## 坑二：看错日志导致误判

### 症状

连续多轮认为「构建失败」，日志里全是 `Failure`。

### 根因

| 启动场景 | 实际写入的日志 |
|---|---|
| 已有编辑器实例在跑，又用 `-logFile` 启动新进程 | 新进程会把参数交给已有实例，**`-logFile` 指定的文件不写入**。真正的日志在 `%LOCALAPPDATA%\Unity\Editor\Editor.log` |
| 全新 batchmode 实例 | `-logFile` 指定的文件 |

`.workbuddy/build_editor.log` 里那一堆 `Failure` 来自更早的旧实例（PID 早已不同），而新实例的 `Editor.log` 里明明写着：

```
Build Finished, Result: Success
```

### 判断规则

**看日志前先确认文件修改时间是本次的**：

```bash
ls -la .workbuddy/build_<ver>.log
tail -30 "$LOCALAPPDATA/Unity/Editor/Editor.log"
```

两个都看，取时间戳新的那个。

---

## 坑三：版本号改了但包还是旧的

### 症状

`ProjectSettings.asset` 里 `bundleVersion: 6.11.0` 已改成 `6.11.1`，构建出来的 APK 仍是 vc11。

### 根因

磁盘文本改了，但**已运行的 Unity 内存里还是旧值**，构建用的是内存值。

### 修法

必须通过 `SerializedObject` 写入并 `SaveAssets`：

```csharp
var so = new UnityEditor.SerializedObject(
    UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
so.FindProperty("bundleVersion").stringValue = "6.11.1";
so.FindProperty("AndroidBundleVersionCode").intValue = 13;
so.ApplyModifiedPropertiesWithoutUndo();
UnityEditor.AssetDatabase.SaveAssets();
```

或者干脆在打包前的 batchmode 进程里写，进程退出时会落盘。

---

## 坑四：签名失败

### 症状

首次构建报「缺少 keystore 密码」。

### 修法

在构建脚本里显式设置（不要写进 `ProjectSettings`，那是要提交的文件）：

```csharp
UnityEditor.EditorUserBuildSettings.androidBuildType = AndroidBuildType.Release;
UnityEditor.PlayerSettings.Android.useCustomKeystore = true;
UnityEditor.PlayerSettings.Android.keystoreName = "C:/Users/Gove/.android/debug.keystore";
UnityEditor.PlayerSettings.Android.keystorePass = "android";
UnityEditor.PlayerSettings.Android.keyaliasName = "androiddebugkey";
UnityEditor.PlayerSettings.Android.keyaliasPass = "android";
```

验证 keystore 口令：

```bash
keytool -list -keystore ~/.android/debug.keystore -storepass android
```

`CliBuild.Android` 已经内置了这套默认值，只在为空时才写，避免覆盖用户配置。

---

## 构建后必做校验

> `CliBuild` 日志里的 `sizeMB` 是 BuildReport 的 `totalSize`，会把 `第九张史莱姆牌_BurstDebugInformation_DoNotShip` 目录一起算进去（实测报 1088.4），**不是 APK 体积**。看 APK 实际大小要用 `ls -l`。

```bash
# 1. 时间戳是本次的
ls -la Builds/A/第九张史莱姆牌.apk

# 2. 版本号
"$UNITY/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/<ver>/aapt2.exe" \
  dump badging "Builds/A/第九张史莱姆牌.apk" | head -3

# 3. MD5（与上传后后台值比对）
md5sum "Builds/A/第九张史莱姆牌.apk"

# 4. 内容与权限
python - <<'PY'
import zipfile, re
z = zipfile.ZipFile("Builds/A/第九张史莱姆牌.apk")
s = z.read('AndroidManifest.xml').decode('utf-16-le', errors='ignore')
print('perms:', sorted(set(re.findall(r'android\.permission\.[A-Z_]+', s))))
h = z.read('assets/privacy-policy-template.html').decode('utf-8-sig', errors='replace')
for kw in ['GAID', 'Android ID', '传感器']:
    print(kw, h.count(kw))
PY
```

正常权限组合应只有：`INTERNET` / `ACCESS_NETWORK_STATE` / `ACCESS_WIFI_STATE`。出现 `READ_PHONE_STATE` 说明清单又被改回去了。

## 提速

构建缓存就绪时约 2.5 分钟（IL2CPP 2035 个目标 + Gradle 约 129 秒）。首次或清缓存后显著变慢。缓存位置：

| 目录 | 内容 |
|---|---|
| `Library/Bee/artifacts/Android` | IL2CPP 中间产物 |
| `Library/Il2cppBuildCache/Android` | IL2CPP 缓存 |
| `Temp/gradleOut` | Gradle 工程 |

一般不需要清。只有在改了 scripting define、包依赖、或 IL2CPP 版本时才值得清 `Library/Bee`。
