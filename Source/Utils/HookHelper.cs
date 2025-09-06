using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;
using Celeste.Mod.Backdrops;
using Celeste.Mod.Entities;
using MonoMod.Cil;
using System.Reflection;
using MonoMod.RuntimeDetour;
using Celeste.Mod.Helpers.LegacyMonoMod;
using MonoMod.Utils;

namespace Celeste.Mod.FunctionalZoomOut.Utils;

// this whole thing is still kinda just a janky messy and im probably just going to use The Normal Way but for now this is nice for prototyping i think
internal static class HookHelper {
    private static bool Initialized;

    internal static void Initialize(Assembly assembly) {
        if (Initialized)
            return;

        Logger.Info("ZoomOutHelperPrototype", "[HookHelper] initializing OnHook, ILHook, HookLoadCallback, and HookUnloadCallback attributes...");

        // get all types in the specified assembly
        foreach (var type in assembly.GetTypesSafe()) {

            // get all static methods in each type
            foreach (var methodInfo in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)) {

                // initialize all onhook attributes on each static method
                foreach (var attribute in methodInfo.GetCustomAttributes<OnHookAttribute>()) {
                    if (!TryGetHookAttributeTarget(attribute, out var targetMethod))
                        continue;

                    Get(attribute.Tag).AddAttribute(new(IsILHook: false, targetMethod, methodInfo));
                }

                // initialize all ilhook attributes on each static method
                foreach (var attribute in methodInfo.GetCustomAttributes<ILHookAttribute>()) {

                    // bail if method doesn't have the correct parameters
                    if (methodInfo.ReturnType != typeof(void) || methodInfo.GetParameters().Length != 1 || methodInfo.GetParameters()[0].ParameterType != typeof(ILContext)) {
                        Logger.Warn("ZoomOutHelperPrototype", $"[HookHelper] invalid method signature for IL hook {methodInfo.DeclaringType.Name}.{methodInfo.Name}!");
                        break;
                    }

                    if (!TryGetHookAttributeTarget(attribute, out var targetMethod))
                        continue;

                    Get(attribute.Tag).AddAttribute(new(IsILHook: true, targetMethod, methodInfo));
                }

                // initialize all hookloadcallback attributes on each static method
                foreach (var attribute in methodInfo.GetCustomAttributes<HookLoadCallbackAttribute>()) {
                    if (methodInfo.ReturnType != typeof(void) || methodInfo.GetParameters().Length != 0) {
                        Logger.Warn("ZoomOutHelperPrototype", $"[HookHelper] invalid method signature for hook load callback {methodInfo.DeclaringType.Name}.{methodInfo.Name}!");
                        break;
                    }

                    Get(attribute.Tag).AddLoadCallback(methodInfo.CreateDelegate<Action>());
                }

                // initialize all hookunloadcallback attributes on each static method
                foreach (var attribute in methodInfo.GetCustomAttributes<HookUnloadCallbackAttribute>()) {
                    if (methodInfo.ReturnType != typeof(void) || methodInfo.GetParameters().Length != 0) {
                        Logger.Warn("ZoomOutHelperPrototype", $"[HookHelper] invalid method signature for hook unload callback {methodInfo.DeclaringType.Name}.{methodInfo.Name}!");
                        break;
                    }

                    Get(attribute.Tag).AddUnloadCallback(methodInfo.CreateDelegate<Action>());
                }
            }
        }

