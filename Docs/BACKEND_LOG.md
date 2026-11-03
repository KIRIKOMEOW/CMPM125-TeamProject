# Backend log

> M2 修复及 M3–M5 已交付并通过 Claude 统一审查；REVIEW_M3_M5 的 Guard 主动调查和油桶延迟连锁两项已修复并提交为 29b4cdd，后端停下等待试玩反馈。下文早期“等待审查”“未实现”是当时的历史记录，最新汇总见文末。Unity 保持打开；不执行 batchmode 或 Play Mode 自动验证。

## 2026-11-03 — M1 核心

### 已实现

- GameState/EnemyType/UpgradeType 枚举、完整 GameEvents 事件与 Raise 方法，保留前端约定签名。
- GameManager 单例、暂停/菜单/失败/胜利/商店状态入口、3 秒清波过渡、同场景重开、金币增减基础接口。M2 再接自动波次和经济循环。
- PoolManager 按预制体队列、预热、IPoolable 生命周期、重复归还防护、ReturnAll。
- IDamageable/DamageInfo、共享 Health、玩家 100 HP/0.6 秒无敌、可选 0.08 秒闪白。
- 玩家移动/鼠标瞄准/身体翻转/可选 Animator；5 把刃、25 伤害、击退 4、10 单位射程、穿透与墙体阻挡。
- 4 秒回收冷却、逐帧 HUD 事件、每刃每阶段伤害去重、同轮三刃命中定身 1 秒；池化复用时不继承上轮命中记录。
- Rusher 50 HP/接触 10 伤害、追击、击退、死亡一次回池；逐波速度 +5%，第 9 波起封顶 +40%。
- Editor 专用 M1Verification 可复现验证入口；不会打入游戏构建，不创建/保存测试场景或预制体资产。
- API 文档及前端交接请求。

### 审查中修复

- Health 属性延迟初始化，消除跨组件 Awake 顺序可能导致的重开 1 HP。
- Blade 同时检查玩家中心到出刃点的墙体，避免贴墙时出刃偏移越过薄墙。
- Unity 6000.6 使用无排序参数的 FindObjectsByType；验证脚本保留 SceneHandle 类型，去除旧版 API 错误/警告。
- 波次过渡若与暂停同帧发生，等待恢复后再打开商店。

### 验证状态

- Unity 6000.6.4f1 batchmode 编译已退出 0；`Logs/compile.log` 中 `error CS`、`warning CS` 均为 0。
- Play Mode 自动验证已退出 0；`Logs/m1-verification.log` 含 `M1_VERIFY PASS`。覆盖：池预热/不同实例/重复归还/复用/ReturnAll、100 HP 与 0.6 秒无敌、回血上限、Rusher 50 HP 与死亡一次、复用后 SpawnId、三刃出刃/回收各 75 伤害、1 秒定身、弹药归还、4 秒冷却及暂停、贴墙出刃、金币扣款、失败及同场景重开、重复/错误清波报告、3 秒清波等待/暂停、商店推进和第 10 波胜利。
- Play Mode 启动存在编辑器 `UnityEditor.Search.SearchDatabase` / `SearchInit.IndexationOnStartup` 的 ArgumentOutOfRangeException。堆栈不涉及游戏代码；没有修改搜索设置、Packages 或 ProjectSettings。验证脚本等待编辑器启动回调结束后才创建测试物体并发 `M1_VERIFY START`，此后所有 Error/Exception/Assert 仍会导致失败；没有按异常类型过滤或屏蔽错误。完整日志保留该启动异常，正式编辑器环境仍需关注它。

### 尚未验证与前端配合

