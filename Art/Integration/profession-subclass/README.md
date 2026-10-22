# 职业·子职接入验收

2026-10-23，实际团结编辑器 Play 模式执行。重命名与美术调整保持原玩法。

- `validation.txt`：321 项检查通过。覆盖 12 职业图标、27 子职图标及旧路径兼容，12 个职业的实际射线点击与数量，德鲁伊空态，职业+阵营交集，三页总览，27 份中英文三行标题，27 张选将卡实际点击，1v1/2v2/8 人完整名称。
- `review.cs.txt`：通过 `execute_csharp_script` 的 Play 模式可重复执行，输出本目录检查和截图。
- `profession-chooser.png`、`fighter-filter.png`、`druid-reserved.png`：职业面板、筛选、预留职业。
- `overview-1/2/3.png`：12/12/3 个角色。
- `detail-*.png`：职业斜体、小字；子职大字；阵营独立第三行。
- `selection-last-page.png`：末行子职选将；`battle-long-names.png`、`battle-2v2.png`、`battle-8p.png`：全部战斗模式长名。

逻辑测试 1052 通过、0 失败；Unity 条件测试编译和 UI 编译通过。新增契约测试对照重构前 27 份攻击、防御、生命、距离，并检查完整命名、旧 ID 兼容及职业数量；原有玩法回归与整局统计也已运行。

完整素材来源、保留版本与映射见 [制作记录](../../Production/profession-subclass-v1/README.md)。此前 Integration 目录中的截图、日志为对应日期历史证据，可能仍显示旧名称；最新验收以本目录为准。

追加美术验收：9 枚职业大类与守望者道途重绘后，39 枚最终图标的像素文件 SHA-256 全不相同，尺寸与透明通道检查通过。当前职业面板、守望者详情与三种战斗模式截图均重新生成。
