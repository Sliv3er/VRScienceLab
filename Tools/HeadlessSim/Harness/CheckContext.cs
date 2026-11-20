// HeadlessSim harness: helper passed to ProjectChecks. Not part of the Unity project.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HeadlessSim
{
    /// <summary>What a project check can do with the running, simulated scene.</summary>
    public sealed class CheckContext
    {
        public int Passed { get; private set; }
        public int Failed { get; private set; }
        public readonly List<string> Failures = new List<string>();

        /// <summary>Records one check; prints PASS / FAIL. Returns the condition.</summary>
        public bool Check(bool condition, string message)
        {
            if (condition) { Passed++; Console.WriteLine("    PASS  " + message); }
            else { Failed++; Failures.Add(message); Console.WriteLine("    FAIL  " + message); }
            return condition;
        }

        public void Section(string title) { Console.WriteLine(); Console.WriteLine("  -- " + title); }
        public void Info(string message) { Console.WriteLine("    info  " + message); }

        /// <summary>First component of type T in the scene (inactive objects included), or null.</summary>
        public T Find<T>() where T : class => Simulation.FindAll<T>(true).FirstOrDefault();
        public List<T> FindAll<T>(bool includeInactive = true) where T : class => Simulation.FindAll<T>(includeInactive);

        /// <summary>First GameObject with that name (inactive objects included), or null.</summary>
        public GameObject FindObject(string name) => Simulation.AllSceneObjects(true).FirstOrDefault(g => g.name == name);
        public List<GameObject> FindObjects(string name) => Simulation.AllSceneObjects(true).Where(g => g.name == name).ToList();

        /// <summary>Advances the simulation by <paramref name="seconds"/> of game time (60 frames per second).</summary>
        public void Step(float seconds = 1f / 60f) { Simulation.RunFor(seconds); }
        public void StepFrames(int frames, float deltaTime = 1f / 60f) { for (int i = 0; i < frames; i++) Simulation.Step(deltaTime); }

        /// <summary>Steps until <paramref name="condition"/> is true or <paramref name="timeoutSeconds"/> of game time elapsed.</summary>
        public bool StepUntil(Func<bool> condition, float timeoutSeconds)
        {
            float end = Time.time + timeoutSeconds;
            while (Time.time < end)
            {
                if (condition()) return true;
                Simulation.Step(1f / 60f);
            }
            return condition();
        }

        /// <summary>UI click through the EventSystem path (ExecuteEvents + IPointerClickHandler).</summary>
        public bool Click(UnityEngine.UI.Button button) => Simulation.Click(button);

        /// <summary>Clicks the first UI button whose GameObject has that name.</summary>
        public bool Click(string buttonObjectName)
        {
            UnityEngine.UI.Button button = Simulation.FindAll<UnityEngine.UI.Button>(true).FirstOrDefault(b => b.name == buttonObjectName);
            if (button == null) { Console.WriteLine($"    info  no UI button named '{buttonObjectName}'"); return false; }
            return Simulation.Click(button);
        }

        /// <summary>Errors, exceptions and warnings logged since <paramref name="fromIndex"/> (LogCapture.Entries.Count taken earlier).</summary>
        public List<LogEntry> LogsSince(int fromIndex, params LogKind[] kinds) => LogCapture.Since(fromIndex, kinds).ToList();
        public int LogMark => LogCapture.Entries.Count;
    }
}
