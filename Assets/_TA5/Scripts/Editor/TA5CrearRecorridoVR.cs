#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

public static class TA5CrearRecorridoVR
{
    const string SceneName = "CiudadFuturista2050_VR";
    const string GroupName = "09_Recorrido_VR";
    const string UndoName = "Crear recorrido VR TA5";
    static readonly float[] RouteZ = { -36f, -30f, -24f, -18f, -12f, -6f, 0f, 6f, 12f, 18f, 23f };

    [MenuItem("TA5/Crear recorrido VR y simulador", false, 90)]
    public static void Create()
    {
        GameObject created = null;
        int undoGroup = -1;
        var newAssets = new List<string>();
        try
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Operacion no disponible durante Play.");
            Require(PrefabStageUtility.GetCurrentPrefabStage() == null, "Operacion no disponible en el modo de edicion de prefabs.");
            Scene scene = SceneManager.GetActiveScene();
            Require(scene.IsValid() && scene.name == SceneName && !string.IsNullOrEmpty(scene.path),
                "Se requiere la escena guardada CiudadFuturista2050_VR.unity.");
            Require(SceneManager.sceneCount == 1, "Se requiere una unica escena abierta: CiudadFuturista2050_VR.");
            Require(EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64,
                "La plataforma activa debe ser Windows de 64 bits.");
            Require(!Objects<Transform>(scene).Any(t => t.name == GroupName),
                "09_Recorrido_VR ya existe. No se ha creado otra copia.");
            Require(!Objects<XROrigin>(scene).Any(), "Ya existe un XR Origin en la escena.");
            Require(!Objects<XRInteractionManager>(scene).Any(), "Ya existe un XR Interaction Manager en la escena.");
            Require(!Objects<MonoBehaviour>(scene).Any(c => c != null &&
                (c.GetType().Name == "XRInteractionSimulator" || c.GetType().Name == "XRDeviceSimulator")),
                "Ya existe un simulador XR en la escena.");

            var floor = Objects<Collider>(scene).FirstOrDefault(c => c.name == "Piso_Ciudad" && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger);
            Require(floor != null, "No se encontro Piso_Ciudad con un Collider activo y sin Is Trigger.");
            foreach (string name in new[] { "Limite_Izquierdo", "Limite_Derecho", "Limite_Entrada", "Limite_Fondo" })
                Require(Objects<Collider>(scene).Any(c => c.name == name && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger),
                    "Falta un Collider activo y sin Is Trigger en " + name + ".");

            GameObject rigAsset = FindSample("XR Origin (XR Rig)");
            GameObject simAsset = FindSample("XR Interaction Simulator");
            Require(rigAsset.GetComponent<XROrigin>() != null, "El prefab de Starter Assets no contiene el componente XR Origin esperado.");
            Require(rigAsset.GetComponentInChildren<TeleportationProvider>(true) != null, "El prefab de Starter Assets 3.3.2 no contiene Teleportation Provider.");
            Require(simAsset.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c != null && c.GetType().Name == "XRInteractionSimulator"),
                "El prefab de la muestra 3.3.2 no contiene XR Interaction Simulator.");

            Physics.SyncTransforms();
            float[] heights = RouteZ.Select(z => FloorHeight(scene, z)).ToArray();
            float spawnY = FloorHeight(scene, -38f) + 0.08f;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Require(lit != null && unlit != null, "No se encontraron los shaders Universal Render Pipeline/Lit y Universal Render Pipeline/Unlit requeridos.");

            Undo.IncrementCurrentGroup();
            undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(UndoName);
            created = new GameObject(GroupName);
            SceneManager.MoveGameObjectToScene(created, scene);
            GameObject managerObject = Child("XR Interaction Manager", created.transform);
            XRInteractionManager manager = managerObject.AddComponent<XRInteractionManager>();
            GameObject rig = (GameObject)PrefabUtility.InstantiatePrefab(rigAsset, created.transform);
            rig.transform.SetLocalPositionAndRotation(new Vector3(0f, spawnY, -38f), Quaternion.identity);
            rig.transform.localScale = Vector3.one;
            XROrigin origin = rig.GetComponent<XROrigin>();
            Require(origin.Camera != null && origin.CameraFloorOffsetObject != null, "El prefab XR no tiene la camara o Camera Offset configurados.");

