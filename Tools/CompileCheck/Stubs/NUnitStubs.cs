// ---------------------------------------------------------------------------
// Minimal NUnit stand-ins so the Module 2 PlayMode test file compiles and runs
// in the local harness. Compile-check only; never imported by Unity.
// ---------------------------------------------------------------------------
using System;

namespace NUnit.Framework
{
    [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public class TearDownAttribute : Attribute { }

    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public static class Assert
    {
        private static void Fail(string message) => throw new AssertionException(message);

        public static void AreEqual(float expected, float actual, float delta, string message = null)
        {
            if (Math.Abs(expected - actual) > delta) Fail($"{message} Expected {expected} ± {delta} but was {actual}.");
        }

        public static void AreEqual(double expected, double actual, double delta, string message = null)
        {
            if (Math.Abs(expected - actual) > delta) Fail($"{message} Expected {expected} ± {delta} but was {actual}.");
        }

        public static void AreEqual(object expected, object actual, string message = null)
        {
            if (!Equals(expected, actual)) Fail($"{message} Expected '{expected}' but was '{actual}'.");
        }

        public static void IsTrue(bool condition, string message = null) { if (!condition) Fail($"{message} Expected true."); }
        public static void IsFalse(bool condition, string message = null) { if (condition) Fail($"{message} Expected false."); }
        public static void IsNotNull(object value, string message = null) { if (value == null) Fail($"{message} Expected not null."); }
        public static void Greater(float a, float b, string message = null) { if (!(a > b)) Fail($"{message} Expected {a} > {b}."); }
        public static void LessOrEqual(float a, float b, string message = null) { if (!(a <= b)) Fail($"{message} Expected {a} <= {b}."); }
    }
}
