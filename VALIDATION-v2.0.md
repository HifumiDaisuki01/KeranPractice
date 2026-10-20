# KeranPractice v2.0 验证记录

状态：**Phase 2 审查构建，已按服主授权热更新主服。Phase 1 核心收到服主真人正常反馈；新增功能尚待逐项真人验收。未发布正式 Release。**


## 最新进度：Phase 1 真人反馈、道具补充与 Phase 2

- 服主在本次会话明确反馈“完美！全部正常工作”。记作 Phase 1 核心真人反馈；没有获得每种按键、跑跳投、双人火区清理的逐项测量，不将其推断为全部专项通过。下方初始夹具/历史记录保留其原测试边界。
- 主服已授权使用热卸载/加载部署，无 CS2 重启，无地图/模式/启动参数修改。隔离实例停止并删除（约139MiB），后续没有创建新测试实例。
- 道具补充：真实捕获后0.65秒，原连接+Pawn+队伍+会话+模式二次检查；重投不触发；只写真实手雷VData指定的弹药槽。KS/KSV自动补充禁用，手动补齐允许；KSD和未知状态禁用。单测覆盖模式矩阵与道具类型，不能代替真人库存测试。
- 原生捕获/工厂的算法与签名未变。道具库保留原快照，加载后重投仍使用当前玩家作为投掷者，不修改原历史；新真实投掷取消选择。
- noflash和原位置传送的底层行为保留。退出新增恢复本插件更改的飞行/伤害状态，不替其他插件重置全局规则。
- 48项单元测试通过，Phase2编译零警告/零错误；新增实际游戏行为暂标“尚未验证”。

### 新功能真人验收步骤

1. `!practice on`，普通KP会话投烟/双闪/HE/火/诱饵，观察补回；枪械射击/换弹仍消耗。KS/KSV同样投掷应消耗，只有 `!nadesupply` 手动补；KSD命令应拒绝。
2. 真实投烟 `!savenade A`，走远、重连后 `!practice on`、`!loadnade A` 返回站位，走远 `!rethrow`；核对烟雾。新投HE后 `!rethrow` 应重投HE。
3. 另一玩家不能列出、加载、删除A；另一个地图隔离；同名覆盖不增加数量；重启持久化测试仅在用户许可维护时进行。
4. 管理员 `!noclip on/off` 检查飞行和落地；`!god on/off` 检查伤害状态恢复；`!practice off`、切队、死亡、回合结束检查恢复；普通玩家不得获得这两项权限。
5. 死亡T/CT `!respawn`，存活拒绝；KSD禁止复活。`!spawn` / `!tspawn 1` / `!ctspawn 1`，位置与视角恢复、无惯性、!back往返；序号越界拒绝。
6. 与KeranStrat并行录制/回放；KSD CT死亡不提前复活、BOT不被修改。需要真实联机验证，未用命令注册代替通过。

## 初始 Phase 1 测试环境（历史记录）

- 正式服只读核查：Linux Debian12、CS2 1.41.9.1/14191、CSS 1.0.376 (653d651)、随CSS安装的.NET10.0.3。
- 构建SDK .NET10.0.402，CSS API NuGet固定1.0.376。编译结果零错误/零警告。
- 测试：同一Linux主机的独立mount namespace/27025实例；独立addons/cfg/logs，CSS只加载KeranPractice v2一个插件。未加载KeranStrat，未修改它的文件或规则。
- 当前Windows开发机常用Steam目录没有CS2客户端；未执行真实图形客户端测试。另一台用户电脑是否进行后续测试不在自动报告中冒充成功。

## 分项状态

| 功能 | 实现 | 已编译 | 已加载 | 服务端/自动测试 | 真人 |
|---|---|---|---|---|---|
| noflash | EventPlayerBlind Pre + 按需0.1s清除本人的FlashDuration | 是 | 是 | v2个人开关尚待真实玩家；旧版行为是用户已验证参考 | 尚未验证 |
| savepos/tp/poslist/delpos | SteamID+地图JSON，原子落盘，零速度传送 | 是 | 是 | 存储、隔离、覆盖、重启读取与迁移自动测试通过；实际玩家传送待验收 | 尚未验证 |
| last/back | 真实投掷站位；另有双点往返，不混用语义 | 是 | 是 | back往返/跨图保护通过单测；真人last/传送待验收 | 尚未验证 |
| rethrow五种 | 已审计原生工厂，完整物理状态+重入保护，无客户端模拟/手工实体回退 | 是 | 是 | 合成工厂投掷有效果；固定状态重复落点0偏差，见下表。真人采集至重演端到端未验证 | 尚未验证 |
| clear | 仅ledger确认的连接/地图/serial handle；inferno使用引擎owner，歧义保留 | 是 | 是 | ledger隔离测试通过；真正双玩家烟雾/火区清理待验收 | 尚未验证 |
| clearall | 仅管理员；跳过BOT/外部标签/未知归属，避免破坏KSD | 是 | 是 | 权限单测通过；全图无条件删除刻意不支持；实际多插件并行待验收 | 尚未验证 |
| autoclear | 延迟清理本人旧批次，新批次不选中；取消与代次检查 | 是 | 是 | 批次/连接/地图选择单测通过；实际延迟清理待验收 | 尚未验证 |
| practice/phelp | 个人会话与中文帮助，不更改全局模式；所有者先批准公共训练 | 是 | 是 | 授权策略单测通过；真实玩家退出状态待验收 | 尚未验证 |
| savenade/loadnade/nades/delnade | 完整快照持久化、SteamID+地图隔离、64条默认上限、同名覆盖、load返回站位并选中 | 是 | 见最新部署记录 | 9项存储/迁移/重启/隔离测试通过 | 尚未验证 |
| god/noclip/respawn/spawn | Pawn句柄绑定、权限、原状态恢复；KSD禁止 | 是 | 见最新部署记录 | 编译通过，实际移动/伤害/复活/传送待真人测试 | 尚未验证 |
| BOT/impacts/轨迹可视化 | 未开放；诊断轨迹JSON不算3D可视化 | 否 | 否 | Phase3；BOT管理实际禁用 | 尚未验证 |

