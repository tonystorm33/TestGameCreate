# Unity 知识体系与使用实例

课程已同步到 PlayScene 的五个知识卡片，共 53 章。建议顺序：生命周期 → 对象池 → ECS → 网络同步 → 热更新。

每章包含原理、使用场景、注意事项和验收练习。界面支持章节选择、15 秒起步的可调自动播放、长代码滚动与复制。

拓展脚本存放在 `Assets/Resources/Learning/Examples`，以 `.cs.txt` 保存供阅读和复制。现在还提供五个真实运行的独立场景与 49 项测试，见 [流程实验室与代码解析](流程实验室与代码解析.md)。工程已安装 Entities 1.0.16；网络实验使用本机真实 TCP，资源更新使用本机真实 HTTP 与构建包。正文的 NGO、Baking 和远程 HTTPS 拓展示例与实验室实现不同，仍应按注明的环境单独验证。

| 主题 | 章节 | 完整实例 | 运行条件 |
| --- | --- | --- | --- |
| 生命周期 | 11 | 生命周期观察器、平滑相机跟随 | Unity 2022.3，支持旧输入接口 |
| 对象池 | 10 | 自动创建的球形子弹池 | Unity 2022.3，内置 ObjectPool |
| ECS | 11 | Baking + 批量移动 System | 单独实验工程安装 Entities 1.0.x；显示网格需要匹配渲染包 |
| 网络同步 | 11 | 服务器权威开关 | 单独实验工程安装 NGO 1.8.1 + Unity Transport，双端运行 |
| 热更新 | 10 | 单包 AssetBundle 资源切换 | HTTPS 资源服务器、对应平台构建包、真实 hash 与 CRC |

ECS 渲染示例需按 [Entities Graphics 1.0 兼容要求](https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.0/manual/requirements-and-compatibility.html) 配置 SRP。

## 生命周期

**学习目标**：理解回调时机与执行次数；区分物理与渲染帧；按启用／禁用管理事件与协程；能排查初始化和重复订阅问题。

**预备知识**：会创建 GameObject、添加组件、读取 Inspector 字段；了解 C# 方法、事件和基本协程。

### 1. 全景与使用场景

生命周期是引擎在特定阶段调用脚本的方法。不要把它看成只走一次的线性流程：Awake、Start 通常各一次；OnEnable／OnDisable 可反复；Update、FixedUpdate、LateUpdate 持续循环。
理解这些阶段，才能把初始化、输入、物理、跟随和清理放在合适的位置。

**使用场景与操作**

角色控制、跟随摄像机、UI 面板开关、对象池复用、场景加载、全局事件监听。

**常见误区与边界**

不要把固定更新当作每帧固定执行一次，也不要依赖不同对象的 Awake 默认先后顺序。

**动手练习与验收**

画出“创建 → 启用 → 首帧 → 持续更新 → 禁用／重新启用 → 销毁”的路径，标出哪些阶段重复。

### 2. Awake：自身初始化

脚本实例的 Awake 只调用一次。初始不活跃的 GameObject 会延迟到激活时调用。脚本组件 enabled 为 false 不等同于 GameObject 不活跃。
适合缓存自身组件、初始化容器和检查必要配置；不要假定另一个对象已完成 Awake。

**使用场景与操作**

缓存 Rigidbody、Renderer、Collider；构建本地列表；验证 Inspector 引用。

**示例片段**

```text
void Awake()
{
    body = GetComponent<Rigidbody>();
    if (body == null) Debug.LogError("缺少 Rigidbody", this);
}
```

**常见误区与边界**

Awake 不适合依赖另一个脚本的 Start 结果。跨对象依赖可显式注入，或通过统一初始化器组织。

**动手练习与验收**

分别测试：对象初始活跃、对象初始不活跃、仅组件禁用，比较 Awake 日志。

### 3. OnEnable：开启与订阅

对象在层级中活跃且组件启用时执行。每次重新启用都可能执行，适合建立这一次活跃期的监听关系。
订阅动作要与 OnDisable 配对，否则反复打开面板后，一次事件可能触发多次处理。

**使用场景与操作**

打开背包时订阅金币变化；角色出池时开启计时器；地图标记启用时监听位置更新。

**示例片段**

```text
void OnEnable() { ScoreChanged += Refresh; }
void OnDisable() { ScoreChanged -= Refresh; }
// 使用同一个委托引用进行订阅与解绑。
```

**常见误区与边界**

匿名 lambda 每次创建的新委托不方便解除，应保存委托或使用命名方法。

**动手练习与验收**

打开／关闭监听对象 5 次后触发一次事件，确认处理日志只出现一次。

### 4. Start：第一次正式运行

Start 只执行一次，且在脚本启用后的首次 Update 前调用。对初始场景中的对象，所有相关 Awake／OnEnable 会先于 Start；运行时动态创建对象需要单独考虑。
适合启动依赖已准备好的数据，但复杂跨对象逻辑更适合显式初始化。

**使用场景与操作**

从已准备的数据模型刷新首屏；初始化关卡状态；启动只需要一次的任务。

**示例片段**

```text
void Start()
{
    RefreshInitialView();
}
```

**常见误区与边界**

对象池每次取出不会重新执行 Start；每次复用的重置应放在 OnGet、初始化接口或 OnEnable。

**动手练习与验收**

反复禁用启用组件，确认 Start 只出现一次，而 OnEnable 多次。

### 5. Update：输入与逐帧逻辑

Update 按渲染帧执行。读取 GetKeyDown 等单帧输入通常放在这里；基于速度的移动需要乘 Time.deltaTime。
把事件驱动需求改成事件处理能减少无效逐帧扫描。

**使用场景与操作**

菜单快捷键、非物理角色位移、UI 状态刷新、逐帧冷却计时。

**示例片段**

```text
void Update()
{
    float x = Input.GetAxisRaw("Horizontal");
    transform.position += Vector3.right * x * speed * Time.deltaTime;
}
```

