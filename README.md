# KeranPractice

> CS2 社区服务器训练插件 · Keran Technology © 2026

轻量、独立的 CS2 训练插件：道具练习、站位保存、闪光免疫、弹着点提示。**不依赖数据库、不修改游戏文件。**

> **版本 1.0.1 说明**：1.0.0 的若干说明超出了代码实际保证（尤其是 `!rethrow`、BOT 隔离、`!clear` 的清理范围）。本版按实机探针结果重写了相关实现，并把能力边界写清楚。详见 [TEST-REPORT-v1.0.1.md](TEST-REPORT-v1.0.1.md)。

---

## 一、兼容性

| 项 | 要求 |
|---|---|
| CS2 服务端 | **1.41.9.1**（ServerVersion 2000933）实测 |
| CounterStrikeSharp | **1.0.376**（.NET 10） |
| Metamod:Source | **2.0.0-dev+1481**（API 18） |
| 运行时 | **.NET 10**（跟随 CSS 1.0.376，不能降到 .NET 8） |
| 系统 | Linux x86_64 / Windows x86_64 |

---

## 二、安装

### Linux

```bash
cd /path/to/cs2/game/csgo
unzip KeranPractice-1.0.1.zip -d .
chown -R steam:steam addons/counterstrikesharp/plugins/KeranPractice
systemctl restart cs2        # 或 RCON: css_plugins reload KeranPractice
```

### Windows

