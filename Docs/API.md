# Night Shift 后端 API 与配置

当前范围：M1 核心 + M2 波次和经济。数值依据 `Docs/DesignDoc.md`；重开/回菜单行为以已批准的 `Docs/REVIEW_M1.md` 为准。M3–M5 尚未实现。

所有类均在全局命名空间。只使用旧 Input Manager；游戏物体不靠 Tag 识别。HUD/菜单/特效应在 `OnEnable` 订阅 `GameEvents`，在 `OnDisable` 取消订阅。

## 状态与枚举

```csharp
public enum GameState { MainMenu, Playing, Paused, Shop, GameOver, Victory }
public enum EnemyType { Rusher, GuardBot, Brute }
public enum UpgradeType { ThrowRate, Damage, MaxHP, MoveSpeed, FanThrow, Heal }
```

`GameState.cs` 包含上述三个枚举，无需挂组件。UpgradeSystem 已实现；FanThrow 的购买入口暂不可用，实际扇形攻击按计划在 M5 实现。

## GameEvents

下表每个事件都有同参数的 `public static void Raise事件名(...)` 方法，事件仅在该静态类内部触发。前端负责监听；不要从显示层调用 Raise 方法模拟游戏状态。

| 事件 | 委托与参数 | 时机 |
| --- | --- | --- |
| PlayerHealthChanged | `Action<int,int>` current, max | 玩家扣血、回血、重置、主动发布 |
| BladesChanged | `Action<int,int>` inHand, max | 投刃、归还、重置、主动发布 |
| RecallCooldownChanged | `Action<float,float>` remaining, total | 回收冷却逐帧及结束的 `(0,total)`；暂停时数值不减少 |
| CoinsChanged | `Action<int>` total | 初始化、增减金币、重置 |
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
| `RestartGame()` | 原场景完整重置后直接开始第 1 波，供结算界面的“再来一局”调用 |
| `ReturnToMainMenu()` | 原场景完整重置后进入 MainMenu，供暂停/结算界面的“回主菜单”调用 |
| `QuitGame()` | `Application.Quit()`；编辑器内不会退出 Play Mode |
| `bool TrySpendCoins(int amount)` | 拒绝负数和余额不足；成功扣钱并发 CoinsChanged |
| `AddCoins(int amount)` | 忽略非正数；限制整数溢出并发 CoinsChanged |
| `CompleteWave(int wave)` | M2 刷怪器报告本波全部完成；重复/过期报告被忽略；等待 3 秒游戏时间后 Shop，波次 10 则 Victory |

除 Playing 外时间倍率为 0；暂停期间波次过渡也暂停。玩家 HP 归零进入 GameOver。重置顺序为：MainMenu → RunReset → 池中对象全部归还 → 停用未池化的场景敌人 → 玩家恢复初始状态 → 清零波次、金币和 HUD 计数。RunReset 会清理刷怪计划、升级、奖励倍率与待拾取物；M3 再接场景道具。RestartGame 完成此流程后立即开始第 1 波。

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

每次状态切回 Playing 都先等待左键释放，再接受下一次按住投掷，防止菜单点击误投刃。显式调用 `TryThrow()` 不经过鼠标输入门控，供游戏逻辑/测试使用。

公开只读属性：`Health Health`、`int BladesInHand`、`int MaxBlades`、`Vector2 AimDirection`。公开方法：`bool TryThrow()`（状态/数量/间隔/引用允许时投掷）、`TakeDamage(DamageInfo info)`、`ResetForNewRun()`（归还自身刃、恢复 Awake 记录的位置/初始 HP 与刃数、清冷却和速度）、`PublishStatus()`（血量/刃数/回收 HUD 快照）。输入和伤害仅 Playing 时生效；GameManager 统一调用重置。

M2 新增只读属性：`MoveSpeed`、`ThrowInterval`、`BladeDamage`、`FanThrowAvailable`、`FanThrowUnlocked`。新增公开方法：

```csharp
public void ApplyUpgrades(int throwRateLevel, int damageLevel, int maxHPLevel, int moveSpeedLevel, bool fanUnlocked);
```

