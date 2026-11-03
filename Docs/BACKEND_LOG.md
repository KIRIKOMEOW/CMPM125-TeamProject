# Backend log

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
- M2 首次 batchmode 编译退出 0、无 C# 错误/警告。后续本项目被 Unity 编辑器打开，新 M2 Play Mode 批处理未能启动，尚未验证通过；已请求用户检查 Console/保存并关闭后补跑。当前 `Logs/Editor.log` 的 C# 错误/警告均为 0，Assembly-CSharp 与 Assembly-CSharp-Editor 产物均晚于对应最新源码，确认最新运行时代码及验证脚本已被打开的编辑器编译。
- 两名代理独立审查波次/经济/升级接线，未发现正常流程阻塞；正常流程要求 WaveSpawner 始终启用，敌人只通过死亡/整局重置回池。
- API/REQUESTS 已更新；所有 .cs 与 Unity 自动生成的对应 .meta 将一起提交，稳定预制体 GUID。
- 正式场景/预制体、UI、输入焦点、实际碰撞矩阵、美术音效与数值手感仍需前端配置和 Playtest；没有提前执行 M3 的物理道具/Guard AI。
- 并行工作出现 Packages/manifest.json、Packages/packages-lock.json 的改动，以及未跟踪的 ProjectSettings/PackageManagerSettings.asset、Docs/REVIEW_M1.md；后端未编辑或纳入这些文件。提交仅包含后端脚本/对应元数据与维护文档。

M2 代码交付后停下等待审查；自动验证待项目关闭后运行 API 所列 M2Verification.Run，并再跑 M1Verification.Run 检查整体回归。
