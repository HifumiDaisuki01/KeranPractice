# KeranPractice v1.0.1 测试报告

> 测试执行：WorkBuddy（2026-10-20）
> 被测对象：`fix/v1.0.1-safety-audit` 分支 + WorkBuddy 后续修复
> 结论：**服务端可验证项全部通过；真人客户端项仍未验证，因此不发布稳定 Release。**

---

## 0. 结论摘要

| 类别 | 项数 | 结果 |
|---|---|---|
| 编译（本地 + GitHub Actions） | 2 | ✅ 全部通过 |
| 单元测试（JSON / 权限 / 冷却） | 29 | ✅ 29 通过 |
| 服务端集成测试（BOT 驱动，游戏内） | 13 | ✅ 13 通过 |
| 存档迁移 | 3 | ✅ 通过 |
| KeranStrat 兼容（配置/数据零改动比对） | 2 | ✅ 通过（1 个自身恢复标记被其启动逻辑消耗，见 §6.7） |
| **真人客户端测试** | 5 | ⛔ **未执行**（无客户端），见 §7 |

**没有把 LOADED、命令注册、编译成功当作功能通过。** 下面每一项都写明证据来源。

---

## 1. 测试环境

| 项 | 值 |
|---|---|
| 游戏服 | `38.55.254.157`（Debian 12，4C/3.8G） |
| CS2 | `1.41.9.1` / ServerVersion 2000933，`-insecure`，de_dust2 死斗 |
| Metamod | `2.0.0-dev+1481`（API 18） |
| CounterStrikeSharp | `1.0.376`（.NET 10.0.3） |
| 插件 | 共 12 个，含 KeranStrat 0.5.5、BotControllerBridge、WeaponPaints build-466 等 |
| 测试方式 | SSH + RCON（TCP 27015）+ systemd 日志 |
| 测试构建 | `dotnet build -c Release -p:DefineConstants=KPTEST`（脚手架只在测试 DLL 中存在） |

---

## 2. 测试方法（三层）

1. **静态/单元层**：把 `JsonStore`、`PracticeConfig`、`PermissionService`、`PlayerState` 源码链接进独立测试工程，不依赖游戏运行。
2. **引擎探针层**：临时插件 `KeranProbe`（测试后已删除）在真实服务器上创建投掷物、注册 `*_detonate` / `inferno_startburn` 事件，验证「手工创建的投掷物到底会不会炸」。
3. **游戏内集成层**：`Tests/TestHarness.cs` 用现有 BOT 作为受测主体，直接驱动 `PositionSystem` / `ToolsSystem` / `FlashSystem` / `GrenadeSystem` / `BotSystem`，断言写进服务器日志（`[KPTEST] PASS/FAIL`）。

> 为什么用 BOT：容器里没有 CS2 客户端，无法以真人身份进服；BOT 是唯一能真正跑在游戏世界里的受控主体。

---

## 3. 对 v1.0.0 结论的复盘（为什么当时是错的）

### 3.1 `!rethrow`

v1.0.0 的实现是：

```csharp
player.ExecuteClientCommandFromServer("+attack");
```

当时只验证了「命令注册成功」「没有抛异常」，就写成了「重投上一颗道具（同一位置、视角、类型）」。**没有验证过是否真的产生了投掷物，也没有验证轨迹。**

复核 CSS 1.0.376 的实现：`ExecuteClientCommandFromServer` → `NativeAPI.IssueClientCommandFromServer`，`ExecuteClientCommand` → `NativeAPI.IssueClientCommand`，两者都不是「直接调用引擎投掷流程」。是否生效取决于客户端是否把 `+attack` 当作输入处理。

更关键的是：**这条路径根本无法在服务端断言成功**。v1.0.0 却把它写成已实现功能。

### 3.2 BOT「完全隔离」

v1.0.0 用「0.5 秒后对全服 BOT 取差集」判断归属。这有三个漏洞：

1. 差集可能包含**别人同时添加的 BOT**；
2. 只比对 `UserId`，而 `UserId` 会被复用；
3. 在 KSD/KSV 运行时，BotController 自己也在增删 BOT，差集必然出错。

当时只验证了「BOT 能加进来」，没有做多人并发、没有做 KSD 并行，就下了「按 UserId 归属，只操作自己创建的」的结论。**这不成立。**

### 3.3 JSON「原子写入」

v1.0.0 的失败分支是：

```csharp
catch { File.Delete(path); File.Move(tmp, path); }
```

`File.Replace` 失败时先删源文件——如果 `Move` 也失败，**原存档已丢**。而且 `Save` 无论成败都返回 void，调用方照样提示“已保存”。

当时只测了「正常写入能读回来」，没测失败注入，也没测损坏文件。