UpgradeSystem 传入本局各项总等级。以 Awake 记录的 Inspector 原值为基础计算，重复应用不会叠加：投掷间隔 `base / (1 + 0.15 × level)`，伤害 `base + 10 × level`，移速 `base × (1 + 0.1 × level)`，最大 HP `base + 25 × level`。最大 HP 改变时 ResetHealth 并满血；其他购买不会意外回血。RunReset 和 ResetForNewRun 都恢复原值。刃上限保持初始 5（没有增加容量的商店商品）。FanThrowAvailable 暂为 false；前端可隐藏该商品，不能购买无实际效果的解锁。

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

Rusher 直线追逐，接触 PlayerController 时走 IDamageable 造成 10 伤害，玩家无敌帧防止每个物理帧扣血。击退使用 AddForce，并短暂让物理接管移动。定身冻结移动，可延长至更晚的结束时间。

M2 死亡顺序：DropTable.Drop → `EnemyBase.Died` → GameEvents.EnemyKilled → 回池。新增 `public static event Action<EnemyBase,int> Died`（enemy, spawnId）仅供后端按生成身份计数；UI/特效仍监听 GameEvents。WaveSpawner 只通过 Died 计数，避免同次死亡重复减数。重置/普通回池不产生死亡奖励；请勿在正常战斗中绕过死亡逻辑直接归还由刷怪器管理的敌人。

## WaveSpawner

在场景始终启用的管理物体上挂一个 WaveSpawner；不需要物理组件。拖 `Rusher Prefab`（RusherAI 组件）、`Spawn Points`（一组空 Transform）。`Guard Bot Prefab` / `Brute Prefab` 先留空，等 M3/M5 完成后接入；对应 Enemy Type 必须正确。`Player` 可选，供编辑状态 Gizmos 预览，开波时优先使用 GameManager.Player。

- 波次预算 `4 + 3 × wave`；Rusher/Guard/Brute 花费 1/2/4。有效 Guard 从第 3 波、Brute 从第 6 波开始，每波先保证各一只，再随机使用剩余预算；未接后续类型时以现有类型填满预算。
- `Spawn Interval = 1.5` 秒游戏时间，首批立即生成。`Enemies Per Burst = 3` 是策划案未指定的可调默认值。
- `Minimum Spawn Distance = 8`，每个敌人生成前重新筛选距离玩家 ≥8 且启用的刷怪点。无合法点就等待重试，保留全部预算，不在近处强行生成。
- Rusher 是确保任何预算余数都能填满的必要引用；缺失/类型不匹配时保留计划等待，不错误清波。完全未配置时 HUD 暂显示按 Rusher 数量估计的预算；引用有效后显示实际敌人数。
- Gizmos：合法点绿圈，过近/禁用/无玩家参考的点红圈；不修改项目 Layer/Tag。

公开只读属性：`CurrentWave`、`WaveBudget`、`PendingEnemies`、`ActiveEnemies`、`RemainingEnemies`、`ElapsedTime`、`IsWaveActive`。无需手动调用开波方法，监听 GameEvents.WaveStarted 自动启动协程。

RemainingEnemies = 待生成 + 存活；只有计划全部生成且本刷怪器追踪的敌人全部死亡才调用 GameManager.CompleteWave。以 EnemyBase 实例 + SpawnId 计数，每次死亡只减一次。暂停时生成/计时冻结，清波后停止计时，3 秒过渡由 GameManager 负责。RunReset 先停止协程/清账，再由 PoolManager 归还实例。不要在战斗中禁用此管理物体或直接回收它追踪的活敌人。

## DropTable

场景挂一个 DropTable 单例，放在始终启用的管理物体。公开 `static DropTable Instance`、只读 `float CurrentCoinMultiplier`，以及 `void Drop(Vector2 position, EnemyType type)`；EnemyBase 死亡自动调用一次，前端不要再次发奖励。

拖 `Coin Prefab` / `Health Prefab`，两种预制体都按下节配置 Pickup。字段默认：Rusher/Guard/Brute 基础金币 2/4/10；Extra Pickup Chance 0.15；Low Health Threshold 0.4；Low Health Weight Multiplier 3；Perfect Wave Coin Multiplier 1.5。

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

