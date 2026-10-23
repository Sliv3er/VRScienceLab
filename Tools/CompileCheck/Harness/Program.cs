// ---------------------------------------------------------------------------
// Local test harness: runs the real Module2InstrumentTests methods against the
// real instrument scripts, using the stub UnityEngine (no editor required).
// Tests that need Unity's physics engine or real frame timing are skipped and
// must be run inside Unity's Test Runner.
// ---------------------------------------------------------------------------
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VRScienceLab.Module2.Tests;

internal static class Program
{
    private static readonly string[] RequiresUnityRuntime =
    {
        "DigitalScale_DetectsObjectThroughPhysicsTrigger", // needs PhysX trigger events
        "Stopwatch_AccumulatesFrameTimeWhileRunning",      // needs real Update() frames
    };

    private static int Main()
    {
        int passed = 0, failed = 0, skipped = 0;
        var suiteType = typeof(Module2InstrumentTests);
        MethodInfo tearDown = suiteType.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<TearDownAttribute>() != null);

        foreach (MethodInfo method in suiteType.GetMethods().OrderBy(m => m.Name))
        {
            bool isTest = method.GetCustomAttribute<TestAttribute>() != null;
            bool isUnityTest = method.GetCustomAttribute<UnityTestAttribute>() != null;
            if (!isTest && !isUnityTest) continue;

            if (RequiresUnityRuntime.Contains(method.Name))
            {
                Console.WriteLine($"SKIP  {method.Name} (requires the Unity runtime)");
                skipped++;
                continue;
            }

            object suite = Activator.CreateInstance(suiteType);
            Time.time = 0f;
            try
            {
                object result = method.Invoke(suite, null);
                if (result is IEnumerator routine)
                {
                    while (routine.MoveNext())
                    {
                        // Simulate the passage of time for WaitForSeconds; other yields are single frames.
                        if (routine.Current is WaitForSeconds wait) Time.time += wait.seconds;
                        else Time.time += Time.deltaTime;
                    }
                }

                Console.WriteLine($"PASS  {method.Name}");
                passed++;
            }
            catch (TargetInvocationException ex)
            {
                Console.WriteLine($"FAIL  {method.Name}: {ex.InnerException?.Message}");
                failed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL  {method.Name}: {ex.Message}");
                failed++;
            }
            finally
            {
                tearDown?.Invoke(suite, null);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"Passed: {passed}  Failed: {failed}  Skipped: {skipped}  Debug.LogError calls: {Debug.ErrorCount}");
        return failed == 0 ? 0 : 1;
    }
}
