// HeadlessSim: the NUnit / Unity Test Framework surface used by PlayMode tests, plus a runner that
// executes [Test] and [UnityTest] methods on the simulator. Not part of the Unity project.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HeadlessSim;
using UnityEngine;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { public string Description { get; set; } }
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)] public class TestCaseAttribute : Attribute { public object[] Arguments { get; } public TestCaseAttribute(params object[] arguments) { Arguments = arguments; } }
    [AttributeUsage(AttributeTargets.Method)] public class SetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class TearDownAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class OneTimeSetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class OneTimeTearDownAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class TestFixtureAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)] public class CategoryAttribute : Attribute { public string Name { get; } public CategoryAttribute(string name) { Name = name; } }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)] public class IgnoreAttribute : Attribute { public string Reason { get; } public IgnoreAttribute(string reason) { Reason = reason; } }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)] public class ExplicitAttribute : Attribute { public ExplicitAttribute() { } public ExplicitAttribute(string reason) { } }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)] public class DescriptionAttribute : Attribute { public DescriptionAttribute(string description) { } }
    [AttributeUsage(AttributeTargets.Method)] public class TimeoutAttribute : Attribute { public TimeoutAttribute(int milliseconds) { } }
    [AttributeUsage(AttributeTargets.Method)] public class OrderAttribute : Attribute { public int Order { get; } public OrderAttribute(int order) { Order = order; } }

    public class AssertionException : Exception { public AssertionException(string message) : base(message) { } }
    public class IgnoreException : Exception { public IgnoreException(string message) : base(message) { } }
    public class InconclusiveException : Exception { public InconclusiveException(string message) : base(message) { } }
    public class SuccessException : Exception { public SuccessException(string message) : base(message) { } }

    public delegate void TestDelegate();

    public static class Assert
    {
        private static string Join(string message, string detail) => string.IsNullOrEmpty(message) ? detail : message + "  " + detail;
        private static string Text(object value) => value == null ? "null" : (value is string s ? "\"" + s + "\"" : value.ToString());

        private static bool IsNumber(object value) => value is sbyte || value is byte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal;

        private static bool ObjectsEqual(object expected, object actual)
        {
            if (expected == null || actual == null)
            {
                if (expected is UnityEngine.Object eo) return eo == null && (actual == null || (actual is UnityEngine.Object ao0 && ao0 == null));
                if (actual is UnityEngine.Object ao) return ao == null;
                return expected == null && actual == null;
            }
            if (IsNumber(expected) && IsNumber(actual)) return Convert.ToDouble(expected) == Convert.ToDouble(actual);
            if (expected is IEnumerable left && actual is IEnumerable right && !(expected is string))
            {
                IEnumerator a = left.GetEnumerator(), b = right.GetEnumerator();
                while (true)
                {
                    bool hasA = a.MoveNext(), hasB = b.MoveNext();
                    if (hasA != hasB) return false;
                    if (!hasA) return true;
                    if (!ObjectsEqual(a.Current, b.Current)) return false;
                }
            }
            return expected.Equals(actual);
        }

        public static void AreEqual(object expected, object actual) { AreEqual(expected, actual, null); }
        public static void AreEqual(object expected, object actual, string message)
        {
            if (!ObjectsEqual(expected, actual)) throw new AssertionException(Join(message, $"Expected: {Text(expected)}  But was: {Text(actual)}"));
        }
        public static void AreEqual(double expected, double actual, double delta) { AreEqual(expected, actual, delta, null); }
        public static void AreEqual(double expected, double actual, double delta, string message)
        {
            if (double.IsNaN(actual) || Math.Abs(expected - actual) > delta) throw new AssertionException(Join(message, $"Expected: {expected} +/- {delta}  But was: {actual}"));
        }
        public static void AreNotEqual(object expected, object actual) { AreNotEqual(expected, actual, null); }
        public static void AreNotEqual(object expected, object actual, string message)
        {
            if (ObjectsEqual(expected, actual)) throw new AssertionException(Join(message, $"Expected: not equal to {Text(expected)}"));
        }
        public static void AreSame(object expected, object actual) { AreSame(expected, actual, null); }
        public static void AreSame(object expected, object actual, string message)
        {
            if (!ReferenceEquals(expected, actual)) throw new AssertionException(Join(message, $"Expected: same as {Text(expected)}  But was: {Text(actual)}"));
        }
        public static void AreNotSame(object expected, object actual) { if (ReferenceEquals(expected, actual)) throw new AssertionException($"Expected: not same as {Text(expected)}"); }
        public static void AreNotSame(object expected, object actual, string message) { if (ReferenceEquals(expected, actual)) throw new AssertionException(Join(message, $"Expected: not same as {Text(expected)}")); }

        public static void IsTrue(bool condition) { IsTrue(condition, null); }
        public static void IsTrue(bool condition, string message) { if (!condition) throw new AssertionException(Join(message, "Expected: True  But was: False")); }
        public static void True(bool condition) { IsTrue(condition, null); }
        public static void True(bool condition, string message) { IsTrue(condition, message); }
        public static void IsFalse(bool condition) { IsFalse(condition, null); }
        public static void IsFalse(bool condition, string message) { if (condition) throw new AssertionException(Join(message, "Expected: False  But was: True")); }
        public static void False(bool condition) { IsFalse(condition, null); }
        public static void False(bool condition, string message) { IsFalse(condition, message); }
        public static void That(bool condition) { IsTrue(condition, null); }
        public static void That(bool condition, string message) { IsTrue(condition, message); }

        private static bool IsNullObject(object value) => value == null || (value is UnityEngine.Object o && o == null);
        public static void IsNull(object value) { IsNull(value, null); }
        public static void IsNull(object value, string message) { if (!IsNullObject(value)) throw new AssertionException(Join(message, $"Expected: null  But was: {Text(value)}")); }
        public static void Null(object value) { IsNull(value, null); }
        public static void Null(object value, string message) { IsNull(value, message); }
        public static void IsNotNull(object value) { IsNotNull(value, null); }
        public static void IsNotNull(object value, string message) { if (IsNullObject(value)) throw new AssertionException(Join(message, "Expected: not null  But was: null")); }
        public static void NotNull(object value) { IsNotNull(value, null); }
        public static void NotNull(object value, string message) { IsNotNull(value, message); }

        public static void Greater(IComparable a, IComparable b) { Greater(a, b, null); }
        public static void Greater(IComparable a, IComparable b, string message) { if (Compare(a, b) <= 0) throw new AssertionException(Join(message, $"Expected: greater than {b}  But was: {a}")); }
        public static void GreaterOrEqual(IComparable a, IComparable b) { GreaterOrEqual(a, b, null); }
        public static void GreaterOrEqual(IComparable a, IComparable b, string message) { if (Compare(a, b) < 0) throw new AssertionException(Join(message, $"Expected: greater than or equal to {b}  But was: {a}")); }
        public static void Less(IComparable a, IComparable b) { Less(a, b, null); }
        public static void Less(IComparable a, IComparable b, string message) { if (Compare(a, b) >= 0) throw new AssertionException(Join(message, $"Expected: less than {b}  But was: {a}")); }
        public static void LessOrEqual(IComparable a, IComparable b) { LessOrEqual(a, b, null); }
        public static void LessOrEqual(IComparable a, IComparable b, string message) { if (Compare(a, b) > 0) throw new AssertionException(Join(message, $"Expected: less than or equal to {b}  But was: {a}")); }
        private static int Compare(IComparable a, IComparable b)
        {
            if (IsNumber(a) && IsNumber(b)) return Convert.ToDouble(a).CompareTo(Convert.ToDouble(b));
            return a.CompareTo(b);
        }

        public static void Contains(object expected, ICollection actual) { Contains(expected, actual, null); }
        public static void Contains(object expected, ICollection actual, string message)
        {
            foreach (object item in actual) if (ObjectsEqual(expected, item)) return;
            throw new AssertionException(Join(message, $"Expected: collection containing {Text(expected)}"));
        }
        public static void IsEmpty(ICollection collection) { if (collection.Count != 0) throw new AssertionException($"Expected: <empty>  But had {collection.Count} items"); }
        public static void IsEmpty(string value) { if (!string.IsNullOrEmpty(value)) throw new AssertionException($"Expected: <empty>  But was: \"{value}\""); }
        public static void IsNotEmpty(ICollection collection) { if (collection.Count == 0) throw new AssertionException("Expected: not <empty>"); }
        public static void IsNotEmpty(string value) { if (string.IsNullOrEmpty(value)) throw new AssertionException("Expected: not <empty>"); }
        public static void IsInstanceOf<T>(object value) { if (!(value is T)) throw new AssertionException($"Expected: instance of {typeof(T).Name}  But was: {Text(value)}"); }

        public static void Fail() { throw new AssertionException("Assert.Fail"); }
        public static void Fail(string message) { throw new AssertionException(message); }
        public static void Ignore() { throw new IgnoreException("Ignored"); }
        public static void Ignore(string message) { throw new IgnoreException(message); }
        public static void Inconclusive(string message) { throw new InconclusiveException(message); }
        public static void Pass() { throw new SuccessException("Pass"); }
        public static void Pass(string message) { throw new SuccessException(message); }

        public static T Throws<T>(TestDelegate code) where T : Exception
        {
            try { code(); }
            catch (T expected) { return expected; }
            catch (Exception other) { throw new AssertionException($"Expected: {typeof(T).Name}  But was: {other.GetType().Name}"); }
            throw new AssertionException($"Expected: {typeof(T).Name}  But no exception was thrown");
        }
        public static void DoesNotThrow(TestDelegate code)
        {
            try { code(); }
            catch (Exception exception) { throw new AssertionException($"Unexpected exception: {exception.GetType().Name}: {exception.Message}"); }
        }
    }

    public static class StringAssert
    {
        public static void Contains(string expected, string actual) { Contains(expected, actual, null); }
        public static void Contains(string expected, string actual, string message)
        {
            if (actual == null || actual.IndexOf(expected, StringComparison.Ordinal) < 0) throw new AssertionException((message ?? "") + $" Expected: String containing \"{expected}\"  But was: \"{actual}\"");
        }
        public static void DoesNotContain(string expected, string actual) { if (actual != null && actual.Contains(expected)) throw new AssertionException($"Expected: String not containing \"{expected}\"  But was: \"{actual}\""); }
        public static void StartsWith(string expected, string actual) { if (actual == null || !actual.StartsWith(expected)) throw new AssertionException($"Expected: String starting with \"{expected}\"  But was: \"{actual}\""); }
        public static void EndsWith(string expected, string actual) { if (actual == null || !actual.EndsWith(expected)) throw new AssertionException($"Expected: String ending with \"{expected}\"  But was: \"{actual}\""); }
        public static void AreEqualIgnoringCase(string expected, string actual) { if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) throw new AssertionException($"Expected: \"{expected}\" (ignoring case)  But was: \"{actual}\""); }
    }

    public static class CollectionAssert
    {
        public static void AreEqual(IEnumerable expected, IEnumerable actual) { Assert.AreEqual(expected, actual); }
        public static void Contains(IEnumerable collection, object item)
        {
            foreach (object element in collection) if (Equals(element, item)) return;
            throw new AssertionException($"Expected: collection containing {item}");
        }
        public static void IsEmpty(IEnumerable collection) { foreach (object _ in collection) throw new AssertionException("Expected: <empty>"); }
        public static void IsNotEmpty(IEnumerable collection) { foreach (object _ in collection) return; throw new AssertionException("Expected: not <empty>"); }
    }
}

