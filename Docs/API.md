# Night Shift 后端 API 与配置

当前范围：M1–M5。数值依据 `Docs/DesignDoc.md`；重开/回菜单行为以已批准的 `Docs/REVIEW_M1.md` 为准。代码交付与运行验证状态分别记录在 BACKEND_LOG；下文配置供前端接线使用。

所有类均在全局命名空间。只使用旧 Input Manager；游戏物体不靠 Tag 识别。HUD/菜单/特效应在 `OnEnable` 订阅 `GameEvents`，在 `OnDisable` 取消订阅。

## 状态与枚举

```csharp
public enum GameState { MainMenu, Playing, Paused, Shop, GameOver, Victory }
public enum EnemyType { Rusher, GuardBot, Brute }
public enum UpgradeType { ThrowRate, Damage, MaxHP, MoveSpeed, FanThrow, Heal }
```

`GameState.cs` 包含上述三个枚举，无需挂组件。UpgradeSystem 与 FanThrow 购买/攻击均已实现。

## GameEvents

下表每个事件都有同参数的 `public static void Raise事件名(...)` 方法，事件仅在该静态类内部触发。前端负责监听；不要从显示层调用 Raise 方法模拟游戏状态。

| 事件 | 委托与参数 | 时机 |
| --- | --- | --- |
| PlayerHealthChanged | `Action<int,int>` current, max | 玩家扣血、回血、重置、主动发布 |
| BladesChanged | `Action<int,int>` inHand, max | 投刃、归还、重置、主动发布 |
| RecallCooldownChanged | `Action<float,float>` remaining, total | 回收冷却逐帧及结束的 `(0,total)`；暂停时数值不减少 |
| CoinsChanged | `Action<int>` total | 初始化、增减金币、重置 |
| CoinCollected | `Action<Vector2,int>` position, amount | M4 新增：金币拾取/进商店结算/缺预制体直接奖励；消费和重置不触发 |
| WaveStarted | `Action<int,int>` wave, totalWaves | 开始一波；刷怪器/掉落奖励状态开始新波次 |
| WaveCleared | `Action<int>` wave | 本波完成，开始 3 秒过渡 |
| EnemiesRemainingChanged | `Action<int>` count | 待生成 + 存活敌人总数；重置为 0 |
| WaveTimerChanged | `Action<float>` elapsedSeconds | 战斗波次逐帧计时；暂停/清波后不增加，重置为 0 |
| GameStateChanged | `Action<GameState>` | 初始主菜单和状态变化 |
| UpgradePurchased | `Action<UpgradeType,int>` type, newLevel | 商店购买成功；Heal 的 level 是本局购买次数 |
| BladeThrown | `Action<Vector2>` position | 成功投出一把刃 |
| EnemyHit | `Action<Vector2,bool>` position, fromRecall | 敌人实际接受伤害 |
| EnemyKilled | `Action<Vector2,EnemyType>` position, type | 掉落与敌人计数结算后、归还对象池前，供表现层使用 |
| PlayerHit | `Action<Vector2>` position | 玩家实际接受伤害 |
| BarrelExploded | `Action<Vector2>` position | 爆炸伤害和冲量结算后 |
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
| `RestartGame()` | 原场景完整重置后直接开始第 1 波，供结算界面的“再来一局”调用 |
| `ReturnToMainMenu()` | 原场景完整重置后进入 MainMenu，供暂停/结算界面的“回主菜单”调用 |
| `QuitGame()` | `Application.Quit()`；编辑器内不会退出 Play Mode |
| `bool TrySpendCoins(int amount)` | 拒绝负数和余额不足；成功扣钱并发 CoinsChanged |
| `AddCoins(int amount)` | 忽略非正数；限制整数溢出并发 CoinsChanged |
| `CompleteWave(int wave)` | M2 刷怪器报告本波全部完成；重复/过期报告被忽略；等待 3 秒游戏时间后 Shop，波次 10 则 Victory |

