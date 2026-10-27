# TapTap CLI 参考

`taptap-cli` 是 TapTap 开发者后台的命令行封装。本项目的开发者 ID `275988`、应用 ID `818915`，Android 包名 `com.govexun.dot`。

```bash
taptap-cli --version   # 本项目验证版本 2026.9.24
```

配置在 `~/.taptap-cli/taptap.json`（含 token），状态在 `~/.taptap-cli/update-state.json`。

---

## 通用约定

所有业务命令的形态一致：

```bash
taptap-cli <服务> <命令> --dev-id <DEV> --app-id <APP> --data '<JSON>'
```

| 参数 | 作用 |
|---|---|
| `--data '<JSON>'` | 除 dev-id / app-id 外的所有入参都塞这里。也支持 `-`（读 stdin）和 `@path`（读文件，路径须在当前工作目录内） |
| `--dry-run` | 只打印将要发出的请求，不执行。**写操作前必跑** |
| `--idempotency-key <key>` | 幂等键。重放同一 key 不会产生重复副作用 |
| `--yes` | 确认执行写操作 / 不可逆操作 |
| `--format pretty` | 人类可读输出（默认 `json`） |

**不要猜参数。** 每个命令的先验契约用 `taptap-cli schema <服务> <命令>` 查，它会给完整 `inputSchema` / `outputSchema`。官方操作文档用 `taptap-cli skills read <name>`。

---

## 读操作

### 版本与审核状态

```bash
taptap-cli app get-app-version --dev-id 275988 --app-id 818915 \
  --data '{"version":"V-20261009"}'
```

返回 `status` / `status_label` / `version` / `revision`，以及关键字段 `logs`：

```json
{"event":"review_submitted","updated_time":1793097822,"version_code":13,"version_name":"6.11.1"}
{"event":"review_rejected","reason":"1. 安装包隐私合规检测未通过…"}
{"event":"review_approved","updated_time":1793100614}
{"event":"published","updated_time":1793100614}
```

`logs` 是唯一的审核事实来源。判断「过了没」只看最后一条 `event`。

```bash
# 全部版本
taptap-cli app list-app-versions --data '{"page":1,"page_size":20}'
```

### 包体与槽位

```bash
taptap-cli app list-packages --data '{"package_types":[]}'
```

返回四块：

| 字段 | 含义 |
|---|---|
| `list` | 全部包体。每项至少含 `package_id` / `type` / `version_name` / `status` |
| `current_bindings` | **当前已绑定的槽位**。空数组表示没有绑定任何包 |
| `package_slots` | 槽位清单：`main` / `windows` / `apk_mini_game_play`，每个带 `available` 和 `expected.kind` |
| `total` | 包体总数 |

注意 `list` 里可能缺 `md5` / `version_code`（返回 `null`），要核 MD5 就在上传后立刻单独查，或者本地 `md5sum` 与上传返回比对。

### 资料完整度与资质

```bash
taptap-cli app analyze-app-status --dev-id 275988 --app-id 818915
taptap-cli qualification:analyze --dev-id 275988 --app-id 818915
```

`qualification:analyze` 会列出每项资质的状态。本项目当前的缺口（`pending_upload`）：

- `game-license` 游戏版号
- `icp` ICP 备案
- `software-copyright` 软件著作权
- `authorization-letters` 授权书
- `security-assessment` 安全评估

`privacy-compliance` / `anti-addiction` / `ai-declaration` 是 `approved`。

这些属于资质材料缺失，**无法通过 CLI 自动化补齐**，必须由用户提供扫描件后上传。

### 资料模块

```bash
# 列出模块
taptap-cli app get-app-module --dev-id 275988 --app-id 818915 --data '{"module_id":"basic-info"}'
```

已知可读的 9 个模块与字段：

| module_id | 字段 |
|---|---|
| `basic-info` | `category` `description` `icon` `title` |
| `developer-info` | 开发者信息 |
| `other-settings` | 其他设置 |
| `profile-promotion` | 商店宣传图 |
| `release-settings` | 发布设置 |
| `package` | 包体相关 |
| `platform-status` | 平台状态 |
| `assets-upload` | 素材上传 |
| `windows-exclusive` | Windows 独占 |

**商店页「隐私政策」字段不在上述模块里**，它的 `field_id` 是 `privacy_policy`，只能通过 `save-changes` 写。

### 提审预检

```bash
taptap-cli app precheck-app-review --dev-id 275988 --app-id 818915 \
  --data '{"release_schedule":{"kind":"immediate"}}'
```

返回阻塞项清单。空即可以提审。这一步是机器预审，通过不代表人工审核通过。