- 正式场景中的鼠标瞄准、移动手感、真实碰撞矩阵、精灵/材质恢复、Animator 与 UI 视觉接线仍需前端在编辑器 Playtest。
- 不存在可运行的正式游戏场景/预制体；这是后端 M1 代码交付，配置步骤见 API。
- M2 尚未实现：WaveSpawner、DropTable、Pickup、UpgradeSystem，所以不会自动生成/计数敌人、掉落、发升级或自行完成波次。死亡接 DropTable 留待 M2。
- M3–M5 尚未实现：GuardBot、投射物、箱子/爆炸桶、手感/音频、可选 Brute/扇形投掷。Health 的 GameFeel 开关在 M4 接入。
- 重开会清理当前 M1 系统；升级/道具重置将在对应里程碑接 RunReset。
- 原有前端归属文件、ProjectSettings 和 Packages 未修改。未手工创建任何 .meta；Unity 导入自动生成的元数据保留在工作目录，当前源码提交不包含它们。前端后续提交场景/预制体时可一并跟踪这些 Unity 生成的 GUID 文件。

按用户工作顺序，M1 交付后停止，等待审查意见再推进 M2。

## 2026-11-03 — M1 审查修复

- 已处理 REVIEW_M1 的第 1、2、4 条：跟踪 Unity 生成的元数据；重开直接第 1 波、回菜单完整重置；从菜单恢复时等待左键释放后才能输入投掷。
- 同步 API、交接说明和 M1 自动验证；第 3 条的刃/箱子物理交互明确留到 M3。
- 更新后的 M1Verification 已通过（退出码 0），新增鼠标释放门控、直接重开第 1 波、暂停/胜利后回菜单完整重置检查；Unity 编译退出 0，C# 错误/警告均为 0。修复单独提交后继续 M2。

## 2026-11-03 — M2 波次与经济

### 已实现

- WaveSpawner 协程：预算 4+3×波次，首批立即、之后每 1.5 秒，默认每批 3；每次选距玩家至少 8 单位的启用刷怪点。没有安全点或有效 Rusher 时保留预算等待。
- 待生成+存活敌人计数、SpawnId 唯一死亡结算、波次计时、暂停冻结、RunReset 清理。后续类型配置有效时，Guard 从第 3 波、Brute 从第 6 波至少各一只，剩余预算随机填满；M2 只需 Rusher。
- EnemyBase 死亡先 DropTable.Drop，再 Died 计数，再 EnemyKilled 表现事件，最后回池；重置/普通回池不发奖励。
- DropTable：基础金币 2/4/10、15% 额外拾取物、血量 <40% 血包权重×3、无伤后下一波金币×1.5；缺金币资源时直接入账。
- Pickup：金币吸附/血包回血/池化复用与防重复拾取，进入 Shop/Victory 收取剩余金币，清场不发奖励。
- UpgradeSystem：完整约定接口、各项价格/上限、Shop 状态与资金/血量检查、成功事件、重置等级。FanThrow 保留 80 价格和一级上限，M5 实际技能完成前禁购。
- PlayerController.ApplyUpgrades：从缓存初值计算射速/伤害/移速/最大 HP；MaxHP 每级+25并满血，其他升级不回血，RunReset/重开恢复初值与五把刃。
- M2Verification：针对上述经济、波次、随机权重边界和重置流程的独立 Play Mode 验证。测试只创建内存物体，不保存场景或预制体。

### 验证与限制

- M1 审查修复已单独提交为 d6fabad；更新后的 M1 Play Mode 验证通过，修复阶段编译 0 错误/0 C# 警告。
- M2 自动验证：**待运行**。本次跳过，不要求用户关闭 Unity，也不等待编辑器释放项目；前端继续使用打开的编辑器。
- 后续验证：Unity 关闭后，一起运行 M1Verification.Run 和 M2Verification.Run。M1 修复阶段的既有通过记录保留，但不能代替 M2 合入后的整体回归。
- 编译确认由 Claude 通过 UnityMCP 读取 Unity Console 完成。先前 batchmode 编译退出 0、无 C# 错误/警告属于历史记录，不代替前端对当前 Console 的确认。
- 两名代理独立审查波次/经济/升级接线，未发现正常流程阻塞；正常流程要求 WaveSpawner 始终启用，敌人只通过死亡/整局重置回池。
- API/REQUESTS 已更新；M2 已提交为 1647d64，新脚本与 Unity 自动生成的对应 .meta 均已跟踪，稳定预制体 GUID。
- 正式场景/预制体、UI、输入焦点、实际碰撞矩阵、美术音效与数值手感仍需前端配置和 Playtest；没有提前执行 M3 的物理道具/Guard AI。
- Packages/manifest.json、Packages/packages-lock.json 与 ProjectSettings/PackageManagerSettings.asset 的改动由前端安装 MCP 产生，归前端管理；后端不编辑、不暂存、不提交。Docs/REVIEW_M1.md 也未纳入后端提交。