        Initialized = true;
    }

    private static HookTag Get(string tag) {
        if (hookTags.TryGetValue(tag, out var hookTag))
            return hookTag;

        return hookTags[tag] = new();
    }

    private static bool TryGetHookAttributeTarget(HookBaseAttribute attribute, out MethodBase target) {
        // try to get a reference to the target method
        switch (attribute.HookType) {
            case HookTypes.Method:
                if (attribute.TargetParameters is null)
                    target = attribute.TargetType.GetMethod(attribute.TargetMemberName, attribute.BindingFlags);
                else
                    target = attribute.TargetType.GetMethod(attribute.TargetMemberName, attribute.BindingFlags, attribute.TargetParameters);

                break;
            case HookTypes.GetProperty:
                target = attribute.TargetType.GetMethod("get_" + attribute.TargetMemberName, attribute.BindingFlags);
                break;
            case HookTypes.SetProperty:
                target = attribute.TargetType.GetMethod("set_" + attribute.TargetMemberName, attribute.BindingFlags);
                break;
            case HookTypes.IEnumerator:
                if (attribute.TargetParameters is null)
                    target = attribute.TargetType.GetMethod(attribute.TargetMemberName, attribute.BindingFlags);
                else
                    target = attribute.TargetType.GetMethod(attribute.TargetMemberName, attribute.BindingFlags, attribute.TargetParameters);

                target = (target as MethodInfo).GetStateMachineTarget();
                break;
            case HookTypes.Constructor:
                if (attribute.TargetParameters is null)
                    throw new Exception("[Hook Helper] cannot hook a constructor if no parameters are specified!");
                else
                    target = attribute.TargetType.GetConstructor(attribute.BindingFlags, attribute.TargetParameters);

                break;
            default:
                throw new Exception("[Hook Helper] hook type not supported!");
        }

        if (target is null) {
            Logger.Warn("ZoomOutHelperPrototype", $"[HookHelper] couldn't find target {attribute.TargetMemberName} in type {attribute.TargetType.FullName} using hook type {attribute.HookType}!");
            return false;
        }

        return true;
    }

    internal static void Uninitialize() {
        if (!Initialized)
            return;

        foreach (var tag in hookTags.Keys)
            UnloadTag(tag);

        foreach (var tag in hookTags.Values)
            tag.Clear();
        hookTags.Clear();

        Initialized = false;
    }

    private static readonly Dictionary<string, HookTag> hookTags = [];

    internal static void LoadTag(string tag = "defaultTag") {
        if (!Initialized || !hookTags.TryGetValue(tag, out HookTag hookTag) || hookTag.Loaded) {
            Logger.Warn("ZoomOutHelperPrototype", $"could not load tag \"{tag}\"! (hookhelper has not been initialized, the hooktag does not exist, or the hooktag is already loaded.)");
            return;
        }

        hookTag.Load();
        Logger.Info("ZoomOutHelperPrototype", $"[HookHelper] loaded hooktag \"{tag}\"!");
    }

    internal static void UnloadTag(string tag = "defaultTag") {
        if (!Initialized || !hookTags.TryGetValue(tag, out HookTag hookTag) || !hookTag.Loaded)
            return;

        hookTag.Unload();
        Logger.Info("ZoomOutHelperPrototype", $"[HookHelper] unloaded hooktag \"{tag}\"!");
    }

    private record HookAttributeInfo(bool IsILHook, MethodBase TargetMethod, MethodInfo HookMethod);

    private class HookTag {
        private readonly HashSet<Action> loadCallbacks = [];
        private readonly HashSet<Action> unloadCallbacks = [];

        private readonly HashSet<Hook> attributeOnHooks = [];
        private readonly HashSet<ILHook> attributeILHooks = [];
        private readonly HashSet<HookAttributeInfo> hookAttributes = [];

        public bool Loaded { get; private set; }

        public void Load() {
            Unload();

            try {
                foreach (var loadCallback in loadCallbacks) {
                    loadCallback.Invoke();

                    Logger.Info("ZoomOutHelperPrototype", $"[HookHelper] ran hook loading callback {loadCallback.Method.Name}!");
                }

                foreach (var hookAttribute in hookAttributes) {
                    if (hookAttribute.IsILHook)
                        attributeILHooks.Add(new(hookAttribute.TargetMethod, hookAttribute.HookMethod.CreateDelegate<ILContext.Manipulator>()));
                    else
                        attributeOnHooks.Add(new(hookAttribute.TargetMethod, hookAttribute.HookMethod));

                    Logger.Info("ZoomOutHelperPrototype", $"[HookHelper] loaded hook {hookAttribute.HookMethod.DeclaringType.Name}.{hookAttribute.HookMethod.Name} for {hookAttribute.TargetMethod.DeclaringType.Name}.{hookAttribute.TargetMethod.Name}!");
                }

                Loaded = true;
            } catch {
                Unload();
                throw;
            }
        }

        public void Unload() {
            foreach (var ilHook in attributeILHooks)
                ilHook?.Dispose();
            attributeILHooks.Clear();

            foreach (var onHook in attributeOnHooks)
                onHook?.Dispose();
            attributeOnHooks.Clear();

            foreach (var unloadCallback in unloadCallbacks) {
                unloadCallback.Invoke();

                Logger.Info("ZoomOutHelperPrototype", $"[HookHelper] ran hook unloading callback {unloadCallback.Method.Name}!");
            }

            Loaded = false;
        }

        public void AddAttribute(HookAttributeInfo attributeInfo) =>
            hookAttributes.Add(attributeInfo);

        public void AddLoadCallback(Action callback) =>
            loadCallbacks.Add(callback);

        public void AddUnloadCallback(Action callback) =>
            unloadCallbacks.Add(callback);

        public void Clear() {
            Unload();

            loadCallbacks.Clear();
            unloadCallbacks.Clear();
            hookAttributes.Clear();
        }
    }
}

