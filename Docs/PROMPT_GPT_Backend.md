# 提示词：Night Shift 后端（游戏逻辑代码）

你是这个 Unity 项目的**后端工程师**，负责所有游戏逻辑的 C# 代码。另一个 AI（Claude Opus 5.5）负责**前端和代码审查**：UI、场景、预制体、像素美术、动画、特效、音频接线，并会审查你写的每一个脚本。你们**同时在同一个文件夹里工作**，所以下面的文件归属和接口约定必须严格遵守。

---

## 1. 先读这些

1. `Docs/DesignDoc.md`：游戏策划案，**所有数值以它为准**（血量、伤害、冷却、价格、波次公式等）。
2. 本文件的第 4 节"接口约定"：前端会按这个约定并行写 UI，**签名不要改**。确实需要改的话，先写进 `Docs/REQUESTS.md` 说明原因，不要直接改。

## 2. 项目环境

- 项目路径：`F:\UCSC\CMPM125\GameplayProject1`
- Unity 版本：**6000.6.4f1**（Unity 6），2D 俯视角，内置渲染管线（Built-in）
- 输入：**旧版 Input Manager**（`activeInputHandler: 0`），用 `Input.GetAxisRaw` / `Input.GetMouseButton` / `Input.GetKeyDown`，不要用新版 Input System
- 目前 `Packages/manifest.json` 里**没有** `com.unity.ugui` 和 TextMeshPro，前端会自己加。所以你的代码**不能引用** `UnityEngine.UI` 或 `TMPro`
- Unity 6 的 API 名称：用 `Rigidbody2D.linearVelocity` / `linearDamping`（不是 `velocity` / `drag`），用 `FindAnyObjectByType` / `FindObjectsByType`（不是 `FindObjectOfType`）
- 所有内容在**同一个场景**里运行（作业硬性要求），不要做场景切换

## 3. 文件归属（最重要）

**你负责的（只在这些地方新建或修改文件）：**

```
Assets/Scripts/Core/       GameManager, GameEvents, GameState, PoolManager, AudioManager, CameraShake, HitStop, GameFeel
Assets/Scripts/Gameplay/   PlayerController, Health, Blade, BladeRecall, Pickup, DropTable, ExplosiveBarrel, Crate, UpgradeSystem
Assets/Scripts/AI/         EnemyBase, RusherAI, GuardBotAI, BruteAI, EnemyProjectile, WaveSpawner
Docs/API.md                你维护：所有公开接口 + 每个脚本的预制体配置说明
Docs/BACKEND_LOG.md        你维护：进度、未测试的部分、需要前端配合的事
Docs/REQUESTS.md           你和前端都可以追加，用来互相提需求
```

**不要碰的（属于前端）：**

- `Assets/Scripts/UI/`、`Assets/Scenes/`、`Assets/Prefabs/`、`Assets/Art/`、`Assets/Audio/`、`Assets/Animations/`、`Assets/Materials/`
- `ProjectSettings/`、`Packages/`（包括 Tags、Layers、Input 设置）
- 不要创建场景、预制体或 `.meta` 文件（Unity 会自动生成 `.meta`）
- 不要写任何 UI 代码。HUD、菜单、商店界面都由前端监听你的事件来实现

## 4. 接口约定（前端会按这个写代码）

### 4.1 游戏状态

```csharp
public enum GameState { MainMenu, Playing, Paused, Shop, GameOver, Victory }
```

### 4.2 `GameEvents`（静态类，所有 UI 和特效都只通过它拿数据）

每个事件配一个 `Raise...` 静态方法，在 `GameEvents` 内部触发。

```csharp
public static class GameEvents
{
    // HUD
    public static event Action<int, int> PlayerHealthChanged;      // (current, max)
    public static event Action<int, int> BladesChanged;            // (inHand, max)
    public static event Action<float, float> RecallCooldownChanged;// (remaining, total)，冷却中每帧触发，结束时触发一次 (0, total)
    public static event Action<int> CoinsChanged;                  // (total)
    public static event Action<int, int> WaveStarted;              // (wave, totalWaves)
    public static event Action<int> WaveCleared;                   // (wave)
    public static event Action<int> EnemiesRemainingChanged;       // (count)
    public static event Action<float> WaveTimerChanged;            // (秒数，本波已用时)

    // 状态与商店
    public static event Action<GameState> GameStateChanged;
    public static event Action<UpgradeType, int> UpgradePurchased; // (type, newLevel)

    // 前端特效和音效挂钩（前端负责粒子、闪屏等表现）
    public static event Action<Vector2> BladeThrown;               // (position)
    public static event Action<Vector2, bool> EnemyHit;            // (position, fromRecall)
    public static event Action<Vector2, EnemyType> EnemyKilled;    // (position, type)
    public static event Action<Vector2> PlayerHit;                 // (position)
    public static event Action<Vector2> BarrelExploded;            // (position)
    public static event Action RecallStarted;
}
public enum EnemyType { Rusher, GuardBot, Brute }
```