M2 提交后停下等待审查，不推进 M3。Unity 保持打开供前端使用；仅在后续 Unity 关闭时一并补跑 M1 和 M2 自动验证。

## 2026-11-03 — M2 审查修复（f790c4e）

- 按 REVIEW_M2 两条意见：Rusher 默认金币 2→3，首波基础收入 21；同批刷怪点按 Transform 去重，安全点不足就缩小本批、保留剩余预算到下一批。
- M2Verification 同步金币/加成取整预期，并覆盖多点去重和重复引用单点限批。
- 提交 `backend: M2 review fixes` 仅包含后端脚本；没有提交前端已改的 DesignDoc、Packages 或 ProjectSettings。
- 更新后的 M1/M2 自动验证：**待运行**。最新代码已确认编译通过：Claude 已在 d803e57 之后通过 UnityMCP 强制刷新并重新编译，Console 0 错误、0 警告，见 REVIEW_M3_M5；这更新了先前仅有 REVIEW_M2 编译确认的记录。

## 2026-11-03 — M3 AI 和物理（03c356d）

- GuardBotAI：75 HP，Patrol/Chase/Attack/Search 状态机、视线射线、6 单位攻击范围、3 秒搜索、Gizmos 与 CurrentStateName；池化敌方子弹检查沿途掩体及枪口遮挡，只伤玩家。
- Crate：动态刚体、默认质量 2/阻尼 5；重开恢复原位。落实 REVIEW_M1 第 3 条：飞刃对非伤害接收器动态刚体施加冲量，每阶段去重；钉住后用局部锚点跟随平移/旋转，回收解除锚点并穿透。不改父子层级，避免目标销毁连带删除刃。
- ExplosiveBarrel：两击爆炸，半径 3、伤害 60、冲量 12；按伤害接收器和刚体分别去重，玩家无敌帧仍受推力，可连锁，下一波/重开恢复。嵌套伤害批次防止玩家先死导致同一爆炸漏伤其他目标。
- 新增 Editor/M3Verification，覆盖 AI/视线/子弹/池化/暂停、推箱/跟随/回收、爆炸去重/冲量/连锁/重生及玩家致死顺序。
- 验证：MCP 资产刷新后 Console 读取未见错误或警告；**M3 自动验证待运行**，未进入 Play Mode，正式碰撞/视线/手感尚未试玩。最终编译与完整提交审查仍交 Claude。
- 前端配合：配置 Guard/EnemyProjectile 预制体、刷怪器 Guard 引用、可选 Patrol/FirePoint；World + Prop/Crate 同时填入 Guard 视线、子弹 Blocking Layers、Blade Wall Layers。箱子/桶根对象不要随特效销毁；组件和暂定可调数值详见 API。

## 2026-11-03 — M4 手感与音频（f456410）