internal enum HookTypes { Method, GetProperty, SetProperty, Constructor, IEnumerator }

/// <summary>
/// the base for both the ILHook and OnHook attributes, not checked for itself, please use those instead.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal class HookBaseAttribute : Attribute {
    internal readonly Type TargetType;
    internal readonly string TargetMemberName;
    internal readonly BindingFlags BindingFlags;
    internal readonly HookTypes HookType;
    internal readonly Type[] TargetParameters;

    internal readonly string Tag;

    internal HookBaseAttribute(Type targetType, string targetMemberName, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, HookTypes hookType = HookTypes.Method, Type[] parameters = null, string tag = "defaultTag") : base() {
        TargetType = targetType;
        TargetMemberName = targetMemberName;
        BindingFlags = bindingFlags;
        HookType = hookType;
        TargetParameters = parameters;
        Tag = tag;
    }
}

/// <summary>
/// the base for both the HookLoadCallback and HookUnloadCallback attributes, not checked for itself, please use those instead.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
internal class HookCallbackBaseAttribute : Attribute {
    internal readonly string Tag;

    internal HookCallbackBaseAttribute(string tag = "defaultTag") : base() {
        Tag = tag;
    }
}

/// <summary>
/// applies the affected method as an ILHook to a target member in the specifed type.<br/>
/// the target defaults to a regular either public or private instance method, but the target can be adjusted through optional parameters.
/// </summary>
internal class ILHookAttribute : HookBaseAttribute {
    internal ILHookAttribute(Type targetType, string targetMemberName, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, HookTypes hookType = HookTypes.Method, Type[] parameters = null, string tag = "defaultTag")
                      : base(targetType, targetMemberName, bindingFlags, hookType, parameters, tag) { }
}

/// <summary>
/// applies the affected method as a Hook to a target member in the specifed type.<br/>
/// the target defaults to a regular either public or private instance method, but the target can be adjusted through optional parameters.
/// </summary>
internal class OnHookAttribute : HookBaseAttribute {
    internal OnHookAttribute(Type targetType, string targetMemberName, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, HookTypes hookType = HookTypes.Method, Type[] parameters = null, string tag = "defaultTag")
                      : base(targetType, targetMemberName, bindingFlags, hookType, parameters, tag) { }
}

/// <summary>
/// marks a method to be run when loading a set of hooks.<br/>
/// (runs before any hook attributes are loaded)
/// </summary>
internal class HookLoadCallbackAttribute : HookCallbackBaseAttribute {
    internal HookLoadCallbackAttribute(string tag = "defaultTag") : base(tag) { }
}

/// <summary>
/// marks a method to be run when unloading a set of hooks.<br/>
/// (runs after all hook attributes are unloaded)
/// </summary>
internal class HookUnloadCallbackAttribute : HookCallbackBaseAttribute {
    internal HookUnloadCallbackAttribute(string tag = "defaultTag") : base(tag) { }
}