解压到 `game\csgo\`，确保：

```
game\csgo\addons\counterstrikesharp\plugins\KeranPractice\KeranPractice.dll
game\csgo\addons\counterstrikesharp\configs\plugins\KeranPractice\KeranPractice.json
```

### 验证

```
css_plugins list        # 应出现 "KeranPractice" (1.0.1)
```

日志：

```
[KeranPractice] 已加载 v1.0.1｜配置: ...｜数据: .../csgo/data/KeranPractice
```

### 从 1.0.0 升级

1.0.0 把存档写在插件目录内（`plugins/KeranPractice/data/`），升级时会被覆盖删除。1.0.1 起数据改到 `game/csgo/data/KeranPractice/`，并在启动时**自动复制**旧文件（原文件保留，方便回滚）。日志会打印：

```
[KeranPractice] 已迁移旧存档 positions.json → .../csgo/data/KeranPractice/positions.json（原文件保留）
```

---

## 三、指令表

所有指令支持 `!xxx` 和 `css_xxx` 两种写法。默认需要先 `!practice` 进入训练会话。

### 训练会话

| 指令 | 说明 |
|---|---|
| `!practice [on/off]` | 开启/关闭训练会话；关闭时恢复 noclip/god 并清除免闪、自动清理 |
| `!phelp` | 中文帮助 |

### 闪光免疫

| 指令 | 说明 |
|---|---|
| `!noflash [on/off]` | 个人闪光免疫；开启瞬间清空白屏 |

只影响自己，不删除闪光弹、不影响他人。

### 位置与传送

| 指令 | 说明 |
|---|---|
| `!savepos [名称]` | 保存坐标 + 视角，不填名称存为 `default` |
| `!tp [名称]` | 传送并清零速度 |
| `!poslist` / `!delpos <名称>` | 列出 / 删除自己的位置 |
| `!last` | 回到上一颗道具的投掷位置与视角 |
| `!spawn` / `!tspawn [n]` / `!ctspawn [n]` | 回出生点 |

### 投掷物

| 指令 | 说明 |
|---|---|
| `!rethrow` | 回到上一颗道具的投掷点/视角/速度，补发同一颗道具并自动投掷 |
| `!clear` | **只清理能确认属于自己**的投掷物与火焰；无法确认归属的一律不动 |
| `!clearall` | 管理员：清理全图所有投掷物与火焰 |
| `!autoclear [on/off]` | 投掷后延迟清理自己的投掷物 |
| `!savenade <名称>` | 保存当前道具点位 |
| `!nades` / `!loadnade <名称或编号>` | 列出 / 就位并补发道具 |

`!rethrow` 的实现与限制见第六节。

### 训练工具

| 指令 | 说明 |
|---|---|
| `!noclip [on/off]` | 自由飞行（出生后自动恢复） |
| `!god [on/off]` | 无敌（只改 `TakesDamage`） |
| `!impacts [on/off]` | 弹着点坐标输出到控制台（5 条/秒限流，无 3D 标记） |
| `!respawn` | 重新复活 |
| `!restart` | 回合重置，仅管理员；`RespectKeranStrat=true` 时直接拒绝 |

### BOT（默认关闭）

| 指令 | 说明 |
|---|---|
| `!bot` / `!tbot` / `!ctbot` | 添加 BOT |
| `!placebot` / `!removebot` / `!clearbots` / `!freezebot` / `!swapbot` | 放置 / 移除 / 清理 / 冻结 / 交换 |

> **默认 `EnableBotManagement=false`**：本插件与 KeranStrat 共用同一 BOT 池，KSD/KSV 运行时开启 BOT 管理会互相干扰。只有在独立测试服才建议显式开启。

---

## 四、配置

路径：`addons/counterstrikesharp/configs/plugins/KeranPractice/KeranPractice.json`

```json
{
  "ConfigVersion": 1,
  "Admins": ["76561199404249776"],
  "PublicCommands": ["noflash", "savepos", "tp", "poslist", "delpos", "last",
                     "clear", "rethrow", "practice", "phelp",
                     "spawn", "tspawn", "ctspawn", "autoclear",
                     "savenade", "nades", "loadnade"],
  "AdminOnlyCommands": ["restart", "clearall"],
  "AllowPublicPowerCommands": false,
  "EnableBotManagement": false,
  "RequirePracticeMode": true,
  "CommandCooldownMs": 300,
  "AutoClearDelaySeconds": 3.0,
  "AutoClearDefault": false,
  "AllowRoundRestart": true,
  "RethrowHoldMs": 120,
  "AutoThrowOnRethrow": true,
  "ReturnAfterRethrow": true,
  "GodModeDefault": false,
  "MaxPositionsPerMap": 64,
  "MaxNadesPerMap": 200,
  "RespectKeranStrat": true
}
```

| 字段 | 说明 |
|---|---|
| `Admins` | SteamID64 白名单，拥有全部权限 |
| `PublicCommands` | 普通玩家可用的指令 |
| `AdminOnlyCommands` | 仅管理员可用 |
| `AllowPublicPowerCommands` | **危险开关**：允许把 god/noclip/respawn/BOT 等高风险指令开放给普通玩家；默认 false |
| `EnableBotManagement` | BOT 管理总开关；默认 false |
| `RequirePracticeMode` | 是否必须先 `!practice`（`practice`/`phelp` 豁免） |
| `RethrowHoldMs` | `!rethrow` 模拟按住攻击键的时长（毫秒） |
| `AutoThrowOnRethrow` | false = 只回到投掷点并补发道具，不自动投掷 |
| `ReturnAfterRethrow` | 重投后是否把玩家送回原位置 |
| `AutoClearDefault` / `GodModeDefault` | 进入 `!practice` 时的初始状态 |
| `RespectKeranStrat` | true 时 `!restart` 直接拒绝 |

> 旧版配置里已废弃的字段（`TeleportOnRethrow`、`CommandAliases`、`ImpactMarkerSeconds` 等）会被忽略，可手动删除。

### 数据文件

```
game/csgo/data/KeranPractice/
├── positions.json     位置存档（SteamID + 地图隔离）
└── nades.json         道具点位（SteamID + 地图隔离）
```

- 写入走「临时文件 + rename」原子替换；失败不会破坏旧文件，也不会误报成功
- 读不出来的文件会备份为 `.corrupt-<UTC时间>-<guid>.bak`，**并在本次进程内拒绝覆盖**（修好文件后重启即可）
- `!poslist` / `!tp` / `!nades` / `!loadnade` 只能看到自己的数据

---

## 五、与 KeranStrat 共存

| 项目 | 实际做法 |
|---|---|
| 指令命名 | 全部 `css_` 前缀，不与 `!ks` / `!ksd` / `!ksv` 抢占 |
| BOT | 默认**不管理**；开启后也只认自己创建且实体句柄匹配的 BOT |
| 回合 | `RespectKeranStrat=true` 时 `!restart` 直接拒绝 |
| 文件 | 不读写 KeranStrat 任何文件或存档 |
| 道具清理 | `!clear` 只删归属可确认的实体；`!clearall` 才是全图清理（管理员） |

---

## 六、已知限制（实测结论）

| 功能 | 限制 | 依据 |
|---|---|---|
| `!rethrow` | 依赖客户端执行 `+attack`；若客户端拒绝模拟按键，只会把你放回投掷点并补发道具，聊天栏会明确提示失败 | 服务端无法直接调用引擎的投掷流程 |
| `!rethrow` | 无法用「重建投掷物实体」实现：手工 `CreateEntityByName` 的投掷物只有物理、**永不引爆** | 探针实测：1.5s/2s 引信、`AcceptInput("Detonate")`、`FireUser1`、`InitializeSpawnFromWorld` 全部无效 |
| `!clear` | 归属无法确认的实体不会被个人清理（包括部分火焰） | fail-closed 设计 |
| `!impacts` | 只有控制台坐标，没有 3D 标记 | CSS 1.0.376 未暴露 DebugDraw |
| `!god` | 只关闭 `TakesDamage` | 坠落/击杀奖励等仍可能造成伤害 |
| `!noclip` | 出生会重置 MoveType，插件在 spawn 后 0.2s 重新应用 | 引擎行为 |
| BOT | 默认关闭 | 与 KeranStrat 共用 BOT 池 |

---

## 七、测试与验收

见 [TEST-REPORT-v1.0.1.md](TEST-REPORT-v1.0.1.md)。摘要：

- 编译：`dotnet build -c Release -warnaserror` 0 错误 0 警告；GitHub Actions 通过
- 单元测试：29 项（JSON 原子写入/损坏保护/权限矩阵/冷却）全部通过
- 服务端集成测试（BOT 驱动）：位置保存/传送/删除、god、noclip、免闪清理、存档隔离、BOT 增删与归属、清理 fail-closed 全部通过
- **未完成**：真人客户端的 `!noflash` 实战效果、`!rethrow` 自动投掷、多人同时训练 —— 需要真人进服

### 测试构建（可选）

仓库内 `Tests/TestHarness.cs` 只在定义 `KPTEST` 时编译：

```bash
dotnet build KeranPractice.csproj -c Release -p:DefineConstants=KPTEST
# 进服后 RCON / 控制台执行：
css_kptest pos|god|noclip|flash|clear|iso|bot
```

---

## 八、目录结构

```
KeranPractice/
├── KeranPractice.csproj
├── KeranPracticePlugin.cs        入口、命令注册、事件
├── Config/PracticeConfig.cs
├── Core/{PlayerState,PermissionService}.cs
├── Models/Records.cs
├── Systems/{Flash,Position,Grenade,Tools,Bot}System.cs
├── Tests/TestHarness.cs          仅 KPTEST 构建
└── Utils/JsonStore.cs
```

---

## 九、许可

MIT License · Keran Technology © 2026

依赖 [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)（MIT）。
