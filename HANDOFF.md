# KeranPractice 交接 — 2026-10-21

v2.0.0 已合并 main、发布正式版。备份前 main：5d94b8053ac8ec4a2062c8dba6ad0bc5737a3ea3；此次只追加交接文档，不修改插件或 Release。

## 已验证及边界

77 项单元测试通过，Linux 部署，服主反馈全部功能正常。逐场景证据以 VALIDATION-v2.0.md 为准，未逐项测量的轨迹精度、多人边界不能补写为通过。

核心：真实原生手雷工厂捕获/重投、归属安全清理、个人免闪、私人站位和道具库、自动补充、训练工具、弹着点/轨迹。Windows 原生重投及 BOT 管理尚未实现。

小更新兼容已修复：整库 SHA256 仅作信息；逐函数检查 ELF 可执行段、唯一签名、重定位掩码指令窗口、call 目标及运行中内存。不能改回整库 SHA 硬门禁，也不能放弃所有 ABI 检查。未知函数单独停用。

可视化采用有上限的个人实体池与 CheckTransmit 过滤；不能恢复无上限创建实体或每次按观察者反复查找原生实体的旧路径。

## 构建及恢复

CounterStrikeSharp API 1.0.376，.NET 10；当次 Linux SDK 10.0.402 位于开发机 /root/ks-help-work/sdk/dotnet，服务端运行时曾实查为 10.0.3。后续先重新检查，不保证机器永远保持该版本。

```sh
dotnet build src/KeranPractice/KeranPractice.csproj -c Release
dotnet test tests/KeranPractice.Tests/KeranPractice.Tests.csproj -c Release
python3 tools/package.py --formal
```

原生 ABI 审查辅助 Capstone 5.0.6 仅开发依赖。参考 MatchZy、Phi-S 和 CSS 的出处、许可见仓库已有文档。

正式 ZIP SHA256：9ed19018ec909c201c0b857c1dddf3b44f63ca6b64aed4758170fecdc8457c5f。

私人 CS2-SERVER 的 linux-development-* 保存 /root/keranpractice-v2 外围的审查脚本、指令窗口和研究文本；Windows 桌面副本也独立保存。数据迁移、安装和回滚见 docs/INSTALL-UPGRADE-ROLLBACK.md。服务器玩家数据不在本次只读本地环境备份的覆盖范围，不应误认为已备份正式服全部存档。

## 下一位立即执行

先核对 Git main、正式版和 VALIDATION-v2.0.md；当前没有授权新功能。恢复环境后先离线构建，不自动部署。保留 KS/KSV 默认仅手动补道具及 KSD 复活隔离。服主明确允许个人 autosupply 跨模式，不能据此开放 KSD 的无敌/复活。
