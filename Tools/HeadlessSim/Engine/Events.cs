// HeadlessSim: UnityEvent with runtime and persistent (Inspector-style) listeners.
// Not part of the Unity project.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HeadlessSim;

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T0>(T0 arg0);
    public delegate void UnityAction<T0, T1>(T0 arg0, T1 arg1);
    public delegate void UnityAction<T0, T1, T2>(T0 arg0, T1 arg1, T2 arg2);

    public enum UnityEventCallState { Off = 0, EditorAndRuntime = 1, RuntimeOnly = 2 }
    public enum PersistentListenerMode { EventDefined, Void, Object, Int, Float, String, Bool }

    /// <summary>One listener stored in the event itself (what the Inspector shows under "On Click ()").</summary>
    public sealed class PersistentCall
    {
        public Object target;
        public string methodName;
        public PersistentListenerMode mode;
        public object argument;
        public UnityEventCallState callState = UnityEventCallState.RuntimeOnly;
    }

    public abstract class UnityEventBase
    {
        internal readonly List<PersistentCall> persistentCalls = new List<PersistentCall>();

        public int GetPersistentEventCount() => persistentCalls.Count;
        public Object GetPersistentTarget(int index) => index >= 0 && index < persistentCalls.Count ? persistentCalls[index].target : null;
        public string GetPersistentMethodName(int index) => index >= 0 && index < persistentCalls.Count ? persistentCalls[index].methodName : string.Empty;
        public UnityEventCallState GetPersistentListenerState(int index) => persistentCalls[index].callState;
        public void SetPersistentListenerState(int index, UnityEventCallState state) { persistentCalls[index].callState = state; }
        public void SetPersistentListenerState(UnityEventCallState state) { foreach (PersistentCall call in persistentCalls) call.callState = state; }
        /// <summary>Simulator helper: the stored listeners (target, method, argument).</summary>
        public IReadOnlyList<PersistentCall> SimPersistentCalls => persistentCalls;

        public abstract void RemoveAllListeners();
        internal abstract Type[] ArgumentTypes { get; }

        internal void AddPersistent(Delegate call, PersistentListenerMode mode, object argument)
        {
            if (call == null) throw new ArgumentNullException(nameof(call));
            var target = call.Target as Object;
            if (ReferenceEquals(target, null))
            {
                Debug.LogError($"Could not register persistent listener '{call.Method.Name}': the method must belong to a UnityEngine.Object instance (got {(call.Target == null ? "a static method" : call.Target.GetType().Name)}).");
                return;
            }
            if (call.Method.Name.StartsWith("<"))
            {
                Debug.LogError("Could not register persistent listener: lambdas and anonymous methods cannot be serialized.");
                return;
            }
            persistentCalls.Add(new PersistentCall { target = target, methodName = call.Method.Name, mode = mode, argument = argument });
        }

        internal void RemovePersistent(int index) { if (index >= 0 && index < persistentCalls.Count) persistentCalls.RemoveAt(index); }

        internal void InvokePersistent(object[] eventArguments)
        {
            if (persistentCalls.Count == 0) return;
            foreach (PersistentCall call in persistentCalls.ToArray())
            {
                if (call.callState == UnityEventCallState.Off) continue;
                if (call.callState == UnityEventCallState.RuntimeOnly && !Application.isPlaying) continue;
                if (call.target == null) continue; // missing (destroyed) target: Unity skips it silently

                object[] arguments;
                Type[] signature;
                switch (call.mode)
                {
                    case PersistentListenerMode.EventDefined: arguments = eventArguments; signature = ArgumentTypes; break;
                    case PersistentListenerMode.Void: arguments = new object[0]; signature = Type.EmptyTypes; break;
                    case PersistentListenerMode.Int: arguments = new[] { call.argument }; signature = new[] { typeof(int) }; break;
                    case PersistentListenerMode.Float: arguments = new[] { call.argument }; signature = new[] { typeof(float) }; break;
                    case PersistentListenerMode.String: arguments = new[] { call.argument }; signature = new[] { typeof(string) }; break;
                    case PersistentListenerMode.Bool: arguments = new[] { call.argument }; signature = new[] { typeof(bool) }; break;
                    default: arguments = new[] { call.argument }; signature = null; break;
                }

                MethodInfo method = FindMethod(call.target.GetType(), call.methodName, signature, arguments);
                if (method == null)
                {
                    Debug.LogError($"UnityEvent: method '{call.methodName}' with a matching signature was not found on {call.target.GetType().Name} (broken persistent listener).");
                    continue;
                }

                try { method.Invoke(call.target, arguments); }
                catch (TargetInvocationException exception) { Simulation.ReportException(exception.InnerException ?? exception, call.target as Component, call.methodName); }
            }
        }

        private static MethodInfo FindMethod(Type type, string name, Type[] signature, object[] arguments)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (MethodInfo candidate in t.GetMethods(flags | BindingFlags.DeclaredOnly))
                {
                    if (candidate.Name != name) continue;
                    ParameterInfo[] parameters = candidate.GetParameters();
                    if (signature != null)
                    {
                        if (parameters.Length != signature.Length) continue;
                        bool match = true;
                        for (int i = 0; i < parameters.Length; i++)
                        {
                            if (!parameters[i].ParameterType.IsAssignableFrom(signature[i])) { match = false; break; }
                        }
                        if (match) return candidate;
                    }
                    else if (parameters.Length == 1 && (arguments[0] == null || parameters[0].ParameterType.IsInstanceOfType(arguments[0])))
                    {
                        return candidate;
                    }
                }
            }
            return null;
        }

        internal UnityEventBase CloneForInstantiate(Dictionary<Object, Object> map)
        {
            var clone = (UnityEventBase)Activator.CreateInstance(GetType(), true);
            foreach (PersistentCall call in persistentCalls)
            {
                Object target = call.target;
                if (map != null && !ReferenceEquals(target, null) && map.TryGetValue(target, out Object mapped)) target = mapped;
                object argument = call.argument;
                if (map != null && argument is Object objectArgument && map.TryGetValue(objectArgument, out Object mappedArgument)) argument = mappedArgument;
                clone.persistentCalls.Add(new PersistentCall { target = target, methodName = call.methodName, mode = call.mode, argument = argument, callState = call.callState });
            }
            return clone;
        }
    }

    [Serializable]
    public class UnityEvent : UnityEventBase
    {
        private readonly List<UnityAction> listeners = new List<UnityAction>();
        internal override Type[] ArgumentTypes => Type.EmptyTypes;
        public void AddListener(UnityAction call) { if (call != null) listeners.Add(call); }
        public void RemoveListener(UnityAction call) { int index = listeners.LastIndexOf(call); if (index >= 0) listeners.RemoveAt(index); }
        public override void RemoveAllListeners() { listeners.Clear(); }
        public void Invoke()
        {
            InvokePersistent(new object[0]);
            foreach (UnityAction listener in listeners.ToArray()) listener();
        }
        /// <summary>Simulator helper: number of listeners added with AddListener.</summary>
        public int SimRuntimeListenerCount => listeners.Count;
    }

    [Serializable]
    public class UnityEvent<T0> : UnityEventBase
    {
        private readonly List<UnityAction<T0>> listeners = new List<UnityAction<T0>>();
        internal override Type[] ArgumentTypes => new[] { typeof(T0) };
        public void AddListener(UnityAction<T0> call) { if (call != null) listeners.Add(call); }
        public void RemoveListener(UnityAction<T0> call) { int index = listeners.LastIndexOf(call); if (index >= 0) listeners.RemoveAt(index); }
        public override void RemoveAllListeners() { listeners.Clear(); }
        public void Invoke(T0 arg0)
        {
            InvokePersistent(new object[] { arg0 });
            foreach (UnityAction<T0> listener in listeners.ToArray()) listener(arg0);
        }
        public int SimRuntimeListenerCount => listeners.Count;
    }

    [Serializable]
    public class UnityEvent<T0, T1> : UnityEventBase
    {
        private readonly List<UnityAction<T0, T1>> listeners = new List<UnityAction<T0, T1>>();
        internal override Type[] ArgumentTypes => new[] { typeof(T0), typeof(T1) };
        public void AddListener(UnityAction<T0, T1> call) { if (call != null) listeners.Add(call); }
        public void RemoveListener(UnityAction<T0, T1> call) { int index = listeners.LastIndexOf(call); if (index >= 0) listeners.RemoveAt(index); }
        public override void RemoveAllListeners() { listeners.Clear(); }
        public void Invoke(T0 arg0, T1 arg1)
        {
            InvokePersistent(new object[] { arg0, arg1 });
            foreach (UnityAction<T0, T1> listener in listeners.ToArray()) listener(arg0, arg1);
        }
        public int SimRuntimeListenerCount => listeners.Count;
    }
}
