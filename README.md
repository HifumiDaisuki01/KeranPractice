# KeranPractice

Keran Technology © 2026 · CS2 社区服务器训练插件 · **v2.0.0**

提供真实道具重投、私人站位与道具库、个人道具补充，以及弹着点和轨迹可视化。Linux 社区服已部署，服主实测反馈全部功能正常。

[下载正式版](https://github.com/HifumiDaisuki01/KeranPractice/releases/tag/v2.0.0) · [安装与回滚](docs/INSTALL-UPGRADE-ROLLBACK.md) · [完整验证记录](VALIDATION-v2.0.md)

## 快速上手

1. 管理员配置 SteamID64 白名单后进服，输入 `!practice on`。
2. 投一颗道具，换位置输入 `!rethrow`，即可观察同一投掷的真实效果。
3. `!savenade A` 保存；`!loadnade A` 返回保存站位并选中；走远后继续 `!rethrow`。
4. `!impacts on` 开弹着点；`!trajectory on` 开轨迹。结束输入 `!practice off`。

普通玩家需服主在配置中批准训练环境，并允许相应个人指令。

## 指令

| 指令 | 功能 |
|---|---|
| `!practice [on/off]` | 个人训练会话；退出恢复本插件临时状态，不更改全局游戏模式 |
| `!phelp` | 分行中文帮助 |
| `!noflash [on/off]` | 个人免闪 |
| `!savepos [名]` / `!tp [名]` | 保存/传送私人位置与视角，默认名default，零速度传送 |
| `!poslist` / `!delpos 名` | 列出/删除本地图私人位置 |
| `!last` | 返回上次真实投掷站位 |
| `!back` | 在上次传送前后的位置往返 |
| `!rethrow` | 重投已选道具或最近真实投掷，不移动玩家；新真实投掷取消存档选择 |
| `!clear` | 清理可确认属于自己的飞行道具、烟雾、火区 |
| `!autoclear on/off [秒]` | 延迟清理本人上一批道具，保留新批次 |
| `!clearall` | 管理员清理可证明归属的道具，跳过BOT、外部标签和未知归属 |
| `!savenade 名` / `!loadnade 名` | 保存完整物理快照；加载时返回站位并选中供重投 |
| `!nades` / `!delnade 名` | 列出/删除当前地图私人道具 |
| `!nadesupply [on/off]` | 补齐烟、双闪、HE、阵营火和诱饵；默认普通KP自动补、KS/KSV仅手动、KSD拒绝 |
| `!autosupply [on/off]` | 管理员个人跨模式自动补充，含KS/KSV/KSD；on启用个人会话，off取消任务，无参数切换 |
| `!noclip [on/off]` / `!god [on/off]` | 管理员飞行/无敌，退出/死亡/切队/回合结束恢复；KSD禁止 |
| `!respawn` | 请求复活死亡的T/CT玩家；KSD禁止 |
| `!spawn [序号]` / `!tspawn [序号]` / `!ctspawn [序号]` | 传送当前阵营/T/CT出生点，不切队；序号从1开始，!back可返回；KSD禁止 |
| `!impacts [on/off]` | 个人蓝色3D弹着点十字，默认3秒；无参数切换 |
| `!trajectory [on/off]` | 本人真实道具轨迹，原投绿/重投橙，线段默认12秒；开启后投新道具或重投 |

`!autosupply`只改变本人道具补发，不改变枪械弹匣/备用弹药，不给BOT或他人补发；不会开放KSD自动复活、无敌等工具。退出训练、离线或切图重置跨模式选择。

快捷绑定示例（会覆盖对应按键原绑定）：

```cfg
bind "MOUSE3" "css_rethrow"
bind "F6" "css_autosupply"
bind "F7" "css_impacts"
bind "F8" "css_trajectory"
```

## 安全与资源边界

- 真实原生工厂捕获/重演，不模拟鼠标、不用未初始化的手工投掷物替代。逐函数校验唯一签名、ELF可执行段、指令窗口、call目标和已加载内存；整库哈希及旧偏移仅作记录。异常函数单独关闭，不能保证任意未来更新都自动兼容。
- 私人位置和完整道具按SteamID、地图隔离，JSON原子保存，默认各64条。保留旧位置迁移和源文件；旧道具缺少物理状态时不伪造迁移。
- 私人可视化通过CheckTransmit过滤；默认每人96/全服256条实体，使用有上限的隐藏复用池。到期隐藏，关闭/退出/断线/地图/回合/卸载删除；不全图删除其他插件beam。不修改sv_showimpacts或全服无限弹药。
- 不修改KeranStrat源码、规则和BOT；BOT管理尚未开放。高风险命令保留管理员保护。

## 环境与构建

Linux CS2，Metamod、CounterStrikeSharp API **1.0.376**，.NET **10**。Windows原生重投未实现；不将Linux验证推断为Windows支持。

```sh
dotnet build src/KeranPractice/KeranPractice.csproj -c Release
dotnet test tests/KeranPractice.Tests/KeranPractice.Tests.csproj -c Release
python3 tools/package.py --formal
```

77项自动测试通过；真人结论来自服主报告，服务端探针与未单独测量的场景在[验证记录](VALIDATION-v2.0.md)中分开标记。旧Release与v1 Git历史保留。
