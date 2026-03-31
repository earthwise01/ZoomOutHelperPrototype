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
    private static bool initialized;

    internal static void Initialize(Assembly assembly) {
        if (initialized)
            return;

        Logger.Info("ZoomOutHelperPrototype", "[HookHelper] initializing OnHook, ILHook, HookLoadCallback, and HookUnloadCallback attributes...");

        // get all types in the specified assembly
        foreach (Type type in assembly.GetTypesSafe()) {

            // get all static methods in each type
            foreach (MethodInfo methodInfo in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)) {

                // initialize all onhook attributes on each static method
                foreach (OnHookAttribute attribute in methodInfo.GetCustomAttributes<OnHookAttribute>()) {
                    if (!TryGetHookAttributeTarget(attribute, out MethodBase targetMethod))
                        continue;

                    Get(attribute.Tag).AddAttribute(new HookAttributeInfo(IsILHook: false, targetMethod, methodInfo));
                }

                // initialize all ilhook attributes on each static method
                foreach (ILHookAttribute attribute in methodInfo.GetCustomAttributes<ILHookAttribute>()) {

                    // bail if method doesn't have the correct parameters
                    if (methodInfo.ReturnType != typeof(void) || methodInfo.GetParameters().Length != 1 || methodInfo.GetParameters()[0].ParameterType != typeof(ILContext)) {
                        Logger.Warn("ZoomOutHelperPrototype", $"[HookHelper] invalid method signature for IL hook {methodInfo.DeclaringType!.Name}.{methodInfo.Name}!");
                        break;
                    }

                    if (!TryGetHookAttributeTarget(attribute, out MethodBase targetMethod))
                        continue;

                    Get(attribute.Tag).AddAttribute(new HookAttributeInfo(IsILHook: true, targetMethod, methodInfo));
                }

                // initialize all hookloadcallback attributes on each static method
                foreach (HookLoadCallbackAttribute attribute in methodInfo.GetCustomAttributes<HookLoadCallbackAttribute>()) {
                    if (methodInfo.ReturnType != typeof(void) || methodInfo.GetParameters().Length != 0) {
                        Logger.Warn("ZoomOutHelperPrototype", $"[HookHelper] invalid method signature for hook load callback {methodInfo.DeclaringType!.Name}.{methodInfo.Name}!");
                        break;
                    }

                    Get(attribute.Tag).AddLoadCallback(methodInfo.CreateDelegate<Action>());
                }

                // initialize all hookunloadcallback attributes on each static method
                foreach (HookUnloadCallbackAttribute attribute in methodInfo.GetCustomAttributes<HookUnloadCallbackAttribute>()) {
                    if (methodInfo.ReturnType != typeof(void) || methodInfo.GetParameters().Length != 0) {
                        Logger.Warn("ZoomOutHelperPrototype", $"[HookHelper] invalid method signature for hook unload callback {methodInfo.DeclaringType!.Name}.{methodInfo.Name}!");
                        break;
                    }

                    Get(attribute.Tag).AddUnloadCallback(methodInfo.CreateDelegate<Action>());
                }
            }
        }

        initialized = true;
    }

    private static HookTag Get(string tag) {
        if (HookTags.TryGetValue(tag, out HookTag hookTag))
            return hookTag;

        return HookTags[tag] = new HookTag();
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
        if (!initialized)
            return;

        foreach (string tag in HookTags.Keys)
            UnloadTag(tag);

        foreach (HookTag tag in HookTags.Values)
            tag.Clear();
        HookTags.Clear();

        initialized = false;
    }

    private static readonly Dictionary<string, HookTag> HookTags = [];

    internal static void LoadTag(string tag = "defaultTag") {
        if (!initialized || !HookTags.TryGetValue(tag, out HookTag hookTag) || hookTag.Loaded) {
            Logger.Warn("ZoomOutHelperPrototype", $"could not load tag \"{tag}\"! (hookhelper has not been initialized, the hooktag does not exist, or the hooktag is already loaded.)");
            return;
        }

        hookTag.Load();
        Logger.Info("ZoomOutHelperPrototype", $"[HookHelper] loaded hooktag \"{tag}\"!");
    }

    internal static void UnloadTag(string tag = "defaultTag") {
        if (!initialized || !HookTags.TryGetValue(tag, out HookTag hookTag) || !hookTag.Loaded)
            return;

        hookTag.Unload();
        Logger.Info("ZoomOutHelperPrototype", $"[HookHelper] unloaded hooktag \"{tag}\"!");
    }

    private record HookAttributeInfo(bool IsILHook, MethodBase TargetMethod, MethodInfo HookMethod);

    private class HookTag {
        private readonly HashSet<Action> _loadCallbacks = [];
        private readonly HashSet<Action> _unloadCallbacks = [];

        private readonly HashSet<Hook> _attributeOnHooks = [];
        private readonly HashSet<ILHook> _attributeILHooks = [];
        private readonly HashSet<HookAttributeInfo> _hookAttributes = [];

        public bool Loaded { get; private set; }

        public void Load() {
            Unload();

            try {
                foreach (Action loadCallback in _loadCallbacks) {
                    loadCallback.Invoke();

                    Logger.Info("ZoomOutHelperPrototype", $"[HookHelper] ran hook loading callback {loadCallback.Method.Name}!");
                }

                foreach (HookAttributeInfo hookAttribute in _hookAttributes) {
                    if (hookAttribute.IsILHook)
                        _attributeILHooks.Add(new ILHook(hookAttribute.TargetMethod, hookAttribute.HookMethod.CreateDelegate<ILContext.Manipulator>()));
                    else
                        _attributeOnHooks.Add(new Hook(hookAttribute.TargetMethod, hookAttribute.HookMethod));

                    Logger.Info("ZoomOutHelperPrototype", $"[HookHelper] loaded hook {hookAttribute.HookMethod.DeclaringType!.Name}.{hookAttribute.HookMethod.Name} for {hookAttribute.TargetMethod.DeclaringType!.Name}.{hookAttribute.TargetMethod.Name}!");
                }

                Loaded = true;
            } catch {
                Unload();
                throw;
            }
        }

        public void Unload() {
            foreach (ILHook ilHook in _attributeILHooks)
                ilHook?.Dispose();
            _attributeILHooks.Clear();

            foreach (Hook onHook in _attributeOnHooks)
                onHook?.Dispose();
            _attributeOnHooks.Clear();

            foreach (Action unloadCallback in _unloadCallbacks) {
                unloadCallback.Invoke();

                Logger.Info("ZoomOutHelperPrototype", $"[HookHelper] ran hook unloading callback {unloadCallback.Method.Name}!");
            }

            Loaded = false;
        }

        public void AddAttribute(HookAttributeInfo attributeInfo) =>
            _hookAttributes.Add(attributeInfo);

        public void AddLoadCallback(Action callback) =>
            _loadCallbacks.Add(callback);

        public void AddUnloadCallback(Action callback) =>
            _unloadCallbacks.Add(callback);

        public void Clear() {
            Unload();

            _loadCallbacks.Clear();
            _unloadCallbacks.Clear();
            _hookAttributes.Clear();
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