Shop/Victory 进入时会一次性收取剩余金币，保证时间暂停不会锁住本波收入；血包保留到后续战斗或重置。Paused/MainMenu/GameOver 不允许拾取。重置时普通归还不会发金币或回血。金币声音在 M4 接入。

## UpgradeSystem

场景放一个 UpgradeSystem 单例并保持启用；`Player` 可拖入或自动取 GameManager.Player。无 UI/物理组件依赖。价格和 Heal Amount 可在 Inspector 调整，默认严格按策划案：

| 类型 | 默认效果 | 上限 | 各等级价格 |
| --- | --- | --- | --- |
| ThrowRate | 投掷频率 +15%/级 | 3 | 20 / 35 / 55 |
| Damage | 伤害 +10/级 | 3 | 25 / 40 / 60 |
| MaxHP | 最大 HP +25/级并满血 | 3 | 20 / 35 / 55 |
| MoveSpeed | 移速 +10%/级 | 2 | 15 / 30 |
| FanThrow | M5 解锁扇形投掷；当前禁购 | 1 | 80 |
| Heal | 回复 50，封顶当前最大 HP | 无限 | 15 |

公开接口：`static UpgradeSystem Instance`、`int GetLevel(UpgradeType t)`、`int GetMaxLevel(UpgradeType t)`（Heal 返回 -1）、`int GetPrice(UpgradeType t)`（满级/无有效价格返回 -1）、`bool CanBuy(UpgradeType t)`、`bool TryBuy(UpgradeType t)`。

CanBuy/TryBuy 要求 Shop 状态、玩家存活、金币足够、未满级；Heal 还要求未满血，FanThrow 还要求功能已可用。TryBuy 成功扣款、应用等级和效果，并触发 CoinsChanged、UpgradePurchased；失败不扣款不升级。Heal 等级代表本局购买次数。RunReset 清零全部等级；UI 进入商店时读取这些方法重建商品显示。

最大 HP 通过 PlayerController.ApplyUpgrades → Health.ResetHealth 应用并触发 PlayerHealthChanged；其他升级不会因重算属性而回血。伤害由投刃时记录，已投出的刃保持当次投掷的伤害，升级影响之后的投掷。

## 后续里程碑

- M3：GuardBotAI、EnemyProjectile、ExplosiveBarrel、Crate。
- M3 同时落实 REVIEW_M1 第 3 条：飞刃推动非 IDamageable 动态刚体、钉在 Prop 后跟随其移动，回收/回池解除父子关系；回收刃仍穿透。
- M4：CameraShake、HitStop、GameFeel、AudioManager。
- M5：可选 BruteAI、扇形投掷。

这些脚本目前不存在，不应提前挂入预制体或从前端调用；约定签名保持待实现状态。

## 自动验证与编译

仅在本项目未被 Unity 编辑器打开时运行。命令不会保存场景/预制体；Play Mode 验证会创建临时内存物体，结束后关闭它自己启动的批处理编辑器。`M1Verification.Run()` 为 Editor 专用、仅接受 batchmode 的测试入口，不挂组件，不进入玩家构建。

```powershell
& 'E:\Unity\6000.6.4f1\Editor\Unity.exe' -batchmode -quit -projectPath 'F:\UCSC\CMPM125\GameplayProject1' -logFile 'F:\UCSC\CMPM125\GameplayProject1\Logs\compile.log'
& 'E:\Unity\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'F:\UCSC\CMPM125\GameplayProject1' -executeMethod M1Verification.Run -logFile 'F:\UCSC\CMPM125\GameplayProject1\Logs\m1-verification.log'
& 'E:\Unity\6000.6.4f1\Editor\Unity.exe' -batchmode -projectPath 'F:\UCSC\CMPM125\GameplayProject1' -executeMethod M2Verification.Run -logFile 'F:\UCSC\CMPM125\GameplayProject1\Logs\m2-verification.log'
```

两条 Play Mode 验证命令不要加 `-quit`，否则会在跨帧检查完成前退出。以退出码 0 和日志中的 `M1_VERIFY PASS` / `M2_VERIFY PASS` 判断测试通过；失败/超时退出 1。测试使用普通游戏时间验证计时逻辑，同时使用非缩放时间监视暂停与超时。测试从对应 START 标记起捕获所有 Error/Exception/Assert，不屏蔽游戏错误。