namespace UnityEngine.TestTools
{
    [AttributeUsage(AttributeTargets.Method)] public class UnityTestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class UnitySetUpAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class UnityTearDownAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Assembly)] public class UnityPlatformAttribute : Attribute { public UnityPlatformAttribute(params RuntimePlatform[] include) { } }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)] public class RequiresPlayModeAttribute : Attribute { public RequiresPlayModeAttribute(bool requiresPlayMode = true) { } }

    /// <summary>
    /// Like the Unity Test Framework: an error or exception logged during a test fails it unless
    /// the message was announced with LogAssert.Expect, and an expected message that never appears fails too.
    /// </summary>
    public static class LogAssert
    {
        internal struct Expectation { public LogType Type; public string Text; public Regex Pattern; public bool Met; }
        internal static readonly List<Expectation> Expectations = new List<Expectation>();
        public static bool ignoreFailingMessages { get; set; }

        public static void Expect(LogType type, string message) { Expectations.Add(new Expectation { Type = type, Text = message }); }
        public static void Expect(LogType type, Regex message) { Expectations.Add(new Expectation { Type = type, Pattern = message }); }
        public static void NoUnexpectedReceived() { }

        internal static void Begin() { Expectations.Clear(); ignoreFailingMessages = false; }

        private static LogType Map(LogKind kind)
        {
            switch (kind)
            {
                case LogKind.Warning: return LogType.Warning;
                case LogKind.Error: return LogType.Error;
                case LogKind.Exception: return LogType.Exception;
                default: return LogType.Log;
            }
        }

        /// <summary>Returns a failure message, or null when the logs of the test are acceptable.</summary>
        internal static string Evaluate(int logStartIndex)
        {
            var problems = new List<string>();
            foreach (LogEntry entry in LogCapture.Since(logStartIndex))
            {
                LogType type = Map(entry.Kind);
                bool matched = false;
                for (int i = 0; i < Expectations.Count; i++)
                {
                    Expectation expectation = Expectations[i];
                    if (expectation.Met || expectation.Type != type) continue;
                    bool match = expectation.Pattern != null ? expectation.Pattern.IsMatch(entry.Message) : entry.Message == expectation.Text;
                    if (!match) continue;
                    expectation.Met = true;
                    Expectations[i] = expectation;
                    matched = true;
                    break;
                }

                if (!matched && !ignoreFailingMessages && (type == LogType.Error || type == LogType.Exception))
                {
                    problems.Add($"Unhandled log message: [{type}] {FirstLine(entry.Message)}");
                }
            }

            foreach (Expectation expectation in Expectations)
            {
                if (!expectation.Met) problems.Add($"Expected log did not appear: [{expectation.Type}] {(expectation.Pattern != null ? "Regex: " + expectation.Pattern : expectation.Text)}");
            }

            return problems.Count == 0 ? null : string.Join(" | ", problems);
        }

        private static string FirstLine(string text)
        {
            int index = text.IndexOf('\n');
            return index >= 0 ? text.Substring(0, index) : text;
        }
    }
}