**常见误区与边界**

不要每帧 Find、反复 GetComponent、创建大数组或拼大量字符串；先用 Profiler 定位。

**动手练习与验收**

将运行帧率分别限制到 30 和 120，测试同样时间内位移是否接近一致。

### 6. FixedUpdate：物理时间步

FixedUpdate 按固定时间步推进，一帧可能零次、一次或多次。物理控制常在此处调用 Rigidbody API；输入可以在 Update 收集并缓存。
Time.fixedDeltaTime 是固定时间步，改变它会影响精度、CPU 工作量和手感。

**使用场景与操作**

Rigidbody 运动、施力、物理角色控制；需要与物理系统保持节奏的逻辑。

**示例片段**

```text
void Update() { inputX = Input.GetAxisRaw("Horizontal"); }
void FixedUpdate()
{
    body.MovePosition(body.position + Vector3.right * inputX * speed * Time.fixedDeltaTime);
}
```

**常见误区与边界**

物理移动直接改 Transform 可能绕过物理行为；不要在 FixedUpdate 单独检测 GetKeyDown，避免漏掉短输入。

**动手练习与验收**

记录同一渲染帧内 FixedUpdate 的次数，修改帧率观察变化。

### 7. LateUpdate：跟随实例

LateUpdate 在 Update 之后执行，适合使用本帧更新后的目标位置进行跟随。下面是可复制的非物理相机脚本。
使用指数形式的平滑系数，让不同帧率下的追随效果更一致。

**使用场景与操作**

保存代码为 FollowCameraExample.cs，挂到 Main Camera；拖入 target，调整 offset；让 target 在 Update 移动。

**完整代码文件**：`Assets/Resources/Learning/Examples/FollowCameraExample.cs.txt`。场景内可以直接阅读并复制完整代码。

**常见误区与边界**

Rigidbody 目标仍要考虑 Rigidbody 插值和移动时机。相机跟随不是所有抖动问题的通用解决方案。

**动手练习与验收**

让目标以固定速度移动，比较相机在 Update 与 LateUpdate 中跟随的视觉效果。

### 8. 协程：等待与清理

协程不是自动在后台线程运行的方法。常规 Unity 协程分段在主线程执行，耗时计算仍会阻塞。
WaitForSeconds 受 timeScale 影响；WaitForSecondsRealtime 适合暂停时仍要计时的 UI。停止协程不等于取消外部请求。

**使用场景与操作**

流程演出、倒计时、分帧生成、轮询等待。需要 CPU 并行时另行考虑 Job System 或任务方案。

**示例片段**

```text
Coroutine routine;
void OnEnable() { routine = StartCoroutine(Countdown()); }
void OnDisable()
{
    if (routine != null) StopCoroutine(routine);
    routine = null;
}
```

**常见误区与边界**

仅禁用 MonoBehaviour 不会自动停止其协程；GameObject 失活或销毁会停止相关协程。外部计时器和事件仍需自己清理。

**动手练习与验收**

分别禁用组件与 GameObject，观察协程输出；补上显式清理后两种路径都不再继续输出。

### 9. OnDisable 与 OnDestroy

OnDisable 对应一次活跃期结束，适合取消订阅、停止持续任务。OnDestroy 对应实例最终销毁，用于释放自己拥有的资源。
OnDestroy 只会对曾经活跃过的对象调用；应用退出和进程被终止也不能依赖所有清理代码完成。

**使用场景与操作**

关闭面板时解绑数据监听；回收角色时取消计时器；销毁管理器时释放池内资源。

**示例片段**

```text
void OnDisable() { Unsubscribe(); StopAllCoroutines(); }
void OnDestroy() { ReleaseOwnedResources(); }
```

**常见误区与边界**

重要存档不能只放在 OnDestroy；保存应有明确时机。销毁别人共享的资源可能影响其他系统。

**动手练习与验收**

写出资源的创建者、使用者、释放者；确认每项资源有明确所有权。

### 10. 完整实例：生命周期观察器

这个脚本同时展示旋转、事件订阅与协程心跳。首次运行输出 Awake → OnEnable → Start；禁用后出现 OnDisable；再次启用时不会重复 Awake／Start。
按 Space 触发共享事件，可验证是否存在重复监听。

**使用场景与操作**

复制为 LifecycleProbe.cs，挂到一个 Cube。将 Active Input Handling 设置为支持旧 Input Manager 或 Both。
在 Console 关闭 Collapse；先禁用脚本，再禁用对象，最后销毁对象观察日志。

**完整代码文件**：`Assets/Resources/Learning/Examples/LifecycleProbe.cs.txt`。场景内可以直接阅读并复制完整代码。

**常见误区与边界**

示例事件为同文件中的 LearningPulse；创建多个观察器时，按键可能被多个脚本同时检测，应先只放一个实例。

**动手练习与验收**

验收：每次启用只有一条心跳链；禁用后无新心跳；重新启用后一次事件只处理一次。

### 11. 排查与综合练习

初始化空引用：检查 Inspector、对象活跃状态及跨对象依赖。
相机抖动：检查移动阶段、物理插值和跟随位置。
打开面板越多回调越多：检查事件解绑。
角色出池后不动：检查只在 Start 重置的状态。

**使用场景与操作**

练习制作计时面板：打开开始计时，关闭停止，重新打开从零计时；返回主界面后没有旧回调。

**常见误区与边界**

Script Execution Order 能组织类别级顺序，但不能代替清晰的依赖设计。

**动手练习与验收**

提交一张日志截图，包含首次创建、两次启用、一次禁用和最终销毁；解释每条日志为何出现。

**官方参考（按示例版本阅读）**

Unity 2022.3：ExecutionOrder、MonoBehaviour、Time.deltaTime、Rigidbody interpolation。

[官方文档](https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html)

## ECS 架构

**学习目标**：理解身份／数据／行为分离；能描述查询、World、Archetype 与 Chunk；知道 Baking、结构变化和 Job 的作用；能规划一个批量移动实例。