- GameFeel.Enabled 默认开启，F1 切换；事件驱动投刃、命中、死亡、玩家受伤、爆炸和回收反馈。关闭时立即清理震屏/停顿/闪白，不改伤害、击退、无敌帧和音频。
- CameraShake：非缩放时间协程、1/32 单位 XY 量化、叠加延长期限、结束/重置恢复自身偏移。HitStop：死亡 0.05 秒、爆炸 0.1 秒；WaitForSecondsRealtime 恢复，重叠延长，暂停/商店/结算切换取消恢复任务。
- AudioManager：DontDestroyOnLoad 单例、默认 12 个复用 2D 音效声道、可空音频映射、pitch 抖动、BGM 状态同步、MasterVolume/PlayerPrefs。停用清理声音，重新启用同步状态。
- 新增 CoinCollected(position, amount) 语义事件，实际金币拾取/商店剩余结算/缺资源兜底入账触发；消费、直接设置经济和重开不会误播拾取声。原约定事件/方法未变。
- M4Verification 已写入；M1–M3 验证准备阶段清除场景已有持久 AudioManager，避免音频事件和随机抖动干扰临时测试。
- 验证：MCP 刷新完成后 editor/state 显示未编译中、未待重载，Console 0 条错误/警告；**M4 自动验证待运行**。未进入 Play Mode，真实音频、材质、像素相机震动和 F1 前后效果尚未试玩。
- 前端配合：始终启用的 GameFeel/HitStop；CameraShake 推荐放 FollowRig 下独立摄像机子节点；AudioManager 独立持久根对象，填 9 种音效/BGM，摄像机保留一个 AudioListener；Options 音量绑定 MasterVolume，UIClick 由按钮显式播放，其他事件无需重复绑定音效。

## 2026-11-03 — M5 Brute 与扇形投掷（d803e57）

- BruteAI：200 HP、基础移速 2、半额击退、锁定方向蓄力 0.8 秒后冲锋；Pursue/WindUp/Charge/Recover 四阶段，Continuous 碰撞检测，撞配置掩体层停止，回收定身可中断蓄力/冲锋，重生/回池清理所有阶段。
- 未定参数采用可调默认值：触发距离 7、冲锋速度 12/时长 0.5 秒、恢复 0.5 秒、结束后冷却 2.5 秒、接触伤害 20/击退 6；普通与冲锋速度均沿用逐波 +5%、最高 +40%。这些数值需要试玩。
- FanThrowAvailable 开放，80 金币解锁且一级封顶。右键一次 3 把，默认角度 -15/0/+15；先取得全部有效实例再消耗弹药/共享冷却，少于 3 把、缺资源或无效 Blade 时不消耗。沿用每把伤害、回收、三刃定身和推箱行为。
- 左右鼠标各自拥有菜单释放门控，右键在解锁后优先占用本帧，避免同时单发/扇形。RunReset/重开清除解锁、恢复五把刃；现有 TryThrow 签名及测试调用行为保留。
- 新增 M5Verification；M2Verification 从“未开放不可买”更新为 80 金币实际购买、一级上限和总 14 次成功购买事件，重置仍检查去除解锁。
- 验证：最后一次 MCP 资产刷新后 editor/state 确认编译及重载完成，Console 读取 0 条错误/警告；**M5 自动验证待运行**。未执行 batchmode、未进入 Play Mode；新测试均只完成源码检查，运行通过与否尚未知。
- 前端配合：Brute 预制体/Charge Blocking Layers/WaveSpawner 引用、蓄力冲锋提示动画、撤除商店 FanThrow“未开放”硬编码；按 API 试玩冲锋节奏与扇形宽度。

## 本轮最终交付与统一审查清单

| 提交 | 内容 | 自动验证 |
| --- | --- | --- |
| f790c4e — backend: M2 review fixes | 金币 3、同批安全点去重、对应回归脚本 | M1/M2 最新合入后回归：待运行 |
| 03c356d — backend: M3 AI and physics | Guard/子弹/箱子/桶、飞刃推箱与跟随 | M3：待运行 |
| f456410 — backend: M4 game feel and audio | 震屏/停顿/F1/白闪/音频/金币音效事件 | M4：待运行 |
| d803e57 — backend: M5 brute and fan throw | Brute、右键扇形、商店开放与回归脚本 | M5：待运行 |

