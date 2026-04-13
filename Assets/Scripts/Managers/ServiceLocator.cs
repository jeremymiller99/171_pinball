// Generated with Antigravity by jjmil on 2026-03-29.
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight static service registry for cross-scene lookups.
/// Replaces scattered FindObjectsByType / FindFirstObjectByType
/// calls with a single registration point per service.
///
/// Usage:
///   Register:   ServiceLocator.Register&lt;ScoreManager&gt;(this);
///   Lookup:     var sm = ServiceLocator.Get&lt;ScoreManager&gt;();
///   Safe:       if (ServiceLocator.TryGet&lt;ScoreManager&gt;(out var sm)) { ... }
///   Cleanup:    ServiceLocator.Unregister&lt;ScoreManager&gt;();
/// </summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> services =
        new Dictionary<Type, object>();

    /// <summary>
    /// Registers an instance for the given type. Overwrites any
    /// previous registration for the same type.
    /// </summary>
    public static void Register<T>(T instance) where T : class
    {
        if (instance == null)
        {
            Debug.LogWarning(
                $"[ServiceLocator] Attempted to register null " +
                $"for {typeof(T).Name}.");
            return;
        }

        services[typeof(T)] = instance;
    }

    /// <summary>
    /// Returns the registered instance, or null if none exists.
    /// </summary>
    public static T Get<T>() where T : class
    {
        if (services.TryGetValue(typeof(T), out object obj))
        {
            T cached = obj as T;
            // Unity's overloaded == catches destroyed components/GameObjects even though
            // the C# reference is non-null. Purge the stale entry and fall through.
            if (cached is UnityEngine.Object unityObj)
            {
                if (unityObj != null) return cached;
                services.Remove(typeof(T));
            }
            else if (cached != null)
            {
                return cached;
            }
        }

        // Fallback for MonoBehaviours that load out-of-order during additive scene loads.
        if (typeof(UnityEngine.Component).IsAssignableFrom(typeof(T)))
        {
            var fallback = UnityEngine.Object.FindAnyObjectByType(typeof(T), FindObjectsInactive.Include) as T;
            if (fallback != null)
            {
                services[typeof(T)] = fallback;
                return fallback;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns true and sets the out parameter if a service of
    /// this type is registered; false otherwise.
    /// </summary>
    public static bool TryGet<T>(out T instance) where T : class
    {
        if (services.TryGetValue(typeof(T), out object obj))
        {
            T cached = obj as T;
            if (cached is UnityEngine.Object unityObj)
            {
                if (unityObj != null)
                {
                    instance = cached;
                    return true;
                }
                services.Remove(typeof(T));
            }
            else if (cached != null)
            {
                instance = cached;
                return true;
            }
        }

        if (typeof(UnityEngine.Component).IsAssignableFrom(typeof(T)))
        {
            var fallback = UnityEngine.Object.FindAnyObjectByType(typeof(T), FindObjectsInactive.Include) as T;
            if (fallback != null)
            {
                services[typeof(T)] = fallback;
                instance = fallback;
                return true;
            }
        }

        instance = null;
        return false;
    }

    /// <summary>
    /// Removes the registration for the given type. Safe to call
    /// even if nothing was registered.
    /// </summary>
    public static void Unregister<T>() where T : class
    {
        services.Remove(typeof(T));
    }

    /// <summary>
    /// Removes all registrations. Typically called during full
    /// scene teardown or application quit.
    /// </summary>
    public static void Clear()
    {
        services.Clear();
    }
}