除 Playing 外时间倍率为 0；暂停期间波次过渡也暂停。玩家 HP 归零进入 GameOver。重置顺序为：MainMenu → RunReset → 池中对象全部归还 → 停用未池化的场景敌人 → 玩家恢复初始状态 → 清零波次、金币和 HUD 计数。RunReset 会清理刷怪计划、升级、奖励倍率、场景道具与手感状态，随后池清理待拾取物。RestartGame 完成此流程后立即开始第 1 波。

场景须配置 WaveSpawner 才会自动刷怪/清波。`StartGame` 会清除手工摆放的测试敌人；正式战斗由 WaveSpawner 生成。仅手工从池中取出的敌人不会纳入当前刷怪器的清波计数。

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

挂载：玩家/敌人根对象各一个 Health。`Initial Max HP` 默认 100；Rusher 生成时由 EnemyBase 的 50 覆盖。玩家可勾 `Is Player`，也会自动识别同物体的 PlayerController。只有玩家启用 0.6 秒无敌帧及透明度闪烁；普通 Heal 不复活死亡对象。`Sprite Renderers` 可拖身体精灵，留空则初始化时取子级；`Flash Material` 可留空，已配材质时闪白 0.08 秒。停用或重置时恢复材质/颜色。GameFeel.Enabled=false 会立即取消当前闪白并跳过后续闪白；无敌帧及其透明度提示仍保留。

## PlayerController

玩家根对象挂 PlayerController、Health、BladeRecall、Rigidbody2D 和非 Trigger 的 CircleCollider2D/CapsuleCollider2D。RequireComponent 会补前三个依赖组件。Rigidbody2D 用 Dynamic、Mass 1、Interpolate；脚本设 Gravity Scale 0、冻结 Z 旋转、Linear Damping 10。Layer 建议 `Player`。

引用与层级：

- `Blade Prefab` 必须拖入下节的刃预制体，否则投掷安全返回 false。
- `Aim Camera` 拖俯视正交摄像机；未配时 Awake 只尝试 Camera.main，没有相机则保留向右瞄准。
- `Body Sprite` 拖身体 SpriteRenderer；留空自动取子级。
- `Aim Pivot` 可选，作为根物体的子物体旋转；身体精灵应与它平级，避免随瞄准旋转。
- `Throw Point` 可选，建议作为 Aim Pivot 的子物体、接近玩家身体；留空从瞄准方向前方 0.45 单位出刃。
- `Animator` 可选。可提供 `Speed` float、`Throw` trigger、`Hurt` trigger；脚本只设置存在且类型正确的参数。

输入：Horizontal/Vertical（旧 Input Manager 默认 WASD/方向键）、鼠标瞄准、按住左键连续投掷、解锁后按住右键三刃扇形投掷、E/空格回收。基础移动速度 6、加速时间约 0.1 秒、松手停下；受击时给物理击退 0.12 秒恢复窗口。5 把刃，每把 25 伤害、击退 4；策划案未给基础投掷间隔，暂定 0.3 秒。

每次状态切回 Playing 都分别等待左右键释放，再接受下一次按住投掷，防止菜单点击误投刃。显式调用 `TryThrow()` / `TryFanThrow()` 不经过鼠标输入门控，供游戏逻辑/测试使用。解锁后左右同时按住，右键优先且本帧不再左键出刃，缺少 3 把时也不会偷偷改为单发。

公开只读属性：`Health Health`、`int BladesInHand`、`int MaxBlades`、`Vector2 AimDirection`。公开方法：`bool TryThrow()`（状态/数量/间隔/引用允许时投掷）、`TakeDamage(DamageInfo info)`、`ResetForNewRun()`（归还自身刃、恢复 Awake 记录的位置/初始 HP 与刃数、清冷却和速度）、`PublishStatus()`（血量/刃数/回收 HUD 快照）。输入和伤害仅 Playing 时生效；GameManager 统一调用重置。

M2 新增只读属性：`MoveSpeed`、`ThrowInterval`、`BladeDamage`、`FanThrowAvailable`、`FanThrowUnlocked`。新增公开方法：

```csharp
public void ApplyUpgrades(int throwRateLevel, int damageLevel, int maxHPLevel, int moveSpeedLevel, bool fanUnlocked);
```

