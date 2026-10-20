# KeranPractice v2.0

Keran Technology © 2026 · 独立重构 · `rewrite/v2.0`

**当前为 Phase 2 审查构建。Phase 1 核心已由服主真人反馈正常；Phase 2 新功能待逐项真人验收，不发布正式 v2.0.0。** 旧v1历史与Release保留。新代码位于src/，不在旧GrenadeSystem/BotSystem/ToolsSystem上叠补丁。

## 目前可测试命令

先 `!practice on`，结束 `!practice off`。普通玩家还需管理员明确开放训练环境；管理员SteamID64白名单不受公共授权限制。

| 命令 | 行为 |
|---|---|
| !phelp | 中文帮助 |
| !noflash [on/off] | 个人免闪，事件清除+仅有人启用时的0.1秒维护 |
| !savepos [名] | 按SteamID+地图保存私人站位、视角，默认名default |
| !tp [名] | 恢复坐标/视角并以零速度传送 |
| !poslist / !delpos 名 | 列出/删除自己的本图位置 |
| !last | 返回最后一次真实投掷的玩家站位，不是倒退历史索引 |
| !back | 在上次传送前与目标位置间往返，不是Refrag历史浏览 |
| !rethrow | 原生工厂重演已选存档或最近真实投掷；不移动玩家、不要求手持手雷 |
| !savenade 名 | 保存完整物理状态和站位；同玩家同地图同名覆盖 |
| !loadnade 名 | 选中私人存档并返回站位；随后可移动到别处 !rethrow；新真实投掷取消选择 |
| !nades / !delnade 名 | 列出/删除本图私人道具 |
| !nadesupply [on/off] | 无参数/on立即补齐烟、双闪、HE、火、诱饵；off关闭个人自动补充。普通会话自动补，KS/KSV只能手动补，KSD禁止 |
| !noclip [on/off] / !god [on/off] | 管理员自由飞行/无敌；退出/死亡/切队/回合结束/卸载恢复原状态；KSD禁止 |
| !respawn | T/CT死亡玩家请求复活；KSD禁止 |
| !spawn [序号] / !tspawn [序号] / !ctspawn [序号] | 当前阵营/T/CT出生点传送，序号从1开始；不切队，!back可返回；KSD禁止 |
| !clear | 仅清理本连接/地图中已证明属于自己的投掷物/烟雾/火区 |
| !autoclear on/off [秒] | 延迟0.1–120秒清理自己上一批，保留新批次；退出/断线/切图/回合结束取消 |
| !clearall | 管理员全局清理可证明为真人所有的道具；外部标签、BOT与未知归属保留并报告 |

**clearall有意不删未知归属/BOT火焰**，保护KeranStrat KSD。此行为边界记录为与无条件全图清理的差别，不假称能安全删除全图一切。

## 机制

- 核心数据、权限、历史、持久化与引擎桥接分离。
- 在当前已审计的五类原生工厂返回时采集真实位置、角度、线速度、角速度、类型、itemIndex、SteamID/Pawn、队伍；附带可读取时的武器ThrowStrength（不可读则为null）、、玩家速度/落地/蹲伏状态。真实物理状态用于重演，按键不用于反推轨迹。
- 投掷强度用于区分左键/轻抛/混合投掷，速度和落地状态记录移动/空中场景；真人各变体精度尚待测试。没有把所有投掷叫作一种成功场景。
- 重演带独立标记和重入保护，不进入原始历史；完整serial-bearing实体handle、连接代次、地图代次用于归属，不能按entity index复用猜所有权。
- 队列和每实体轨迹有硬上限；不做每帧全图扫描。仅主动clearall扫描少量指定实体族。诊断文件最多128份。
- 重投创建后只提示等待引爆；事件确认后记录生效。火区还需要真实inferno和非零FireCount；烟雾体积、伤害/致盲专项证据见验证报告，不能用“没异常”代替。

## 状态

48项自动测试通过；当前Linux版本五类原生工厂已完成隔离固定状态测试。真实烟雾效果、致盲事件、HE伤害、火区与燃烧伤害、诱饵启动分别记录。

这些是**合成工厂投掷/BOT目标集成测试，不是玩家真实左键/轻抛/跑跳投的完整验收**。普通双人隔离、真实烟雾画面清理、重连后的操作和KeranStrat并行真人测试仍待执行。

Phase 2 私人道具库和玩家工具已编译；真人使用、重连后加载重投、无敌伤害测试和飞行恢复均待验收。Phase 3 BOT/弹着点未开放。BOT 管理仍禁用，不占用、不创建、不冻结 KSD BOT。

道具补充只修改已识别手雷的弹药槽，不修改枪械弹匣/备用弹药，不设置 `sv_infinite_ammo`、购买规则或地图模式。KS/KSV/KSD 通过当前版本 KeranStrat 的只读兼容适配器识别，布局未知时停止补充/工具操作；不改 KeranStrat 文件。普通训练指仅 KeranPractice 会话，服务器处于 KS Practice 时仍按 KS 规则处理。

绑定示例：`bind "MOUSE3" "css_rethrow"`、`bind "F6" "css_nadesupply"`。同一键的新绑定会替换原绑定。

## 构建和交付

```sh
dotnet build src/KeranPractice/KeranPractice.csproj -c Release
dotnet test tests/KeranPractice.Tests/KeranPractice.Tests.csproj -c Release
python3 tools/package.py
```

依赖.NET10 SDK及CSS API 1.0.376（运行时由服务器提供）。构建要求零错误零警告，CI上传审查ZIP，不自动发正式Release。

[安装/数据迁移/回滚](docs/INSTALL-UPGRADE-ROLLBACK.md) · [逐项真实验证](VALIDATION-v2.0.md) · [技术研究与许可证](research/RESEARCH.md) · [v1历史说明](docs/HISTORY-v1.md)
