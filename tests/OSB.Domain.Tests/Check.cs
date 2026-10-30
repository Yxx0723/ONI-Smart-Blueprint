using System;
using System.Collections.Generic;

namespace OSB.Domain.Tests
{
    /// <summary>
    /// 极简断言器。每条断言失败即记录上下文并继续执行下一个用例，
    /// 以便一次运行暴露全部失败点而不是首错即停。
    /// </summary>
    internal static class Check
    {
        private static string _currentCase = "<none>";

        internal static int Passed;
        internal static int Failed;

        internal static readonly List<string> Failures = new List<string>();

        internal static void BeginCase(string name)
        {
            _currentCase = name;
        }

        internal static void True(bool condition, string message)
        {
            if (condition)
            {
                Passed++;
                return;
            }

            Failed++;
            string line = _currentCase + ": " + message;
            Failures.Add(line);
            Console.WriteLine("  FAIL  " + line);
        }

        internal static void False(bool condition, string message) => True(!condition, message);

        internal static void Equal<T>(T expected, T actual, string message)
        {
            bool ok = EqualityComparer<T>.Default.Equals(expected, actual);
            True(ok, message + " | 期望=" + expected + " 实际=" + actual);
        }

        internal static void NotEqual<T>(T notExpected, T actual, string message)
        {
            bool ok = !EqualityComparer<T>.Default.Equals(notExpected, actual);
            True(ok, message + " | 不应等于 " + notExpected);
        }

        internal static void NotNull(object value, string message) => True(value != null, message);

        internal static void Throws<TException>(Action action, string message) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                Passed++;
                return;
            }
            catch (Exception e)
            {
                Failed++;
                string line = _currentCase + ": " + message + " | 抛出了 " + e.GetType().Name + " 而非 " + typeof(TException).Name;
                Failures.Add(line);
                Console.WriteLine("  FAIL  " + line);
                return;
            }

            Failed++;
            string noThrow = _currentCase + ": " + message + " | 未抛出 " + typeof(TException).Name;
            Failures.Add(noThrow);
            Console.WriteLine("  FAIL  " + noThrow);
        }
    }
}