**预备知识**：C# struct、值类型与引用类型；基本 MonoBehaviour；性能测量。代码示例面向 Entities 1.0.x，不使用早期 ConvertToEntity API。

### 1. 适用场景与选择

ECS 将 Entity 的身份、Component 的数据和 System 的行为分开。收益来自数据组织与批量执行，不是把类改成 struct 就自动更快。
传统 GameObject 仍适合大量编辑器交互、UI 和已有插件；两者可以协同。

**使用场景与操作**

大量同类单位、弹幕、群集、模拟、批量状态处理。少量角色或开发规模较小的玩法可以先用 MonoBehaviour。

**常见误区与边界**

迁移成本、团队经验、表现层依赖都需要计入。应先测量真正瓶颈。

**动手练习与验收**

列出一个项目中适合 ECS 的系统与适合 GameObject 的系统，并说明原因。

### 2. Entity：身份

Entity 是某个 World 中的实体句柄，带有索引与版本信息。实体销毁后，旧句柄不能继续当作有效实体使用。
数据归属在组件上，业务行为主要由系统组织。

**使用场景与操作**

把一个敌人、子弹、路径点等逻辑对象抽象为身份；用组件组合表达它具有什么能力。

**示例片段**

```text
// Entities 环境中的片段
Entity enemy = entityManager.CreateEntity();
bool alive = entityManager.Exists(enemy);
```

**常见误区与边界**

不要把来自不同 World 的 Entity 混用；实体句柄不是可永久跨存档使用的业务 ID。

**动手练习与验收**

设计 EnemyId 业务组件，用于存档关联，而不是直接序列化 Entity 的索引。

### 3. Component：数据

普通 IComponentData 值类型组件应围绕要被一起访问的数据设计。如 MoveSpeed、Health、Team。
组件粒度太大容易让无关系统读取额外数据；太碎也会增加组合和管理成本。

**使用场景与操作**

移动系统需要位置与速度；伤害系统需要生命值与防御；阵营筛选需要 Team。

**示例片段**

```text
public struct Health : IComponentData { public float Value; }
public struct MoveSpeed : IComponentData { public float Value; }
```

**常见误区与边界**

并非所有 ECS 组件都天然无托管分配。Managed Component、Shared Component、DynamicBuffer 有各自用途与限制。

**动手练习与验收**

为一个可移动、可受伤的敌人画出组件组合；说明每个系统读写什么。

### 4. System 与查询

System 通过查询选择同时具备目标组件的实体，再执行批量行为。查询读写声明还会影响依赖分析。
ISystem 适合非托管系统逻辑；SystemBase 也有合适的使用场景。

**使用场景与操作**

对所有拥有 LocalTransform 与 MoveSpeed 的实体执行移动；对 Health<=0 的实体安排死亡处理。

**示例片段**

```text
foreach (var (position, speed) in
    SystemAPI.Query<RefRW<LocalTransform>, RefRO<MoveSpeed>>())
{
    position.ValueRW.Position.x += speed.ValueRO.Value * dt;
}
```

**常见误区与边界**

读写权限要准确；不必要的写权限可能增加系统依赖。这个代码片段必须位于合适的系统上下文中。

**动手练习与验收**

设计一条查询：移动系统排除 Frozen 标签，同时只读取速度、写入位置。

### 5. World、Archetype 与 Chunk

World 管理一组实体与系统。Archetype 表示某种组件类型组合；同组合的数据按 Chunk 存放。
连续布局能改善批量访问，但实体跨组合、共享组件和动态缓冲会影响实际布局。

**使用场景与操作**

先把“一批同类对象的相同行为”表达成批量查询，再结合 Profiler 判断数据布局的价值。

**常见误区与边界**

不能假定所有实体始终处于一个 Chunk，也不能把 ECS 与自动多线程混为一谈。

**动手练习与验收**

解释：向敌人添加 DeadTag 为什么可能改变它所属的 Archetype？

### 6. Baking：作者数据到运行数据

Baking 将编辑器中的作者数据转换为运行时实体数据。Baker 读取 MonoBehaviour 配置，创建实体并添加组件。
SubScene 是典型工作流；表现为可渲染网格时，还需要相应渲染支持。

**使用场景与操作**

设计师用 Inspector 调 speed；Baker 将它写入 MoveSpeed 组件；运行时系统只处理运行数据。

**示例片段**

```text
public override void Bake(MoveAuthoring authoring)
{
    Entity e = GetEntity(TransformUsageFlags.Dynamic);
    AddComponent(e, new MoveSpeed { Value = authoring.speed });
}
```

**常见误区与边界**

不同 Entities 大版本 API 差异明显。示例使用 1.0 的 Baking 流程，不能套用旧版转换教程。

**动手练习与验收**

改变作者组件 speed，检查烘焙后的实体组件数值是否同步变化。

### 7. 结构变化与 ECB

创建／销毁实体、添加／移除组件等结构变化可能移动数据并带来同步成本。
EntityCommandBuffer 先记录操作，在合适阶段集中回放。它能组织变更时机，但不消除结构变化本身的成本。

**使用场景与操作**

大量子弹生成、死亡实体销毁、批量添加状态标签。

**示例片段**

```text
// 示意，真实项目还要设置 ECB 的创建与回放阶段
var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
ecb.DestroyEntity(deadEntity);
ecb.Playback(entityManager);
ecb.Dispose();
```

**常见误区与边界**

不要在遍历查询时随意立即改变其结构；Job 内需使用相应 ECB 并行接口与依赖管理。

**动手练习与验收**

比较“每帧添加／移除状态组件”与“使用可启用组件切换状态”的设计成本。

### 8. Jobs 与 Burst

Jobs 可以把独立的数据工作安排到工作线程，Burst 可以编译适合的代码路径。
需要明确读写依赖和分配器生命周期，避免主线程提前访问仍由 Job 使用的数据。

**使用场景与操作**

