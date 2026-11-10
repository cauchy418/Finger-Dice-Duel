# 文档索引

仓库首页在 [README](../README.md)。这里按「想找什么」分类。文档只保留**当前有效**的版本；被取代的设计稿一律从仓库删除，需要时用 `git log` 翻回。

## 想上手玩

| 文档 | 内容 |
|---|---|
| [模式与战斗](modes-and-combat.md) | 一回合怎么走、距离与护盾怎么算、六种模式细则、13 类伤害与 25 种负面状态 |
| [角色图鉴](roster.md) | 58 名子职的数值与关键机制、阵营分类、全角色胜率 |
| [德鲁伊重构与孢子结社](druid-rework-2026-11-05.md) | 独立荒野体力、四结社、充能与双武僧初始气 |
| [音乐与音频](music-and-audio.md) | 配乐切换逻辑与设置持久化 |
| [剧情教学](Adventure/four-chapter-v3/README.md) | 四章 40 关，万象回廊与高塔重排 |
| [共同流程](Adventure/four-chapter-v3/common-flow.md) | 中央实战确认、敌方整备、50 轮与两个重试入口 |

## 想改代码

| 文档 | 内容 |
|---|---|
| [RULES](../RULES.md) | 权威规则原文，代码与它冲突时以它为准 |
| [CODELY](../CODELY.md) | 工程结构、实现细节、开发约定与技术债 |
| [开发与测试](development.md) | 技术栈、测试命令、验证流程 |
| [状态与特殊效果AI入口](ai-state-policy.md) | 全职业状态权重、注册接口、优先级与无副作用验证 |
| [AI 策略与权重](ai-strategy-reference.md) | 58名子职的自身／针对策略与配置；含野蛮人低体力实时估值 |
| [知识领域](knowledge-domain.md) | 知识层数、独立力场伤害与困惑 |
| [牧师与净化领域](purification-domain.md) | 共同回复、领域赐福、圣火与灼伤实现及验证 |
| [身份规则与动画更新](identity-and-animation-update.md) | 忠臣扩展接口、动画核对及身份采样验收 |
| [格挡定义](combat-defense.md) | 格挡、抗性与状态时长的扣减规则 |
| [技能文案](character-skill-copy-draft.md) | 全部技能正文 |
| [提交与推送约定](../CONTRIBUTING.md) | 文件该放哪、什么该提交、一轮改动怎么组织 |

## 剧情关卡设计

只保留现行四章版本。

| 文档 | 内容 |
|---|---|
| [four-chapter-v3](Adventure/four-chapter-v3/README.md) | 四章 40 关、技能教学与实战、旧存档迁移 |
| [第一章与第四章](Adventure/four-chapter-v3/chapter-01-and-04.md) | 边境哨站、残旗战场 |
| [第二章：万象回廊](Adventure/four-chapter-v3/chapter-02-gallery.md) | 11–20 十战，一元素一关 |
| [第三章：法师高塔](Adventure/four-chapter-v3/chapter-03-tower.md) | 21–30 十战，法师九学派 |
| [指引台词](Adventure/four-chapter-v3/guide-dialogues.md) | 40 关全部教学台词 |
| [NPC 与美术](Adventure/four-chapter-v3/npcs-and-art.md) | 指引角色、背景与素材 |
| [剧情与迁移](Adventure/four-chapter-v3/story-and-migration.md) | 旧存档映射与章节关系 |

> 逐关时代（`level-01`～`level-30`、`chapter-02`、`chapter-03`）与三章连续战斗版（`single-battle-v2`）的设计稿已下线，需要时从 git 历史取回。

## 表现与美术

| 文档 | 内容 |
|---|---|
| [动画进度（P1）](animation-p1-progress.md) · [动画进度（P2/P3）](animation-p2-p3-progress.md) | 技能动画覆盖范围 |
| [动画清单](skill-and-status-animation-list.md) | 每名子职、每个状态需要的表现 |
| [战斗动画设计](combat-animation-design.md) | 挥击、飞行、格挡与闪避的时间轴 |
| [武僧改造](monk-rework-design.md) · [奥法五式](arcane-five-implementation.md) | 专项改造记录 |
| [美术制作](../Art/Production/) | 原图、内置 imagegen 提示词、导入清单 |

## 统计

| 文档 | 内容 |
|---|---|
| [胜率采样](../Tools/Statistics/2026-11-10-identity/README.md) | 58子职66,964局汇总；本轮身份新采2,600局，其余模式注明沿用来源 |
| [机器数据](../Tools/Statistics/2026-11-10-identity/results.json) | 可复核的原始结果 |
| [统计目录约定](../Tools/Statistics/README.md) | 一轮采样一个目录，旧目录在新一轮提交里删除 |