namespace HeadlessSim
{
    public enum TestOutcome { Passed, Failed, Skipped }

    public sealed class TestResult
    {
        public string Name;
        public TestOutcome Outcome;
        public string Message;
        public double Seconds;
        public override string ToString() => $"{Outcome.ToString().ToUpperInvariant(),-7} {Name}{(string.IsNullOrEmpty(Message) ? "" : "  -- " + Message)}";
    }

    /// <summary>Runs NUnit-style tests on the simulator: fresh world and play mode per test.</summary>
    public static class TestRunner
    {
        /// <summary>Tests that cannot run here: full name or method name -> reason.</summary>
        public static readonly Dictionary<string, string> SkipList = new Dictionary<string, string>();

        /// <summary>Upper bound of simulated frames per test (guards against endless loops).</summary>
        public static int MaxFramesPerTest = 400000;
        public static double MaxWallClockSecondsPerTest = 120.0;

        private static int frames;
        private static Stopwatch watch;

        public static List<TestResult> RunAll(Assembly assembly, Func<Type, bool> fixtureFilter = null)
        {
            var results = new List<TestResult>();
            foreach (Type type in assembly.GetTypes().OrderBy(t => t.FullName))
            {
                if (type.IsAbstract || !type.IsClass || type.IsGenericTypeDefinition) continue;
                if (fixtureFilter != null && !fixtureFilter(type)) continue;
                MethodInfo[] tests = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null || m.GetCustomAttribute<UnityEngine.TestTools.UnityTestAttribute>() != null)
                    .OrderBy(m => m.MetadataToken).ToArray();
                if (tests.Length == 0) continue;

                object fixture;
                try { fixture = Activator.CreateInstance(type); }
                catch (Exception exception)
                {
                    results.Add(new TestResult { Name = type.Name, Outcome = TestOutcome.Failed, Message = "Fixture could not be created: " + exception.Message });
                    continue;
                }

                foreach (MethodInfo test in tests) results.Add(RunOne(fixture, type, test));
            }
            return results;
        }