            origin.CameraYOffset = 1.65f;
            Vector3 offset = origin.CameraFloorOffsetObject.transform.localPosition;
            offset.y = 1.65f;
            origin.CameraFloorOffsetObject.transform.localPosition = offset;
            origin.Camera.nearClipPlane = 0.05f;
            origin.Camera.farClipPlane = 200f;
            origin.Camera.fieldOfView = 65f;
            origin.Camera.enabled = true;
            origin.Camera.tag = "MainCamera";
            CharacterController body = rig.GetComponent<CharacterController>();
            Require(body != null, "El prefab XR no contiene Character Controller.");
            body.height = 1.8f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.radius = 0.3f;
            body.stepOffset = 0.25f;
            body.skinWidth = 0.035f;

            TeleportationProvider teleport = rig.GetComponentInChildren<TeleportationProvider>(true);

            foreach (LocomotionProvider provider in rig.GetComponentsInChildren<LocomotionProvider>(true))
                provider.enabled = provider is TeleportationProvider || provider is SnapTurnProvider || provider is GravityProvider;
            foreach (SnapTurnProvider turn in rig.GetComponentsInChildren<SnapTurnProvider>(true))
                turn.turnAmount = 45f;

            int controllerManagers = 0;
            foreach (MonoBehaviour component in rig.GetComponentsInChildren<MonoBehaviour>(true))
            {
                Require(component != null, "Starter Assets 3.3.2 contiene un componente Missing Script.");
                if (component.GetType().Name != "ControllerInputActionManager") continue;
                var serialized = new SerializedObject(component);
                SerializedProperty motion = serialized.FindProperty("m_SmoothMotionEnabled");
                SerializedProperty turn = serialized.FindProperty("m_SmoothTurnEnabled");
                Require(motion != null && turn != null, "Esta version de Controller Input Action Manager no coincide con Starter Assets 3.3.2.");
                motion.boolValue = false;
                turn.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                controllerManagers++;
            }
            Require(controllerManagers == 2, "Se esperaban dos Controller Input Action Manager, uno por mando.");
            foreach (XRBaseInteractor interactor in rig.GetComponentsInChildren<XRBaseInteractor>(true))
                interactor.interactionManager = manager;

