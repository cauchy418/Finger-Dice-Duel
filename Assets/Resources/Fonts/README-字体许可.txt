界面字体 — 字体许可说明
=====================================

本目录包含Noto Sans CJK SC（思源黑体）与Source Han Serif CN（思源宋体），均使用静态OTF。当前资源与用途：

| 文件 | 用途 | 字重 |
|---|---|---|
| `NotoSansCJKsc-Medium.otf` | 正文（`UiFont.Regular`） | Medium |
| `NotoSansCJKsc-Bold.otf` | 数字 / 强调及标题回退（`UiFont.Bold`） | Bold |
| `SourceHanSerifCN-SemiBold.otf` | 奇幻界面标题（`UiFont.Title`） | SemiBold |
| `NotoSansCJKsc-LICENSE.txt` | 思源黑体许可全文 | — |
| `SourceHanSerif-LICENSE.txt` | 思源宋体许可全文 | — |

许可：**SIL Open Font License 1.1 (OFL-1.1)**

这意味着：
- ✅ 可以自由使用、修改、嵌入、随软件一起分发（包括商业用途）。
- ✅ 可以再分发，但必须附带本许可说明。
- ❌ 不能单独出售字体文件本身。
- ❌ 修改后再分发时，不能继续使用 "Noto" 这个保留字体名称（Reserved Font Name）。

思源黑体上游：https://github.com/notofonts/noto-cjk
思源宋体来源与许可：见同目录SourceHanSerif-README.txt及SourceHanSerif-LICENSE.txt
许可全文：https://openfontlicense.org/

────────────────────────────────────────────────────────────

为什么选它：

1. **一个文件同时覆盖中文与拉丁字母**（含 A-Z / a-z / 0-9 与 `/ : * + - =` 等
   界面符号），所以**不需要再搭配英文字体**。
2. 授权干净（OFL），可商用可分发。
3. 提供 **7 个静态字重**，能精确控制粗细。

⚠️ 为什么必须是**静态**字体（这条是踩过坑的）：

之前用的是 `NotoSansSC-Regular.ttf`，它是**可变字体（Variable Font）**
（文件里有 `fvar` 表）。Unity 的旧版动态字体管线不支持选择可变轴，
只会取它的**默认实例** —— 当时默认实例是 Thin，所以界面中文**又细又虚**。

`fonttools` 在本机装不上、网络也不可达，无法把可变字体实例化为静态字重，
因此直接改用上游发布的**静态 OTF**。现在的两个字重都没有 `fvar` 表。

**换字体时必须确认：目标文件不能是可变字体。**

────────────────────────────────────────────────────────────

替换 / 新增字重时：

1. 把 `.otf` 或 `.ttf` 放进本目录。
   **不要放 `.ttc`（字体集合）** —— `Resources.Load<Font>` 读不了。
2. 改 `Assets/Scripts/UI/UiFont.cs` 里的
   `RegularResourcePath` / `BoldResourcePath`，标题在Resolve中加载SourceHanSerifCN-SemiBold（相对Resources、不带扩展名）。
3. 当前正文Medium、强调Bold、标题SemiBold。
   有真 Bold 时 `UiFont.ApplyBold` 会用真粗体，不再让引擎做伪粗体合成。

────────────────────────────────────────────────────────────

后续可优化（不急）：

- 三个OTF合计约43MiB，会整份进包。
  若要显著减小包体，可迁移到 TextMeshPro 的「动态 SDF 字体资产」：
  按需烘焙实际用到的字形，通常能省下绝大部分体积。
- 也可以只保留一个含少量汉字的子集（用 `pyftsubset` 裁掉没用到
  的汉字），但游戏里角色名 / 提示文案可能变化，需谨慎。