        private static TestResult RunOne(object fixture, Type type, MethodInfo test)
        {
            var result = new TestResult { Name = type.Name + "." + test.Name };
            if (SkipList.TryGetValue(result.Name, out string reason) || SkipList.TryGetValue(test.Name, out reason))
            {
                result.Outcome = TestOutcome.Skipped; result.Message = reason;
                return result;
            }
            var ignore = test.GetCustomAttribute<NUnit.Framework.IgnoreAttribute>() ?? type.GetCustomAttribute<NUnit.Framework.IgnoreAttribute>();
            if (ignore != null)
            {
                result.Outcome = TestOutcome.Skipped; result.Message = "[Ignore] " + ignore.Reason;
                return result;
            }

            watch = Stopwatch.StartNew();
            frames = 0;
            Simulation.ResetWorld();
            int exceptionsBefore = Simulation.Exceptions.Count;
            int logStart = LogCapture.Entries.Count;
            string previousPhase = LogCapture.Phase;
            bool previousEcho = LogCapture.Echo;
            LogCapture.Phase = "test " + result.Name;
            LogCapture.Echo = false;
            UnityEngine.TestTools.LogAssert.Begin();
            UnityEngine.InputSystem.Keyboard.SimRemove();
            UnityEngine.InputSystem.Mouse.SimRemove();

            try
            {
                Simulation.EnterPlayMode();
                InvokeAll(fixture, type, typeof(NUnit.Framework.SetUpAttribute));
                object returned = test.Invoke(fixture, null);
                if (returned is IEnumerator routine) Drive(routine);
                result.Outcome = TestOutcome.Passed;
            }
            catch (Exception exception)
            {
                Classify(Unwrap(exception), result);
            }

            try { InvokeAll(fixture, type, typeof(NUnit.Framework.TearDownAttribute)); }
            catch (Exception exception)
            {
                if (result.Outcome == TestOutcome.Passed) Classify(Unwrap(exception), result);
            }

            if (result.Outcome == TestOutcome.Passed)
            {
                string logProblem = UnityEngine.TestTools.LogAssert.Evaluate(logStart);
                if (logProblem != null) { result.Outcome = TestOutcome.Failed; result.Message = logProblem; }
                else if (Simulation.Exceptions.Count > exceptionsBefore)
                {
                    result.Outcome = TestOutcome.Failed;
                    result.Message = "Exception in a Unity message: " + FirstLine(Simulation.Exceptions[exceptionsBefore]);
                }
            }

            try { Simulation.ExitPlayMode(); } catch (Exception) { }
            Time.timeScale = 1f;
            LogCapture.Phase = previousPhase;
            LogCapture.Echo = previousEcho;
            result.Seconds = watch.Elapsed.TotalSeconds;
            return result;
        }

