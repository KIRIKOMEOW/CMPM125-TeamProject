# 前后端协作请求

## 2026-11-03 — Backend / M1 交接

1. 请按 `Docs/API.md` 配置同一场景的 GameManager、PoolManager、Player，以及 Blade/Rusher 预制体。后端没有改动任何场景或预制体。
2. 请建立/确认 `Player`、`Enemy`、`Blade`、`World`（后续可加 `Crate`、道具层）并设置碰撞矩阵。Blade 的 Wall Layers 必须手动指定；后端没有修改 Tags/Layers 或任何 ProjectSettings。
3. 玩家身体精灵与 Aim Pivot 应为同级子物体；将 Body Sprite、可选 Animator、Aim Camera、Blade Prefab 引用拖好。可选材质/动画/相机未提供时不会抛空引用异常。
4. HUD 在 OnEnable/OnDisable 成对监听 GameEvents；先订阅再调用 PlayerController.PublishStatus 可主动获取快照。菜单按钮沿用约定的 GameManager 方法。`RestartGame` 按 DesignDoc 的 retry → Main Menu 重置后回主菜单；开始按钮调用 StartGame。
5. M1 不含自动刷怪/清波、掉落、升级、音频或手感管理器，后续按 M2–M5 接入。不要把 M1 的空敌人 HUD 视作最终波次逻辑。后端将按要求等待本次审查后继续 M2。
6. 新增 `GameEvents.RunReset` / `RaiseRunReset()` 供波次、升级与场景道具清理使用；新增 `GameManager.CompleteWave(int wave)` 供 M2 刷怪器报告清波。原第 4 节的签名未改动。
7. 策划案未指定的基础投掷间隔、刃飞行/回收速度、Rusher 基础速度等已作为可调默认值列在 API 中，需正式场景 Playtest 调优。

请审查 M1 脚本和接口文档，反馈碰撞/动画接线需求。暂无需要批准的既有接口变更。

## 2026-11-03 — M1 审查响应

- 按 REVIEW_M1 第 1 条提交 Unity 已生成的脚本/目录 .meta；后续脚本与对应 .meta 同批提交。
- 第 2 条覆盖上文的旧交接说明：RestartGame 完整重置后直接开始第 1 波；ReturnToMainMenu 完整重置后进入主菜单。
- 第 4 条已为每次进入 Playing 添加左键释放门控；UI 清理 EventSystem 选中状态仍由前端负责。
- 第 3 条按用户要求留到 M3：非 IDamageable 的动态刚体接受冲量，Prop 层挡住出刃，停留的刃跟随命中刚体，回收/回池解除父子关系；回收仍穿透。

## 2026-11-03 — M2 交接

- 请在始终启用的管理物体上新增 WaveSpawner、DropTable、UpgradeSystem（各一个）。WaveSpawner 配 Rusher Prefab 和距玩家可达 8 单位以上的刷怪点；Guard/Brute 待对应里程碑再拖。可拖 Player 以在编辑状态看绿/红 Gizmos。
- 请新增 Coin、Health 两种 Pickup 预制体并拖到 DropTable；组件/碰撞体/建议 Layer 见 API。默认血包 25、额外金币 1、吸附半径/速度 12，均为策划案未指定的可调手感值。
- 商店可用约定的 UpgradeSystem 接口查询价格/等级/可购性并购买，成功监听 UpgradePurchased、CoinsChanged、PlayerHealthChanged。FanThrow 的价格/上限已提供，但 M5 前暂禁购，可先隐藏该项。
- 敌人数 HUD 包含待生成 + 存活敌人。金币进入 Shop/Victory 时自动结算剩余拾取物，防止暂停导致购物缺钱；血包保留到后续战斗或重置。
- REVIEW_M1 的三项 M2 附带要求已接入：PlayerController.ApplyUpgrades 及原值恢复、MaxHP +25 满血和血量事件、死亡接 DropTable 与唯一清波计数。
- 保持波次管理器全程启用，勿在正常战斗中直接 Pool.Return 活敌人；杀死敌人走 IDamageable，清场走 GameManager 的重开/回菜单。
- 第 3 条飞刃/箱子物理交互仍留在 M3；未提前改动前端 Layer/场景/预制体。

