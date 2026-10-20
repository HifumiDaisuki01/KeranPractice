# KeranPractice

> CS2 社区服务器训练插件 · Keran Technology © 2026

轻量、独立、可随时删除的 CS2 训练插件。提供道具练习、站位保存、闪光免疫、BOT 训练等能力，**不依赖数据库、不修改游戏文件、不与 KeranStrat 冲突**。

---

## 一、兼容性

| 项 | 要求 |
|---|---|
| CS2 服务端 | **1.41.9.1**（ServerVersion 2000933） |
| CounterStrikeSharp | **1.0.376**（.NET 10） |
| Metamod:Source | **2.0.0-dev+1481**（API 18） |
| 运行时 | **.NET 10**（跟随 CSS 1.0.376，不能降到 .NET 8） |
| 系统 | Linux x86_64 / Windows x86_64 |

> ⚠️ 插件目标框架是 `net10.0`，因为 CSS 1.0.376 本身就是 .NET 10。降到 net8.0 会加载失败。

---

## 二、安装

### Linux

```bash
cd /path/to/cs2/game/csgo
unzip KeranPractice-1.0.0.zip -d .
chown -R steam:steam addons/counterstrikesharp/plugins/KeranPractice
systemctl restart cs2        # 或 RCON: css_plugins load KeranPractice
```

### Windows