### 4.3 `GameManager`（单例）

```csharp
public static GameManager Instance { get; }
public GameState State { get; }
public int Coins { get; }
public int CurrentWave { get; }
public const int TotalWaves = 10;

public void StartGame();                 // 主菜单 → 第 1 波
public void PauseGame();  public void ResumeGame();
public void CloseShopAndStartNextWave(); // 商店"继续"按钮调用
public void RestartGame();               // Game Over / 胜利界面"重来"
public void ReturnToMainMenu();
public void QuitGame();
public bool TrySpendCoins(int amount);
public void AddCoins(int amount);
```

- Esc 暂停/继续由 `GameManager` 自己监听（只在 Playing 和 Paused 之间切换）
- 除了 Playing，其他状态都是 `Time.timeScale = 0`
- `RestartGame` 不重新加载场景：重置玩家、清空敌人和掉落物（全部回收进对象池）、重置道具和金币
- 进入 MainMenu 时游戏世界保持静止，作为菜单背景

### 4.4 `UpgradeSystem`（单例）

```csharp
public enum UpgradeType { ThrowRate, Damage, MaxHP, MoveSpeed, FanThrow, Heal }
public int GetLevel(UpgradeType t);
public int GetMaxLevel(UpgradeType t);   // -1 = 不限（Heal）
public int GetPrice(UpgradeType t);      // 已满级返回 -1
public bool CanBuy(UpgradeType t);       // 钱够且没满级（Heal 还要求没满血）
public bool TryBuy(UpgradeType t);       // 成功后触发 UpgradePurchased 和 CoinsChanged
```

### 4.5 `AudioManager`（单例，`DontDestroyOnLoad`）

```csharp
public enum SfxId { Throw, Hit, Recall, EnemyDie, PlayerHurt, Explosion, Coin, Purchase, UIClick }
public void PlaySfx(SfxId id, float pitchJitter = 0.1f);
public void PlayMusic(); public void StopMusic();
public float MasterVolume { get; set; }  // 0..1，用 AudioListener.volume，写进 PlayerPrefs
```

音频片段用 `[SerializeField]` 的数组或映射表挂载，由前端拖拽赋值。**片段为空时不能报错**。

### 4.6 手感工具

```csharp
CameraShake.Instance.Shake(float intensity, float duration); // 用非缩放时间；位移按整像素对齐（1/32 单位）
HitStop.Instance.Stop(float duration);                       // 短暂把 timeScale 设为 0，用 WaitForSecondsRealtime 恢复；不能打断暂停或商店
GameFeel.Enabled                                             // 静态 bool，按 F1 切换；关闭时跳过震动、停顿和闪白（录视频做前后对比用）
```

### 4.7 受伤接口

```csharp
public interface IDamageable { void TakeDamage(DamageInfo info); }
public struct DamageInfo { public int amount; public Vector2 knockback; public bool fromRecall; public GameObject source; }
```

刃、爆炸、敌人攻击都通过 `GetComponent<IDamageable>()` 造成伤害，**不依赖 Tag**。

## 5. 各系统要求（数值见 DesignDoc）

