# Tools/HeadlessTests — 无头测试工具

## 这是什么

一套**不启动编辑器**就能编译并运行全部游戏逻辑测试的工具。

为什么需要它：本工程是团结引擎（Unity 衍生物）项目，但游戏逻辑（`Core` / `Characters` /
`Assistant`）刻意**不依赖任何 Unity 类型**。因此可以脱离编辑器，用 Roslyn 直接编译成
一个普通 .NET 可执行文件跑测试 —— 一次完整回归只需 2~3 秒，而启动编辑器通常要几分钟。

```
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/HeadlessTests/run-tests.ps1
```

> 注意：本机（Windows PowerShell 5.1）没有 `pwsh`（PowerShell 7），所以上面的命令用
> `powershell`。如果你装了 PowerShell 7，把 `powershell` 换成 `pwsh` 也可以。
> 脚本文件本身以 **UTF-8 with BOM** 保存 —— 5.1 没有 BOM 会把中文注释读成乱码并报语法错。
> 修改脚本后请保持 BOM。

## 它做了什么

1. 自动定位本机团结引擎安装目录（也可用 `-EditorRoot` 指定，或设环境变量 `FDD_EDITOR_ROOT`）。
2. 从编辑器目录借用 **Roslyn 编译器**（`DotNetSdkRoslyn/csc.dll`）和 **BCL 引用程序集**
   （`MonoBleedingEdge/lib/mono/4.8-api`）。**不需要安装 .NET SDK**，只需要 .NET 运行时。
3. 把 `Assets/Scripts`、`Assets/Tests` 与 `Program.cs` 一起编译。
4. 运行全部测试并输出结果，失败时返回非 0 退出码。

产物落在 `bin/`（已被 `.gitignore` 忽略，删除后会自动重建）。

## 参数

| 参数 | 说明 |
|---|---|
| `-EditorRoot <path>` | 手动指定编辑器 `Editor/Data` 目录 |
| `-Define <symbols>` | 追加编译定义。用 `-Define FDD_UNITY_TESTS` 可校验"Unity 侧测试代码"能否编译（此时只编译成库、不执行，并自动引入 `nunit.framework.dll`） |
| `-Quiet` | 只输出测试结果，不输出编译信息 |

运行器还支持两个命令行参数（直接传给 `dotnet bin/HeadlessTests.dll`）：

| 参数 | 说明 |
|---|---|
| `<filter>` | 按**测试类名**子串过滤，例如 `ShieldTests` |
| `-v` / `--verbose` | 失败时打印堆栈 |

> `-Define FDD_UNITY_TESTS` 是**唯一能替代"打开编辑器跑一次"的廉价检查**：
> 它验证 Unity/NUnit 侧那套条件编译能不能通过。改完 asmdef 或 shim 后建议跑一次。

## 同一套测试如何兼容两种环境

`Assets/Tests/EditMode/UnityTestShim.cs` 是一个门面：

- 定义 `FDD_UNITY_TESTS` 时（Unity/Tuanjie EditMode Test Runner），它把
  `[TestFixture]` / `[SetUp]` / `[TearDown]` / `[Test]` / `Assert.*` 映射到 **NUnit**。
- 未定义时（本工具），它自己提供一套极简的等价实现，并由 `Program.cs` 用反射调度。

因此测试文件里**只允许**使用这套门面提供的 API。
不要直接用 NUnit 专有 API（`Assert.That` / `Is.EqualTo` / `[TestCase]` 等），
否则无头路径会编译失败。

由于 C# 的 `using` 指令不会跨文件生效，每个测试文件都带有一段：

```csharp
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
```

在无头路径下这段是空的（类型来自 shim），在 Unity 路径下它把 NUnit 引入作用域。

## 与 Unity Test Runner 的关系

- 无头工具：**日常回归的主力**，秒级反馈。
- Unity Test Runner：最终验收，会同时覆盖 Unity 侧的程序集定义是否配置正确
  （`Assets/Tests/EditMode/FingerDiceDuel.Tests.EditMode.asmdef`）。

两者跑的是**完全相同的测试文件**，所以不会出现"无头过了、编辑器里挂"的分裂。