初始自动测试26项全部通过；证据见research/evidence/core.trx和构建日志。单测不是引擎或真人验证。

## 原生投掷集成证据

首次夹具对每类以固定位置、线速度、角速度创建一颗原生投掷物，再以同状态重复一颗。**“第一颗”也是工厂合成，不是真人自然投掷**；没有声称验证了采集玩家输入的完整链路。

| 类型 | 两次终点偏差 | 实际证据 | 首轮结果 |
|---|---:|---|---|
| smoke | 0 units | smokegrenade_detonate + DidSmokeEffect=true | 有效烟雾状态 |
| flash | 0 units | flashbang_detonate + player_blind事件（BOT目标） | 引爆/致盲事件 |
| HE | 0 units | hegrenade_detonate + 实际health damage 82/85 | 引爆且有伤害，伤害值不声明完全相同 |
| molotov | 0 units | inferno_startburn、16处火焰、health damage 40/40 | 火区与燃烧伤害 |
| decoy | 0 units | decoy_started | 进入工作状态 |

首轮结果不可代替最终二进制复测；最终结果单独保存在native-fixture-final.json，并记录插件DLL SHA256。重复样本很少，不外推为所有地图/投掷状态精度。

## 真人验收步骤（仍待执行）

1. 确认测试服只有本插件处理这些命令，配置SteamID管理员；!practice on。
2. Dust2固定站位依次正常投出五类道具，离开原位置输入!rethrow，确认不用拿雷、不用按鼠标、不传送玩家。每类连续重投至少3次；保存原始与重演轨迹/落点偏差。
3. 左键、右键轻抛、双键分别检查ThrowStrength、初速与落点；跑投、跳投分别记录误差，不把固定样本0偏差扩写为跑跳投成功。
4. 两名真人各投不同道具，A执行!clear/!autoclear不清B，验证空中道具、烟雾画面和正在伤害的火区；归属不明的火区必须保留并明确提示。
5. 录制重投后再次重投，检查原始snapshot.Id不变，重投未被记录为新原始；死亡换队重生时新Pawn不会获得旧连接任务。
6. noflash开关双人对照；位置命名/同名覆盖/重启/跨玩家访问与删除；back多次往返。
7. 最后才在受控并行环境加载未修改的KeranStrat，运行KS/KSV/KSD；确认没有删/冻/抢BOT。当前实例不加载KS，因此不能声称已经通过此项集成。

## 已知限制/失败与恢复

- 初次隔离实例启动因Metamod需要正确工作目录/库路径失败并退出；尚未执行插件调用，不算成功。修正后完成首轮引擎测试。
- 隔离瞬时systemd unit在停止后被回收，首次更新后restart找不到unit；重新systemd-run创建后继续，没有改正式服服务。
- native参数来自当前特定Linux服务端审计；Windows未验ABI，关闭重投；游戏更新或签名变化关闭重投。
- 当前角速度读到了实际生成后的首帧值；紧贴墙首次碰撞前后的精度需实际轨迹比对，不承诺所有特殊情况完全相同。
- fire归属多个候选或owner未知时不猜，不按位置找最近实体；个人clear会保留这些实体。这是安全边界，需真人测评覆盖率。
- 某些道具类型的引擎事件不包含完整serial，本插件仅在唯一匹配观察记录时确认；事件缺失/歧义超时记录未通过，不发成功消息。
- 未验证全量功能，不发正式Release，不把自动夹具写成真人测试。

## 最终构建复测

初始Phase1最终DLL SHA256：`dd4e7bca5f786c0dabc42d42eb2f026454f71caa98f5a6f917c3795a6195e850`。十项工厂夹具全部有对应引爆/效果事件；五个同状态重复样本终点差为0。两次烟雾均DidSmokeEffect=true，两次闪光均有player_blind，两次HE均有health damage，两次燃烧弹均有火区与伤害，诱饵均decoy_started。详见research/evidence/native-fixture-final.json；它明确RealHumanThrows=false。该夹具执行时正式服旧DLL和配置hash保持，见isolation-and-environment.json；后续已按授权热更新主服。

## Phase 2 主服部署检查

- 加载版本 `2.0.0-phase2-review`；`kpv2_status`：native=ready，bots=disabled。
- 12个插件均LOADED，包括KeranStrat0.5.5、模型插件和其余既有插件；`sv_infinite_ammo=0`。
- 只读检查时用户实际正在KSD运行中的会话，`kpv2_supply_status`返回manualBlocked=True、autoBlocked=True，与KSD状态一致。未为测试强制结束该会话或改变地图/模式。
- 新增命令通过AddCommand注册且插件加载正常；逐项真人调用结果仍待验收，不能据此标为功能通过。
- 主服保留部署前的插件、配置、data备份；未复制游戏、运行时或创建第二实例。
