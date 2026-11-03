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
