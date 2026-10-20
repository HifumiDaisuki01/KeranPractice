# Changelog

## 1.0.1 — 2026-10-20

安全审计与实测修正版。**未改动 v1.0.0 Release**。

### 修复（审计发现的实际问题）

- **`!rethrow` 完全重写**：1.0.0 的「`ExecuteClientCommandFromServer("+attack")`」并未验证过是否能真正触发投掷。
  1.0.1 改为「回到投掷点 + 恢复当时视角与玩家速度 + 补发同一颗道具 + 客户端真实按键」，
  并以 `grenade_thrown` 事件作为成功判据；1.2 秒内没收到事件就明确提示失败，不再假报成功。
- **删除「重建投掷物实体」方案**：探针实测证明服务端 `CreateEntityByName` 造出的投掷物只有物理轨迹，
  **永不引爆**（1.5s/2s 引信、`AcceptInput("Detonate")`、`FireUser1`、`InitializeSpawnFromWorld` 均无效），
  因此该路径不可能复现烟雾/火焰/爆炸，已彻底移除。
- **`!clear` 改为按投掷者过滤**：只删除能确认归属自己的实体，源头不明的跳过（fail-closed）；
  新增管理员 `!clearall` 做全图清理。1.0.0 会误删他人的道具与 KeranStrat 战术道具。
- **BOT 管理默认关闭**：新增 `EnableBotManagement`，默认 false（与 KeranStrat 共用 BOT 池）。
  归属判断从「0.5 秒内新增 BOT 的 UserId」升级为「UserId + 实体句柄一致」，多人同时添加时放弃认领而不是乱认。
- **权限加固**：新增 `AllowPublicPowerCommands`（默认 false），即便旧配置把 god/noclip/BOT 写进
  `PublicCommands` 也不会对普通玩家开放；`!phelp` 补上权限检查。
- **存档按 SteamID 隔离**：`!nades` / `!loadnade` / `!savenade` 之前按地图查询，能读到同图他人点位，已修复。
- **JSON 原子写入**：删除「`File.Replace` 失败后先删源文件再 Move」的丢数据路径；损坏文件在本次进程内拒绝覆盖。
- **数据目录迁移**：从 `plugins/KeranPractice/data/` 移到 `csgo/data/KeranPractice/`，启动时自动复制旧文件（不删原件）。
- **`!practice off` 清理**：关闭会话时强制清除 noclip/god 残留状态位（即使角色已失效），避免下次出生又被贴上。
- **god 恢复值**：记录进入前的 `TakesDamage`，退出时按原值恢复，不再一律设 true。
- 修复 BOT 聊天回包：对 BOT 调用 `PrintToChat` 会打空指针，改为写日志（`PositionSystem.Reply`）。

### 新增

- `!clearall`（管理员）
- 配置项：`AllowPublicPowerCommands`、`EnableBotManagement`、`RethrowHoldMs`、
  `AutoThrowOnRethrow`、`ReturnAfterRethrow`
- GitHub Actions：net10 严格编译（`-warnaserror`）
- `Tests/TestHarness.cs`：仅 `KPTEST` 构建，用 BOT 驱动服务端集成测试
- `TEST-REPORT-v1.0.1.md`

### 移除

- 无效/未实现的配置项：`TeleportOnRethrow`、`CommandAliases`、`ImpactMarkerSeconds`、
  `MaxImpactMarkers`、`ConsoleSteamId`、`AutoStartPracticeForWhitelist`
- `AdminOnlyCommands` 默认值里的 `give` / `admin`（没有对应指令）

### 已知问题

- `!rethrow` 的自动投掷依赖客户端响应 `+attack`；高延迟或客户端拦截时退化为「就位 + 补发道具」
- `!impacts` 仍然只有控制台坐标
- 真人客户端相关项（免闪实战、自动重投、多人隔离）尚未实测，见 TEST-REPORT

---

## 1.0.0 — 2026-10-20

首个正式版本。

### 新增

**训练会话**
- `!practice` 开启/关闭训练会话，关闭时自动恢复被插件修改的状态
- `!phelp` 中文帮助菜单

**闪光免疫**
- `!noflash [on/off]` 个人闪光免疫
- 基于 `EventPlayerBlind` + `FlashDuration` 清零，配合 0.1 秒兜底定时器（无人使用时自动停止）
- 开启瞬间清除已有闪白

**位置系统**
- `!savepos [名称]` / `!tp [名称]` / `!poslist` / `!delpos`
- `!last` 回到上一颗道具投掷点
- `!spawn` / `!tspawn [n]` / `!ctspawn [n]` 出生点传送
- 按 `SteamID + 地图` 隔离，JSON 原子写入

**投掷物**
- `!rethrow` 重投上一颗道具（传送回原点 + 恢复视角 + 给道具 + 模拟投掷）
- `!clear` 清除投掷物与火焰
- `!autoclear [on/off]` 投掷后延迟自动清理
- `!savenade` / `!nades` / `!loadnade`

**训练工具**
- `!noclip` / `!god` / `!impacts` / `!respawn`
- `!restart`（默认受 KeranStrat 保护阻止）

**BOT**
- `!bot` / `!tbot` / `!ctbot` / `!placebot` / `!removebot`
- `!clearbots` / `!freezebot` / `!swapbot`
- 按 UserId 归属，只操作本插件创建的 BOT

**基础设施**
- JSON 配置 + 无 BOM 原子写入
- SteamID64 权限白名单 + 指令冷却
- 热重载完整清理（定时器、玩家状态）
- KeranStrat 共存：指令前缀隔离、BOT 归属隔离、回合重置保护

### 已知问题

- `!rethrow` 在移动中投掷时轨迹不能 100% 复现（重投不带水平速度）
- `!impacts` 只有控制台坐标输出，无 3D 标记（CSS 1.0.376 未暴露 DebugDraw）
- 游戏内功能需真人验证（见 README 第七、八节）
