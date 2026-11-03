# Night Shift 后端 API 与配置

当前范围：M1 核心。数值依据 `Docs/DesignDoc.md`。M2–M5 尚未实现；不要把预留事件当作已有波次、商店、掉落或音频系统。

所有类均在全局命名空间。只使用旧 Input Manager；游戏物体不靠 Tag 识别。HUD/菜单/特效应在 `OnEnable` 订阅 `GameEvents`，在 `OnDisable` 取消订阅。

## 状态与枚举

```csharp
public enum GameState { MainMenu, Playing, Paused, Shop, GameOver, Victory }
public enum EnemyType { Rusher, GuardBot, Brute }
public enum UpgradeType { ThrowRate, Damage, MaxHP, MoveSpeed, FanThrow, Heal }
```

`UpgradeType` 先行定义，以便前端编译事件接线；`UpgradeSystem` 在 M2 实现。`GameState.cs` 包含上述三个枚举，无需挂组件。

## GameEvents

下表每个事件都有同参数的 `public static void Raise事件名(...)` 方法，事件仅在该静态类内部触发。前端负责监听；不要从显示层调用 Raise 方法模拟游戏状态。

| 事件 | 委托与参数 | 时机 |
| --- | --- | --- |
| PlayerHealthChanged | `Action<int,int>` current, max | 玩家扣血、回血、重置、主动发布 |
| BladesChanged | `Action<int,int>` inHand, max | 投刃、归还、重置、主动发布 |
| RecallCooldownChanged | `Action<float,float>` remaining, total | 回收冷却逐帧及结束的 `(0,total)`；暂停时数值不减少 |
| CoinsChanged | `Action<int>` total | 初始化、增减金币、重置 |
| WaveStarted | `Action<int,int>` wave, totalWaves | 开始一波；M2 刷怪器接入点 |
| WaveCleared | `Action<int>` wave | 本波完成，开始 3 秒过渡 |
| EnemiesRemainingChanged | `Action<int>` count | M1 重置为 0；实际计数由 M2 产生 |
| WaveTimerChanged | `Action<float>` elapsedSeconds | M1 重置为 0；逐帧计时由 M2 产生 |
| GameStateChanged | `Action<GameState>` | 初始主菜单和状态变化 |
| UpgradePurchased | `Action<UpgradeType,int>` type, newLevel | M2 预留 |
| BladeThrown | `Action<Vector2>` position | 成功投出一把刃 |
| EnemyHit | `Action<Vector2,bool>` position, fromRecall | 敌人实际接受伤害 |
| EnemyKilled | `Action<Vector2,EnemyType>` position, type | 死亡结算一次；归还对象池前 |
| PlayerHit | `Action<Vector2>` position | 玩家实际接受伤害 |
| BarrelExploded | `Action<Vector2>` position | M3 预留 |
| RecallStarted | `Action` | 成功启动一轮回收 |
| RunReset | `Action` | 新增后端生命周期钩子：冻结世界后、归还对象池前 |

进入 Play Mode 时清空静态事件，避免关闭 Domain Reload 后保留上一轮监听器。推荐保留 Unity 默认的 Scene Reload；关闭所有重载的工作流未验证。

## GameManager

场景只放一个，挂在常驻启用的管理物体上。`Player` 字段拖玩家根对象；未拖时在初始化/重置阶段查找一次。`Wave Clear Delay` 默认 3 秒。不要挂在随菜单隐藏的 UI 物体上。

公开属性：`static GameManager Instance`、`GameState State`、`int Coins`、`int CurrentWave`、`const int TotalWaves = 10`、`PlayerController Player`（均只读）。

