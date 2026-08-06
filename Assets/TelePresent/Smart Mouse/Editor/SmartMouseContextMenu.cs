/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TelePresent.SmartMouse
{
    internal static class SmartMouseContextMenu
    {
        static bool _wasFocusOnGameObjectsEnabledByScroll = false;

        static List<MethodInfo> _cachedMenuMethods;

        [InitializeOnLoadMethod]
        static void BuildMenuMethodCache() => _cachedMenuMethods = GetMenuMethods();

        static List<MethodInfo> GetMenuMethods() =>
            TypeCache.GetMethodsWithAttribute<SmartMouseContextMenuItemAttribute>()
                .OrderBy(GetMenuOrder)
                .ThenBy(m => m.DeclaringType?.FullName, StringComparer.Ordinal)
                .ToList();

        static int GetMenuOrder(MethodInfo method)
        {
            var attribute = method.GetCustomAttribute<SmartMouseContextMenuItemAttribute>();
            return attribute != null ? attribute.Order : 1000;
        }

        public static void ShowContextMenu(SceneView sceneView, Vector2 mousePosition, Ray worldRay)
        {
            SmartMouseUtility.EnsureBVH();

            GenericMenu menu = new GenericMenu();
            RegisterDynamicMenuItems(menu, sceneView, mousePosition, worldRay);
            menu.ShowAsContext();
            SmartMouseController.isContextMenuVisible = false;
        }

        static void RegisterDynamicMenuItems(GenericMenu menu, SceneView sceneView, Vector2 mousePosition, Ray worldRay)
        {
            IEnumerable<MethodInfo> methods = _cachedMenuMethods ?? GetMenuMethods();
            foreach (var methodInfo in methods)
            {
                var attribute = methodInfo.GetCustomAttribute<SmartMouseContextMenuItemAttribute>();
                var parameters = methodInfo.GetParameters();

                if (!string.IsNullOrEmpty(attribute?.Path))
                {
                    if (!methodInfo.IsStatic || parameters.Length != 0)
                    {
                        Debug.LogError($"Smart Mouse: {methodInfo.DeclaringType.FullName}.{methodInfo.Name} - a menu item with a path must be a parameterless static method.");
                        menu.AddDisabledItem(new GUIContent($"{attribute.Path} (Error)", "Signature must be a parameterless static method."));
                        continue;
                    }

                    if (attribute.RequiresSelection && Selection.gameObjects.Length == 0)
                    {
                        menu.AddDisabledItem(new GUIContent(attribute.Path));
                    }
                    else
                    {
                        menu.AddItem(new GUIContent(attribute.Path), false, () =>
                        {
                            try
                            {
                                methodInfo.Invoke(null, null);
                            }
                            catch (Exception e)
                            {
                                Debug.LogError($"Smart Mouse: context menu item {methodInfo.DeclaringType.FullName}.{methodInfo.Name} threw:");
                                Debug.LogException(e.InnerException ?? e);
                            }
                        });
                    }
                    continue;
                }

                if (methodInfo.IsStatic &&
                    parameters.Length == 4 &&
                    parameters[0].ParameterType == typeof(GenericMenu) &&
                    parameters[1].ParameterType == typeof(SceneView) &&
                    parameters[2].ParameterType == typeof(Vector2) &&
                    parameters[3].ParameterType == typeof(Ray))
                {
                    try
                    {
                        methodInfo.Invoke(null, new object[] { menu, sceneView, mousePosition, worldRay });
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Smart Mouse: context menu method {methodInfo.DeclaringType.FullName}.{methodInfo.Name} threw:");
                        Debug.LogException(e.InnerException ?? e);
                        menu.AddDisabledItem(new GUIContent($"Custom Error/{methodInfo.Name} Failed", e.InnerException?.Message ?? e.Message));
                    }
                }
                else
                {
                    Debug.LogError($"Smart Mouse: {methodInfo.DeclaringType.FullName}.{methodInfo.Name} does not match either menu form: a static method that either has a path on the attribute and takes no parameters, or takes (GenericMenu, SceneView, Vector2, Ray).{(methodInfo.IsStatic ? "" : " The method is not static.")}");
                    menu.AddDisabledItem(new GUIContent($"Menu Error/{methodInfo.Name} (Error)", methodInfo.IsStatic ? "Signature does not match either menu form." : "The method must be static."));
                }
            }
        }

        // Mirrors the static flag so it survives a domain reload.
        const string FocusScrollFlipKey = "SmartMouse_FocusEnabledByScroll";

        public static void ToggleFocusOnGameObjects()
        {
            SmartMouseSettings.FocusOnGameObjects = !SmartMouseSettings.FocusOnGameObjects;
            if (SmartMouseSettings.FocusOnGameObjects)
            {
                _wasFocusOnGameObjectsEnabledByScroll = true;
                SessionState.SetBool(FocusScrollFlipKey, true);
            }
        }

        public static bool IsFocusOnGameObjects() => SmartMouseSettings.FocusOnGameObjects;

        // Manual toggles clear the scroll marker, or the next overlay deactivate undoes them.
        public static void MarkFocusManuallySet()
        {
            _wasFocusOnGameObjectsEnabledByScroll = false;
            SessionState.EraseBool(FocusScrollFlipKey);
        }

        public static void DisableFocusOnGameObjectsIfEnabledByScroll()
        {
            if (SmartMouseSettings.FocusOnGameObjects &&
                (_wasFocusOnGameObjectsEnabledByScroll || SessionState.GetBool(FocusScrollFlipKey, false)))
            {
                SmartMouseSettings.FocusOnGameObjects = false;
            }
            _wasFocusOnGameObjectsEnabledByScroll = false;
            SessionState.EraseBool(FocusScrollFlipKey);
        }

        [InitializeOnLoadMethod]
        static void RestoreFocusModeAfterReload()
        {
            if (SessionState.GetBool(FocusScrollFlipKey, false))
                DisableFocusOnGameObjectsIfEnabledByScroll();
        }
    }
}
