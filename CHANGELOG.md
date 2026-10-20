# Changelog

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

## 2.0.0 review — rewrite/v2.0

- Replaced v1 architecture with independent domain/persistence, sessions/access policy and audited Linux native capture/replay.
- Phase1: noflash, private positions, last/back, native rethrow, scoped cleanup and cancellable batch autoclear.
- Current build: zero compiler warnings/errors, 26 automated tests, ten isolated synthetic native throws with actual effects and five zero-error repeat landings. These are NOT human throw acceptance.
- Phase2/3 remain unimplemented pending Phase1 acceptance; no BOT management, fake impacts or stable Release. Formal server v1 DLL/config unchanged.
