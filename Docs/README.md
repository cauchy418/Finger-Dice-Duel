# 文档索引

仓库首页在 [README](../README.md)。这里按「想找什么」分类。

## 想上手玩

| 文档 | 内容 |
|---|---|
| [模式与战斗](modes-and-combat.md) | 一回合怎么走、距离与护盾怎么算、六种模式细则、13 类伤害与 21 种状态 |
| [角色图鉴](roster.md) | 55 名子职的数值与关键机制、阵营分类、全角色胜率 |
| [剧情教学](Adventure/single-battle-v2/README.md) | 三章三十战的连续战斗版设计 |
| [共同规则与 NPC](Adventure/single-battle-v2/common.md) | 操演护持、实战号角、调律与整备 |
| [音乐与音频](music-and-audio.md) | 配乐切换逻辑与设置持久化 |

## 想改代码

| 文档 | 内容 |
|---|---|
| [RULES](../RULES.md) | 权威规则原文，代码与它冲突时以它为准 |
| [CODELY](../CODELY.md) | 工程结构、实现细节、开发约定与技术债 |
| [开发与测试](development.md) | 技术栈、测试命令、验证流程 |
| [AI 策略与权重](ai-strategy-reference.md) | 55 名子职的自身／针对策略与配置 |
| [格挡定义](combat-defense.md) | 格挡、抗性与状态时长的扣减规则 |
| [技能文案](character-skill-copy-draft.md) | 全部技能正文 |

## 剧情关卡设计

| 文档 | 内容 |
|---|---|
| [single-battle-v2](Adventure/single-battle-v2/README.md) | **现行**：三章连续战斗版，同场教学转自由战斗 |
| [chapter-01](Adventure/single-battle-v2/chapter-01.md) · [chapter-02](Adventure/single-battle-v2/chapter-02.md) · [chapter-03](Adventure/single-battle-v2/chapter-03.md) | 分章十战配置 |
| [implementation-audit](Adventure/single-battle-v2/implementation-audit.md) | 改造前的差异清单与验收要求 |
| `Adventure/level-*.md`、`Adventure/chapter-0*.md` | **历史**：逐关独立样本时代的旧设计稿，仅作实现记录保留 |

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
| [胜率采样报告](../Tools/Statistics/2026-10-30/README.md) | 2026-10-30，58,476 局的完整分母、种子与复现命令 |
| [机器数据](../Tools/Statistics/2026-10-30/results.json) | 可复核的原始结果 |
