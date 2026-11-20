// HeadlessSim harness: builds the lab scene with the project's own editor builder, plays it,
// runs the project checks and the PlayMode tests, all on the simulated engine.
// Not part of the Unity project.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HeadlessSim;
using UnityEngine;

internal static class Program
{
    private const string BuilderType = "VRScienceLab.Module2.EditorTools.Module2SceneBuilder";
    private const string BuilderMethod = "BuildLabScene";

    private static int Main(string[] args)
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (Exception) { }

        bool skipTests = args.Contains("--skip-tests");
        bool skipBuild = args.Contains("--skip-build");
        bool starterAssets = args.Contains("--starter-assets");
        LogCapture.Verbose = args.Contains("--verbose");
        string testFilter = ValueOf(args, "--tests");
        string workingDirectory = ValueOf(args, "--workdir") ?? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "project");

        // The project code writes generated PNG files below "Assets/..." relative to the current
        // directory; run inside a scratch folder so the repository is never touched.
        Directory.CreateDirectory(workingDirectory);
        Environment.CurrentDirectory = workingDirectory;

        Console.WriteLine("==================================================================");
        Console.WriteLine(" HeadlessSim - simulated Unity run of VR Science Lab / Module 2");
        Console.WriteLine("==================================================================");
        Console.WriteLine($" working directory: {workingDirectory}");

        var failures = new List<string>();
        var context = new CheckContext();

        if (!args.Contains("--no-self-test")) RunSelfTests(failures);

        if (!skipBuild)
        {
            if (starterAssets) UnityEditor.EditorSim.RegisterStarterAssetsStandIn();
            bool built = BuildScene(failures);
            PrintSceneSummary();
            if (args.Contains("--navmesh-map")) PrintNavMeshMap(ValueOf(args, "--mark"));
            if (built)
            {
                PlayScene(failures);
                RunProjectChecks(context, failures);
            }
        }

        List<TestResult> results = null;
        if (!skipTests) results = RunTests(testFilter, failures);

        // ---------------------------------------------------------------- Summary
        Console.WriteLine();
        Console.WriteLine("==================================================================");
        Console.WriteLine(" SUMMARY");
        Console.WriteLine("==================================================================");
        Console.WriteLine($" project checks : {context.Passed} passed, {context.Failed} failed");
        if (results != null)
        {
            Console.WriteLine($" tests          : {results.Count(r => r.Outcome == TestOutcome.Passed)} passed, " +
                              $"{results.Count(r => r.Outcome == TestOutcome.Failed)} failed, {results.Count(r => r.Outcome == TestOutcome.Skipped)} skipped");
        }
        if (failures.Count == 0)
        {
            Console.WriteLine(" RESULT         : OK");
            return 0;
        }

        Console.WriteLine($" RESULT         : {failures.Count} FAILURE(S)");
        foreach (string failure in failures) Console.WriteLine("   - " + failure);
        return 1;
    }

    private static string ValueOf(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static Type FindType(string fullName)
    {
        return Assembly.GetExecutingAssembly().GetType(fullName, false);
    }

    // ------------------------------------------------------------------ 0. The simulator checks itself
    private static void RunSelfTests(List<string> failures)
    {
        Console.WriteLine();
        Console.WriteLine("[0] Simulator self-tests (engine stand-in, independent of the project)");
        LogCapture.Phase = "self-test";
        List<string> problems = HeadlessSim.SelfTest.EngineSelfTests.Run(out int passed);
        Console.WriteLine($"    {passed} passed, {problems.Count} failed");
        foreach (string problem in problems) failures.Add("Simulator self-test failed: " + FirstLine(problem));
        UnityEngine.SceneManagement.SceneManager.SavedScenes.Clear();
        UnityEngine.SceneManagement.SceneManager.activeScenePath = "";
        LogCapture.Entries.Clear();
        Simulation.Exceptions.Clear();
    }

    // ------------------------------------------------------------------ 1. Edit mode: build the scene
    private static bool BuildScene(List<string> failures)
    {
        Console.WriteLine();
        Console.WriteLine($"[1] Edit mode: {BuilderType}.{BuilderMethod}()");
        LogCapture.Phase = "build";
        int logStart = LogCapture.Entries.Count;

        Type builder = FindType(BuilderType);
        MethodInfo method = builder?.GetMethod(BuilderMethod, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            failures.Add($"Scene builder {BuilderType}.{BuilderMethod} not found in the compiled project.");
            Console.WriteLine("    builder not found");
            return false;
        }

        bool completed = true;
        try
        {
            method.Invoke(null, null);
        }
        catch (TargetInvocationException exception)
        {
            completed = false;
            Exception inner = exception.InnerException ?? exception;
            failures.Add($"Scene builder threw {inner.GetType().Name}: {inner.Message}");
            Console.WriteLine($"    EXCEPTION {inner.GetType().Name}: {inner.Message}");
            Console.WriteLine(Indent(inner.StackTrace, "      "));
        }

        int errors = LogCapture.Count(LogKind.Error, logStart) + LogCapture.Count(LogKind.Exception, logStart);
        int warnings = LogCapture.Count(LogKind.Warning, logStart);
        Console.WriteLine($"    builder {(completed ? "completed" : "ABORTED")}: {errors} error(s), {warnings} warning(s), {LogCapture.Count(LogKind.Log, logStart)} log message(s)");
        foreach (LogEntry entry in LogCapture.Since(logStart, LogKind.Error, LogKind.Exception)) failures.Add("Build " + entry.Kind + ": " + FirstLine(entry.Message));

        string scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        Console.WriteLine($"    saved scene: {(string.IsNullOrEmpty(scenePath) ? "(none)" : scenePath)}, assets in memory: {UnityEditor.AssetDatabase.SimAssetCount}");
        if (completed && string.IsNullOrEmpty(scenePath))
        {
            failures.Add("The builder finished without saving a scene.");
            return false;
        }
        return completed;
    }

    // ------------------------------------------------------------------ 2. Summary of the built scene
    private static void PrintSceneSummary()
    {
        Console.WriteLine();
        Console.WriteLine("[2] Scene summary");
        var all = Simulation.AllSceneObjects(true).ToList();
        Console.WriteLine($"    {Simulation.RootObjects.Count} root objects, {all.Count} GameObjects in total");
        foreach (GameObject root in Simulation.RootObjects)
        {
            int descendants = root.GetComponentsInChildren<Transform>(true).Length - 1;
            string components = string.Join(", ", root.GetComponents<Component>().Where(c => !(c is Transform)).Select(c => c.GetType().Name));
            Console.WriteLine($"      {root.name,-42} {descendants,5} descendants{(root.activeSelf ? "" : " (inactive)")}{(components.Length > 0 ? "  [" + components + "]" : "")}");
        }

        var histogram = new Dictionary<string, int>();
        foreach (GameObject go in all)
        {
            foreach (Component component in go.GetComponents<Component>())
            {
                string name = component.GetType().Name;
                histogram[name] = histogram.TryGetValue(name, out int count) ? count + 1 : 1;
            }
        }

        Console.WriteLine("    components:");
        var line = new System.Text.StringBuilder("      ");
        foreach (KeyValuePair<string, int> pair in histogram.OrderByDescending(p => p.Value).ThenBy(p => p.Key))
        {
            string item = $"{pair.Key} x{pair.Value}";
            if (line.Length + item.Length > 108) { Console.WriteLine(line.ToString().TrimEnd(' ', ',')); line = new System.Text.StringBuilder("      "); }
            line.Append(item).Append(", ");
        }
        if (line.Length > 6) Console.WriteLine(line.ToString().TrimEnd(' ', ','));

        Console.WriteLine("    NavMesh: " + UnityEngine.AI.NavMeshWorld.Describe());
    }

    /// <summary>
    /// ASCII picture of the baked NavMesh (10 cm per character, north = +Z at the top):
    /// '.' walkable, '#' not walkable, 'o' carved by a NavMeshObstacle, 'A' NavMeshAgent,
    /// letters = children of the object named by --mark (for example "Robot Stations").
    /// </summary>
    private static void PrintNavMeshMap(string markParent)
    {
        foreach (UnityEngine.AI.NavGrid grid in UnityEngine.AI.NavMeshWorld.Grids())
        {
            bool[] live = UnityEngine.AI.NavMeshWorld.SimLive(grid);
            int step = 2;
            int columns = grid.Width / step, rows = grid.Height / step;
            var map = new char[rows, columns];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    int walkable = 0, baked = 0;
                    for (int dz = 0; dz < step; dz++) for (int dx = 0; dx < step; dx++)
                    {
                        int index = grid.Index(c * step + dx, r * step + dz);
                        if (live[index]) walkable++;
                        if (grid.Walkable[index]) baked++;
                    }
                    map[r, c] = walkable == step * step ? '.' : (baked == step * step ? 'o' : (walkable > 0 ? ':' : '#'));
                }
            }

            var legend = new List<string>();
            Action<Vector3, char> mark = (position, symbol) =>
            {
                int c = grid.CellX(position.x) / step, r = grid.CellZ(position.z) / step;
                if (r >= 0 && c >= 0 && r < rows && c < columns) map[r, c] = symbol;
            };
            foreach (UnityEngine.AI.NavMeshAgent agent in Simulation.FindAll<UnityEngine.AI.NavMeshAgent>(false)) { mark(agent.transform.position, 'A'); legend.Add($"A = agent '{agent.name}' at {agent.transform.position}"); }
            if (!string.IsNullOrEmpty(markParent))
            {
                GameObject parent = Simulation.AllSceneObjects(true).FirstOrDefault(g => g.name == markParent);
                if (parent != null)
                {
                    for (int i = 0; i < parent.transform.childCount && i < 26; i++)
                    {
                        Transform child = parent.transform.GetChild(i);
                        char symbol = (char)('a' + i);
                        mark(child.position, symbol);
                        bool onMesh = UnityEngine.AI.NavMesh.SamplePosition(child.position, out UnityEngine.AI.NavMeshHit hit, 0.05f, UnityEngine.AI.NavMesh.AllAreas);
                        legend.Add($"{symbol} = '{child.name}' at {child.position} ({(onMesh ? "on the NavMesh" : "NOT on the NavMesh")})");
                    }
                }
                else legend.Add($"(no object named '{markParent}')");
            }

            Console.WriteLine($"    NavMesh map, floor y={grid.FloorY:0.00}, x {grid.OriginX:0.00}..{grid.OriginX + grid.Width * UnityEngine.AI.NavGrid.Cell:0.00}, z {grid.OriginZ:0.00}..{grid.OriginZ + grid.Height * UnityEngine.AI.NavGrid.Cell:0.00} (+Z at the top):");
            for (int r = rows - 1; r >= 0; r--)
            {
                var line = new System.Text.StringBuilder("      ");
                for (int c = 0; c < columns; c++) line.Append(map[r, c]);
                Console.WriteLine(line.ToString());
            }
            foreach (string entry in legend) Console.WriteLine("      " + entry);
        }
    }

    // ------------------------------------------------------------------ 3. Play mode
    private static void PlayScene(List<string> failures)
    {
        Console.WriteLine();
        Console.WriteLine("[3] Play mode: EnterPlayMode + 3 s");
        LogCapture.Phase = "play";
        int logStart = LogCapture.Entries.Count;
        int exceptionsBefore = Simulation.Exceptions.Count;
        try
        {
            // Unity enters play mode from the serialized scene: anything the builder left only in
            // memory (non-serialized fields, AddListener callbacks, property blocks) does not survive.
            // Reloading the saved scene reproduces that before the first Awake.
            string scenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            Console.WriteLine($"    scene reloaded from its saved (serialized) state: {Simulation.AllSceneObjects(true).Count()} GameObjects");
            Simulation.EnterPlayMode();
            Simulation.RunFor(3f);
        }
        catch (Exception exception)
        {
            failures.Add($"Play mode threw {exception.GetType().Name}: {exception.Message}");
            Console.WriteLine($"    EXCEPTION {exception.GetType().Name}: {exception.Message}");
            Console.WriteLine(Indent(exception.StackTrace, "      "));
        }

        int exceptions = Simulation.Exceptions.Count - exceptionsBefore;
        int errors = LogCapture.Count(LogKind.Error, logStart);
        Console.WriteLine($"    {Time.frameCount} frames simulated, t = {Time.time:0.00} s: {exceptions} exception(s), {errors} error(s), {LogCapture.Count(LogKind.Warning, logStart)} warning(s)");
        for (int i = exceptionsBefore; i < Simulation.Exceptions.Count; i++) failures.Add("Play exception: " + FirstLine(Simulation.Exceptions[i]));
        foreach (LogEntry entry in LogCapture.Since(logStart, LogKind.Error)) failures.Add("Play error: " + FirstLine(entry.Message));

        // Where the rigid bodies ended up (the simulator only knows vertical falls onto collider boxes).
        List<Simulation.BodyState> bodies = Simulation.BodyStates();
        Console.WriteLine($"    rigid bodies after 3 s: {bodies.Count(b => b.Rest == Simulation.BodyRest.Resting)} resting, " +
                          $"{bodies.Count(b => b.Rest == Simulation.BodyRest.Falling)} falling, {bodies.Count(b => b.Rest == Simulation.BodyRest.Embedded)} overlapping a solid collider, " +
                          $"{bodies.Count(b => b.Rest == Simulation.BodyRest.Kinematic)} kinematic, {bodies.Count(b => b.Rest == Simulation.BodyRest.Held || b.Rest == Simulation.BodyRest.NoCollider)} other");
        foreach (Simulation.BodyState body in bodies)
        {
            if (body.Rest == Simulation.BodyRest.Kinematic) continue;
            Console.WriteLine("      " + body);
        }
    }

    // ------------------------------------------------------------------ Project checks (extension point)
    private static void RunProjectChecks(CheckContext context, List<string> failures)
    {
        Console.WriteLine();
        Console.WriteLine("[3b] Project checks (Harness/ProjectChecks.cs)");
        LogCapture.Phase = "checks";
        int exceptionsBefore = Simulation.Exceptions.Count;
        try
        {
            ProjectChecks.Run(context);
        }
        catch (Exception exception)
        {
            failures.Add($"ProjectChecks threw {exception.GetType().Name}: {exception.Message}");
            Console.WriteLine($"    EXCEPTION {exception.GetType().Name}: {exception.Message}");
            Console.WriteLine(Indent(exception.StackTrace, "      "));
        }

        for (int i = exceptionsBefore; i < Simulation.Exceptions.Count; i++) failures.Add("Exception during project checks: " + FirstLine(Simulation.Exceptions[i]));
        foreach (string failure in context.Failures) failures.Add("Check failed: " + failure);
        Console.WriteLine($"    {context.Passed} passed, {context.Failed} failed");
    }

    // ------------------------------------------------------------------ 4. PlayMode tests
    private static List<TestResult> RunTests(string filter, List<string> failures)
    {
        Console.WriteLine();
        Console.WriteLine("[4] PlayMode tests on the simulator (fresh world per test)");
        LogCapture.Phase = "tests";

        // Tests that need real rigid-body dynamics (rotation, lateral pushes, friction, bouncing) would be
        // listed here: TestRunner.SkipList["TestName"] = "reason". The simulator only makes bodies fall
        // straight down onto the collider boxes below them, which is enough for the current tests.

        List<TestResult> results = TestRunner.RunAll(Assembly.GetExecutingAssembly(),
            type => type.Namespace != null && !Serialization.IsEngineType(type) && (filter == null || type.FullName.Contains(filter)));

        if (filter != null) results = results.Where(r => r.Name.Contains(filter) || true).ToList();
        foreach (TestResult result in results)
        {
            Console.WriteLine("    " + result);
            if (result.Outcome == TestOutcome.Failed) failures.Add("Test failed: " + result.Name + " -- " + result.Message);
        }

        Console.WriteLine($"    total {results.Count}: {results.Count(r => r.Outcome == TestOutcome.Passed)} passed, " +
                          $"{results.Count(r => r.Outcome == TestOutcome.Failed)} failed, {results.Count(r => r.Outcome == TestOutcome.Skipped)} skipped");
        return results;
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        int index = text.IndexOf('\n');
        return index >= 0 ? text.Substring(0, index).TrimEnd() : text;
    }

    private static string Indent(string text, string prefix)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return string.Join(Environment.NewLine, text.Split('\n').Take(12).Select(l => prefix + l.TrimEnd()));
    }
}