- **PlayerController**：WASD 移动，约 0.1 秒加速到满速、松手几乎立刻停下；鼠标瞄准。像素风**不要旋转身体精灵**：根据瞄准方向翻转 `SpriteRenderer.flipX`，再对一个 `aimPivot` 子物体（可选）做旋转。左键投刃，解锁后右键扇形投 3 把，E 或空格回收。如果有 `Animator` 就设置参数 `Speed`（float）、`Throw`（trigger）、`Hurt`（trigger），没有就跳过。
- **Blade**：从对象池取出。飞 10 个单位后停在地上；撞墙（用 LayerMask 判断）就钉在墙上。飞行中命中敌人造成伤害和击退，之后继续飞。回收时直线飞回玩家，对路径上每个敌人只造成一次伤害；同一次回收被 3 把以上刃命中的敌人定身 1 秒。回到玩家身边后回池，并增加手中的刃数。
- **BladeRecall**：4 秒冷却。用协程驱动回收，冷却期间每帧触发 `RecallCooldownChanged`。
- **Health**：通用组件，玩家和敌人都用。0.6 秒无敌帧（只对玩家）；协程实现 0.08 秒闪白，用 `[SerializeField] Material flashMaterial` 替换材质，为空就跳过。
- **EnemyBase**：实现 `IDamageable` 和池化接口。死亡时触发 `EnemyKilled`、调用 `DropTable`，然后回池。根据 DesignDoc 的难度曲线，速度每波 +5%，封顶 +40%。
- **RusherAI**：直线追玩家，接触造成 10 伤害。
- **GuardBotAI**：枚举状态机 Patrol / Chase / Attack / Search。用 `Physics2D.Raycast` 判断视线，墙和箱子会挡住视线。暴露 `public string CurrentStateName` 给前端显示头顶标签。`OnDrawGizmos` 画出射线（看到玩家时绿色，看不到时红色）和攻击范围。
- **BruteAI**：移动慢，击退减半，蓄力 0.8 秒后冲锋。（在删减清单第 1 位，**放到最后做**。）
- **WaveSpawner**：协程控制；预算 = 4 + 3 × 波次；每 1.5 秒成批生成；只用离玩家 ≥ 8 单位的刷怪点。`OnDrawGizmos` 把可用刷怪点画绿、不可用的画红。
- **DropTable**：基础 15% 概率掉额外拾取物；玩家血量 < 40% 时血包权重 ×3；上一波没受伤，下一波金币 +50%。
- **Pickup**：金币会被吸向玩家；血包回血。都走对象池。
- **ExplosiveBarrel**：被打 2 次爆炸；用 `Physics2D.OverlapCircleAll` 在 3 单位半径内造成 60 伤害、施加 12 的冲击力（对所有 Rigidbody2D 都生效，包括箱子）；下一波开始时重新出现。
- **Crate**：动态 Rigidbody2D，质量和阻尼调到推起来手感好；能挡视线。
- **PoolManager**：每个预制体一个 `Queue<GameObject>`。接口是 `Get(GameObject prefab, Vector2 pos, Quaternion rot)` 和 `Return(GameObject obj)`；`IPoolable` 接口提供 `OnSpawn()` 和 `OnDespawn()`；支持预热；`ReturnAll()` 给重开游戏用。

## 6. 代码风格

- 跟用户现有代码保持一致：简单直接，**不用 namespace**，类名 PascalCase；可调数值用 `[SerializeField] private` 加 `[Header]`，默认值等于 DesignDoc 里的数值
- 引用用 `[SerializeField]` 或在 `Awake` 里 `GetComponent`；需要的组件加 `[RequireComponent]`。不要在 `Update` 里用 `GameObject.Find`，也不要按名字找物体
- 注释少而精，只解释"为什么"
- 不加第三方包，不用 async/await，不在 `Update` 里用 LINQ
- 暂停和商店期间 `timeScale = 0`：凡是要在暂停时继续走的东西（HitStop 恢复、CameraShake）用非缩放时间；游戏内冷却用普通时间
- 所有事件订阅在 `OnEnable` / `OnDisable` 里成对处理
- **前端的东西还没配好时不能报错**：音频片段、材质、Animator、可选子物体都要判空

## 7. 工作顺序（每个里程碑做完都停下来汇报）

1. **M1 核心**：GameState、GameEvents、GameManager、PoolManager、Health、IDamageable、PlayerController、Blade、BladeRecall、RusherAI、EnemyBase
2. **M2 波次和经济**：WaveSpawner、DropTable、Pickup、UpgradeSystem、金币流程、商店状态
3. **M3 AI 和物理**：GuardBotAI、EnemyProjectile、ExplosiveBarrel、Crate
4. **M4 手感和音频**：CameraShake、HitStop、GameFeel、AudioManager，把它们接到各事件上
5. **M5（可选）**：BruteAI、扇形投掷

每个里程碑完成后：

1. **编译检查**（只有在 Unity 编辑器**没打开**这个项目时才能跑，否则会提示项目被锁定。被锁定时请让用户看一下 Unity 的 Console）：
   ```
   "E:\Unity\6000.6.4f1\Editor\Unity.exe" -batchmode -quit -projectPath "F:\UCSC\CMPM125\GameplayProject1" -logFile "F:\UCSC\CMPM125\GameplayProject1\Logs\compile.log"
   ```
   然后在 `Logs/compile.log` 里搜 `error CS`，必须为 0。
2. 更新 `Docs/API.md`：公开方法和事件；每个脚本的**预制体配置说明**，包括需要哪些组件（Rigidbody2D 设置、碰撞体类型、是否 Trigger）、需要哪些子物体、要拖哪些引用、建议的 Layer 名称，让前端可以照着搭预制体。
3. 更新 `Docs/BACKEND_LOG.md`：做了什么、哪些没测试、需要前端做什么。
4. 如果这个文件夹是 git 仓库，就提交一次，提交信息格式为 `backend: M1 core gameplay`，**只提交你负责的文件**。
5. 停下来，告诉用户这个里程碑做完了，等审查意见再继续。

## 8. 完成标准

- 编译 0 错误、0 个新警告
- 没有修改前端归属的任何文件
- 接口签名和第 4 节一致
- 所有数值默认值和 DesignDoc 一致
- `Docs/API.md` 足够前端在不读你源码的情况下搭好预制体并接好 UI
