# 德鲁伊徽记与形态图案（2026-10-24）

使用内置 imagegen，独立绘制2枚结社徽记及7张形态图案。精确最终提示词见 [prompts.json](prompts.json)，源路径与SHA256见 [selected-manifest.json](selected-manifest.json)。

| 资源 | 主体 | 运行时键 |
|---|---|---|
| 月亮结社 | 月相环、橡叶和根系，银蓝与深绿 | `Subclasses/druid_circle_of_the_moon` |
| 星辰结社 | 八芒星、黄铜星盘与星座 | `Subclasses/druid_circle_of_the_stars` |
| 熊、狼、鹰、蛇 | 月夜森林中的自然动物，旧金方框 | `Forms/moon_bear`、`moon_wolf`、`moon_eagle`、`moon_snake` |
| 射手、圣杯、巨龙 | 星光长弓、治疗圣杯、虚化星龙，旧金方框 | `Forms/stars_archer`、`stars_chalice`、`stars_dragon` |

月亮结社按玩家关于相似性的反馈采用月相符印版，避免与野蛮人·野兽道途的狼首弯月徽记混淆；野兽道途原图保留。原熊掌试稿提示词保留在prompts.json，最终月相版的独立提示词也见 [moon-revision-prompt.txt](moon-revision-prompt.txt)。

`sources/`保留正式原始生成PNG，`selected/`为512×512 RGBA，按Alpha边界裁切后等比缩放至480px并透明居中。沿用用户已授权的程序裁切与缩放。运行时文件位于 `Assets/Resources/Art/Battle/v1/` 对应目录，不依赖默认生成目录。

`Tools/ArtPipeline/import-druid-art.cjs`在全部尺寸、Alpha和哈希检查通过后导入，不手改meta。已接入总导入链。当前39子职+12职业共51枚正式徽记均独立；形态图另计7张。实际界面与操作验收见 [接入记录](../../Integration/druid-forms/README.md)。