UpgradeSystem 传入本局各项总等级。以 Awake 记录的 Inspector 原值为基础计算，重复应用不会叠加：投掷间隔 `base / (1 + 0.15 × level)`，伤害 `base + 10 × level`，移速 `base × (1 + 0.1 × level)`，最大 HP `base + 25 × level`。最大 HP 改变时 ResetHealth 并满血；其他购买不会意外回血。RunReset 和 ResetForNewRun 都恢复原值。刃上限保持初始 5（没有增加容量的商店商品）。FanThrowAvailable 现为 true，FanThrowUnlocked 表示本局是否已购解锁。

`bool TryFanThrow()` 要求解锁、Playing、玩家存活、至少 3 把刃、共享投掷冷却结束和有效刃/池引用。先取得并验证 3 个实例，全部成功才扣 3 把并开始冷却；失败回收已申请实例，不扣弹药/冷却。默认 `Fan Half Spread = 15` 度（可调暂定值），方向为瞄准 -15/0/+15；伤害和击退使用当前升级值，发一次 BladesChanged、每把一次 BladeThrown。扇形刃与单发共用回收/定身/推箱逻辑；重开清除解锁。

## Blade / BladeRecall

刃预制体根对象挂 Blade、Rigidbody2D、CircleCollider2D；Collider 建议 Radius 0.1、Offset `(0,0)`。脚本设 Rigidbody2D 为 Kinematic、零重力、冻结 Z 旋转，Collider 为 Trigger。精灵/拖尾由前端配置。建议 Layer `Blade`，不需要依赖 Trigger 回调命中，因为脚本每个物理步沿移动路径 CircleCast，避免高速穿透薄物体。

- `Wall Layers` **必须配置**为阻挡投刃的层（建议 `World` + `Prop`/`Crate`，含箱子和爆炸桶）。默认空，以免擅自绑定项目 Layer 编号。
- `Hit Layers` 默认 Everything；推荐设 Enemy 和之后可受伤道具层。查询会自动包含 Wall Layers，以免过滤掉墙。
- 最大飞行距离 10；出刃速度暂定 18、回收速度 24、接回距离 0.25，后三项为策划案未定的可调数值。
- 出刃可穿透敌人，一段出刃对同一敌人仅伤害一次；墙前停止或到达射程后落地。回收朝玩家当前位置直线飞行，可穿过墙；一把刃同轮回收只命中同一敌人一次。

Blade 公开只读属性 `CanRecall`、`IsReturning`、`IsResting`、`Rigidbody2D StuckBody`，公开池化方法 `OnSpawn()`、`OnDespawn()`。由 PlayerController 负责取出并初始化，不要从 UI 单独调用 Pool.Get 制造未归属的刃。正常接回增加手持数量；重开时归还不会重复增加数量。

无 IDamageable 的动态刚体在出刃和回收两个阶段分别最多接受一次刃冲量，复合碰撞体不重复计数。出刃命中 Wall Layers 后记录碰撞刚体的局部位置/角度，停用刃物理模拟，LateUpdate 跟随箱子平移/旋转；采用锚点跟随，不改变父子层级，避免箱子销毁连带删除刃。回收/回池清除锚点并恢复模拟。被钉物体失活或碰撞体消失时，刃留在最后位置，仍可回收；回收继续穿透墙和箱子。

BladeRecall 与玩家放同一根对象，无需额外物理组件。默认 `Cooldown = 4`、`Root Hit Count = 3`、`Root Duration = 1`。公开只读属性：`float CooldownRemaining`、`float Cooldown`；公开方法：`bool TryRecall()`、`ResetRecall()`、`PublishStatus()`。没有可回收的刃时不消耗冷却。协程启动全部在外的飞行/落地刃并管理冷却；每轮独立统计敌人命中数，第三把命中使存活敌人定身 1 秒。对象池复用的敌人按 SpawnId 区分，不继承上一次生成的命中统计。暂停时冷却保留原值，完成发 `(0,4)`。

## EnemyBase / RusherAI

EnemyBase 为抽象基类，不单独挂载；Rusher 只挂 RusherAI（继承所有基础字段）+ Health + Rigidbody2D + 非 Trigger 的 CircleCollider2D/CapsuleCollider2D。可选 SpriteRenderer/动画由前端制作。