            XRRayInteractor[] teleportRays = rig.GetComponentsInChildren<XRRayInteractor>(true)
                .Where(r => r.name.IndexOf("Teleport", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            Require(teleportRays.Length == 2, "No se encontraron los dos Teleport Interactor de Starter Assets.");
            InteractionLayerMask interactionMask = teleportRays[0].interactionLayers;
            Require(interactionMask.value != 0, "La mascara de interaccion de teletransporte esta vacia.");
            foreach (XRRayInteractor ray in teleportRays)
            {
                ray.interactionLayers = interactionMask;

                ray.raycastMask = Physics.DefaultRaycastLayers;
                ray.raycastTriggerInteraction = QueryTriggerInteraction.Ignore;
                ray.maxRaycastDistance = 20f;
                ray.velocity = 12f;
            }

            EnsureFolder("Assets/_TA5/Materials/RecorridoVR");
            Material cyan = NewMaterial("M_TP_Cian", unlit, new Color(0.04f, 0.80f, 0.89f), newAssets);
            Material blue = NewMaterial("M_TP_Azul", lit, new Color(0.10f, 0.21f, 0.29f), newAssets);
            GameObject destinations = Child("Puntos_Teletransporte", created.transform);
            for (int i = 0; i < RouteZ.Length; i++)
            {
                string suffix = i == 0 ? "Inicio" : i == 1 ? "Portal" : i == 6 ? "Plaza" : i == 10 ? "Torre" : "Camino";
                GameObject point = Child("TP_" + (i + 1).ToString("00") + "_" + suffix, destinations.transform);
                point.transform.localPosition = new Vector3(0f, heights[i] + 0.004f, RouteZ[i]);
                Disc("Borde_Cian", point.transform, 2.6f, 0.024f, 0.012f, cyan);
                Disc("Centro_Azul", point.transform, 2.28f, 0.026f, 0.015f, blue);
                BoxCollider collider = point.AddComponent<BoxCollider>();
                collider.size = new Vector3(2.6f, 0.028f, 2.6f);
                collider.center = new Vector3(0f, 0.014f, 0f);
                Transform destination = Child("Destino", point.transform).transform;
                destination.localPosition = new Vector3(0f, 0.065f, 0f);
                TeleportationAnchor anchor = point.AddComponent<TeleportationAnchor>();
                anchor.interactionManager = manager;
                anchor.interactionLayers = interactionMask;
                anchor.colliders.Clear();
                anchor.colliders.Add(collider);
                anchor.teleportationProvider = teleport;
                anchor.teleportAnchorTransform = destination;
                anchor.matchOrientation = MatchOrientation.WorldSpaceUp;
                anchor.matchDirectionalInput = false;
                anchor.teleportTrigger = BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
                anchor.filterSelectionByHitNormal = true;
                anchor.upNormalToleranceDegrees = 30f;
            }
            GameObject simulator = (GameObject)PrefabUtility.InstantiatePrefab(simAsset, created.transform);
            simulator.name = "XR Interaction Simulator";
            simulator.tag = "EditorOnly";
            simulator.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            simulator.transform.localScale = Vector3.one;

            foreach (Component component in created.GetComponentsInChildren<Component>(true))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(simulator);
            Undo.RegisterCreatedObjectUndo(created, UndoName);

            foreach (Transform t in Objects<Transform>(scene).ToArray())
            {
                if (t.IsChildOf(created.transform)) continue;
                if (t.name == "08_Navegacion" || t.name == "Jugador_PC_Movil")
                {
                    Undo.RecordObject(t.gameObject, UndoName);
                    t.gameObject.SetActive(false);
                    if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
                }
            }
            foreach (Camera camera in Objects<Camera>(scene))
            {
                if (camera.transform.IsChildOf(created.transform)) continue;
                Undo.RecordObject(camera, UndoName);
                camera.enabled = false;
                if (PrefabUtility.IsPartOfPrefabInstance(camera)) PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            }
            foreach (AudioListener listener in Objects<AudioListener>(scene))
            {
                if (listener.transform.IsChildOf(created.transform)) continue;
                Undo.RecordObject(listener, UndoName);
                listener.enabled = false;
                if (PrefabUtility.IsPartOfPrefabInstance(listener)) PrefabUtility.RecordPrefabInstancePropertyModifications(listener);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = created;
            Debug.Log("Recorrido VR: jugador XR, simulador EditorOnly y 11 destinos creados.", created);
            EditorUtility.DisplayDialog("Recorrido VR creado",
                "Jugador XR, simulador y 11 puntos de teletransporte creados en 09_Recorrido_VR.", "Entendido");
        }
        catch (Exception ex)
        {
            if (undoGroup >= 0) Undo.RevertAllDownToGroup(undoGroup);
            if (created != null) UnityEngine.Object.DestroyImmediate(created);
            foreach (string path in newAssets) AssetDatabase.DeleteAsset(path);
            Debug.LogWarning("Recorrido VR: " + ex.Message);
            EditorUtility.DisplayDialog("Recorrido VR - Error", ex.Message, "Entendido");
        }
    }

    static IEnumerable<T> Objects<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true));
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static GameObject FindSample(string name)
    {
        Require(AssetDatabase.IsValidFolder("Assets/Samples"), "No existe Assets/Samples. Se requieren Starter Assets y XR Interaction Simulator.");
        string[] paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Samples" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetFileNameWithoutExtension(p) == name).ToArray();
        string[] exactVersion = paths.Where(p => p.Contains("/3.3.2/")).ToArray();
        if (exactVersion.Length > 0) paths = exactVersion;
        Require(paths.Length == 1, "Se esperaba un solo prefab llamado '" + name + "' en Assets/Samples y se encontraron " + paths.Length + ". Se requiere un unico prefab compatible con XR Interaction Toolkit 3.3.2.");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]);
        Require(prefab != null, "No se pudo cargar " + paths[0]);
        return prefab;
    }

    static float FloorHeight(Scene scene, float z)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(0f, 1f, z), Vector3.down, 3f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        var usable = hits.Where(h => h.collider.gameObject.scene == scene && h.point.y >= -0.2f && h.point.y <= 0.4f && h.normal.y > 0.9f).ToArray();
        Require(usable.Length > 0, "No se detecto pavimento con Collider en X=0, Z=" + z + ".");
        return usable.Max(h => h.point.y);
    }

    static GameObject Child(string name, Transform parent)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj;
    }

    static void Disc(string name, Transform parent, float diameter, float height, float y, Material material)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = new Vector3(0f, y, 0f);
        obj.transform.localScale = new Vector3(diameter, height / 2f, diameter);
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Material NewMaterial(string name, Shader shader, Color color, List<string> createdAssets)
    {
        Material material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/_TA5/Materials/RecorridoVR/" + name + ".mat");
        AssetDatabase.CreateAsset(material, path);
        createdAssets.Add(path);
        return material;
    }
}
#endif