| 方法 | 行为 |
| --- | --- |
| `StartGame()` | 仅 MainMenu 可用；重置本局、进入 Playing、发出第 1 波事件 |
| `PauseGame()` / `ResumeGame()` | 仅 Playing ↔ Paused；Esc 自动调用 |
| `CloseShopAndStartNextWave()` | 仅 Shop 可用；递增波次并发事件 |
| `RestartGame()` | 原场景重置并回 MainMenu，遵循策划案 retry → Main Menu；开始按钮再调用 StartGame |
| `ReturnToMainMenu()` | 取消波次过渡、冻结现有世界作为菜单背景；再次 StartGame 才清空 |
| `QuitGame()` | `Application.Quit()`；编辑器内不会退出 Play Mode |
| `bool TrySpendCoins(int amount)` | 拒绝负数和余额不足；成功扣钱并发 CoinsChanged |
| `AddCoins(int amount)` | 忽略非正数；限制整数溢出并发 CoinsChanged |
| `CompleteWave(int wave)` | M2 刷怪器报告本波全部完成；重复/过期报告被忽略；等待 3 秒游戏时间后 Shop，波次 10 则 Victory |

除 Playing 外时间倍率为 0；暂停期间波次过渡也暂停。玩家 HP 归零进入 GameOver。重置顺序为：MainMenu → RunReset → 池中对象全部归还 → 停用未池化的场景敌人 → 玩家恢复初始状态 → 清零波次、金币和 HUD 计数。M2/M3 将通过 RunReset 清理升级、掉落和道具；目前没有这些系统。

**M1 不自动生成敌人，也不自动判定清波。** `StartGame` 会清除手工摆放的测试敌人；测试时应在开始后从池中取 Rusher。正式生成由 M2 `WaveSpawner` 负责。

## PoolManager / IPoolable

场景只放一个 PoolManager，挂在始终启用且缩放为 `(1,1,1)` 的管理物体上。`Prewarm Entries` 中拖预制体和期望空闲数量（条目默认 5）。无需 Rigidbody2D/Collider。

```csharp
public static PoolManager Instance { get; }
public GameObject Get(GameObject prefab, Vector2 pos, Quaternion rot);
public void Return(GameObject obj);
public void ReturnAll();
public void Prewarm(GameObject prefab, int count);
public interface IPoolable { void OnSpawn(); void OnDespawn(); }
```

每个源预制体独立一个 `Queue<GameObject>`。预热只创建空闲实例，不触发 OnSpawn/OnDespawn；首次取出时依次激活（Awake/OnEnable）再调用层级内缓存的 IPoolable.OnSpawn。归还先 OnDespawn，再停用；重复归还不会重复入队。活跃实例位于场景根节点，空闲实例放在自动生成的禁用子物体下。不要在预热后动态添加池化组件。

M1 的 IPoolable 都放在预制体根对象。若以后在禁用子物体上挂 IPoolable，该实现必须自行延迟初始化：池回调包含禁用子级，而 Unity 尚未对它执行 Awake。仅装饰用的禁用子物体不受影响。

`ReturnAll` 用于重开，回调期间拒绝新 Get；空引用或该阶段的 Get 返回 null。归还非池化场景对象会执行清理并停用，但无法通过 Get 恢复原对象。前端应把 Blade、Rusher 做成预制体并通过池获取，不要用 Destroy 结束它们的生命周期。

## 伤害与 Health

```csharp
public interface IDamageable { void TakeDamage(DamageInfo info); }
public struct DamageInfo
{
    public int amount;
    public Vector2 knockback;
    public bool fromRecall;
    public GameObject source;
}
```

`PlayerController` 和 `EnemyBase` 实现 IDamageable；Health 只管理血量。这样一个根物体上只有一个伤害接收器，`GetComponent<IDamageable>()` 不会绕过敌人的击退/死亡结算。碰撞体可在根物体或同一 Rigidbody2D 的子物体上。造成伤害时先找碰撞体上的接口，必要时找 attachedRigidbody 上的接口。

Health 公开只读属性：`CurrentHP`、`MaxHP`、`IsDead`、`IsInvulnerable`。公开方法：`bool ApplyDamage(DamageInfo info)`（是否接受伤害）、`Heal(int amount)`、`ResetHealth(int maxHP)`（设置上限并满血、清除反馈与无敌）、`PublishStatus()`（仅玩家发 HUD）。实例事件：`Action<DamageInfo> Damaged`、`Action Died`。普通攻击应调用 IDamageable，不直接调用 ApplyDamage。

