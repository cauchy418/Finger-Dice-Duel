# 老兵旁白与共同规则排版

2026-10-24。游戏内规则说明只讲共同规则：运算、数字效果、护盾／距离、伤害保护、先攻、跳过、状态、各模式胜负与身份。去除具体子职名称和专属技能举例；完整技能仍留在图鉴与RULES详细参考。

`WrapReadableText`按完整数字、单位及状态词组换行，中文闭标点附前词，避免独立标点、数字被拆开或末行搬字后溢出。老兵的过程旁白连续排版，开场／总结保留段落；固定高度不足时仅小幅调字号，先攻提示复用同一逻辑。

## 实际引擎验证

[validation.txt](validation.txt)记录最终798项检查：逐一打开十关，读取并显示所有Beat、开场与总结；每行测宽、测正文高度、检查标点不独占行和文字不丢失。第五战打开真实先攻老兵面板，规则弹窗检查宽度和顶部／底部滚动。没有执行胜利流程或写PlayerPrefs；恢复AI等待时长、返回主菜单。

本验收只验证文本布局和可访问性；完整教学战斗流程仍由既有章节测试与[十战验收](../adventure-chapter-finale/README.md)覆盖。

- [初战开场](intro-1.png)、[终战开场](intro-10.png)。
- [初战过程指引](narration-1-1.png)、[八战长旁白](narration-8-1.png)、[先攻老兵](initiative-veteran.png)。
- [共同规则顶部](common-rules-top.png)、[共同规则底部](common-rules-bottom.png)。

复验：本地Codely Play模式执行`helpers.cs.txt`＋`review.cs.txt`。所有临时检查与截图通过真实引擎字体测量和Canvas生成。
