// ---------------------------------------------------------------------------
// 统一的测试 API 门面。
//
// 同一套测试文件要能在两种环境运行：
//   1. Unity / Tuanjie EditMode Test Runner（定义 FDD_UNITY_TESTS 时，映射到 NUnit）
//   2. 纯 .NET 无头运行器（Tools/HeadlessTests，不定义该宏时使用本文件内的极简实现）
//
// 因此测试文件里只允许使用本文件导出的 [TestFixture] / [SetUp] / [TearDown] / [Test]
// 与 Assert.*，不直接写 NUnit 专有 API（如 Assert.That / Is.EqualTo / TestCase）。
// 详见 RULES.md 与 Tools/HeadlessTests/README.md。
// ---------------------------------------------------------------------------

#if FDD_UNITY_TESTS
using NUnit.Framework;
#else
using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Tests
{
    /// <summary>标记测试类。</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TestFixtureAttribute : Attribute { }

    /// <summary>标记每个测试方法执行前调用的初始化方法。</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SetUpAttribute : Attribute { }

    /// <summary>标记每个测试方法执行后调用的清理方法。</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TearDownAttribute : Attribute { }

    /// <summary>标记一个测试方法。</summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    /// <summary>断言失败时抛出，由无头运行器捕获并报告。</summary>
    public sealed class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    /// <summary>极简断言库，API 与 NUnit 的经典模型保持一致。</summary>
    public static class Assert
    {
        private static string Combine(string message, string userMessage)
            => string.IsNullOrEmpty(userMessage) ? message : message + " | " + userMessage;

        public static void True(bool condition, string message = null)
        {
            if (!condition) throw new AssertionException(Combine("期望 true，实际 false。", message));
        }

        public static void False(bool condition, string message = null)
        {
            if (condition) throw new AssertionException(Combine("期望 false，实际 true。", message));
        }

        public static void IsTrue(bool condition, string message = null) => True(condition, message);

        public static void IsFalse(bool condition, string message = null) => False(condition, message);

        public static void Null(object actual, string message = null)
        {
            if (actual != null)
                throw new AssertionException(Combine($"期望 null，实际 {Format(actual)}。", message));
        }

        public static void NotNull(object actual, string message = null)
        {
            if (actual == null)
                throw new AssertionException(Combine("期望非 null，实际为 null。", message));
        }

        public static void AreEqual(int expected, int actual, string message = null)
        {
            if (expected != actual)
                throw new AssertionException(
                    Combine($"期望 {expected}，实际 {actual}。", message));
        }

        public static void AreEqual(string expected, string actual, string message = null)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new AssertionException(
                    Combine($"期望 \"{expected}\"，实际 \"{actual}\"。", message));
        }

        public static void AreEqual(object expected, object actual, string message = null)
        {
            if (!Equals(expected, actual))
                throw new AssertionException(
                    Combine($"期望 {Format(expected)}，实际 {Format(actual)}。", message));
        }

        public static void AreNotEqual(int expected, int actual, string message = null)
        {
            if (expected == actual)
                throw new AssertionException(
                    Combine($"期望不等于 {expected}，但相等。", message));
        }

        public static void AreSame(object expected, object actual, string message = null)
        {
            if (!ReferenceEquals(expected, actual))
                throw new AssertionException(
                    Combine($"期望同一实例 {Format(expected)}，实际 {Format(actual)}。", message));
        }

        public static void IsEmpty<T>(IReadOnlyCollection<T> collection, string message = null)
        {
            if (collection == null || collection.Count != 0)
                throw new AssertionException(
                    Combine($"期望集合为空，实际 {collection?.Count ?? 0} 项。", message));
        }

        /// <summary>断言 <paramref name="code"/> 抛出 <typeparamref name="T"/>，并返回该异常。</summary>
        public static T Throws<T>(Action code, string message = null) where T : Exception
        {
            if (code == null) throw new ArgumentNullException(nameof(code));

            try
            {
                code();
            }
            catch (T expected)
            {
                return expected;
            }
            catch (Exception other)
            {
                throw new AssertionException(Combine(
                    $"期望抛出 {typeof(T).Name}，实际抛出 {other.GetType().Name}: {other.Message}",
                    message));
            }

            throw new AssertionException(Combine($"期望抛出 {typeof(T).Name}，但没有抛出任何异常。", message));
        }

        private static string Format(object value)
            => value == null ? "null" : value.ToString();
    }
}
#endif
