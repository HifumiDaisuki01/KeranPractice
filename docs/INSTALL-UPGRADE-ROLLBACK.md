# 安装、迁移和回滚

这是 main 的 v2.0.0 正式发行；旧Release与Git历史保留。

## 安装

1. Linux CS2 + CSS 1.0.376 / .NET10；按服主批准的维护窗口部署；本次按服主要求直接热更新主服，不复制游戏或运行时。Windows原生重投未验证，自动禁用。
2. 备份现有 `addons/counterstrikesharp/plugins/KeranPractice/` 与 `configs/plugins/KeranPractice/`，包括全部data。不要同时加载旧新两个插件，否则相同命令会冲突。
3. 先 `css_plugins unload KeranPractice`，解压ZIP的game/csgo/到对应游戏目录；插件文件为KeranPractice.dll、KeranPractice.Core.dll和deps.json，模块内gamedata/keranpractice-v2.json必须一起复制；然后 `css_plugins load KeranPractice`。无需新增自编译.so，但重投确实调用CSS封装的原生引擎函数，绝非纯托管仿真。
4. 配置新 `ConfigVersion:2`，填写Admins里的SteamID64。`TrainingEnvironmentApproved` 默认false；只有服务器所有者明确认定训练环境后才设true，普通玩家仍需个人practice会话及允许的个人命令。
5. 控制台 `kpv2_status` 应显示native ready或明确disabled原因。native禁用时noflash/位置可继续使用；没有签名不猜地址、不模拟鼠标、不退回CreateEntityByName。
6. 真人先 !practice on，再按VALIDATION-v2.0.md测试矩阵。日志和诊断路径在插件data目录。未取得真人记录不得宣称核心验收完成。

## 安全配置迁移

旧版PublicCommands、AdminOnlyCommands、AllowRoundRestart等危险授权不用于v2。
旧Admins白名单可保留，但ConfigVersion<2时公共命令授权被关闭。
高风险clearall、god/noclip、BOT/回合命令即使写入PublicPersonalCommands也不能获得公共权限。当前已注册管理员god/noclip；BOT/回合命令未注册。

## 数据迁移

v2写入 `plugins/KeranPractice/data/archive-v2.json`，不覆盖旧positions.json。
初次启动读取同目录原positions.json，按地图、SteamID和名字导入，保留原文件，并写入迁移标记；现有v2同名数据优先。损坏或未知版本停止加载并保留文件，不当成空数据库覆盖。

旧nades.json只有站位/角度/holdMs，没有真实初始速度、角速度或原生状态。不能伪造完整投掷记录；本次原样保留旧nades.json，尚不导入为可重投道具。后续需真人重新投掷采集。

## 回滚

先卸载KeranPractice；备份当前data，再恢复旧DLL/deps/配置。不要用旧备份覆盖新保存的archive-v2.json；回退v1时保留v2存档供以后使用。重新加载并检查旧插件版本。
主服已有部署前备份及 `/root/keranpractice-v2-main-deploy/rollback.sh`（回退v1）和 `rollback-phase2.sh`（回退Phase1+补充）。本次未修改KeranStrat源码/配置/战术数据。

## 更新游戏后

gamedata/keranpractice-v2.json是本插件私有的版本审计manifest，不是可随意放进CSS全局gamedata目录的通用JSON。
其中SHA256和旧偏移用于诊断，不再作为整库硬锁。每个工厂分别校验唯一签名、ELF可执行段、约500字节重定位屏蔽的已审计指令、直接调用目标及已加载内存；无关变化或函数整体移动可继续启用，实际函数变化暂停该函数。不能仅改hash或扩大通配符强行放行，需要重新核对ABI和实际引爆效果。

正式安装包把示例配置放在examples，解压不会覆盖当前配置或玩家数据。升级只替换DLL/Core依赖/deps与模块gamedata；备份后卸载/加载KeranPractice即可，不需要重启CS2或切图。