### 3.4 「不与 KeranStrat 冲突」

当时只核对了「指令前缀不同」，没有做双插件同时在线测试，也没有检查 BOT 池、回合重置、道具清理对 KSD 录像的影响。`!clear` 会无差别删除全图投掷物与火焰，直接会清掉 KSD 战术道具——**这条结论当时没有任何证据**。

---

## 4. 对 ChatGPT 修复分支（PR #1）的复核

PR 的方向是对的（权限锁、fail-closed 清理、BOT 熔断、存档迁移、CI），但**不能直接合并**。逐条：

| # | PR 做法 | 复核结论 |
|---|---|---|
| 1 | `!rethrow` 改为读取 `InitialVelocity` / `AngVelocity` 后 `CreateEntityByName` 重建 | ❌ **不成立**。见 §5 |
| 2 | `entity.InitialPosition.X = ...` / `InitialVelocity.X = ...` / `AngVelocity.X = ...` | ❌ **无效写入**。这三个属性只有 getter，IL 是 `Schema.GetSchemaValue<Vector>(...)`，每次返回新对象；改 `.X` 只改到临时副本。已删除 |
| 3 | 用 `Schema.SetSchemaValue<Vector>` 写 netvar | ❌ **危险**。`DataTypeExtensions.ToDataType(typeof(Vector))` 返回 `DATA_TYPE_POINTER`，写进去的是托管对象指针，会污染实体 netvar。已放弃 |
| 4 | `!clear` 按 `Thrower` 过滤 | ✅ 方向正确，但 `CInferno` 不是 `CBaseGrenade`，PR 会静默跳过火焰；已补 `OwnerEntity` 回退并明确 fail-closed |
| 5 | BOT 归属加 `EntityHandle.Raw` 校验 | ✅ 正确，但 `added.Count == 0` 分支不可达、0 个新 BOT 时提示语错误；已修 |
| 6 | `EnableBotManagement` 默认 false | ✅ 正确，已保留 |
| 7 | 存档迁到 `csgo/data/KeranPractice` | ⚠️ **只改了路径，没有迁移代码**，旧存档会“消失”。已补自动复制（保留原件） |
| 8 | JSON 失败分支改成 `catch(IOException) { Move(overwrite) }` | ✅ 不再先删源文件；但 `.tmp` 残留未清理，已补 |
| 9 | `!practice off` 清 noflash/autoclear | ⚠️ 只清了状态位，没处理“角色失效时 noclip/god 状态位残留”，已补 |
| 10 | 删除 `noclip/god/bot` 出 `PublicCommands` | ⚠️ 旧配置里仍然有这些项，仅删默认值不够；已加 `AllowPublicPowerCommands` 强制闸门 |
| 11 | 配置项 `AutoClearDefault` / `GodModeDefault` / `CommandAliases` 等 | ❌ 前两个是死配置，后几个从未实现；已实现前两个、删除其余 |
| 12 | 新增 `css_clearall` | ✅ 正确，已保留 |
| 13 | CI `-warnaserror` | ✅ 正确，已保留 |

---

## 5. 关键实测：手工创建的投掷物不会引爆

这是推翻 PR 核心实现的决定性证据。探针插件在真实服务器上执行：

```
css_probe_fire smoke 1 0 0 0 0 0 800      # CreateEntityByName + IsLive + DetonateTime=+1.5s
```

日志：

```
[Probe] FIRE smoke mode=1 ent=381 pos=(0.0,0.0,800.0) vel=(0.0,0.0,0.0) det=161.41 live=True
[Probe]   t=0.25 smoke pos=(0.0,0.0,775.0) vel=(0.0,0.0,-200.0) live=True b=0
[Probe]   t=1.00 smoke pos=(0.0,0.0,400.0) vel=(0.0,0.0,-800.0) live=True b=0
[Probe]   t=1.25 smoke pos=(0.0,0.0,202.5) vel=(0.0,0.0,431.9) live=True b=1
[Probe]   t=4.00 smoke pos=(0.0,0.0,211.7) vel=(0.0,0.0,9.4)   live=True b=5
```

- 物理正常（重力、弹跳都对）
- `DetonateTime` 已写入并读回（`det=161.41`，当前时间约 159.9）
- **1.5 秒后没有 `smokegrenade_detonate`，没有烟雾，实体一直弹到 5 秒后仍存活**

追加验证（全部无效）：

| 手段 | 结果 |
|---|---|
| 先写属性再 `DispatchSpawn` | 不引爆 |
| `AcceptInput("Detonate")` | 无事件、实体仍在 |
| `AcceptInput("FireUser1")` + `AcceptInput("InitializeSpawnFromWorld")`（MatchZy 风格） | 不引爆 |
| HE / 闪光 / 燃烧 / 诱饵 | 同样只有物理，无爆炸/火焰 |

