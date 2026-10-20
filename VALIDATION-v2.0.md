# KeranPractice v2.0 验证记录

状态：**Phase 1 审查构建。未完成真人验收，不是正式2.0 Release，不替换正式服v1.x。**

## 实际环境

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
| savenade/loadnade等 | 未开放；旧站位不足以伪造完整物理记录 | 否 | 否 | 尚未实现，Phase2不掩盖Phase1缺口 | 尚未验证 |
| god/noclip/respawn/spawn | 未开放 | 否 | 否 | Phase2待核心真人验收后继续 | 尚未验证 |
| BOT/impacts/轨迹可视化 | 未开放；诊断轨迹JSON不算3D可视化 | 否 | 否 | Phase3；BOT管理实际禁用 | 尚未验证 |

自动测试26项全部通过；证据见research/evidence/core.trx和构建日志。单测不是引擎或真人验证。

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

最终DLL SHA256：`dd4e7bca5f786c0dabc42d42eb2f026454f71caa98f5a6f917c3795a6195e850`。十项工厂夹具全部有对应引爆/效果事件；五个同状态重复样本终点差为0。两次烟雾均DidSmokeEffect=true，两次闪光均有player_blind，两次HE均有health damage，两次燃烧弹均有火区与伤害，诱饵均decoy_started。详见research/evidence/native-fixture-final.json；它明确RealHumanThrows=false。正式服旧DLL和配置hash保持，见isolation-and-environment.json。
