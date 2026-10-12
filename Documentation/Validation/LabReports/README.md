# 实验室验证结果

Unity 2022.3.62f3c1；Entities 1.0.16；Windows Editor。

| 实验 | 通过 | 失败 |
| --- | --- | --- |
| ECS | 8 | 0 |
| TCP 网络同步 | 10 | 0 |
| HTTP AssetBundle 更新 | 10 | 0 |
| 对象池 | 11 | 0 |
| 生命周期 | 10 | 0 |
| 合计 | 49 | 0 |

另已验证五个知识卡片的实验入口按钮与每个场景的 Esc 返回。
交付检查通过：五个场景均在 Build Settings；五份代码解析均导入；八个运行源文件的展示镜像逐字一致；最终编译通过。

具体断言见同目录中的 ecs.txt、network.txt、hotupdate.txt、pool.txt、lifecycle.txt、navigation.txt。
热更新 CRC 故障测试会在 Unity 日志中出现预期 CRC Mismatch；场景通过“未提交且保留旧实例”的断言确认正确处理。

用例实际运行本机 TCP / HTTP 与真实构建包；没有覆盖跨机器网络、生产身份认证、远程 HTTPS、代码热更新或多包磁盘事务。
再运行：Tools → Unity Learning → Run All Lab Scene Tests。该菜单同步展示源码，构建包，进入五个真实 PlayMode 场景并记录报告。