- Unity 一直保留打开，按用户“不抢鼠标”要求停止界面操作后仅使用 Unity MCP 刷新、读状态/Console，未再操作鼠标/键盘/窗口。没有启动 batchmode 或自动测试，也没有改 Play Mode 状态。
- 上述四个提交已由 Claude 统一审查通过，d803e57 后最新代码已通过 UnityMCP 强制刷新/重新编译确认：0 错误、0 警告。REVIEW_M3_M5 的两条修复记录见下文；编译确认不等于自动运行验证通过。
- 当前 M1–M5 自动验证均为 **待运行**。待 Unity 关闭后按 API 中五个入口串行补跑，尤其同时回归 M1/M2；历史 M1 通过记录不替代最终版本回归。
- 正式场景中的完整 10 波、胜负/重开/暂停/商店、输入焦点、碰撞矩阵与掩体、音效/BGM/音量保存、材质/相机与 F1 对比、性能及所有暂定数值尚需前端 Playtest。
- 四个新提交只包含负责目录的 C# 脚本及 Unity 自动生成的对应 .meta。没有手写元数据；没有暂存/提交 Docs/DesignDoc.md、REVIEW 文档、Packages/ 或 ProjectSettings/ 的前端改动，也没有修改场景/预制体/UI/美术/音频资产。
- Docs/API.md、Docs/BACKEND_LOG.md、Docs/REQUESTS.md 已更新到 M5，按“只提交脚本和 .meta”的范围保留未提交工作区改动，供同目录 Claude 审查。

后端按要求停在此处，等待用户统一审查反馈；不自行进入下一轮改动或测试。

## 2026-11-03 — M3–M5 审查修复（29b4cdd）

- 按 Docs/REVIEW_M3_M5.md 完成两条修改；原四个里程碑提交已审查通过，Claude 在 d803e57 之后确认最新代码编译 0 错误、0 警告，上文旧确认状态已更新。
- GuardBotAI 新增 Investigate After（默认 5 秒）。Patrol 持续未见玩家时，快照玩家当前位置转 Search；搜索 3 秒仍未发现后，以自身当前位置重建 ±Patrol Radius 巡逻点并重新计时，之后继续调查。正常视线 Chase/Attack 优先级保持，暂停冻结计时，池化重生清理计时和巡逻状态。
- ExplosiveBarrel 对来自另一只桶的正伤害直接排定连锁，不需要先受普通攻击；Chain Reaction Delay 默认 0.08 秒，使用 WaitForSeconds。等待期间命中去重且不提前引爆；普通攻击仍为两击。非 Playing 状态（含 Pause/Shop/GameOver）、RunReset/ResetBarrel、停用组件均取消待引爆，恢复游戏不补炸。
- 每段实际爆炸独立结算伤害批次；玩家在某段中死亡不影响该段其他目标结算，随后非 Playing 状态取消后续待引爆的桶。取消不消费原有普通攻击命中次数，重置恢复完整两击。
- M3Verification 已同步新增 Guard 定时调查/巡逻中心重建/暂停/复用及新桶连锁/防重复/取消行为的回归检查；**待运行**。M1–M5 自动验证继续等 Unity 关闭后串行补跑，没有启动 batchmode、测试或 Play Mode。
- 两项生产代码均完成源码与交叉检查；API 中对应行为与两个新 Inspector 参数已同步。Brute 暂定数值、LayerMask 接线和 Camera 子物体约定不改，后续由前端试玩反馈。
- 本次修复及验证脚本写完后，也通过现有 Unity MCP 刷新并请求脚本编译；editor/state 确认编译/重载已完成，Console 0 错误、0 警告。仅后台 API 操作，没有控制鼠标/键盘/窗口，没有关闭 Unity 或运行测试。
- 沿用提交范围：只提交本次后端 C# 脚本，现有对应 .meta 已跟踪且未改动。BACKEND_LOG/API 保留工作区更新；Packages/、ProjectSettings/ 和前端资产未改动、未暂存。

已提交 `29b4cdd — backend: M3-M5 review fixes`（GuardBotAI、ExplosiveBarrel、M3Verification 三个脚本）。已停下，后续只等待前端试玩反馈。