- Rigidbody2D：Dynamic、Gravity Scale 0、Freeze Rotation Z；建议 Mass 1、Linear Damping 2、Interpolate。脚本保证零重力/冻结旋转，其他设置由预制体负责。
- Layer 建议 `Enemy`；与 Player、World、Crate 碰撞。无需 Tag。
- Enemy Type = Rusher、Max HP = 50、Contact Damage = 10。Target 可留空，生成时优先取 GameManager.Player。
- 速度基数暂定 3.5（策划案未给基础速度）；第 1 波 ×1、第 2 波 ×1.05、第 9 波起 ×1.4。加速度暂定 30、接触击退 3、击退恢复窗口 0.15 秒，均可调。

公开只读属性：`EnemyType Type`、`Health Health`、`int SpawnId`（每次生成递增）、`float MoveSpeed`、`bool IsRooted`。公开方法：`Initialize(int wave, PlayerController player)`（生成后设置波次/目标）、`TakeDamage(DamageInfo info)`、`ApplyRoot(float duration)`、`Despawn()`、`OnSpawn()`、`OnDespawn()`。池自动调用后两项，调用方不要再次调用。

Rusher 直线追逐，接触 PlayerController 时走 IDamageable 造成 10 伤害，玩家无敌帧防止每个物理帧扣血。击退使用 AddForce，并短暂让物理接管移动。定身冻结移动，可延长至更晚的结束时间。

M2 死亡顺序：DropTable.Drop → `EnemyBase.Died` → GameEvents.EnemyKilled → 回池。新增 `public static event Action<EnemyBase,int> Died`（enemy, spawnId）仅供后端按生成身份计数；UI/特效仍监听 GameEvents。WaveSpawner 只通过 Died 计数，避免同次死亡重复减数。重置/普通回池不产生死亡奖励；请勿在正常战斗中绕过死亡逻辑直接归还由刷怪器管理的敌人。

## WaveSpawner

在场景始终启用的管理物体上挂一个 WaveSpawner；不需要物理组件。拖 `Rusher Prefab`（RusherAI 组件）、`Guard Bot Prefab`（GuardBotAI）、`Brute Prefab`（BruteAI）与 `Spawn Points`（一组空 Transform）。Guard/Brute 的 Type 由类固定，Rusher 保持 Enemy Type=Rusher。`Player` 可选，供编辑状态 Gizmos 预览，开波时优先使用 GameManager.Player。

- 波次预算 `4 + 3 × wave`；Rusher/Guard/Brute 花费 1/2/4。有效 Guard 从第 3 波、Brute 从第 6 波开始，每波先保证各一只，再随机使用剩余预算；未接后续类型时以现有类型填满预算。
- `Spawn Interval = 1.5` 秒游戏时间，首批立即生成。`Enemies Per Burst = 3` 是策划案未指定的可调默认值。
- `Minimum Spawn Distance = 8`，每个敌人生成前重新筛选距离玩家 ≥8 且启用的刷怪点；同批不重复使用同一 Transform。有效点少于批量时缩小本批，余下预算留到下一批。无合法点就等待重试，保留全部预算。
- Rusher 是确保任何预算余数都能填满的必要引用；缺失/类型不匹配时保留计划等待，不错误清波。完全未配置时 HUD 暂显示按 Rusher 数量估计的预算；引用有效后显示实际敌人数。
- Gizmos：合法点绿圈，过近/禁用/无玩家参考的点红圈；不修改项目 Layer/Tag。

公开只读属性：`CurrentWave`、`WaveBudget`、`PendingEnemies`、`ActiveEnemies`、`RemainingEnemies`、`ElapsedTime`、`IsWaveActive`。无需手动调用开波方法，监听 GameEvents.WaveStarted 自动启动协程。

RemainingEnemies = 待生成 + 存活；只有计划全部生成且本刷怪器追踪的敌人全部死亡才调用 GameManager.CompleteWave。以 EnemyBase 实例 + SpawnId 计数，每次死亡只减一次。暂停时生成/计时冻结，清波后停止计时，3 秒过渡由 GameManager 负责。RunReset 先停止协程/清账，再由 PoolManager 归还实例。不要在战斗中禁用此管理物体或直接回收它追踪的活敌人。