结论：**手工实体缺少引擎在 `ThrowGrenade` 流程里设置的 think/初始化，公开 API 无法补齐。** 唯一能造出“真道具”的是带签名的原生工厂（MatchZy 的 `CSmokeGrenadeProjectile_CreateFunc` 等）——签名随版本变化，不适合本插件。

因此 1.0.1 **删除了整个实体重建路径**，改为引擎真实投掷回放。

> 附带说明：测试期间还试过 `logic_script` + VScript `SpawnGrenadeProjectile`，写入 `m_iszScriptFile` 后服务端直接崩溃并被 systemd 拉起（测试专用探针，未进入正式代码）。该方案已放弃，不建议后续采用。

---

## 6. 实测结果

### 6.1 编译

```
$ dotnet build KeranPractice.csproj -c Release -warnaserror
Build succeeded. 0 Warning(s) 0 Error(s)
KeranPractice.dll = 67,584 bytes
```

GitHub Actions（分支 `fix/v1.0.1-safety-audit`）：2 次 run 全部 `success`。

### 6.2 单元测试（29/29）

```
PASS save returns true / roundtrip count / roundtrip float / roundtrip steamid
PASS no BOM / overwrite ok / overwrite count / no tmp left
PASS corrupt load returns empty / corrupt backup created / corrupt save refused / corrupt file untouched
PASS empty load default / missing load default / bulk intact
PASS admin god allowed / public god blocked / public noclip blocked / public bot blocked
PASS public noflash needs practice / public noflash in practice / public practice always
PASS admin clearall / public clearall blocked / explicit power grant
PASS noclip still blocked without grant / no practice requirement
PASS first command allowed / second command throttled
== 29 passed, 0 failed ==
```

其中「corrupt save refused」验证了损坏文件在本次进程内**拒绝覆盖**，且原文件字节未变。

### 6.3 存档迁移

在旧目录放一个合法 `positions.json`，重启：

```
[KeranPractice] 已迁移旧存档 positions.json → .../game/csgo/data/KeranPractice/positions.json（原文件保留）
[KeranPractice] 已加载 v1.0.1｜...｜数据: .../game/csgo/data/KeranPractice
```

新文件内容与旧文件逐字节一致，旧目录文件仍在（可回滚）。

### 6.4 位置系统（游戏内，BOT）

```
[KPTEST] PASS pos.save_tp distance=0.00
[KPTEST] PASS pos.isolation_count count=1
[KPTEST] PASS pos.delete
```

保存 → 位移 (200,100,40) → 传送回来，误差 0.00；删除后列表为空。

### 6.5 god / noclip

```
[KPTEST] PASS god.on
[KPTEST] PASS god.off_restores before=True after=True
[KPTEST] PASS noclip.on before=MOVETYPE_NONE
[KPTEST] PASS noclip.off now=MOVETYPE_NONE expect=MOVETYPE_NONE
```

BOT 被 BotController 固定在 `MOVETYPE_NONE`，开关 noclip 后能准确恢复原值——这正是 1.0.1 修的点（1.0.0 一律恢复 WALK）。

### 6.6 免闪（定时器路径）

```
[KPTEST] PASS flash.timer_clears value=0.00
[KPTEST] PASS flash.off_state
```

人为把 `FlashDuration` 设为 5，0.35 秒内被清零。**`EventPlayerBlind` 事件路径仍未被真实闪光验证**（见 §7）。

### 6.7 道具清理（fail-closed）

```
[KPTEST] PASS clear.unowned_skipped removed=0
[KPTEST] PASS clear.admin_call_ok removed=2
```

- 场上 2 个无归属（Thrower 无法解析）投掷物：以某 SteamID 调用个人清理 → 删除 **0** 个，实体都还在
- 随后管理员全图清理 → 删除 2 个

这正是审计要求的「不能误删他人道具」。

### 6.8 多人/存档隔离

```
[KPTEST] PASS iso.pos_separate la=1 lb=1 laX=192.0 lbX=256.0
[KPTEST] PASS iso.nade_own_only x=256.0 expect=256.0
```

两个不同 SteamID 使用同名点位 `same`，各自只看到 1 条、坐标不同；B 的 `!loadnade` 只加载 B 自己的坐标。

### 6.9 BOT 管理（临时开启 `EnableBotManagement`）

```
[KPTEST] PASS bot.add before=1 after=2
[KPTEST] PASS bot.owned_tracked owned=1
[KPTEST]    recorded id=12 handle=296026125
[KPTEST]    live id=12 handle=296026125 name=Mae
[KPTEST] PASS bot.clearbots ownedId=12 still=False
[KPTEST] PASS bot.ownership_cleared
```