挂载：玩家/敌人根对象各一个 Health。`Initial Max HP` 默认 100；Rusher 生成时由 EnemyBase 的 50 覆盖。玩家可勾 `Is Player`，也会自动识别同物体的 PlayerController。只有玩家启用 0.6 秒无敌帧及透明度闪烁；普通 Heal 不复活死亡对象。`Sprite Renderers` 可拖身体精灵，留空则初始化时取子级；`Flash Material` 可留空，已配材质时闪白 0.08 秒。停用或重置时恢复材质/颜色。M4 再接 GameFeel 开关。

## PlayerController

玩家根对象挂 PlayerController、Health、BladeRecall、Rigidbody2D 和非 Trigger 的 CircleCollider2D/CapsuleCollider2D。RequireComponent 会补前三个依赖组件。Rigidbody2D 用 Dynamic、Mass 1、Interpolate；脚本设 Gravity Scale 0、冻结 Z 旋转、Linear Damping 10。Layer 建议 `Player`。

引用与层级：

- `Blade Prefab` 必须拖入下节的刃预制体，否则投掷安全返回 false。
- `Aim Camera` 拖俯视正交摄像机；未配时 Awake 只尝试 Camera.main，没有相机则保留向右瞄准。
- `Body Sprite` 拖身体 SpriteRenderer；留空自动取子级。
- `Aim Pivot` 可选，作为根物体的子物体旋转；身体精灵应与它平级，避免随瞄准旋转。
- `Throw Point` 可选，建议作为 Aim Pivot 的子物体、接近玩家身体；留空从瞄准方向前方 0.45 单位出刃。
- `Animator` 可选。可提供 `Speed` float、`Throw` trigger、`Hurt` trigger；脚本只设置存在且类型正确的参数。

输入：Horizontal/Vertical（旧 Input Manager 默认 WASD/方向键）、鼠标瞄准、按住左键连续投掷、E/空格回收。右键扇形投掷留到 M5。基础移动速度 6、加速时间约 0.1 秒、松手停下；受击时给物理击退 0.12 秒恢复窗口。5 把刃，每把 25 伤害、击退 4；策划案未给基础投掷间隔，暂定 0.3 秒。

公开只读属性：`Health Health`、`int BladesInHand`、`int MaxBlades`、`Vector2 AimDirection`。公开方法：`bool TryThrow()`（状态/数量/间隔/引用允许时投掷）、`TakeDamage(DamageInfo info)`、`ResetForNewRun()`（归还自身刃、恢复 Awake 记录的位置/初始 HP 与刃数、清冷却和速度）、`PublishStatus()`（血量/刃数/回收 HUD 快照）。输入和伤害仅 Playing 时生效；GameManager 统一调用重置。

## Blade / BladeRecall

刃预制体根对象挂 Blade、Rigidbody2D、CircleCollider2D；Collider 建议 Radius 0.1、Offset `(0,0)`。脚本设 Rigidbody2D 为 Kinematic、零重力、冻结 Z 旋转，Collider 为 Trigger。精灵/拖尾由前端配置。建议 Layer `Blade`，不需要依赖 Trigger 回调命中，因为脚本每个物理步沿移动路径 CircleCast，避免高速穿透薄物体。

- `Wall Layers` **必须配置**为阻挡投刃的层（建议 `World`；箱子是否钉刃可按设计再加入 `Crate`）。默认空，以免擅自绑定项目 Layer 编号。
- `Hit Layers` 默认 Everything；推荐设 Enemy 和之后可受伤道具层。查询会自动包含 Wall Layers，以免过滤掉墙。
- 最大飞行距离 10；出刃速度暂定 18、回收速度 24、接回距离 0.25，后三项为策划案未定的可调数值。
- 出刃可穿透敌人，一段出刃对同一敌人仅伤害一次；墙前停止或到达射程后落地。回收朝玩家当前位置直线飞行，可穿过墙；一把刃同轮回收只命中同一敌人一次。

Blade 公开只读属性 `CanRecall`、`IsReturning`，公开池化方法 `OnSpawn()`、`OnDespawn()`。由 PlayerController 负责取出并初始化，不要从 UI 单独调用 Pool.Get 制造未归属的刃。正常接回增加手持数量；重开时归还不会重复增加数量。