## DropTable

场景挂一个 DropTable 单例，放在始终启用的管理物体。公开 `static DropTable Instance`、只读 `float CurrentCoinMultiplier`，以及 `void Drop(Vector2 position, EnemyType type)`；EnemyBase 死亡自动调用一次，前端不要再次发奖励。

拖 `Coin Prefab` / `Health Prefab`，两种预制体都按下节配置 Pickup。字段默认：Rusher/Guard/Brute 基础金币 3/4/10（REVIEW_M2 调整）；Extra Pickup Chance 0.15；Low Health Threshold 0.4；Low Health Weight Multiplier 3；Perfect Wave Coin Multiplier 1.5。已序列化旧值 2 的组件需前端改为 3。

策划案未定的可调值：额外金币 1、血包回血 25、金币/血包基础权重各 1。额外掉落先抽 15% 概率，再按权重选种类；血量严格低于 40% 时血包权重乘 3，所以额外拾取物中血包占比由 50% 变为 75%。金币金额乘当前倍率后取整。

无伤记录只统计本波有效 PlayerHit；WaveCleared 锁定结果，奖励在紧接着的下一波开始时生效。第 1 波/重开倍率为 1，当前波倍率不会因本波是否受伤即时改变。最后一只敌人先生成掉落再报告清波，避免错用下一波的奖励状态。

缺金币预制体、PoolManager 或 Pickup 组件时，直接记入对应金币作为接线未完成时的后备；缺血包预制体则安全跳过该次额外血包。正式表现需要前端把引用配齐。

## Pickup

金币与血包各建一个外观预制体，根物体均挂 Pickup + Rigidbody2D + CircleCollider2D；建议 Collider Radius 0.2、Offset 0。脚本设置 Kinematic、Gravity Scale 0、Freeze Rotation、Trigger。建议 Layer `Pickup`，与 Player 触发；无需 Tag。精灵/动画由前端配置。

```csharp
public enum PickupType { Coin, Health }
public PickupType Type { get; }
public int Amount { get; }
public void Initialize(PickupType type, int amount, PlayerController player);
public bool TryCollect(PlayerController player);
public void OnSpawn();
public void OnDespawn();
```

通常由 DropTable 执行 Pool.Get 后 Initialize，不在 Inspector 指定金额/类型。Coin Attraction 的默认吸附范围 12、速度 12、收集距离 0.35（均为未定手感数值）。金币进入范围后靠近玩家；血包留在原处，接触/靠近时回血，满血不消费血包。成功收集只结算一次并归还对象池。

Shop/Victory 进入时会一次性收取剩余金币，保证时间暂停不会锁住本波收入；血包保留到后续战斗或重置。Paused/MainMenu/GameOver 不允许拾取。重置时普通归还不会发金币或回血。金币成功收取触发 CoinCollected，AudioManager 自动播放对应音效。

## UpgradeSystem

场景放一个 UpgradeSystem 单例并保持启用；`Player` 可拖入或自动取 GameManager.Player。无 UI/物理组件依赖。价格和 Heal Amount 可在 Inspector 调整，默认严格按策划案：

| 类型 | 默认效果 | 上限 | 各等级价格 |
| --- | --- | --- | --- |
| ThrowRate | 投掷频率 +15%/级 | 3 | 20 / 35 / 55 |
| Damage | 伤害 +10/级 | 3 | 25 / 40 / 60 |
| MaxHP | 最大 HP +25/级并满血 | 3 | 20 / 35 / 55 |
| MoveSpeed | 移速 +10%/级 | 2 | 15 / 30 |
| FanThrow | 解锁右键三刃扇形投掷 | 1 | 80 |
| Heal | 回复 50，封顶当前最大 HP | 无限 | 15 |

公开接口：`static UpgradeSystem Instance`、`int GetLevel(UpgradeType t)`、`int GetMaxLevel(UpgradeType t)`（Heal 返回 -1）、`int GetPrice(UpgradeType t)`（满级/无有效价格返回 -1）、`bool CanBuy(UpgradeType t)`、`bool TryBuy(UpgradeType t)`。

