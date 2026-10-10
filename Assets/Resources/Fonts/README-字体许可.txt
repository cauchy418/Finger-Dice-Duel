Noto Sans CJK SC — 字体许可说明
=====================================

本目录下的字体文件来自 **Noto Sans CJK SC**（思源黑体的 Google 发行名）：

| 文件 | 用途 | 字重 |
|---|---|---|
| `NotoSansCJKsc-Medium.otf` | 正文（`UiFont.Regular`） | Medium |
| `NotoSansCJKsc-Bold.otf` | 标题 / 强调（`UiFont.Bold`） | Bold |
| `NotoSansCJKsc-LICENSE.txt` | 许可全文 | — |

许可：**SIL Open Font License 1.1 (OFL-1.1)**

这意味着：
- ✅ 可以自由使用、修改、嵌入、随软件一起分发（包括商业用途）。
- ✅ 可以再分发，但必须附带本许可说明。
- ❌ 不能单独出售字体文件本身。
- ❌ 修改后再分发时，不能继续使用 "Noto" 这个保留字体名称（Reserved Font Name）。

上游项目：https://github.com/notofonts/noto-cjk
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
   `RegularResourcePath` / `BoldResourcePath`（相对 `Resources`、不带扩展名）。
3. 字重建议：正文 `Medium`、标题 `Bold`。
   有真 Bold 时 `UiFont.ApplyBold` 会用真粗体，不再让引擎做伪粗体合成。

────────────────────────────────────────────────────────────

后续可优化（不急）：

- 这两个 OTF 各约 16 MB，合计 32 MB，会**整份进包**。
  若要显著减小包体，可迁移到 TextMeshPro 的「动态 SDF 字体资产」：
  按需烘焙实际用到的字形，通常能省下绝大部分体积。
- 也可以只保留一个含少量汉字的子集（用 `pyftsubset` 裁掉没用到
  的汉字），但游戏里角色名 / 提示文案可能变化，需谨慎。
