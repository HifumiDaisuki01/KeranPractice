# 个人弹着点和道具轨迹

先 `!practice on`：

- `!impacts on` 开启蓝色3D小十字弹着点；`!impacts off` 关闭并清除本人的弹着点。无参数切换。
- `!trajectory on` 开启本人道具轨迹；原投绿色，重投橙色。开启后投新道具或使用 `!rethrow`；不会追画已经飞行的旧道具。`!trajectory off` 关闭并清除本人轨迹。无参数切换。
- `bind "F7" "css_impacts"`、`bind "F8" "css_trajectory"` 可绑定开关，会覆盖原按键绑定。
- 退出训练、离线、切图或回合结束清理本插件标记；两个开关不会改变他人设置。无敌/飞行等原权限不变。

## 性能和配置

| 配置 | 默认 | 范围 |
|---|---:|---:|
| MaxVisualsPerPlayer | 192 | 12–256 |
| MaxVisualsGlobal | 512 | 12–512 |
| ImpactSeconds | 3秒 | 0.5–10秒 |
| TrajectorySeconds | 12秒 | 1–20秒 |

每颗道具最多96段、每tick最多8个弹着事件；达到限额淘汰最早过期标记。复杂弹跳/慢速滚动的轨迹是实际离散采样线段，并非完整逐tick曲线。仅在有标记时运行过期timer。这里只登记本插件beam，绝不全图删除其他插件beam。

## 验证边界

服务端可核查加载、beam真实生成、末端坐标回读、过期删除、数量及发送过滤回调。`kpv2_visual_status`为只读诊断，`kpv2_visual_probe`仅服务器控制台生成不可见探针。

尚需用户验证实际客户端画面、双玩家互不可见、关闭和地图变化后无残留。没有客户端画面测试就不宣称可视化真人验收通过。

参考机制：[MatchZy PracticeMode](https://github.com/shobhit-pathak/MatchZy/blob/main/PracticeMode.cs) 的beam用法（MIT，许可证已随项目保留）；[CSS CBeam API](https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Generated/Schema/Classes/CBeam.g.cs) 与 [CheckTransmit API](https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Core/Model/CCheckTransmitInfo.cs)。实现为本项目独立代码，依赖服务器现有CSS发送ABI，不新增原生扩展。