CanBuy/TryBuy 要求 Shop 状态、玩家存活、金币足够、未满级；Heal 还要求未满血，FanThrow 还要求功能已可用。TryBuy 成功扣款、应用等级和效果，并触发 CoinsChanged、UpgradePurchased；失败不扣款不升级。Heal 等级代表本局购买次数。RunReset 清零全部等级；UI 进入商店时读取这些方法重建商品显示。

最大 HP 通过 PlayerController.ApplyUpgrades → Health.ResetHealth 应用并触发 PlayerHealthChanged；其他升级不会因重算属性而回血。伤害由投刃时记录，已投出的刃保持当次投掷的伤害，升级影响之后的投掷。

## GuardBotAI / EnemyProjectile

Guard 预制体挂 GuardBotAI + Health + Dynamic Rigidbody2D + 非 Trigger 碰撞体，物理配置同 Rusher；只挂一种 EnemyBase 派生类。Type 固定 GuardBot，生成 HP 固定取 Guard Max HP（默认 75），攻击范围 6、丢失视线后搜索 3 秒。公开 `string CurrentStateName` 和 `bool HasLineOfSight`，其余池化/伤害接口继承 EnemyBase。Gizmos 画红/绿视线与攻击范围圈。

- `Sight Blocking Layers` 必配 World + Prop/Crate，忽略 Trigger，墙和箱子遮挡视线。未配置时视线无遮挡。
- `Patrol Waypoints` 可选，每次生成缓存世界位置；为空则在出生点左右 2 单位巡逻。
- `Projectile Prefab` 拖带 EnemyProjectile 的预制体；`Fire Point` 可选，未配使用朝向前方 0.45 单位。检测范围 12、射击间隔 1.2、到点容差 0.2 为可调暂定值。
- 四状态：看到目标且距离 ≤6 时 Attack，否则 Chase；失去目标 Search 最后位置。Patrol 中超过 `Investigate After`（默认 5 秒）仍未见玩家时，记录玩家当时的位置并进入 Search 调查；Search 3 秒结束仍不可见时，以自身当前位置重建左右 `Patrol Radius` 两个点并重新计时，下一次调查继续向玩家靠近。初次生成可使用配置的巡逻点，调查后改用新中心。定身/击退期间不射击；暂停冻结计时，池化复用清空旧调查状态。

敌方子弹挂 EnemyProjectile + Rigidbody2D + CircleCollider2D（建议 Radius 0.1）；脚本设置 Kinematic、零重力、冻结旋转、Trigger。Layer 建议 EnemyProjectile；`Blocking Layers` 必配 World + Prop/Crate；`Hit Layers` 包含 Player。路径 CircleCast 与枪口到敌人中心的遮挡检查防薄墙穿透，先碰掩体先回池；只伤玩家，不伤敌人。公开 `Launch(Vector2 aim, EnemyBase source)`、`OnSpawn()`、`OnDespawn()`，由 Guard 取池后调用。默认速度 10、伤害 10、击退 3、射程 16、寿命 4 秒均为策划未指定的可调值；重开由池统一清除。

## Crate / ExplosiveBarrel

Crate 为场景道具：根对象挂 Crate + Rigidbody2D + BoxCollider2D；脚本设置 Dynamic、零重力、冻结 Z、非 Trigger，默认 Mass 2、Linear Damping 5 可调。Layer 建议 Prop/Crate，必须包含在 Guard 视线、敌方子弹遮挡和飞刃 Wall Layers 中。公开 `ResetForNewRun()`；RunReset 自动恢复初始位置/旋转/速度，正常换波保留箱子位置。

ExplosiveBarrel 根对象挂 ExplosiveBarrel + Rigidbody2D + CircleCollider2D；脚本设 Static、零重力、冻结 Z、非 Trigger。Layer 建议 Prop；渲染器和碰撞体可在子物体，脚本缓存并恢复各自启用状态。`Affected Layers` 默认 Everything，应覆盖 Player、Enemy 和动态道具。公开 `bool IsExploded`、`int HitsRemaining`、`TakeDamage(DamageInfo)`、`ResetBarrel()`。