## 2026-11-03 — M2 审查修复与 M3–M5 统一交接

以下覆盖上文按里程碑递进的旧“未实现/暂禁购”说明；既有约定签名保持不变。用户已要求连续完成并交一次统一审查，Unity 保持打开、跳过自动运行验证。

1. REVIEW_M2 已落实：Rusher 默认 3 金币、每批安全刷怪点去重。若前端已序列化 DropTable 的旧值 2，请在 Inspector 改为 3；给刷怪器至少 3 个不同且有机会距玩家 ≥8 的点可维持三只一批，点少时会分批等待。
2. GuardBotAI/EnemyProjectile：按 API 做 75 HP Guard 和子弹预制体，拖入 WaveSpawner、Guard.ProjectilePrefab。Sight Blocking Layers、Projectile.Blocking Layers 都应包含 World + Prop/Crate；视线/射击间隔等暂定可调值已列 API。CurrentStateName 可供头顶状态标签读取。
3. Crate/ExplosiveBarrel：箱子 Dynamic、桶 Static、均非 Trigger。Blade.WallLayers 必须加箱子/桶的 Prop 层，HitLayers 覆盖敌人和可伤害桶。飞刃推箱、钉住后跟随、解除后回收穿透已实现；跟随使用局部锚点，不实际挂父子节点。桶爆炸自身隐藏/下一波重现，表现层勿 Destroy 或禁用根对象；爆炸粒子可独立生成。
4. GameFeel/HitStop 各一个并始终启用；CameraShake 建议使用跟随父节点下独立 Camera 子节点，父缩放 1，其他跟随脚本不要覆盖同一 localPosition。F1 已接入关闭震屏/停顿/闪白，伤害/击退/无敌和音频保留。需要前端配置 Health.flashMaterial、拖尾、粒子、红边等效果并试玩对比。
5. AudioManager 放独立持久根，填 Sound Effects 的 9 种 SfxId 和 Music Clip，摄像机一个 AudioListener。除 UIClick 外自动监听游戏事件；按钮显式播放 UIClick，音量滑条绑定 MasterVolume。新增 CoinCollected(Vector2 position, int amount) 与对应 Raise 方法，避免监听 CoinsChanged 导致购买/重置误播拾取音。
6. BruteAI 挂预制体根并拖 WaveSpawner.BrutePrefab；HP 200、击退系数 0.5、蓄力 0.8。Charge Blocking Layers 配 World + Prop/Crate；速度/冲锋范围/时长/伤害的暂定默认值见 API，需前端试玩调整、提供蓄力提示/动画。
7. 扇形商品已开放：FanThrowAvailable=true，80 金币/上限 1；请撤掉前端“未开放”硬编码，继续使用 CanBuy/GetPrice 等接口。右键每次消耗 3 把、默认 -15/0/+15 度、与左键共用冷却。左右键菜单释放门控均保留；重开恢复未解锁。玩家在已有场景上无需新增组件。
8. 请 Claude 统一读取 Console 并审查各后端提交；本轮 MCP 仅刷新代码、读取状态/Console，没有启动测试或 Play Mode。M1–M5 自动验证全部标记“待运行”，Unity 关闭后串行执行 API 中命令，结果再写 BACKEND_LOG。正式 10 波完整流程、真实碰撞矩阵、商店/UI、音画与手感尚待前端联调。

本轮脚本与 Unity 自动生成的对应 .meta 分里程碑提交；文档按用户“只提交脚本和 .meta”的范围保留为工作区更新，供同目录审查。Packages/、ProjectSettings/、场景、预制体、美术/音频资产没有纳入后端提交。
