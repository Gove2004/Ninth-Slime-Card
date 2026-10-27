# 隐私合规自查清单

TapTap 上架的机审会跑「安装包隐私合规检测」。本项目 6.11.0 就是在这项上被驳回的，驳回原文：

```
安装包隐私合规检测未通过，请在「消息中心」查看报告并修复，
如未收到报告可通过工单咨询。
```

这项检测查两件事：**包内声明的采集范围** vs **代码/SDK 实际采集的内容**，以及**是否存在未声明用途的敏感权限**。三者必须一致。

---

## 一、文案：声明要覆盖实采

包内隐私弹窗：`Assets/StreamingAssets/privacy-policy-template.html`
商店页字段：`field_id = privacy_policy`（`save-changes` 写）

**两处必须一致**，改一处就得查另一处。

### 本项目实采清单

| 信息 | 获取主体 | 用途 |
|---|---|---|
| **GAID**（谷歌广告标识符） | APP 自身 | 广告归因与统计分析，可在「设置-隐私-广告」关闭 |
| **Android ID** | TapSDK | 登录状态识别、成就同步、防作弊 |
| **设备传感器信息** | Unity3D 引擎框架 | 判断屏幕方向与运行状态，不上传 |

### 写法要点

- **按获取主体分列**，不要笼统写「我们收集设备标识符」。检测报告会区分「APP 自身」和「第三方 SDK」。
- **不要留模糊表述**。旧版写的「设备标识符（如 IMEI/IDFA/OAID，具体视平台而定）」属于过度声明——实际没采集 IMEI，但声明里有，反而成为不符项。已删除。
- **每项要写用途**，只列名字不写用途同样不合规。
- **传感器要说明是否上传**。「仅本地用于……」和「会上传」是完全不同的表述。

### 结构建议

```
一、我们如何收集和使用您的个人信息              ← GAID 放在这里的 <ul> 里
二、我们如何收集设备标识与传感信息              ← Android ID / 传感器，按获取主体分列
三、我们如何使用 Cookie 和同类技术
四、我们如何共享、转让、公开披露您的个人信息      ← 第三方 SDK 列表放这里
五、您的权利
六、我们如何保护未成年人的个人信息
七、个人信息的存储
八、本政策的更新
九、联系我们
```

GAID 是 APP 自己读的，和账号信息、游戏数据同属「我方收集」，所以放第一节；Android ID 和传感器是 SDK / 引擎框架读的，单独开一节区分主体。这样检测报告里「APP 自身」与「第三方 SDK」两栏都能对上。

插入新章节后**记得整体重排编号**，否则会出现两个「三、」。

---

## 二、权限：删掉所有声明了但没用到的敏感权限

### 高危权限清单

| 权限 | 风险 |
|---|---|
| `READ_PHONE_STATE` | 读取电话状态 / 设备 ID，检测必看 |
| `READ_SMS` / `RECEIVE_SMS` | 短信 |
| `READ_CONTACTS` | 通讯录 |
| `ACCESS_FINE_LOCATION` | 精确定位 |
| `READ_EXTERNAL_STORAGE` / `MANAGE_EXTERNAL_STORAGE` | 存储 |
| `CAMERA` / `RECORD_AUDIO` | 摄像头 / 麦克风 |
| `QUERY_ALL_PACKAGES` | 应用列表 |
| `READ_CALL_LOG` | 通话记录 |

### 排查方法

1. **看清单源码**

```bash
cat Assets/Plugins/Android/AndroidManifest.xml
```

2. **看打包后 APK 的真实权限**（源码清单会和 SDK 合并，只看源码不全）

```python
import zipfile, re
z = zipfile.ZipFile("Builds/A/<包名>.apk")
s = z.read('AndroidManifest.xml').decode('utf-16-le', errors='ignore')
print(sorted(set(re.findall(r'android\.permission\.[A-Z_]+', s))))
```

3. **逐个权限反查代码是否真的用**

```bash
grep -rn "getDeviceId\|TelephonyManager\|READ_PHONE_STATE" Assets/Scripts Assets/Plugins 2>/dev/null
```

4. **查第三方 SDK 是否引入**

```bash
grep -rn "READ_PHONE_STATE" Library/PackageCache/
cat Library/PackageCache/com.taptap.sdk.core@*/Mobile/Editor/NativeDependencies.xml
```

### 本项目结论

`AndroidManifest.xml` 里原本挂着 `READ_PHONE_STATE`，代码中**一次都没用**，TapSDK 的依赖声明里也没有它——纯属早期模板残留。移除后实际权限只剩：

```
INTERNET
ACCESS_NETWORK_STATE
ACCESS_WIFI_STATE
```

这三个都是常规权限，不触发隐私检测告警。

---

## 三、第三方 SDK

`四、我们如何共享、转让、公开披露您的个人信息` 一节要列全 SDK，每个写清**名称 + 用途 + 涉及的个人信息 + 隐私政策链接**。

