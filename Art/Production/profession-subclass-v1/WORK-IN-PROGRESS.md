# 职业 / 子职重构进度

用户要求：27 个既有角色改为职业·子职命名，物理文件、代码符号、ID、资源与文档同步；12 个职业徽记（含预留德鲁伊），重画身份变化的子职；图鉴职业筛选，详情三行（小字斜体职业中英、大字子职中英、阵营）。完整映射见 migration.json。

基线 ab4edff，工作区最初干净。已迁移200个文本文件和196个文件路径，保留meta原字节；已复制11个职业徽记，新增CharacterProfession、CharacterIdentity与旧ID兼容，扩充角色和图鉴扁平数据。下一步CodexScreen职业筛选与三行详情。

已向用户提出两项可选澄清：技能仅换名称和设定是否保留规则；德鲁伊先作为无子职的预留职业。尚未回复时按保持玩法和精确27归属推进。

美术：内置 image_gen 正在绘制 druid、rogue_swashbuckler、wizard_school_of_conjuration（functions cell228），以及 warlock_the_archfey、warlock_the_great_old_one、fighter_tactician（cell232）。结果及完整提示词存 functions.store 中，完成后复制至本目录。还计划 paladin_oath_of_the_crown、ranger_gloom_stalker、bard_college_of_valor。

后续：停自动刷新→单次重命名与 GUID 跟随移动（不手写meta）→职业/子职数据模型与兼容别名→图鉴过滤与排版→最新徽记导入与旧美术导入保护→针对性/全量测试和三项编译检查→实际编辑器验收→恢复随机报告→提交推送main。源代码UTF8 BOM，勿改Library/Temp/Logs/.codely.packages。没有子代理授权。

自动刷新已暂停：SessionState键FDD.ProfessionRefactor.AutoRefreshBlocked=true；完成必须调用AssetDatabase.AllowAutoRefresh并清除该标记，再退出Play/刷新。
