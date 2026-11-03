# M1 审查意见（Claude，前端 / 审查）

结论：**M1 通过，改完下面 1 到 4 条后可以进 M2**。编译 0 错误 0 警告，自动验证通过。代码结构清楚，池化、SpawnId 去重、暂停时冷却冻结、重开时的清理顺序都处理得很好。

## 需要修改

### 1. 脚本的 `.meta` 文件没有提交（先改这条）

`Assets/Scripts/**` 下的 `.meta` 现在都是未跟踪状态。脚本的 GUID 存在 `.meta` 里，前端搭的预制体就是靠这个 GUID 引用脚本。如果 `.meta` 丢了或重新生成，所有预制体上都会变成 "Missing Script"。

- 请把你负责的脚本和文件夹对应的 `.meta` 提交进去（`Assets/Scripts.meta`、`Assets/Scripts/{AI,Core,Core/Editor,Gameplay}.meta` 以及每个 `.cs.meta`）。
- 以后每个里程碑，`.cs` 和它的 `.meta` 一起提交。

### 2. `RestartGame` 和 `ReturnToMainMenu` 的行为要对调

- 现在：`RestartGame` 会重置并回到主菜单；`ReturnToMainMenu` 不重置，敌人和金币会留在主菜单背景里。
- 期望：
  - `RestartGame()`：完整重置后**直接开始第 1 波**（Game Over 和胜利界面上的"再来一局"按钮调用它）。
  - `ReturnToMainMenu()`：完整重置后进入 MainMenu（暂停菜单和结算界面上的"回主菜单"按钮调用它）。
- 策划案流程图里画的是 "retry → Main menu"，那张图画得不准确，以这里为准。

### 3. 飞刃要能推动箱子等物理物体（M3 做，但现在先确认思路）

现在 `Blade.DealDamage` 只处理 `IDamageable`，碰到没有 `IDamageable` 的动态刚体（箱子）时什么都不做。这一项关系到物理交互的 30 分：

- 飞刃命中**没有 `IDamageable` 的动态 `Rigidbody2D`** 时，用 `AddForce(heading * knockback, ForceMode2D.Impulse)` 推它一下。
- 箱子要像墙一样挡住飞出去的刃（刃会钉在箱子上）。前端会让 Blade 的 `wallLayers` 包含箱子所在的 `Prop` 层。但"钉在箱子上"时刃要跟着箱子动，建议把 `Resting` 状态的刃挂到被击中的刚体下面作为子物体，回收或回池时再解除父子关系。
- 回收途中飞回来的刃仍然穿墙、穿箱子，不需要改。

### 4. 点"继续游戏"按钮时会误扔一把刃

`PlayerController.Update` 用的是 `Input.GetMouseButton(0)`。暂停菜单里点"继续"的那一下，鼠标在恢复后的下一帧仍然按着，就会扔出一把刃。

- 修法：状态切回 Playing 后，等鼠标左键松开一次再允许投掷。比如监听 `GameStateChanged` 设置一个 `waitForMouseRelease` 标志，在 `Input.GetMouseButtonUp(0)` 或检测到左键未按下时清除。
- 键盘焦点导致空格触发 UI 按钮的问题由前端处理（点击后清掉 EventSystem 的选中状态），后端不用管。

## M2 时请顺便做

- 商店升级需要修改数值，目前 `throwInterval`、`bladeDamage`、`moveSpeed`、`maxBlades` 都是私有字段，没有对外接口。请在 `PlayerController` 上加 `ApplyUpgrades(...)` 之类的方法或属性，由 `UpgradeSystem` 调用；`RunReset` 时恢复默认值。
- 最大血量升级：Max HP 要"+25 并回满血"，可以直接用 `Health.ResetHealth(newMax)`，然后确认会触发 `PlayerHealthChanged`。
- `EnemyBase` 死亡时要接 `DropTable` 和刷怪器的剩余敌人计数。目前 `EnemyKilled` 事件已经有了，`WaveSpawner` 可以订阅它，也可以在 `HandleDeath` 里直接回调。两种都行，但不要重复计数。

## 不用改，记录一下

- `M1Verification.cs` 放在 `Core/Editor` 下面，只在命令行模式下运行，不会进构建，可以保留。
- Play Mode 启动时的 `SearchDatabase` 异常来自编辑器自身的索引，跟游戏代码无关。
- `GameEvents.RunReset` 和 `GameManager.CompleteWave` 这两个新增接口没问题，前端不依赖它们。
