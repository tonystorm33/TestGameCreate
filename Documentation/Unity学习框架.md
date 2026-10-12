# Unity 学习小岛

打开 `Assets/Scenes/PlayScene.unity`，点击 Play 即可进入知识目录。
各知识详情页都有“进入独立流程实验室”入口，真实运行演示与代码调用链见 [流程实验室与代码解析](流程实验室与代码解析.md)。
界面使用 Unity 2022.3 自带 UI Toolkit，无需安装额外包或导入 TMP 示例资源。

## 已有功能

- 卡通风格圆角卡片、分类、中文搜索、本次运行的探索记录。
- 生命周期：步骤高亮动画、进度条、播放／暂停、上一步／下一步、重新开始。
- ECS、网络同步、热更新：概念分步展示和伪代码。
- 对象池：取出、回收、复用计数、容量限制和重置。
- 返回知识目录按钮和 Esc 返回。退出时停止调度并清理展示内容。

知识正文保留图文课程。独立实验室现已实际运行 Entities 1.0.16、TCP 状态同步和本机 HTTP AssetBundle 资源更新，不包含 C# 代码热更新运行时。
生命周期按概念分步展示，持续更新阶段和启用／禁用循环需要结合说明理解。

五个主题已扩展为 53 个章节，覆盖学习目标、适用场景、机制、完整实例、常见误区与验收练习。
完整内容见 [Unity知识体系与实例](Unity知识体系与实例.md)。
`Assets/Resources/Learning/Examples` 中提供 6 个 `.cs.txt` 完整脚本，界面可以滚动阅读与复制。
这些文件作为教学文本加载，复制并改为 `.cs` 后按课程说明配置实验场景；ECS 与网络实例需要另外安装注明版本的包。
章节目录与导航位于正文上方，自动播放默认每节停留 15 秒，可调至 3～60 秒；长章节建议暂停后手动阅读。
目录搜索也会查找章节的标题、原理与使用场景，例如“协程”“回滚”“预热”。

## 代码结构

| 文件 | 职责 |
| --- | --- |
| `KnowledgeTopic.cs` | ScriptableObject 内容配置，包含分类、色彩、展示类型和步骤 |
| `KnowledgePresentation.cs` | `IKnowledgePresentation` 接口；基类统一 `Enter` / `Exit` / `Dispose` |
| `StepPresentation.cs` | 分步图文与定时高亮动画 |
| `ObjectPoolPresentation.cs` | 可交互 UI 对象池示意 |
| `KnowledgeLearningApp.cs` | 加载目录、导航、筛选、字体和展示工厂注册 |
| `Resources/Learning/Learning.uss` | 配色、卡片、按钮、布局及窄屏样式 |

运行时生成 UIDocument 和 PanelSettings，场景只需挂载一个 `KnowledgeLearningApp`。
界面元素在 Play 期间生成，可在 UI Toolkit Debugger 中查看；退出 Play 后不会保存在 Hierarchy 中。

## 添加一个知识点

1. 在 Project 中选择 `Assets/Resources/Learning/Topics`。
2. 右键 → Create → Unity Learning → Knowledge Topic。
3. 填写唯一 `id`、`order`、标题、分类、简介、徽标和强调色。
4. 选择 `Steps`，配置每个步骤的标题、解释和代码；Note 写边界或学习提示。
5. 进入 Play，卡片会自动加入目录。`order` 决定排序，不需要修改主界面代码。

进一步填写 Learning Goals、Prerequisites、References；章节的 Usage、Pitfalls、Exercise 可补充场景、误区与练习。
Example Code 引用完整 `.txt` 资源，配置后会优先于短 Code 显示。对象池表现同样支持课程步骤，交互实验位于课程前面。

## 添加新的表现方式

继承 `KnowledgePresentation`，实现 `OnEnter()`。通过 `Host` 构建 UI，使用 `Topic` 读取配置。
在 `OnExit()` 中停止动画／协程、解除外部事件订阅、释放视频播放器或销毁临时 3D 对象。
基类会清空 Host，但不会自动销毁自行创建的场景对象。

在 `PresentationKind` 添加标识，然后在 App 启动注册对应工厂，例如：

```csharp
RegisterPresentation(PresentationKind.YourNewKind, () => new YourPresentation());
```

此示例中的新枚举值与类名是占位符，应替换为实际实现。
内容资产引用枚举数值，扩展时请在枚举末尾追加，避免改变旧值。
工厂应返回新实例；未来视频、3D 实验和测验可以遵守同一套进入／退出接口。

## 字体与验证

本机通过 TextCore 动态系统字体优先使用微软雅黑／黑体等中文字体，启用多图集以支持持续探索更多中文内容。发布到其他平台时，将具有适当授权的中文字体放入工程，
在场景组件的 `Chinese Font` 字段中指定它；没有匹配的中文字体时可能缺少中文字形。

菜单 `Tools → Unity Learning → Validate Knowledge Framework` 可检查资源、步骤按钮、退出清理、重新进入以及对象池复用。
验证报告输出到 `Documentation/Validation/FrameworkReport.txt`。还应在 Game 视图手动检查：

- 搜索与分类组合、返回后筛选状态保留。
- 生命周期播放后返回目录，进入其他知识，无旧播放残留。
- 对象池取出 → 回收 → 取出，小球编号不变，复用数增加。
- 调整 Game 视图大小，内容可以滚动，按钮和文本不被裁切。

## 官方参考

- [Unity 2022.3 生命周期顺序](https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html)
- [Unity ObjectPool API](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Pool.ObjectPool_1.html)
- [Entities 1.0 文档](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/index.html)
- [NGO 1.8.1 状态同步](https://mp-docs.dl.it.unity3d.com/netcode/1.8.1/basics/networkvariable/)
- [AssetBundle 介绍](https://docs.unity3d.com/2022.3/Documentation/Manual/AssetBundlesIntro.html)
