# KeranPractice v1.0.1 — 外部代码审查与 WorkBuddy 实测交接
> **不要把编译、指令注册或 LOADED 标记写成游戏内功能实测通过。**
> 此分支由 ChatGPT 经 GitHub Connector 修改；因为当前环境无 dotnet SDK、CS2 客户端或服务端，所有新功能均 **待 CI 编译及实机验证**。原 v1.0.0 不应自动部署。

## 原 v1.0.0 的实际错误（请 WorkBuddy 逐一复盘并承认）
1. `!rethrow` 假定 `ExecuteClientCommandFromServer("+attack")` 能模拟玩家投掷；没有记录真实投掷物速度、角速度和投掷方式。旧版不能声称轨迹可靠重现。
2. `!clear` / `!autoclear` 直接遍历并删除全图烟雾、火焰与投掷物，无所有者隔离。会干扰 KS/KSD。
3. `bot_add` 后 0.5 秒对全服新增 BOT 做差集归属判断；和 KeranStrat 竞争，`clearbots` 可误删他人的 BOT。
4. 旧配置 PublicCommands 默认开放 noclip/god/BOT 等；RequirePracticeMode 可由普通玩家自行执行。
5. 道具存档按地图而非 SteamID 查询；可读取同图其他玩家的保存道具。
6. JSON `File.Replace` 失败时先删源文件再移动临时文件，可能丢数据；写入失败依然报告成功。
7. `!practice off` 保留 noflash/autoclear，god 恢复强制 TakesDamage=true；旧回调可跨图运行。
8. 发布说明声称"不与 KeranStrat 冲突"缺乏双插件实测；配置项有未使用的选项（如别名）。

## 当前修复分支的实装范围
- 实施旧配置也无法绕过的高危命令权限锁；新增 AllowPublicPowerCommands，默认 false。
- BOT 管理增加安全熔断：EnableBotManagement 默认 false。试验性归属查验仍非强原子绑定，**不能在 KSD 时启用 BOT 管理**。
- `!clear` / `!autoclear` 改为按投掷者校验，仅清理已识别的本人投掷物；管理员独立 `!clearall` 为全图清理。源头不明的实体跳过，烟火效果可能仍残留。
- `!rethrow` 放弃不可靠的模拟键位，尝试读取 projectile InitialVelocity / AngVelocity，并直接重建实体。**这仍是需要严格核实的实验实现**：烟雾、燃烧弹、爆炸初始化可能需要引擎原生 factory；如果不完整，必须回退/禁用并记录原因。
- 保存失败不再一律报告成功，数据存储迁移到 csgo/data/KeranPractice（注意旧插件目录 `data/` 需手动迁移），损坏数据拒绝覆盖。
- 退出训练会话关闭 noflash/autoclear，修复 god 恢复值、卸载残留。
- 增加 GitHub Actions net10 还原与严格编译检查。

## WorkBuddy 必须执行的验收
### A. 编译与源码审查（必须先做）
1. 查看本 PR 的 diff；运行 GitHub Actions。所有编译错误、警告全部修完再合并。禁止声明未执行的测试通过。
2. 重点检查 Event OnEntitySpawned 的时序与 GrenadeThrown 的先后，`LastGrenade` 如果在实体事件后才创建则快照丢失。需要做正确的关联、跨回合清理和地图边界检测。
3. 校验 `EntityHandle.Raw` 类型、network handles 的 Controller / Thrower 实际 API，避免 C# 类型错或无效实体访问。
4. 旧数据从 `addons/counterstrikesharp/plugins/KeranPractice/data` 迁至 `game/csgo/data/KeranPractice`；给出迁移脚本并确认未丢数据。
5. 检查已有配置与新默认值的兼容。严禁在未确认时把旧的开放命令恢复。
6. `File.Move(overwrite:true)` 是否满足目标文件系统原子性；出错不能损害现有数据。完善并测试。
7. 复核缺失配置项：AutoClearDefault、GodModeDefault、CommandAliases 等，若未实现须移除/注明。
8. 验证 FlashDuration/FlashMaxAlpha/SetStateChanged 以及 noclip/god 行为是否需要额外同步。

### B. CS2 实机（必须在空闲时段，并完整保留日志）
- `!savepos A`→移动→`!tp A`：位置角度；断服/重启后存档还在。
- 两人 A 免闪、B 不免闪；两人互掷闪，并验证切换、退出练习/重新出生没有残留。
- 烟、闪、HE、燃烧瓶、诱饵五种：各做左投/右投/双键、跳投、移动投；`!rethrow` 是否生成对应实体、爆炸/烟火是否正确、落点偏差、本人是否保持站位；不能仅以出现聊天反馈作证据。
- A、B 同时投烟，A `!clear` / `!autoclear` 不能删 B 的弹或 KS 战术道具；测试火焰/烟雾已经生效的情况。
- KSD 防守5 BOT运行中：在安全配置下 BOT 功能拒绝运行；不得破坏 KSD 配额、录像、重放。单独测试服开启 EnableBotManagement 做多人竞争测试。
- 管理员与普通玩家各测试 !god、!noclip、!respawn、!clearall 权限；测试重连、切图、热重载和断线时定时器。
- 验证 `!nades` / `!loadnade` SteamID 隔离，同名与满额覆盖。
- 故意制造无效 JSON 的测试必须在**备份的测试环境**进行，不要破坏正式存档。

### C. 交付证据与结论
提交 `TEST-REPORT-v1.0.1.md`，逐条写测试环境版本、控制台日志、复现步骤、通过/失败、实机证据、已知限制。修复剩余 bug，重新 CI 编译，并在验证通过后提供 Release 候选 ZIP SHA256。
不要擅自覆盖 v1.0.0 Release，不要覆盖你的 KeranStrat 仓库；未得到实测结果前不要发布为稳定 v1.0.1。