成千上万个单位的独立移动或计算。先有正确的单线程系统，再测量并引入并行。

**示例片段**

```text
// 后续扩展方向：IJobEntity + ScheduleParallel
// 声明只读／可写数据，正确传递并完成依赖。
```

**常见误区与边界**

调度并行不保证一定更快；小任务的调度开销可能超过收益。不能在 Job 中任意调用 GameObject API。

**动手练习与验收**

分别测量主线程循环与 Job 方案，在同等实体数和构建条件下比较时间。

### 9. 完整实例：批量移动

完整示例由作者组件、MoveSpeed 数据组件和 MoveSystem 系统组成。系统逐帧更新所有匹配实体的 X 坐标。
示例优先清晰表达数据流，没有提前引入 Job 或 Burst 优化。

**使用场景与操作**

在独立实验工程使用 Unity 2022.3 与 Entities 1.0.x；安装匹配的 Entities Graphics 1.0.x；该版本需要 SRP，URP 使用 Forward+，也可使用匹配的 HDRP，不支持内置渲染管线。
创建 SubScene，将带 MoveAuthoring 的多个 Cube 放进去。作者类保存为 MoveAuthoring.cs，系统可拆成 MoveSystem.cs。

**完整代码文件**：`Assets/Resources/Learning/Examples/EcsMovementExample.cs.txt`。场景内可以直接阅读并复制完整代码。

**常见误区与边界**

本工程已安装 Entities 1.0.16，实际演示见 EcsFlowDemo。此 Baking / Graphics 拓展示例仍以文本展示，渲染配置未由流程实验测试覆盖。

**动手练习与验收**

验收：不同 speed 的实体以不同速度沿 X 移动；没有 MoveSpeed 的实体不被系统更新。

### 10. 性能与排查

先明确目标设备、实体数量和性能预算。测量 CPU 主线程、Job 时间、同步点、结构变化与内存分配。
编辑器、开发构建和正式构建的结果可能不同，应在可比较环境中测试。

**使用场景与操作**

示例扩展到 100、1000、10000 个实体，记录同一段移动逻辑的耗时。

**常见误区与边界**

System 未执行时检查 World、系统启用状态、RequireForUpdate 和查询匹配；实体不显示时检查 SubScene 与渲染烘焙。

**动手练习与验收**

为相同数量的 GameObject 与实体各写移动实验，记录数据，而不是仅凭帧率印象比较。

### 11. 综合练习：群体移动

扩展实例：为实体增加方向、边界与速度数据；系统到达边界后改变方向；不同阵营使用不同配置。
把移动、生命值和表现拆开，避免一个巨大的系统处理所有事情。

**使用场景与操作**

群体单位、简单鱼群或巡逻队演示。

**常见误区与边界**

如果只为少量个体构建复杂实体架构，学习价值可以很高，但工程收益需单独判断。

**动手练习与验收**

验收：系统不依赖逐个 MonoBehaviour.Update；冻结标签可暂停指定实体；所有原生容器按生命周期释放。

**官方参考（按示例版本阅读）**

Entities 1.0：ISystem、Baker、SystemAPI.Query、EntityCommandBuffer、组件与 Archetype。

[官方文档](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/index.html)

## 网络同步

**学习目标**：分清本地对象、网络身份、所有权和权威；区分状态同步与事件 RPC；理解 tick、延迟、插值与预测；能搭建双端状态同步案例。

**预备知识**：生命周期、C# 事件、客户端／服务器基础。本实例固定面向 Netcode for GameObjects 1.8.1，与 Entities Netcode 是不同方案。

### 1. 使用场景与同步模型

多人游戏需要让不同进程对共享世界保持合理一致。每端拥有自己的内存，普通 C# 字段变化不会自动传到另一端。
常见方案包括状态同步、帧同步等；本课程围绕客户端／服务器的权威状态同步。

**使用场景与操作**

合作游戏、共享房间、多人交互、服务器判定的战斗。排行榜请求与多人实时同步有不同的频率和一致性需求。

**常见误区与边界**

不能把发送一个 Transform 当作完整网络系统；连接、身份、权限、时间与断线恢复都需要设计。

**动手练习与验收**

写出门开关同步和玩家移动同步各需要哪些数据，哪些只是本地表现。

### 2. 网络身份与对象生成

NetworkObject 提供网络对象标识；NetworkBehaviour 在其上实现网络行为。实例化一个本地对象并不意味着所有客户端都生成它。
动态生成通常由有权限的一端进行网络 Spawn。

**使用场景与操作**

玩家预制体、掉落物、可交互门、敌人。配置网络预制体，并明确生成和销毁的权威方。

**示例片段**

```text
// NGO 环境中的片段，服务器执行
var obj = Instantiate(prefab);
obj.GetComponent<NetworkObject>().Spawn();
```

**常见误区与边界**

普通 Destroy 与网络 Despawn 的职责要分清。场景内对象与动态对象的 OnNetworkSpawn／Start 顺序不同。

**动手练习与验收**

解释为什么每个客户端自行 Instantiate 同一门模型不会自动拥有统一网络身份。

### 3. 所有权与权威

所有权常用于表示哪个客户端控制或拥有对象；权威决定谁能产生被接受的状态。两者不是同一个概念。
本课程选择服务器写权威状态、客户端提交操作请求。

**使用场景与操作**

生命值、背包、奖励等由服务器验证并修改；客户端可显示自己的输入效果。

**常见误区与边界**

客户端持有对象所有权也不代表它能任意写服务器权限的 NetworkVariable。

**动手练习与验收**

列出玩家角色的位置、生命值、动画、相机分别由谁产生、校验和显示。

### 4. 持续状态：NetworkVariable

NetworkVariable 用于保存当前状态，能将当前值同步给晚加入的客户端。
在 OnNetworkSpawn 订阅变化并立即应用已有值，在 OnNetworkDespawn 解绑；只等变化事件可能漏掉初始显示。

**使用场景与操作**

