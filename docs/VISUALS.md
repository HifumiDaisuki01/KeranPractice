# 个人可视化

`!practice on`后，`!impacts on/off`控制个人蓝色小十字，`!trajectory on/off`控制本人道具轨迹，原投绿、重投橙。开启轨迹后需投新道具或重投，已经飞行的旧道具不会补画。

| 配置 | 默认 | 范围 |
|---|---:|---:|
| MaxVisualsPerPlayer | 96 | 12–256 |
| MaxVisualsGlobal | 256 | 12–512 |
| ImpactSeconds | 3秒 | 0.5–10秒 |
| TrajectorySeconds | 12秒 | 1–20秒 |

实际线段来自飞行坐标采样，不是逐tick完整曲线。每颗最多96段；弹着点每tick最多2个事件、每秒16个，超出采样丢弃；弹着点至多占48条实体。达到总限额时复用/淘汰旧标记，显示时间可能因此缩短。

到期的beam关闭并保留在有上限的个人复用池，避免每颗子弹反复生成和删除。关闭对应功能、退出、离线、回合/地图结束或卸载会删除相关实体。只管理本插件登记的完整serial handle，不清其他插件beam。

CheckTransmit按SteamID+连接+地图会话过滤；发送热路径仅使用同步删除通知维护的索引快照，不为每条线、每个接收者反复调用原生实体解析。`kpv2_visual_status`提供active/allocated/created/reused、事件/绘制/限速丢弃、发送过滤计数。

`kpv2_visual_probe`是控制台隐藏探针，只验证几何、到期隐藏、同一实体复用及延迟删除，不能代替客户端画面。服主已报告修复后全部功能正常；未提供的双人专项或长期高并发数据不标成已测量。

参考：[MatchZy](https://github.com/shobhit-pathak/MatchZy/blob/main/PracticeMode.cs) 的beam机制（MIT许可证随项目保留），[CSS](https://github.com/roflmuffin/CounterStrikeSharp)的实体及发送API。运行时不新增原生扩展或Workshop资源。