        private static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException && exception.InnerException != null) exception = exception.InnerException;
            return exception;
        }

        private static void Classify(Exception exception, TestResult result)
        {
            if (exception is NUnit.Framework.IgnoreException || exception is NUnit.Framework.InconclusiveException)
            {
                result.Outcome = TestOutcome.Skipped; result.Message = exception.Message;
            }
            else if (exception is NUnit.Framework.SuccessException)
            {
                result.Outcome = TestOutcome.Passed;
            }
            else if (exception is NUnit.Framework.AssertionException)
            {
                result.Outcome = TestOutcome.Failed; result.Message = exception.Message.Trim();
            }
            else
            {
                result.Outcome = TestOutcome.Failed;
                result.Message = $"{exception.GetType().Name}: {exception.Message} @ {FirstLine(exception.StackTrace ?? "").Trim()}";
            }
        }

        private static string FirstLine(string text)
        {
            int index = text.IndexOf('\n');
            return index >= 0 ? text.Substring(0, index) : text;
        }

        private static void InvokeAll(object fixture, Type type, Type attribute)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.GetCustomAttribute(attribute) == null) continue;
                object returned = method.Invoke(fixture, null);
                if (returned is IEnumerator routine) Drive(routine);
            }
        }

        private static void StepFrame(float deltaTime)
        {
            if (++frames > MaxFramesPerTest) throw new NUnit.Framework.AssertionException($"Test exceeded {MaxFramesPerTest} simulated frames (endless loop?).");
            if (watch.Elapsed.TotalSeconds > MaxWallClockSecondsPerTest) throw new NUnit.Framework.AssertionException($"Test exceeded {MaxWallClockSecondsPerTest:0} s of wall-clock time.");
            Simulation.Step(deltaTime);
        }

        /// <summary>Runs a test coroutine, stepping the simulation for every yield like the Unity test runner.</summary>
        public static void Drive(IEnumerator routine)
        {
            const float frame = 1f / 60f;
            while (routine.MoveNext())
            {
                object current = routine.Current;
                if (current is IEnumerator nested)
                {
                    Drive(nested);
                }
                else if (current is WaitForSeconds wait)
                {
                    float target = Time.time + wait.seconds;
                    do { StepFrame(frame); } while (Time.time < target - 1e-5f && Time.timeScale > 0f);
                }
                else if (current is WaitForFixedUpdate)
                {
                    StepFrame(Simulation.FixedDeltaTime);
                }
                else if (current is CustomYieldInstruction custom)
                {
                    do { StepFrame(frame); } while (custom.keepWaiting);
                }
                else
                {
                    StepFrame(frame); // null, WaitForEndOfFrame, AsyncOperation, anything else: next frame
                }
            }
        }
    }
}