- 默认关闭时：`bot.disabled_by_default` ✅（命令直接拒绝，不执行）
- 开启后：添加成功、UserId+实体句柄匹配、只清掉自己那一个；其余 BOT（id 5/7/9/11）未被触碰
- 测试后已把 `EnableBotManagement` 改回 `false`

### 6.10 与 KeranStrat 的兼容性比对

| 项 | 测试前 | 测试后 |
|---|---|---|
| `KeranStrat.json` md5 | `90180827c0670b3ba75bd2767bfe56e7` | `90180827c0670b3ba75bd2767bfe56e7` |
| `plugins/KeranStrat/data` 文件清单 | 含 `practice-recovery.json` | 该文件消失，**其余文件逐字节一致** |

说明：`practice-recovery.json` 是 KeranStrat 自己的“崩溃恢复标记”，在服务器重启时由其启动逻辑消耗/删除，与本插件无关（本插件从不读写该目录）。如果它代表一次未完成的 KSD 会话，那么该恢复机会已随这次重启失效——**这是本次测试对服务器产生的唯一副作用**，已在交付说明中标注。

---

## 7. 未验证项（必须真人客户端）

以下项目**没有**通过，也没有被声称通过：

| # | 项目 | 原因 |
|---|---|---|
| 1 | `!noflash` 真实闪光下不白屏 | 需要真人被闪，`EventPlayerBlind` 才能触发 |
| 2 | `!rethrow` 自动投掷是否真的把道具扔出去 | 依赖客户端响应 `+attack`，服务端无法伪造 |
| 3 | `!rethrow` 轨迹/落点还原度 | 同上 |
| 4 | 两人同时训练时的免闪/清理隔离 | 需要两个真人 |
| 5 | KSD/KSV 运行中与本插件并行 | 需要真人发起 KSD 会话 |

### 给用户的 5 分钟验收脚本

进服后按顺序执行，观察聊天栏反馈：

```
!practice
!noflash                 # 让队友/自己扔闪，确认不白屏
!savepos a
!noclip                  # 能飞
!noclip
!tp a                    # 回到原位
（扔一颗烟）
!rethrow                 # 期望：被拉回投掷点并自动扔出；若客户端拒绝，会明确提示失败
!clear                   # 只清自己的烟
（让队友扔一颗烟，再 !clear，确认队友的烟还在）
!savenade s1
!nades
!loadnade s1
!practice off            # 状态恢复
```

`!rethrow` 三种可能的结果都会在聊天栏明确写出，不会静默：

- `重投成功：烟雾弹 已由引擎真实投出。`
- `自动投掷未生效（客户端拒绝模拟按键）。已把你放回投掷点并补发道具，请手动投掷。`
- `正在按原始站位/视角/速度重投 烟雾弹…`（1.2 秒内等待事件）

---

## 8. 遗留问题与风险

| 级别 | 问题 | 处理 |
|---|---|---|
| 高 | `!rethrow` 自动投掷未实测 | 阻塞稳定 Release；若客户端不响应，退化为「就位+补发」，不假报成功 |
| 中 | `!clear` 对 `inferno`（燃烧地面）只能靠 `OwnerEntity` 判断，可能漏删 | 已 fail-closed，需真人用燃烧瓶验证 |
| 中 | `!clear` 的“删除自己”正向路径未在游戏内验证（BOT SteamID=0 无法构造归属） | 需真人扔烟后 `!clear` 验证 |
| 低 | `!impacts` 只有控制台坐标 | 保持现状并在 README 说明 |
| 低 | `!rethrow` 会给玩家补发道具（无限道具） | 训练服可接受；如需限制可后续加配置 |
| 低 | 服务商下行限速 4 Mbps（与插件无关） | 需联系客服 |

---

## 9. 产物

| 文件 | 说明 | SHA256 |
|---|---|---|
| `KeranPractice.dll` | 67,584 bytes，net10.0，Release，不含测试脚手架 | `5ae10ce8a009232dc384aa487981820a5f733b7ee2e7db736fb0d7a0670723de` |
| `KeranPractice-1.0.1-rc1.zip` | 安装包（DLL + deps + 示例配置 + 文档），53,494 bytes | `56a5c7ef9dc3a8dec288f4e3265ad67ce382e802c094e794c3f9519ddb74e133` |
| `TEST-REPORT-v1.0.1.md` | 本文件 | — |

SHA256 见 Release 说明 / 交付消息。

**未发布 GitHub Release**：按交接要求，真人客户端项未通过前不发布稳定版；v1.0.0 Release 保持不动。