普通攻击两次正伤害命中触发半径 3 的爆炸，每个伤害接收器一次 60 伤害，每个刚体一次 12 冲量；复合碰撞体去重。Player/Enemy 新增 `ApplyKnockback(Vector2 impulse)`，让冲击力拥有短暂物理恢复窗口，玩家无敌帧也不免疫冲量。伤害 source 上有另一只 ExplosiveBarrel 时，不需要预先受击：等待 `Chain Reaction Delay`（默认 0.08 秒游戏时间）即引爆。等待期间重复命中不会叠加协程或提前引爆；离开 Playing（包括暂停/商店/结算）、RunReset/ResetBarrel、停用组件都会取消，恢复时不补爆，取消不消耗原有命中次数。每次实际爆炸先标记去重，并在该次伤害批次结束后才结算玩家死亡，避免同一爆炸漏伤。爆炸只隐藏碰撞/渲染，不停用根对象，下一 WaveStarted 或 RunReset 恢复初始状态；前端不要 Destroy/禁用桶根对象。

## BruteAI

预制体挂 BruteAI + Health + Dynamic Rigidbody2D + 非 Trigger 碰撞体，物理配置同 Rusher；脚本启用 Continuous 碰撞检测。Type 固定 Brute，Brute Max HP 默认 200，Knockback Resistance 默认 0.5（乘基础击退倍率，正常保持基础值 1），Windup Duration 默认 0.8 秒。公开 `string CurrentStateName`、`bool IsCharging`，其余接口继承 EnemyBase。拖到 WaveSpawner 的 Brute Prefab 后，第 6 波开始按预算生成。

状态 Pursue → WindUp → Charge → Recover：进入范围且冷却允许时锁定当时的目标方向，停下蓄力 0.8 秒后沿该方向冲锋；撞到 Charge Blocking Layers 掩体结束冲锋。`Charge Blocking Layers` 必配 World + Prop/Crate，玩家接触通过 IDamageable 伤害，无 Tag 依赖。回收定身会取消蓄力/冲锋进入恢复；暂停冻结全部阶段计时，池化/重开清空旧阶段和冷却。

以下策划未定，均可在 Inspector 调整：基础移速 2、触发距离 7、冲锋速度 12、冲锋时长 0.5、恢复 0.5、结束后冷却 2.5、接触伤害 20/击退 6。普通移动和冲锋速度都按每波 +5%、上限 +40% 增长；第 9 波起冲锋 16.8。需前端试玩确认节奏和冲锋提示动画；锁定方向可由对象朝向之外的前端特效表达，脚本不旋转像素身体。

## GameFeel / CameraShake / HitStop

GameFeel 与 HitStop 各放一个在始终启用的管理物体上，不需要物理组件。GameFeel 暴露静态 `bool Enabled { get; set; }`（默认 true）和 `event Action<bool> EnabledChanged`；F1 在 Update 切换。事件桥接投刃/敌人受伤与死亡/玩家受伤/爆炸/回收到震屏；敌人死亡停顿 0.05 秒、爆炸停顿 0.1 秒，强度和时长均可调。关闭开关会取消现有震屏/停顿/白闪，保留游戏伤害、击退、无敌帧与音频。

CameraShake 公开 `static CameraShake Instance`、`Shake(float intensity, float duration)`。挂摄像机或单独管理物体并拖 `Shake Target`，留空使用自身 Transform。推荐 FollowRig → Camera 子物体，把震动放在 Camera 子物体上，跟随脚本只移动父节点，父节点缩放保持 1；不要让其他脚本逐帧覆写同一 Shake Target 的 localPosition。震动使用非缩放时间，局部 XY 位移以 1/32 单位取整；叠加取最大强度/更晚期限，结束/禁用/RunReset/F1 关闭时减去自身偏移，不改变跟随父节点。

HitStop 公开 `static HitStop Instance`、`Stop(float duration)`。仅 Playing 时接收，暂设 timeScale=0，通过 WaitForSecondsRealtime 恢复；重叠请求延长到更晚截止点。暂停、商店、失败、主菜单、重置会取消待恢复任务，绝不把这些状态恢复到 timeScale=1。禁用或关闭手感时，只有仍处 Playing 且冻结由自己持有才恢复时间。

## AudioManager