BladeRecall 与玩家放同一根对象，无需额外物理组件。默认 `Cooldown = 4`、`Root Hit Count = 3`、`Root Duration = 1`。公开只读属性：`float CooldownRemaining`、`float Cooldown`；公开方法：`bool TryRecall()`、`ResetRecall()`、`PublishStatus()`。没有可回收的刃时不消耗冷却。协程启动全部在外的飞行/落地刃并管理冷却；每轮独立统计敌人命中数，第三把命中使存活敌人定身 1 秒。对象池复用的敌人按 SpawnId 区分，不继承上一次生成的命中统计。暂停时冷却保留原值，完成发 `(0,4)`。

## EnemyBase / RusherAI

EnemyBase 为抽象基类，不单独挂载；Rusher 只挂 RusherAI（继承所有基础字段）+ Health + Rigidbody2D + 非 Trigger 的 CircleCollider2D/CapsuleCollider2D。可选 SpriteRenderer/动画由前端制作。

- Rigidbody2D：Dynamic、Gravity Scale 0、Freeze Rotation Z；建议 Mass 1、Linear Damping 2、Interpolate。脚本保证零重力/冻结旋转，其他设置由预制体负责。
- Layer 建议 `Enemy`；与 Player、World、Crate 碰撞。无需 Tag。
- Enemy Type = Rusher、Max HP = 50、Contact Damage = 10。Target 可留空，生成时优先取 GameManager.Player。
- 速度基数暂定 3.5（策划案未给基础速度）；第 1 波 ×1、第 2 波 ×1.05、第 9 波起 ×1.4。加速度暂定 30、接触击退 3、击退恢复窗口 0.15 秒，均可调。

公开只读属性：`EnemyType Type`、`Health Health`、`int SpawnId`（每次生成递增）、`float MoveSpeed`、`bool IsRooted`。公开方法：`Initialize(int wave, PlayerController player)`（生成后设置波次/目标）、`TakeDamage(DamageInfo info)`、`ApplyRoot(float duration)`、`Despawn()`、`OnSpawn()`、`OnDespawn()`。池自动调用后两项，调用方不要再次调用。

Rusher 直线追逐，接触 PlayerController 时走 IDamageable 造成 10 伤害，玩家无敌帧防止每个物理帧扣血。击退使用 AddForce，并短暂让物理接管移动。定身冻结移动，可延长至更晚的结束时间。死亡发 EnemyKilled 一次后回池，回池不会发死亡事件。M2 尚未接 DropTable，因此 M1 击杀没有掉落。

## 后续里程碑

- M2：WaveSpawner、DropTable、Pickup、UpgradeSystem、实际敌人数/计时/波次推进与经济循环。
- M3：GuardBotAI、EnemyProjectile、ExplosiveBarrel、Crate。
- M4：CameraShake、HitStop、GameFeel、AudioManager。
- M5：可选 BruteAI、扇形投掷。

这些脚本目前不存在，不应提前挂入预制体或从前端调用；约定签名保持待实现状态。

## 自动验证与编译

仅在本项目未被 Unity 编辑器打开时运行。命令不会保存场景/预制体；Play Mode 验证会创建临时内存物体，结束后关闭它自己启动的批处理编辑器。`M1Verification.Run()` 为 Editor 专用、仅接受 batchmode 的测试入口，不挂组件，不进入玩家构建。

```powershell
& 'E:\Unity\6000.6.4f1\Editor\Unity.exe' -batchmode -quit -projectPath 'F:\UCSC\CMPM125\GameplayProject1' -logFile 'F:\UCSC\CMPM125\GameplayProject1\Logs\compile.log'
& 'E:\Unity\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'F:\UCSC\CMPM125\GameplayProject1' -executeMethod M1Verification.Run -logFile 'F:\UCSC\CMPM125\GameplayProject1\Logs\m1-verification.log'
```

第二条不要加 `-quit`，否则会在跨帧检查完成前退出。以退出码 0 和日志中的 `M1_VERIFY PASS` 判断测试通过；失败/超时退出 1。测试使用普通游戏时间验证无敌/冷却/定身和波次等待，同时使用非缩放时间监视暂停与超时。测试从 `M1_VERIFY START` 起捕获所有 Error/Exception/Assert，不屏蔽游戏错误。
