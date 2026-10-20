# 安装、迁移和回滚

这是 rewrite/v2.0 的 Phase 1 审查构建，不是已完成真人验收的正式2.0发行。

## 安装

1. Linux CS2 + CSS 1.0.376 / .NET10；先用独立测试实例，不直接替换繁忙正式服。Windows原生重投未验证，自动禁用。
2. 备份现有 `addons/counterstrikesharp/plugins/KeranPractice/` 与 `configs/plugins/KeranPractice/`，包括全部data。不要同时加载旧新两个插件，否则相同命令会冲突。
3. 测试实例停服，解压ZIP的game/csgo/到对应游戏目录；插件文件为KeranPractice.dll、KeranPractice.Core.dll和deps.json，模块内gamedata/keranpractice-v2.json必须一起复制。无需新增自编译.so，但重投确实调用CSS封装的原生引擎函数，绝非纯托管仿真。
4. 配置新 `ConfigVersion:2`，填写Admins里的SteamID64。`TrainingEnvironmentApproved` 默认false；只有服务器所有者明确认定训练环境后才设true，普通玩家仍需个人practice会话及允许的个人命令。
5. 控制台 `kpv2_status` 应显示native ready或明确disabled原因。native禁用时noflash/位置可继续使用；没有签名不猜地址、不模拟鼠标、不退回CreateEntityByName。
6. 真人先 !practice on，再按VALIDATION-v2.0.md测试矩阵。日志和诊断路径在插件data目录。未取得真人记录不得宣称核心验收完成。

## 安全配置迁移

旧版PublicCommands、AdminOnlyCommands、AllowRoundRestart等危险授权不用于v2。
旧Admins白名单可保留，但ConfigVersion<2时公共命令授权被关闭。
高风险clearall、god/noclip、BOT/回合命令即使写入PublicPersonalCommands也不能获得公共权限。当前构建未注册BOT/god/noclip/回合命令。

## 数据迁移

v2写入 `plugins/KeranPractice/data/archive-v2.json`，不覆盖旧positions.json。
初次启动读取同目录原positions.json，按地图、SteamID和名字导入，保留原文件，并写入迁移标记；现有v2同名数据优先。损坏或未知版本停止加载并保留文件，不当成空数据库覆盖。

旧nades.json只有站位/角度/holdMs，没有真实初始速度、角速度或原生状态。不能伪造完整投掷记录；本次原样保留旧nades.json，尚不导入为可重投道具。后续需真人重新投掷采集。

## 回滚

测试实例停服；把新插件目录移到备份位置，恢复旧DLL/deps/配置与原data；保留v2 archive-v2.json供日后继续测试，不覆盖旧数据。恢复后启动并检查旧插件版本。
本次隔离测试无需回滚正式服：正式服旧DLL和规则未改，KeranStrat源码/配置/战术数据未动。

## 更新游戏后

gamedata/keranpractice-v2.json是本插件私有的版本审计manifest，不是可随意放进CSS全局gamedata目录的通用JSON。
它包括Linux server二进制SHA256、每个签名及唯一匹配的文件偏移。游戏更新即停止原生重投；重新审计ELF可执行区、参数ABI和隔离引爆/伤害后再更新，不能只把新hash写进去。