把压缩包解压到 `game\csgo\`，确保目录变成：

```
game\csgo\addons\counterstrikesharp\plugins\KeranPractice\KeranPractice.dll
game\csgo\addons\counterstrikesharp\configs\plugins\KeranPractice\KeranPractice.json
```

重启服务器，或控制台执行 `css_plugins load KeranPractice`。

### 验证

```
css_plugins list        # 应出现 "KeranPractice" (1.0.0)
```

日志里会打印：

```
[KeranPractice] 已加载 v1.0.0｜配置: ...｜数据: ...
```

### 卸载

删除两个目录即可，插件不写注册表、不改游戏文件：

```
addons/counterstrikesharp/plugins/KeranPractice/
addons/counterstrikesharp/configs/plugins/KeranPractice/
```

---

## 三、指令表

所有指令都支持 `!xxx` 和 `css_xxx` 两种写法。**首次使用先 `!practice` 进入训练会话**（可在配置里关掉这个限制）。

### 训练会话

| 指令 | 说明 |
|---|---|
| `!practice [on/off]` | 开启/关闭训练会话；关闭时会恢复被插件改过的状态 |
| `!phelp` | 中文帮助菜单 |

### 闪光免疫

| 指令 | 说明 |
|---|---|
| `!noflash [on/off]` | 开关个人闪光免疫；开启瞬间清除已生效的闪白 |

> 只影响自己。不删除闪光弹、不改变爆炸、不影响其他人。

### 位置与传送

| 指令 | 说明 |
|---|---|
| `!savepos [名称]` | 保存当前位置（坐标 + 视角）；不填名称存为 `default` |
| `!tp [名称]` | 传送；不填传送到 `default`；会清零速度防惯性 |
| `!poslist` | 列出本图自己保存的位置 |
| `!delpos <名称>` | 删除位置 |
| `!last` | 回到上一颗道具的投掷位置与视角 |
| `!spawn` | 回到本方出生点 |
| `!tspawn [编号]` | 传送到 T 出生点（可指定第几个） |
| `!ctspawn [编号]` | 传送到 CT 出生点 |

### 投掷物

| 指令 | 说明 |
|---|---|
| `!rethrow` | 重投上一颗道具（同一位置、视角、类型） |
| `!clear` | 清除场上所有投掷物与火焰 |
| `!autoclear [on/off]` | 投掷后自动清理开关（延迟可配） |
| `!savenade <名称>` | 保存当前点位为道具点位 |
| `!nades` | 列出本图已保存的道具点位 |
| `!loadnade <名称或编号>` | 传送就位 + 发放对应道具 |

### 训练工具

| 指令 | 说明 |
|---|---|
| `!noclip [on/off]` | 自由飞行 |
| `!god [on/off]` | 无敌 |
| `!impacts [on/off]` | 弹着点提示（控制台输出坐标，5 条/秒限流） |
| `!respawn` | 重新复活 |
| `!restart` | 回合重置（默认受 KeranStrat 保护阻止） |

### BOT

| 指令 | 说明 |
|---|---|
| `!bot` / `!tbot` / `!ctbot` | 添加 BOT（自动/指定阵营） |
| `!placebot` | 把 BOT 放到你的位置 |
| `!removebot` | 移除当前指向的 BOT |
| `!clearbots` | 清理**本插件创建的** BOT |
| `!freezebot` | 冻结/解冻 BOT |
| `!swapbot` | 与 BOT 交换位置 |

> BOT 按 UserId 归属，只操作自己创建的，不会动其他插件的 BOT。

---

## 四、配置

路径：`addons/counterstrikesharp/configs/plugins/KeranPractice/KeranPractice.json`
首次启动自动生成，修改后**重载插件**生效（或重启）。

```json
{
  "ConfigVersion": 1,
  "Admins": ["76561199404249776"],
  "PublicCommands": ["noflash", "savepos", "tp", "..."],
  "AdminOnlyCommands": ["restart", "clearall", "give", "admin"],
  "RequirePracticeMode": true,
  "CommandCooldownMs": 300,
  "AutoClearDelaySeconds": 3.0,
  "AutoClearDefault": false,
  "AllowRoundRestart": true,
  "TeleportOnRethrow": true,
  "RethrowHoldMs": 120,
  "GodModeDefault": false,
  "ImpactMarkerSeconds": 8.0,
  "MaxImpactMarkers": 80,
  "MaxPositionsPerMap": 64,
  "MaxNadesPerMap": 200,
  "RespectKeranStrat": true
}
```

| 字段 | 说明 |
|---|---|
| `Admins` | SteamID64 白名单，拥有全部权限 |
| `PublicCommands` | 普通玩家可用的指令（去掉 `css_` 前缀） |
| `AdminOnlyCommands` | 仅管理员可用 |
| `RequirePracticeMode` | 是否必须先 `!practice`（`!practice` / `!phelp` 豁免） |
| `CommandCooldownMs` | 同一玩家两条指令的最小间隔，防刷 |
| `AutoClearDelaySeconds` | `!autoclear` 的清理延迟 |
| `AllowRoundRestart` | 是否允许 `!restart` |
| `TeleportOnRethrow` | `!rethrow` 是否传送回原点（最准） |
| `RethrowHoldMs` | 模拟投掷按住时长（影响力度） |
| `RespectKeranStrat` | 开启时 `!restart` 直接拒绝，避免中断录制 |

### 数据文件

```
addons/counterstrikesharp/plugins/KeranPractice/data/
├── positions.json    位置存档（按 SteamID + 地图隔离）
└── nades.json        道具点位（按 SteamID + 地图隔离）
```

- 原子写入（临时文件 + rename），不会写坏存档
- 存档损坏时自动备份为 `.corrupt-时间戳`，不会直接覆盖
- **跨玩家隔离**：`!poslist` / `!tp` 只能看到和访问自己的存档

---

## 五、与 KeranStrat 共存

本插件**独立运行**，不修改 KeranStrat 任何文件。

| 兼容点 | 做法 |
|---|---|
| 指令不冲突 | 用 `css_` 前缀，`!ks` / `!ksd` / `!ksv` 完全独立 |
| BOT 隔离 | 按 UserId 记录自己创建的 BOT，只操作自己的 |
| 不抢会话 | 不接管录制、回放、切图、X 光 |
| 不擅自重启回合 | `!restart` 在 `RespectKeranStrat: true` 时直接拒绝 |
| 多人隔离 | 每个玩家的状态/存档按 SteamID 分开 |

如果发现指令冲突，可在配置的 `CommandAliases` 里改备用名（预留字段，当前版本注册固定名）。

---

## 六、已知限制

| 功能 | 限制 | 原因 |
|---|---|---|
| `!rethrow` | 移动中投掷的轨迹不能 100% 复现 | 重投走引擎真实物理，但玩家的水平速度不参与；静止投掷最准 |
| `!impacts` | 没有 3D 标记，只有控制台坐标 | CSS 1.0.376 未暴露 DebugDraw API，用实体标记会造成大量实体堆积 |
| `!god` | 只关闭 `TakesDamage` | 部分模式（如坠落/击杀奖励）仍可能造成伤害 |
| `!restart` | 默认被 KeranStrat 保护阻止 | 回合重置会中断录制 |
| `!noclip` | 出生后自动恢复 | 出生会重置 MoveType，插件在 spawn 后 0.2 秒重新应用 |
| 存档 | 位置/道具按 `SteamID + 地图` 隔离 | 换图后看不到上一张图的存档（设计如此） |

---

## 七、测试记录

> 如实记录：哪些验证过，哪些没验证。

### 已验证（服务端）

| 项 | 结果 |
|---|---|
| `dotnet build -c Release` | ✅ 0 错误 0 警告，产出 `KeranPractice.dll` 61,440 字节 |
| 插件加载 | ✅ `[#12:LOADED] "KeranPractice" (1.0.0)` |
| 命令注册 | ✅ **30/30** 条命令全部存在 |
| 配置生成 | ✅ 首次启动自动生成，无 BOM |
| 热重载 | ✅ 卸载 → 加载，无异常、无重复注册 |
| 卸载清理 | ✅ 玩家状态恢复、定时器停止 |

### 未验证（需真人进服）

以下是**必须在游戏内由真人验证**的项，本环境没有可用的 CS2 客户端：

- `!noflash` 的闪光免疫实际效果（`EventPlayerBlind` + `FlashDuration` 清零）
- `!savepos` / `!tp` 的传送落点与视角
- `!rethrow` 的投掷还原程度
- `!savenade` / `!loadnade`
- `!noclip` / `!god` 的实际表现
- `!bot` 系列（BOT 创建、归属、冻结、交换）
- 多人同时训练的隔离
- 与 KeranStrat 同时在线时的互不干扰

**不要把这些当作已验证。** 需要在真实服务器上按第八节逐条走一遍。

---

## 八、建议的验收步骤

进服后按顺序执行：

```
!practice
!phelp                       # 帮助能出来
!savepos test1
!tp test1                    # 能回到原位
!noclip                      # 能飞
!noclip
!noflash                     # 让队友扔闪，确认不白
!clear                       # 场上投掷物被清掉
（扔一颗烟）
!rethrow                     # 能重投
!savenade smoke1
!nades
!loadnade smoke1
!bot
!placebot
!freezebot
!swapbot
!clearbots
!practice off                # 临时状态恢复
```

---

## 九、目录结构

```
KeranPractice/
├── KeranPractice.csproj
├── KeranPracticePlugin.cs        入口、命令注册、事件
├── Config/PracticeConfig.cs      配置模型
├── Core/
│   ├── PlayerState.cs            每玩家运行时状态
│   └── PermissionService.cs      权限与冷却
├── Models/Records.cs             存档模型
├── Systems/
│   ├── FlashSystem.cs            闪光免疫
│   ├── PositionSystem.cs         位置/传送/出生点
│   ├── GrenadeSystem.cs          投掷物
│   ├── ToolsSystem.cs            noclip/god/impacts/practice/help
│   └── BotSystem.cs              BOT 管理
└── Utils/JsonStore.cs            原子 JSON 读写
```

---

## 十、许可

MIT License · Keran Technology © 2026

依赖 [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)（MIT）。