单独创建始终启用的 AudioManager 根对象（不要和其他场景管理器共用根对象），脚本 DontDestroyOnLoad。`Sound Effects` 数组每项选 id 并拖 AudioClip，`Music Clip` 拖 BGM；可选 `Music Source` 放在同一持久根或其子物体。未指定时自动创建 BGM Source，另自动创建默认 12 个可复用 2D SFX AudioSource。不用手动添加 SFX Source；场景摄像机保留一个 AudioListener。缺任何片段时对应播放安全跳过。

```csharp
public enum SfxId { Throw, Hit, Recall, EnemyDie, PlayerHurt, Explosion, Coin, Purchase, UIClick }
public static AudioManager Instance { get; }
public void PlaySfx(SfxId id, float pitchJitter = 0.1f);
public void PlayMusic();
public void StopMusic();
public float MasterVolume { get; set; }
```

MasterVolume 限 0..1，写 AudioListener.volume 和 PlayerPrefs 的 `NightShift.MasterVolume`，应用退出/失去前台暂停时保存。SFX Volume 默认 1、Music Volume 默认 0.5。SFX 优先空闲声音源，满载循环复用，默认 pitch 0.9–1.1。Gameplay 事件自动播放前八种声音；前端按钮显式 `AudioManager.Instance?.PlaySfx(SfxId.UIClick, 0f)`，不要重复绑定自动播放的事件。Playing 开始 BGM，Paused/Shop 保持播放位置，MainMenu/GameOver/Victory 停止；组件停用时清理所有播放，重新启用按当前状态同步。音量滑条读取/设置 MasterVolume 即可。

## 自动验证与编译

当前自动验证全部待运行；按用户要求，Unity 打开期间不执行下列命令。只有本项目编辑器关闭后才串行运行。命令不会保存场景/预制体；Play Mode 验证会创建临时内存物体，结束后关闭它自己启动的批处理编辑器。`M1Verification.Run()` 至 `M5Verification.Run()` 均为 Editor 专用、仅接受 batchmode 的测试入口，不挂组件，不进入玩家构建。当前编译可由 Claude 用 Unity MCP 查看 Console 确认。

```powershell
& 'E:\Unity\6000.6.4f1\Editor\Unity.exe' -batchmode -quit -projectPath 'F:\UCSC\CMPM125\GameplayProject1' -logFile 'F:\UCSC\CMPM125\GameplayProject1\Logs\compile.log'
& 'E:\Unity\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'F:\UCSC\CMPM125\GameplayProject1' -executeMethod M1Verification.Run -logFile 'F:\UCSC\CMPM125\GameplayProject1\Logs\m1-verification.log'
& 'E:\Unity\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'F:\UCSC\CMPM125\GameplayProject1' -executeMethod M2Verification.Run -logFile 'F:\UCSC\CMPM125\GameplayProject1\Logs\m2-verification.log'
& 'E:\Unity\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'F:\UCSC\CMPM125\GameplayProject1' -executeMethod M3Verification.Run -logFile 'F:\UCSC\CMPM125\GameplayProject1\Logs\m3-verification.log'
& 'E:\Unity\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'F:\UCSC\CMPM125\GameplayProject1' -executeMethod M4Verification.Run -logFile 'F:\UCSC\CMPM125\GameplayProject1\Logs\m4-verification.log'
& 'E:\Unity\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'F:\UCSC\CMPM125\GameplayProject1' -executeMethod M5Verification.Run -logFile 'F:\UCSC\CMPM125\GameplayProject1\Logs\m5-verification.log'
```

五条 Play Mode 验证命令不要加 `-quit`，否则会在跨帧检查完成前退出。以退出码 0 和对应日志中的 `M1_VERIFY PASS` 至 `M5_VERIFY PASS` 判断测试通过；失败/超时退出 1。测试使用普通游戏时间验证计时逻辑，同时使用非缩放时间监视暂停与超时。测试从对应 START 标记起捕获所有 Error/Exception/Assert，不屏蔽游戏错误。M1–M3 覆盖早期系统回归，M4 覆盖反馈/暂停/金币事件，M5 覆盖扇形和 Brute；这些脚本不能代替正式场景十波通关与 UI/音画试玩。