门是否打开、准备状态、得分、当前生命值。设置读写权限并仅在网络对象有效生成期间操作运行状态。

**示例片段**

```text
public override void OnNetworkSpawn()
{
    state.OnValueChanged += OnChanged;
    Apply(state.Value);
}
public override void OnNetworkDespawn()
{
    state.OnValueChanged -= OnChanged;
}
```

**常见误区与边界**

不要在对象尚未 Spawn 时随意改运行值；Start 不能替代所有网络生命周期阶段。

**动手练习与验收**

让第二个客户端在开关已打开后加入，确认它立即显示正确状态。

### 5. 瞬时事件：RPC

RPC 表达一次网络请求或通知，如“请求打开门”“播放一次效果”。它不自动保存一个可供晚加入客户端恢复的长期状态。
请求与最终状态可以配合：客户端发 RPC，服务器校验后写 NetworkVariable。

**使用场景与操作**

交互请求、一次性动画触发、技能指令。对持续状态使用可恢复的状态容器。

**示例片段**

```text
[ServerRpc(RequireOwnership = false)]
void RequestServerRpc(ServerRpcParams rpc = default)
{
    ulong sender = rpc.Receive.SenderClientId;
    // 根据 sender 校验请求后更新状态。
}
```

**常见误区与边界**

RequireOwnership=false 只放宽调用资格，不替代权限校验。不能相信客户端直接提交的伤害或奖励。

**动手练习与验收**

比较“只广播开门 RPC”和“同步 bool 状态”在晚加入情况下的差别。

### 6. Tick、快照与带宽

网络 tick 是网络逻辑节奏，不一定等于渲染帧或 FixedUpdate。快照可以按频率发送，并结合序号与时间戳处理乱序。
带宽粗估需要包含对象数、每次发送字节、频率、玩家数与协议开销。

**使用场景与操作**

大量移动对象需要压缩、变化检测和兴趣管理；只同步玩家真正需要知道的内容。

**示例片段**

```text
粗估：20 对象 × 每对象 24 字节 × 每秒 10 次 = 4800 字节/秒
// 仅是单个接收端的负载估计，未计算头部与重传。
```

**常见误区与边界**

不要无测量地在每帧为所有对象发送所有数据；可靠传输也会受到重传和队头阻塞影响。

**动手练习与验收**

估算 100 个对象、20Hz 位置快照的每客户端负载，再讨论兴趣管理如何减少它。

### 7. 插值：让远端显示平滑

客户端缓冲若干权威快照，再按服务器时间轴显示中间位置。插值减少抖动，但增加显示延迟。
它适合远端对象的表现层；碰撞和命中判定仍需明确权威与时间策略。

**使用场景与操作**

远端玩家、移动平台和车辆的视觉平滑。先正确同步，再添加插值。

**示例片段**

```text
// 概念伪代码，不含网络时钟和缓冲实现
float t = Mathf.InverseLerp(previousTime, nextTime, renderTime);
visual.position = Vector3.Lerp(previousPosition, nextPosition, t);
```

**常见误区与边界**

收到快照时仅 Lerp 当前值不是完整的时间缓冲插值；无新快照时还需处理外推限制或保持状态。

**动手练习与验收**

模拟不等间隔快照，比较直接赋值与时间轴插值的抖动。

### 8. 预测、校正与回滚

本地预测先响应输入，服务器确认后校正；完整预测可能还需保存输入序列、权威状态和重放逻辑。
回滚可以指重新模拟状态，也可以指服务器为命中判定回看历史，必须明确具体含义。

**使用场景与操作**

高响应要求的玩家移动、动作游戏。门开关等低频交互可先等待服务器结果。

**常见误区与边界**

本课程开关示例没有预测／回滚实现；不能把视觉插值称为预测。

**动手练习与验收**

画出输入序号 10 到达服务器、服务器状态回传、本地重放 11 和 12 的流程。

### 9. 完整实例：服务器权威开关

每端显示一个 Cube。按钮只提交 Toggle 请求；服务器确认发送者连接并限频，然后修改 bool 状态；各端通过变化事件刷新颜色。
该示例突出“输入请求 → 验证 → 权威状态 → 显示”。

**使用场景与操作**

独立实验工程安装 NGO 1.8.1 与 Unity Transport。场景建立 NetworkManager、UnityTransport；Cube 添加 NetworkObject 和脚本，拖入 Renderer。
一个构建运行 Host，编辑器运行 Client，连接相同地址端口；启动按钮绑定 BeginHost／BeginClient，交互按钮绑定 RequestToggle。使用内置 Standard 材质。

**完整代码文件**：`Assets/Resources/Learning/Examples/NetworkSwitchExample.cs.txt`。场景内可以直接阅读并复制完整代码。

**常见误区与边界**

文本示例未安装到主工程，也未做真实联网实机验证。示例限频为全局限频，正式项目应按发送者、距离和权限完善。

**动手练习与验收**

验收：两端颜色一致；快速点击被限频；晚加入客户端显示当前状态；断开／重连不留下旧订阅。

### 10. 断线、重连与验证

区分连接建立、身份认证、角色生成、状态同步与可开始交互。断线时应停止使用失效对象并更新 UI。
重连需要恢复业务状态，不只是再次调用连接接口。

**使用场景与操作**

房间准备界面、重新连接提示、状态恢复与超时退出。

**常见误区与边界**

Host 同时包含服务器和本地客户端，单测 Host 不能覆盖独立 Client 的行为。

**动手练习与验收**

至少测试 Host+Client、第二个晚加入 Client、断线和重连；再增加延迟、抖动与丢包条件。

### 11. 综合练习：同步宝箱

把开关实例扩展为宝箱：服务器保存是否已打开，检验玩家距离和请求频率，奖励只发一次。
客户端播放表现动画，但动画不能自己决定奖励。

**使用场景与操作**

合作游戏的共享交互和服务器判定奖励。

**常见误区与边界**

重发请求、双玩家同时点击、晚加入和断线重连都可能暴露重复奖励问题。

