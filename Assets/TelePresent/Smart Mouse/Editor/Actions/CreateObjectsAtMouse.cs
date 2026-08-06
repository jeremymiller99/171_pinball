/*******************************************************
Product - Smart Mouse Selection Tools
  Publisher - TelePresent Games
              http://TelePresentGames.dk
  Author    - Martin Hansen
  Created   - 2026
  (c) 2026 Martin Hansen. All rights reserved.
*******************************************************/

using UnityEditor;
using UnityEngine;

namespace TelePresent.SmartMouse
{
    internal static class CreateObjectsAtMouse
    {
        const float DefaultRayDistance = 4f;

        static Vector3? _menuSurfaceNormal;

        [SmartMouseContextMenuItem(10)]
        public static void RegisterCreateMenuItems(GenericMenu menu, SceneView sceneView, Vector2 mousePosition, Ray worldRay)
        {
            Vector3 hitPoint = SmartMouseController.GetRayHitPointOrDefault(worldRay, DefaultRayDistance, out _menuSurfaceNormal);

            menu.AddSeparator("Create/");
            menu.AddItem(new GUIContent("Create/Empty GameObject"), false, () => CreateEmptyObjectAtLocation(hitPoint));
            menu.AddItem(new GUIContent("Create/2D Object/Sprite"), false, () => CreateSpriteAtLocation(hitPoint));
            menu.AddItem(new GUIContent("Create/3D Object/Cube"), false, () => CreatePrimitiveAtLocation(PrimitiveType.Cube, hitPoint));
            menu.AddItem(new GUIContent("Create/3D Object/Sphere"), false, () => CreatePrimitiveAtLocation(PrimitiveType.Sphere, hitPoint));
            menu.AddItem(new GUIContent("Create/3D Object/Capsule"), false, () => CreatePrimitiveAtLocation(PrimitiveType.Capsule, hitPoint));
            menu.AddItem(new GUIContent("Create/3D Object/Cylinder"), false, () => CreatePrimitiveAtLocation(PrimitiveType.Cylinder, hitPoint));
            menu.AddItem(new GUIContent("Create/3D Object/Plane"), false, () => CreatePrimitiveAtLocation(PrimitiveType.Plane, hitPoint));
            menu.AddItem(new GUIContent("Create/3D Object/Quad"), false, () => CreatePrimitiveAtLocation(PrimitiveType.Quad, hitPoint));
            menu.AddItem(new GUIContent("Create/Effects/Particle System"), false, () => CreateParticleSystemAtLocation(hitPoint));
            menu.AddItem(new GUIContent("Create/Light/Directional Light"), false, () => CreateLightAtLocation(LightType.Directional, hitPoint));
            menu.AddItem(new GUIContent("Create/Light/Point Light"), false, () => CreateLightAtLocation(LightType.Point, hitPoint));
            menu.AddItem(new GUIContent("Create/Light/Spot Light"), false, () => CreateLightAtLocation(LightType.Spot, hitPoint));
            menu.AddItem(new GUIContent("Create/Light/Area Light"), false, () => CreateLightAtLocation(LightType.Rectangle, hitPoint));
            menu.AddItem(new GUIContent("Create/Audio/Audio Source"), false, () => CreateAudioSourceAtLocation(hitPoint));
            menu.AddItem(new GUIContent("Create/UI/Canvas"), false, () => CreateUIElementAtLocation<Canvas>(hitPoint, "Canvas"));
            menu.AddItem(new GUIContent("Create/UI/Button"), false, () => CreateUIElementAtLocation<UnityEngine.UI.Button>(hitPoint, "Button"));
            menu.AddItem(new GUIContent("Create/Camera"), false, () => CreateCameraAtLocation(hitPoint));
            menu.AddItem(new GUIContent("Create/Volume/Box Volume"), false, () => CreateBoxVolumeAtLocation(hitPoint));
            menu.AddItem(new GUIContent("Create/Volume/Sphere Volume"), false, () => CreateSphereVolumeAtLocation(hitPoint));
        }

        static void CreateEmptyObjectAtLocation(Vector3 position)
        {
            GameObject newObject = new GameObject("Empty GameObject");
            PlaceObjectAtLocation(newObject, position);
        }

        static void CreatePrimitiveAtLocation(PrimitiveType type, Vector3 position)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            PlaceObjectAtLocation(primitive, position, type == PrimitiveType.Plane);
        }

        static void CreateSpriteAtLocation(Vector3 position)
        {
            GameObject spriteObject = new GameObject("New Sprite");
            spriteObject.AddComponent<SpriteRenderer>();
            PlaceObjectAtLocation(spriteObject, position);
        }

        static void CreateParticleSystemAtLocation(Vector3 position)
        {
            GameObject particleSystemObject = new GameObject("New Particle System");
            particleSystemObject.AddComponent<ParticleSystem>();
            PlaceObjectAtLocation(particleSystemObject, position);
        }

        static void CreateLightAtLocation(LightType lightType, Vector3 position)
        {
            GameObject lightGameObject = new GameObject(lightType + " Light");
            Light lightComp = lightGameObject.AddComponent<Light>();
            lightComp.type = lightType;
            PlaceObjectAtLocation(lightGameObject, position);
        }

        static void CreateAudioSourceAtLocation(Vector3 position)
        {
            GameObject audioSourceObject = new GameObject("New Audio Source");
            audioSourceObject.AddComponent<AudioSource>();
            PlaceObjectAtLocation(audioSourceObject, position);
        }

        static void CreateUIElementAtLocation<T>(Vector3 position, string name) where T : Component
        {
            GameObject uiObject = new GameObject(name, typeof(RectTransform));
            uiObject.AddComponent<T>();
            PlaceObjectAtLocation(uiObject, position);
        }

        static void CreateCameraAtLocation(Vector3 position)
        {
            GameObject cameraObject = new GameObject("New Camera");
            cameraObject.AddComponent<Camera>();
            PlaceObjectAtLocation(cameraObject, position);
        }

        static void CreateBoxVolumeAtLocation(Vector3 position)
        {
            GameObject boxVolume = new GameObject("Box Volume");
            boxVolume.AddComponent<BoxCollider>();
            PlaceObjectAtLocation(boxVolume, position);
        }

        static void CreateSphereVolumeAtLocation(Vector3 position)
        {
            GameObject sphereVolume = new GameObject("Sphere Volume");
            sphereVolume.AddComponent<SphereCollider>();
            PlaceObjectAtLocation(sphereVolume, position);
        }

        static void PlaceObjectAtLocation(GameObject obj, Vector3 position, bool isPlane = false)
        {
            SmartMouseCompat.PlaceNewObjectInActiveStage(obj);

            Undo.RegisterCreatedObjectUndo(obj, "Create " + obj.name);
            EditorUtility.SetDirty(obj);

            Selection.activeGameObject = obj;

            if (isPlane)
            {
                obj.transform.position = position;
            }
            else
            {
                SmartMouseUtility.PlaceObjectAtPosition(obj, position, _menuSurfaceNormal);
            }
        }
    }
}
