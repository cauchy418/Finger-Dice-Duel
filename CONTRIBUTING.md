# 提交与推送约定

仓库只保留**当前有效**的一份。凡是「这一版取代了上一版」的东西，上一版删掉，需要时用 `git log` 翻回来。目录结构比提交历史更重要——读仓库的人应该看得到现在是什么样，不用先学会考古。

## 一次改动一个提交

- 提交信息第一行说清楚「改了什么」，需要时第二段写「为什么」。
- 不把顺手改的无关文件混进同一个提交。改文档和改代码分开。
- 推送前先跑一遍 `git status`，确认没有构建产物、采样中间文件、临时脚本被带上去。

## 文件放哪里

| 路径 | 放什么 | 不放什么 |
|---|---|---|
| `Assets/Scripts/` | 运行时逻辑：规则内核、角色、AI、UI | 设计文档、截图 |
| `Assets/Tests/` | 测试用例 | 测试输出、报告 |
| `Docs/` | 面向人读的**现行**文档 | 被取代的旧设计稿 |
| `Docs/Adventure/four-chapter-v3/` | 现行四章 40 关的设计 | 逐关时代、旧章序的稿件 |
| `Tools/` | 能重复运行的脚本与工具 | 运行结果 |
| `Tools/Statistics/<日期>-<主题>/` | 一轮完整的胜率采样 | 半轮结果、单模式试跑 |
| `Art/Production/` | 原图、生成提示词、导入清单 | 游戏内资源副本 |
| `Art/Integration/` | 每个功能点的接入验收（截图 + 说明） | 未接入的素材 |
| 仓库根目录 | `README.md`、`RULES.md`、`CODELY.md`、`CODE-STYLE.md`、`CONTRIBUTING.md`、`LICENSE` | 其他任何文件 |

## 四条硬规则

**1. 日期只出现在 `Tools/Statistics/` 的子目录名里，不按日期建根目录。**

禁止出现 `2026-11-07/`、`new/`、`v2/`、`backup/` 这类只表示时间的目录。一轮采样就叫 `Tools/Statistics/2026-11-09-state-ai/`，主题词说明这一轮改了什么。

**2. 新一轮进来时删掉旧一轮。**

`Tools/Statistics/` 下**同时只有一个采样目录**。新目录提交的那次，把旧目录一并 `git rm`。跨轮对比不要靠保留旧目录来实现——在当轮 `README.md` 里写一节「与上一轮相比」，数字和结论都写进去。旧数据在 git 历史里，`git show <commit>:<path>` 随时能取。

**3. 设计稿只保留现行版本。**

`Docs/` 下不建 `Archive/`、不建 `legacy/`。一章文档被新版取代，旧版直接删除，新文档里写一句「旧版已下线，可从 git 历史取回」。同一件事不要有两份都在用的文档。

**4. 数据不散落。**

- 胜率数字只出现在两处：`Tools/Statistics/<当前轮>/README.md`（完整表）和 `Docs/roster.md`（图鉴终表）。别处引用时给链接，不复制表格。
- 规则数字（子职数、状态数、组合数）只以 `RULES.md` 为准，改规则时同一提交内同步 `RULES.md`、`CODELY.md`、`README.md`、`Docs/roster.md`。
- 临时脚本不要提交。跑完删掉，或放进 `Tools/` 并注明用途。

## 提交前自检

1. **链接**：新增或移动文档后，全仓扫一遍相对链接。`Docs/README.md` 是索引，它列出的每个文件都必须存在。
2. **数字**：子职数、负面状态数、对局局数、回归项数，四处（README／RULES／CODELY／roster）必须一致。
3. **测试**：`Tools/HeadlessTests/run-tests.ps1` 通过为主；只改了文档可以跳过，但要说明。
4. **旧文件**：确认这次提交里没有「同一个东西的上一版」还活着。

## 各目录的说明文档

| 目录 | 说明 |
|---|---|
| [`Docs/README.md`](Docs/README.md) | 文档总索引，按「想找什么」分类 |
| [`Tools/Statistics/README.md`](Tools/Statistics/README.md) | 采样的目录约定与复现命令 |
| [`Tools/HeadlessTests/README.md`](Tools/HeadlessTests/README.md) | 无头测试怎么跑 |
| [`Art/Production/`](Art/Production/) | 美术原图与提示词 |
| [`Docs/Adventure/four-chapter-v3/README.md`](Docs/Adventure/four-chapter-v3/README.md) | 现行剧情设计 |