---

## 写操作

### 上传包体

```bash
taptap-cli upload-apk "<apk路径>" --dev-id 275988 --app-id 818915 \
  --idempotency-key upload-6111-20261027 --yes
```

耗时较长（91.6 MB 约需数分钟），建议放 `run_in_background: true`。上传成功后 `list-packages` 里会出现新条目，记下 `package_id`。

### 绑定槽位

入参建议写文件避免转义问题：

```json
{
  "slot": "main",
  "package": { "id": "1071807", "type": "apk" },
  "expected": { "kind": "apk", "apk_id": "1071715" }
}
```

```bash
taptap-cli app select-package --dev-id 275988 --app-id 818915 \
  --data @.workbuddy/sel_6111.json \
  --idempotency-key bind-6111-20261027 --yes
```

`expected.apk_id` 是**乐观锁**——必须填绑定前的那个 `apk_id`，否则会冲突。绑定前先 `list-packages` 读当前绑定值。

**返回 ok 不等于生效**，必须读回：

```bash
taptap-cli app list-packages --data '{"package_types":[]}' \
  | python -c "import sys,json;print(json.load(sys.stdin)['data']['result']['current_bindings'])"
```

### 创建草稿版本

```bash
taptap-cli app create-draft --dev-id 275988 --app-id 818915 --data '{}' --yes
```

返回 `based_on_version_id` / `version_id` / `status=draft`。

**线上版本状态下不允许改商店资料**，会报：

```
422 failed_precondition: 当前状态不允许本次操作
（当前应用还没有草稿版本，请先调用 create-draft 创建草稿后再操作）
```

所以「改商店资料」的实际链路是：`create-draft` → `save-changes` → `submit-app-review`。

### 改资料字段

```bash
taptap-cli app save-changes --dev-id 275988 --app-id 818915 \
  --data '{"changes":[{"field_id":"privacy_policy",
                       "value":"<新值>",
                       "expected":"<当前严格值>"}]}' \
  --idempotency-key privacy-link-20261027 --yes
```

两条硬性约束：

1. **必须带 `expected`**（先把 `get-app-module` / 商店页读到的严格值填进去）。缺了会报：
   ```
   save-changes requires $.changes[0] to include expected unless force=true was explicitly authorized
   ```
2. **不要用 `force=true` 兜底**。CLI 明说「do not use force=true as automatic recovery from a 422 response」——它绕过的正是防误覆盖的保护。

### 提审

```bash
taptap-cli app submit-app-review --dev-id 275988 --app-id 818915 \
  --data '{"release_schedule":{"kind":"immediate"}}' \
  --idempotency-key submit-6111-20261027 --yes
```

`release_schedule.kind` 目前用过 `immediate`（审核通过即发布）。

### 撤审

```bash
taptap-cli app withdraw-app-review --dev-id 275988 --app-id 818915 \
  --data '{}' --idempotency-key withdraw-<原因>-<日期> --yes
```

只有状态是「审核中」时可用。已被驳回或已上线时不需要撤。

---

## 资质类命令

```bash
taptap-cli qualification get-qualification-status --dev-id 275988 --app-id 818915
taptap-cli qualification save-qualification-draft   --dev-id 275988 --app-id 818915 \
  --data '{"qualification":{"qualification_type":"privacy-compliance",
                            "privacy_qualification":{"privacy_policy_link":"<URL>"}}}'
taptap-cli qualification submit-qualification --dev-id 275988 --app-id 818915 \
  --data '{"qualification_type":"privacy-compliance"}'
```

`save-qualification-draft` 只存草稿，不影响线上；`submit-qualification` 是 **high-risk** 操作，需用户明确同意后才能加 `--yes`。

`get-qualification-status` 返回的 `submitted_fields` 可用来确认草稿是否真的写进去了。

---

## 排障

| 现象 | 原因 | 处理 |
|---|---|---|
| `validation: invalid_argument` + 提到 `expected` | 缺乐观锁字段 | 读当前值填进 `expected` |
| `422 failed_precondition` + 提到 create-draft | 没有草稿版本 | 先 `create-draft` |
| `409` 冲突 | `expected` 与实际值不符 | 重新读当前值 |
| `--data @file` 报路径错 | 文件不在当前工作目录内 | 用相对路径或先 `cd` |
| 上传后包体 version 不对 | 版本号没写进 Unity 内存 | 用 `SerializedObject` 重写并重新打包 |

## 更新 CLI

```bash
taptap-cli update
```

当前 2026.9.24，最新 2026.10.27。升级后先跑 `taptap-cli schema` 复核参数契约再动写操作。