**动手练习与验收**

验收：无论同时点击多少次，奖励只产生一次；晚加入看到已打开；不在范围内的请求被拒绝。

**官方参考（按示例版本阅读）**

NGO 1.8.1：NetworkVariable、ServerRpc、NetworkObject、NetworkTransform。

[官方文档](https://mp-docs.dl.it.unity3d.com/netcode/1.8.1/basics/networkvariable/)

## 对象池

**学习目标**：判断何时需要池化；理解 Get／Release／Destroy 和容量；重置所有复用状态；能实现可运行的子弹池并测量分配变化。

**预备知识**：生命周期、Rigidbody、集合、回调、基本 Profiler。代码使用 Unity 2022.3 内置 UnityEngine.Pool.ObjectPool<T>。

### 1. 使用场景与成本

对象池把频繁创建／销毁变成取出／回收。它可减少实例化、销毁与部分分配成本，但会常驻更多内存，并带来状态重置和资源管理复杂度。

**使用场景与操作**

子弹、飘字、命中特效、滚动列表条目、反复打开的窗口。极少使用或体积很大的对象不一定适合长期池化。

**常见误区与边界**

池化不代表零 GC；对象内部的字符串、列表扩容、事件、材质实例仍可能分配。

**动手练习与验收**

先观察当前互动小球实验，再说明“创建次数不变，复用次数增加”代表什么。

### 2. Get：获取对象

有空闲对象时直接返回；没有空闲对象时调用创建回调。取出回调用于开启对象并准备活跃状态。
在真正开始逻辑前，应完成这次使用所需的初始化。

**使用场景与操作**

角色出池后重置位置、生命值、目标、倒计时与动画；特效出池后重置播放状态。

**示例片段**

```text
var item = pool.Get();
item.transform.position = spawnPosition;
item.Initialize(config);
```

**常见误区与边界**

OnEnable 可能在外部 Initialize 前执行。依赖参数的启动逻辑应放到显式初始化之后。

**动手练习与验收**

列出一颗子弹取出时要重置的至少六个字段。

### 3. Release：回收对象

回收时停止持续逻辑，移除活跃集合，清空运动或订阅状态，关闭对象，再放回空闲区。
同一个对象必须只回收一次；多种结束路径需要统一进入回收函数。

**使用场景与操作**

子弹超时、命中后回收；飘字播放完成后回收；窗口关闭后回收。

**示例片段**

```text
if (!active.Contains(item)) return;
active.Remove(item);
pool.Release(item);
```

**常见误区与边界**

命中与超时可能在邻近时机重复请求回收。collectionCheck 有助于开发期检测，但业务逻辑仍需保证一次性。

**动手练习与验收**

让同一个对象连续尝试回收两次，确认不会重复入池或出现活跃数负数。

### 4. 四个回调与所有权

createFunc 创建；actionOnGet 激活；actionOnRelease 清理并关闭；actionOnDestroy 在池无法继续保存对象等场景释放最终资源。
谁创建池，谁负责池的关闭与未回收对象管理。

**使用场景与操作**

一个场景发射器拥有子弹池；场景卸载时统一清理，子弹通过拥有者请求回收。

**示例片段**

```text
pool = new ObjectPool<Item>(Create, OnGet, OnRelease, OnDestroy,
    collectionCheck: true, defaultCapacity: 16, maxSize: 32);
```

**常见误区与边界**

ObjectPool 不是线程安全的，GameObject 操作放在主线程。池 Clear 主要清理池中空闲对象，不能替代所有已取出对象的回收。

**动手练习与验收**

画出池拥有者、活跃子弹、空闲子弹的引用关系，指出卸载场景时谁清理谁。

### 5. 容量与预热

defaultCapacity 是内部容器初始容量，不等于预创建对象数；maxSize 控制可保留的空闲数量，不是所有活跃对象的上限。
需要限制同时发射数时，要额外限制 CountActive 或业务活跃集合。

**使用场景与操作**

弹幕上限、特效预算、可见 UI 数量。预热可把创建成本移到可接受的加载阶段。

**示例片段**

```text
var warming = new List<Item>();
for (int i = 0; i < 20; i++) warming.Add(pool.Get());
foreach (var item in warming) pool.Release(item);
```

**常见误区与边界**

连续 Get 后立即 Release 可能一直复用同一对象，不能保证预热 N 个独立对象。预热时要避免激活副作用。

**动手练习与验收**

说明为何 maxSize=32 时仍可能有 100 个活跃对象，以及如何限制它们。

### 6. 状态重置清单

位置／旋转、速度／角速度、生命值、目标引用、碰撞标记、动画、粒子、音效、Trail、计时器、事件订阅与业务回调都需要考虑。
不同类型对象应有各自的 ResetForUse 与 ResetForPool。

**使用场景与操作**

子弹之前命中过目标，下次复用必须清除命中标记；拖尾对象复用时清空旧轨迹。

**示例片段**

```text
body.velocity = Vector3.zero;
body.angularVelocity = Vector3.zero;
target = null;
trail.Clear();
// 片段：字段由具体对象定义。
```

**常见误区与边界**

简单 SetActive(false) 不会自动清除普通字段和外部事件监听。

**动手练习与验收**

让同一子弹连续复用 50 次，确认速度、目标、命中次数和拖尾没有继承旧状态。

### 7. 完整实例：子弹池

按住 Space 连续发射自动创建的球形子弹。两秒超时或触发碰撞后回收；再次发射复用已有实例。
示例同时限制活跃数量与池中空闲数量，并在禁用发射器时回收活跃对象。

**使用场景与操作**

复制为 ProjectilePoolExample.cs，挂到空对象。相机朝向发射器前方，输入设置支持 Input Manager 或 Both。
可创建带 Collider 的目标；球形子弹具有 Rigidbody 与 Trigger Collider。

**完整代码文件**：`Assets/Resources/Learning/Examples/ProjectilePoolExample.cs.txt`。场景内可以直接阅读并复制完整代码。

**常见误区与边界**

这个示例没有伤害、阵营过滤和忽略发射器碰撞逻辑；发射器放在空对象上便于观察。速度使用 Unity 2022.3 Rigidbody.velocity API。

**动手练习与验收**

验收：持续发射后 Hierarchy 中对象数量趋于稳定；重新启用发射器后可继续发射；超时和碰撞都能回收。

### 8. UI、特效与其他池

UI 池需要重置数据绑定、按钮事件和滚动位置；粒子池需清理发射状态并正确判断结束；音效池需要重置音量和播放参数。
同一种池化接口可以有不同的具体清理动作。

**使用场景与操作**

任务列表条目、伤害飘字、爆炸特效、一次性音源。

**常见误区与边界**

反复给同一个 Button 加监听但不解绑会出现多次响应。不同特效时长不应硬编码成统一回收时间。

**动手练习与验收**

将当前小球实验改为飘字条目，复用编号不变但文本和位置更新。

### 9. 性能测量与误区

用 Profiler 比较相同发射速率下的 Instantiate／Destroy 和池方案。记录尖峰、GC Alloc、CPU 时间与常驻对象数。
同时观察池化对象内部逻辑，因为瓶颈可能在碰撞、渲染或过多 Update。

**使用场景与操作**

在目标设备开发构建上运行，先暖机，再采集同样时长的数据。

**常见误区与边界**

对象数量稳定不等于内存完全稳定；材质实例与外部引用也可能造成泄漏。不要只看平均 FPS。

**动手练习与验收**

记录两种方案各 30 秒数据，解释池方案节省了什么、增加了什么。

### 10. 综合练习：三个池

分别制作子弹、爆炸、飘字三个池。让子弹命中触发特效和飘字，各自按完成条件回收。
为每个池增加累计创建、活跃、空闲、峰值活跃和拒绝生成次数。

**使用场景与操作**

射击与战斗反馈系统的完整小实验。

**常见误区与边界**

多个池之间不要互相持有已经失效的对象引用；回收回调应解绑而不是继续启动新的任务。

**动手练习与验收**

验收：重复战斗 5 分钟数量不持续增长；退出场景后没有旧回调；重复回收被可靠阻止。

**官方参考（按示例版本阅读）**

Unity 2022.3：ObjectPool<T>、Get、Release、Clear、CountActive／CountInactive。

[官方文档](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Pool.ObjectPool_1.html)

## 热更新

**学习目标**：区分资源更新、配置更新与代码热更新；理解版本、清单、校验、依赖与切换；能演练单包下载加载；知道失败恢复和缓存边界。

**预备知识**：协程、HTTP、JSON、资源加载、平台构建概念。实例为 AssetBundle 资源更新，不包含 C# 代码热更新。

### 1. 使用场景与三种更新

资源更新替换纹理、音效、预制体等；配置更新替换已有客户端能够解释的数据；代码热更新改变执行逻辑，通常需要额外运行时方案。
客户端必须已经具备解释新资源和配置的能力。

**使用场景与操作**

活动美术、关卡资源、数值表、语言包、可按现有规则组织的关卡。修改底层原生功能通常需要完整客户端发布。

**常见误区与边界**

AssetBundle 不能直接提供新的编译 C# 类；预制体上的脚本类型仍需要在客户端可用。

**动手练习与验收**

分别判断：换皮肤、修改掉落表、新增脚本类型、更新原生插件，属于哪种更新。

### 2. 版本与兼容性

客户端版本、资源版本与配置协议版本是不同维度。版本检查先确认兼容关系，再计算差异。
清单应表达资源名、版本、哈希、大小、平台、依赖和最低支持客户端版本。

**使用场景与操作**

客户端请求远端清单，检查平台与协议；必要时提示完整客户端更新。

**示例片段**

```text
{
  "schemaVersion": 1,
  "minClientVersion": "1.0.0",
  "platform": "StandaloneWindows64",
  "bundle": "reward",
  "hash": "由构建清单填写的32位哈希",
  "crc": 0,
  "bytes": 123456
}
```

**常见误区与边界**

版本号不能简单按字符串比较，例如 1.10 和 1.2；示意清单中的占位值不能作为真实构建哈希。

**动手练习与验收**

设计客户端版本过旧、平台不匹配、配置字段未知时的处理路径。

### 3. 构建与平台

AssetBundle 必须针对目标平台构建。资源名、依赖与哈希应从构建结果生成，不能靠手写猜测。
把构建、上传、清单发布和回滚记录组成固定流程。

**使用场景与操作**

创建 Reward.prefab，设置其 AssetBundle 名称 reward；编辑器调用 BuildPipeline.BuildAssetBundles；取回 Manifest 中的 hash 和实际 CRC。

**示例片段**

```text
// Editor 文件中的构建片段
var manifest = BuildPipeline.BuildAssetBundles(outputPath,
    BuildAssetBundleOptions.ChunkBasedCompression,
    BuildTarget.StandaloneWindows64);
Hash128 hash = manifest.GetAssetBundleHash("reward");
BuildPipeline.GetCRCForAssetBundle(outputPath + "/reward", out uint crc);
```

**常见误区与边界**

outputPath 目录必须先存在；Windows 的包不能直接当作 Android 包使用。依赖包需要一起构建和管理。

**动手练习与验收**

从构建结果记录文件大小、hash、CRC、平台和资源完整路径。

### 4. 下载与校验

网络下载可能超时、失败或收到不完整内容，需要检查 UnityWebRequest.Result 和错误信息。
Hash128 可参与缓存版本识别；CRC 校验可检测数据损坏，但它们不等同于来源真实性验证。

**使用场景与操作**

设置超时，避免重复请求；让 UI 展示下载进度与失败原因；生产系统使用可信发布与签名策略。

**示例片段**

```text
using (var request = UnityWebRequestAssetBundle.GetAssetBundle(url, hash, crc))
{
    request.timeout = 30;
    yield return request.SendWebRequest();
    if (request.result != UnityWebRequest.Result.Success) yield break;
}
```

**常见误区与边界**

不能把 CRC 当作密码学签名；下载成功也不说明资源名存在或依赖完整。

**动手练习与验收**

测试错误 URL、超时和错误 CRC，确认不会直接替换当前可用内容。

### 5. 缓存、依赖与内存

AssetBundle、加载出的资源和实例有不同生命周期。Unload(false) 卸载包数据而保留已加载资源；Unload(true) 会影响从包加载的资源。
缓存版本和内存引用也要分别管理。

**使用场景与操作**

复用已缓存版本减少流量；先加载依赖包，再加载主包；切换内容后释放不再使用的对象与资源。

**示例片段**

```text
// 包生命周期与已加载资源引用需要分别设计
bundle.Unload(false);
// Resources.UnloadUnusedAssets 应在可接受的时机评估调用。
```

**常见误区与边界**

不能把 Unload(false) 理解为所有旧资源已经完全释放；共享依赖仍被别的对象使用时，不要直接 Unload(true)。

**动手练习与验收**

画出包、Prefab 资源、实例、共享材质的引用关系，解释何时才可释放。

### 6. 切换与失败回退

安全的切换应先让新版本资源可用，再改变业务使用的引用。出错时继续保留旧版本，记录失败并允许重试。
多包更新需要事务式清单提交与完整依赖检查，不能下载一个就随意覆盖一个。

**使用场景与操作**

先暂存新资源，验证加载，再切换活动界面；网络断开时继续显示本地已有内容。

**示例片段**

```text
检查兼容性 → 下载暂存 → 校验依赖 → 试加载 → 切换引用 → 提交版本
失败 → 保持当前版本 → 记录与重试
```

**常见误区与边界**

下面的单包示例只保证实例切换前保留旧实例，不实现多包磁盘事务和生产级回滚。

**动手练习与验收**

在下载、校验、加载与切换各阶段制造失败，说明当前画面应如何变化。

### 7. 完整实例：单包资源更新

脚本下载一个无外部包依赖的资源包，加载 Reward.prefab，创建新实例后再移除旧实例。
使用真实构建哈希与 CRC 配合缓存和校验，重复点击下载按钮不会并发启动多个任务。

**使用场景与操作**

复制为 AssetBundleUpdateExample.cs，挂到空对象；填写 HTTPS 资源包 URL、完整资源名、Hash128 与 CRC；按钮绑定 DownloadAndApply。
构建并托管两个可区分外观的版本，切换配置后重复测试。

**完整代码文件**：`Assets/Resources/Learning/Examples/AssetBundleUpdateExample.cs.txt`。场景内可以直接阅读并复制完整代码。

**常见误区与边界**

这是待配置资源服务器和实际 AssetBundle 后运行的文本示例，本次未部署服务器或验证远程下载。只处理一个无外部包依赖的资源；不能用于任意多包生产更新。

**动手练习与验收**

验收：有效包替换外观；无效资源名不销毁旧实例；断网显示错误；同版本可复用缓存。

### 8. Addressables 的角色

Addressables 提供地址、依赖和资源加载管理，并可使用远端内容与内容更新工作流。
它能简化资源管理，但仍需要版本兼容、托管、容量、失败处理和正确释放句柄。

**使用场景与操作**

项目资源规模扩大、依赖复杂时，考虑按项目实际需求引入 Addressables。

**常见误区与边界**

不能把 Addressables 当作 C# 代码热更新运行时，也不能忘记 Addressables.Release 等生命周期管理。

**动手练习与验收**

比较手动管理 AssetBundle 与地址式加载的维护成本，列出仍需自己处理的更新问题。

### 9. 代码热更新的边界

代码热更新方案需要考虑 AOT、类型保留、裁剪、泛型、反射、跨程序集调用和平台规则。
具体方案必须按平台、Unity 版本、维护成本与发布要求评估。

**使用场景与操作**

确实需要改变逻辑而不能通过既有配置实现时，再单独设计和验证代码更新方案。

**常见误区与边界**

本工程没有接入任何代码热更新库；不要把下载 .cs 文件、加载 AssetBundle 或编辑器脚本重载称作运行时 C# 热更新。

**动手练习与验收**

给一个新增技能的需求，分析能否通过已有技能行为和数据组合实现。

### 10. 综合练习：活动奖励更新

先发布红色奖励 Prefab，再更新为蓝色。清单保存平台、版本、文件大小、hash、CRC 与资源名。
失败时保留红色；成功后切换蓝色；重启后根据已提交版本恢复。

**使用场景与操作**

活动资源与本地版本的完整教学实验。

**常见误区与边界**

落盘版本清单、磁盘空间和多包依赖不包含在单包脚本中，综合练习需要自行补全。

**动手练习与验收**

测试首次下载、缓存命中、断网、错误 CRC、错误资源名、低磁盘空间与旧客户端兼容性，记录验收结果。

**官方参考（按示例版本阅读）**

Unity 2022.3：AssetBundles、UnityWebRequestAssetBundle、Hash128、Caching。

[官方文档](https://docs.unity3d.com/2022.3/Documentation/Manual/AssetBundlesIntro.html)

[官方文档](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Networking.UnityWebRequestAssetBundle.GetAssetBundle.html)

## 扩展内容规范

新增课程配置时，应填写学习目标、预备知识和官方参考。每个步骤可以配置 Explanation、Usage、Pitfalls、Exercise、Code 或 Example Code。

Code 是短片段，Example Code 是完整 TextAsset，二者同时配置时优先显示完整实例。示例应注明 Unity / 包版本、文件名、挂载对象、Inspector 配置、操作步骤、预期结果和未实现的边界。

建议后续按需求加入协程与异步、输入系统、UI、物理、资源管理、渲染、动画、性能分析和构建发布专题；当前五个主题已经实际制作，以上为后续扩展建议。
