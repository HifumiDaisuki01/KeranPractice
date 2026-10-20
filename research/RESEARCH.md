# 实现研究与许可证

## 固定参考版本

精确commit见upstream-commits.json，引用默认分支仅作来源导航，不能默认为当前游戏可安全调用。

- MatchZy (MIT): https://github.com/shobhit-pathak/MatchZy ，核查PracticeMode.cs、GrenadeThrownData.cs、GrenadeProjectiles.cs、EventHandlers.cs和gamedata/matchzy.json。原生函数签名和ABI调用形式参考该MIT项目，保留许可与Shobhit Pathak署名。这里重新实现独立模块，没有复制整个训练/比赛架构。
- Phi-S (GPL-3.0): https://github.com/Phi-S/cs2-practice-mode ，只研究命令/历史与清理的思路，不复制其GPL代码。它的若干类型使用手工CreateEntity并设置属性，clear还会无条件删除inferno；这些实现不符合本任务可靠性/隔离要求，均未采用。
- CSS API v1.0.376: https://github.com/roflmuffin/CounterStrikeSharp ，实际读取MemoryFunction、DynamicHook、CHandle和当前schema字段定义；只使用API，不把其GPL源文件拷进插件。

## 并非“上游有就一定可靠”

本次读取的MatchZy会在原生工厂缺失时退回实体API，闪光弹也没有独立原生工厂；其若干调用传两次线速度而不是角速度，并在后续手动填属性。对本任务不沿用这些回退。

## 当前Linux原生审计

游戏1.41.9.1/14191，libserver.so SHA256：d73f5d3b360fce87708451a239f8961399b34a2f7e4354326a5948c8676cfad2。

四个MIT上游签名在当前文件各唯一匹配；第五个flash工厂通过flashbang_projectile字符串的RIP引用、相邻工厂调用形式、函数序言/返回路径和隔离引爆测试定位，未从HE实体名称猜地址。

| 工厂 | 文件偏移 | ELF虚拟地址（非运行时绝对地址） |
|---|---|---|
| smoke | 0x145d340 | 0x145e340 |
| he | 0xd25890 | 0xd26890 |
| molotov | 0x1459ea0 | 0x145aea0 |
| decoy | 0x145cba0 | 0x145dba0 |
| flash | 0xd24f30 | 0xd25f30 |

该ELF可执行PT_LOAD段地址比文件偏移高0x1000；不能直接把文件偏移当运行时函数地址。插件不硬编码这些地址进行call，而以审计文件hash+唯一签名+指定偏移双重核验，再让CSS解析函数。参数形式为SysV：4个向量/角度指针、pawn指针、itemIndex；smoke额外team。CSS负责封装调用和hook，无另一个自编译.so。

独立工厂用于有效引爆，而非Utilities.CreateEntityByName<CBaseCSGrenadeProjectile>()或ExecuteClientCommand模拟攻击。烟雾/火区和伤害必须用实际引擎证据验证。

##隔离测试

使用单独mount namespace，将addons/cfg/logs绑定到私有目录，复用只读游戏文件；只有本插件和CSS，独立端口27025。不加载KeranStrat或人物插件，不改变正式服运行规则。首轮因工作目录导致Metamod找不到真正server接口，启动失败；按正式服bin/linuxsteamrt64工作目录和库路径修正后加载正常。该失败记录不计为通过。

测试夹具在环境变量KERANPRACTICE_ISOLATED=1且server console调用时才允许运行。它创建原生合成投掷及BOT目标，不作为真人验收或BOT管理功能宣传。正式环境该入口拒绝执行。