本项目涉及：

| SDK | 用途 | 采集 |
|---|---|---|
| **TapSDK** | TapTap 登录、内嵌社区、数据统计 | 设备信息、网络信息、Android ID |
| **Unity3D 引擎框架** | 游戏运行时基础框架 | 设备型号、系统版本、**设备传感器信息** |

注意旧版写的是「Unity Analytics / 引擎统计」——本项目并没有接 Unity Analytics，只是用了引擎框架，名称写错也会成为不符项。已改为「Unity3D 引擎框架」。

---

## 四、被驳回后的完整循环

```
读驳回原因 → 补文案 / 删权限 → 升版本号 → 重新打包 → 上传 → 绑槽 → 重提审
```

**版本号必须升。** 同 versionCode 重复提交会被拒，也无法区分批次。

本项目实践：

| 版本 | versionCode | 结果 |
|---|---|---|
| 6.11.0 | 12 | 驳回（隐私合规检测未通过） |
| 6.11.1 | 13 | **审核通过并发布**（重新提审后 9 分钟） |

第二轮之所以快，是因为只改了隐私文案和权限，没有动功能。纯合规性修复的审核通常比新功能快。

---

## 五、隐私政策托管

### 历史问题

原来托管在 GitHub：

```
https://github.com/ETN-xun/dot_privacy/blob/main/privacy-policy-template.md
```

两个问题：

1. 该仓库**没有推送权限**（clone 可读、push 被拒），改不了内容
2. 里面的信息已经过期（写着「聚光灯-神之倒三角」「IP 授权待补齐」这类不存在的内容）

### 现行方案

改用**腾讯文档公开链接**：

```
https://docs.qq.com/doc/DQ3FRSmtjaWxFZmN5
```

已验证无登录态可访问（HTTP 200）。

### 创建要点（踩过坑）

- ❌ **不要用 `create_smartcanvas_by_mdx`**。智能文档会混入「一、智能文档是什么？」这类默认模板内容，不能当公开政策用。
- ✅ 用 `doc-mcp create_with_markdown`，正文走 **`base64_markdown`** 参数（直接传 `markdown` 或 `mdx` 会被忽略/报错）。
- ✅ 创建后必须 `manage.set_privilege` 设 `policy=2`（所有人可读），再 `manage.get_privilege` 读回确认。

```bash
cd "C:/Users/Gove/.workbuddy/plugins/cache/workbuddy-builtin/tencent-docs-plugin/<ver>/skills/tencent-docs"

python tencentdocs.py tdoc_call doc-mcp create_with_markdown '{"title":"<标题>","base64_markdown":"<b64>"}'
python tencentdocs.py tdoc_call tencent-docs manage.set_privilege '{"file_id":"<id>","policy":2}'
python tencentdocs.py tdoc_call tencent-docs manage.get_privilege '{"file_id":"<id>"}'
```

### 换成腾讯文档后，商店页链接还没切

商店页 `privacy_policy` 仍指向旧 GitHub 地址。切换被拦了一次：

```
422 failed_precondition: 当前状态不允许本次操作
（当前应用还没有草稿版本，请先调用 create-draft 创建草稿后再操作）
```

即：线上版本状态下不能直接改商店资料。要换得走 `create-draft` → `save-changes` → `submit-app-review`，意味着**重新走一轮审核**。

---

## 六、上线前自查

- [ ] 包内隐私弹窗文案与实采一致，按获取主体分列
- [ ] 商店后台 `privacy_policy` 字段指向的内容与包内一致
- [ ] 无未声明用途的敏感权限（`READ_PHONE_STATE` 之类）
- [ ] 第三方 SDK 名称准确、用途明确、附政策链接
- [ ] 未成年人保护章节存在（本项目 `六、`）
- [ ] 联系我们章节有可用邮箱
- [ ] 解包 APK 复核文案与权限（不能只信源码）
- [ ] 版本号已升，`bundleVersion` 与 `AndroidBundleVersionCode` 同步

## 七、资质材料（不由代码决定）

`taptap-cli qualification:analyze` 会列出这些，本项目当前 `pending_upload`：

| 资质 | 状态 |
|---|---|
| `game-license` 游戏版号 | ❌ pending_upload |
| `icp` ICP 备案 | ❌ pending_upload |
| `software-copyright` 软件著作权 | ❌ pending_upload |
| `authorization-letters` 授权书 | ❌ pending_upload |
| `security-assessment` 安全评估 | ❌ pending_upload |
| `privacy-compliance` 隐私合规 | ✅ approved |
| `anti-addiction` 防沉迷 | ✅ approved |
| `ai-declaration` AI 声明 | ✅ approved |

版号在国内是强制项。**这些必须用户提供扫描件**，CLI 无法自动补齐。

注意：6.11.1 是在这些资质仍为 `pending_upload` 的情况下通过审核并上线的——说明当前阶段机审放行了。但后续人工抽查可能会追。
