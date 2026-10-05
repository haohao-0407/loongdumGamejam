# 粘贴到新对话的 Prompt

我们继续协作 Unity 项目。请先读取：

`D:\project\LoongJam2026-\loongdumGamejam\Docs\ConversationHandoff.md`

仓库是 `D:\project\LoongJam2026-\loongdumGamejam`，预期分支 `leftfloor`，HEAD `ab108da`。请实际检查当前分支、工作区和暂存区，再阅读项目说明、适用 AGENTS.md 与任务相关代码。

上一对话已经按我要求撤销全部第三关制作产物，恢复到制作前状态。反转术式仍保留：只有下半身落地且站在指定格子上，按 F 交换双方水平位置，各自保留高度；门用 E。我已亲自试玩确认交换成功。请不要自动重建第三关。

制作前已有且必须保留的未提交修改是 `Assets/Scenes/Whitebox1.unity` 与 `ProjectSettings/Packages/com.unity.probuilder/Settings.json`。迁移另新增了 Docs 下的交接文档与本 prompt。不要还原、覆盖或删除这些文件，也不要把它们当作第三关残留。

如果后续我让你重新设计或制作第三关：全部复用 **Whitebox1 实际使用的资源**，不能仅以「项目里有」为依据加入其他模型、材质或实体。要求四至五个小解谜单元、流程不重复、有足够信息与效果反馈、有确定且唯一的解、避免死档与逃课。参考文件位置和已知设计风险见交接文档，请重读原件，核对其规则与 Unity 现有明暗/交互机制，不能沿用已撤回实现的验证结论。

协作规则：只改明确任务范围；保护已有改动和 .meta/GUID；不无故保存共享场景或资源；未经授权不改公共接口、依赖、Unity 版本、Packages 或 ProjectSettings。不要自行切换分支、提交、推送、合并或发布。origin/main 曾 fetch，但没有 merge。冲突或越界先说明并询问；常规可逆实现自主推进。

使用中文。若连接 Unity MCP，先确认实例、场景、未保存改动和 Play Mode。修改前简述范围，完成后报告涉及文件、真实验证结果和遗留问题。迁移后的第一步请只核对现状并简要汇报，等待我给出下一项具体任务。
