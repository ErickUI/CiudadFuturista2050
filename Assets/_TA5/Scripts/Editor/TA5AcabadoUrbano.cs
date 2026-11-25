#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TA5.EditorTools
{
    public static class TA5AcabadoUrbano
    {
        private const string Grupo = "07_Acabado_Urbano";

        [MenuItem("TA5/Mejorar piso, bordes y vegetacion", false, 130)]
        public static void Crear()
        {
            if (!EscenaLista(out Scene escena)) return;
            if (Buscar(escena, Grupo) != null)
            { Aviso("07_Acabado_Urbano ya existe. No se ha creado otra copia."); return; }

            Renderer suelo = RendererDe(Buscar(escena, "Piso_Ciudad"));
            GameObject caminoPortal = Buscar(escena, "Camino_Portal_Plaza");
            if (caminoPortal == null) caminoPortal = Buscar(escena, "Camino_PortalPlaza");
            Renderer camino1 = RendererDe(caminoPortal);
            Renderer camino2 = RendererDe(Buscar(escena, "Camino_Plaza_Torre"));
            Renderer portal = RendererDe(Buscar(escena, "Portal_Viga_Superior"));
            Renderer neonPortal = RendererDe(Buscar(escena, "Neon_Portal_Superior"));
            GameObject jardinera = Buscar(escena, "Jardinera_1");
            string[] limites = { "Limite_Izquierdo", "Limite_Derecho", "Limite_Entrada", "Limite_Fondo" };
            Renderer[] bordes = new Renderer[limites.Length];
            for (int i = 0; i < limites.Length; i++) bordes[i] = RendererDe(Buscar(escena, limites[i]));
            if (suelo == null || camino1 == null || camino2 == null || portal == null
                || neonPortal == null || jardinera == null || Array.Exists(bordes, r => r == null))
            {
                Aviso("Faltan elementos requeridos: el piso, los dos caminos, el portal, su neon, Jardinera_1 y los cuatro limites de 06_Acabado_Final.");
                return;
            }
            if (suelo.sharedMaterial == null || camino1.sharedMaterial == null
                || portal.sharedMaterial == null || neonPortal.sharedMaterial == null)
            { Aviso("Falta un material en el piso, el camino o el portal."); return; }

            Bounds area = suelo.bounds;
            if (Vector3.Distance(new Vector3(area.center.x, area.max.y, area.center.z), Vector3.zero) > 0.05f
                || Mathf.Abs(area.size.x - 60) > 0.1f || Mathf.Abs(area.size.z - 90) > 0.1f)
            { Aviso("Esta distribucion requiere el piso de 60 x 90, centrado en X/Z = 0 y con superficie Y = 0."); return; }

            string carpeta = null;
            GameObject conjunto = null;
            int grupoUndo = -1;
            bool terminado = false;
            try
            {
                string libre = AssetDatabase.GenerateUniqueAssetPath("Assets/_TA5/Materials/AcabadoUrbano");
                string guid = AssetDatabase.CreateFolder("Assets/_TA5/Materials",
                    libre.Substring(libre.LastIndexOf('/') + 1));
                if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("No se pudo crear la carpeta de materiales.");
                carpeta = AssetDatabase.GUIDToAssetPath(guid);
                Material pavimento = MaterialNuevo(suelo.sharedMaterial, carpeta, "M_Suelo_Azul", "3C536C", 0.10f, 0.46f);
                Material paseo = MaterialNuevo(camino1.sharedMaterial, carpeta, "M_Camino_Claro", "6B849F", 0.08f, 0.44f);
                Material plaza = MaterialNuevo(camino1.sharedMaterial, carpeta, "M_Plaza_Clara", "8A9FB5", 0.06f, 0.38f);
                Material acera = MaterialNuevo(camino1.sharedMaterial, carpeta, "M_Acera_Azul", "7189A2", 0.08f, 0.4f);
                Material verde = MaterialNuevo(suelo.sharedMaterial, carpeta, "M_Verde_Urbano", "43876A", 0, 0.2f);
                Material verdeClaro = MaterialNuevo(suelo.sharedMaterial, carpeta, "M_Verde_Claro_Urbano", "7BB388", 0, 0.2f);
                Material metal = portal.sharedMaterial;
                Material cian = neonPortal.sharedMaterial;

                conjunto = new GameObject(Grupo);
                SceneManager.MoveGameObjectToScene(conjunto, escena);
                Transform aceras = Hijo("01_Aceras", conjunto.transform);
                Transform cenefas = Hijo("02_Bordes_Cian", conjunto.transform);
                Transform arboles = Hijo("03_Arboles", conjunto.transform);
                Transform esquinas = Hijo("04_Esquinas", conjunto.transform);

                int numero = 0;
                foreach (float x in new[] { -17f, 17f })
                    foreach (float z in new[] { -16f, 6f, 30f })
                    {
                        int n = ++numero;
                        Cubo("Acera_Edificio_" + n, aceras, V(x, 0.012f, z), V(11.5f, 0.024f, 11.5f), acera, true);
                        float ladoInterior = x < 0 ? x + 5.6f : x - 5.6f;
                        Cubo("Cenefa_Edificio_" + n, aceras, V(ladoInterior, 0.039f, z), V(0.07f, 0.03f, 10.9f), cian, false);
                    }

                for (int i = 0; i < bordes.Length; i++)
                {
                    Bounds b = bordes[i].bounds;
                    Vector3 escala = b.size.x > b.size.z ? V(b.size.x, 0.03f, 0.12f) : V(0.12f, 0.03f, b.size.z);
                    Cubo("Cenefa_" + limites[i], cenefas, V(b.center.x, b.max.y + 0.015f, b.center.z), escala, cian, false);
                }
                numero = 0;
                foreach (float x in new[] { -29.675f, 29.675f })
                    foreach (float z in new[] { -44.675f, 44.675f })
                    {
                        int n = ++numero;
                        Cubo("Poste_Esquina_" + n, esquinas, V(x, 0.7f, z), V(0.65f, 1.4f, 0.65f), metal, true);
                        Cubo("Tapa_Cian_" + n, esquinas, V(x, 1.43f, z), V(0.65f, 0.06f, 0.65f), cian, false);
                    }

                GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(jardinera);
                numero = 0;

                foreach (float lado in new[] { -1f, 1f })
                {
                    foreach (float z in new[] { -28f, -6f, 16f, 38f })
                        Arbol(prefab, jardinera, arboles, escena, ++numero, V(lado * 25, 0, z), verde, verdeClaro);
                    foreach (float z in new[] { -5f, 18f })
                        Arbol(prefab, jardinera, arboles, escena, ++numero, V(lado * 17, 0, z), verde, verdeClaro);
                    foreach (float z in new[] { -35f, 39f })
                        Arbol(prefab, jardinera, arboles, escena, ++numero, V(lado * 13, 0, z), verde, verdeClaro);
                }

                Undo.IncrementCurrentGroup();
                grupoUndo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("TA5 - Acabado urbano");
                Undo.RegisterCreatedObjectUndo(conjunto, "TA5 - Vegetacion y bordes");
                Asignar(suelo, pavimento, true);
                Asignar(camino1, paseo, true);
                Asignar(camino2, paseo, true);
                foreach (Renderer r in bordes) Asignar(r, metal, true);
                Renderer basePlaza = RendererDe(Buscar(escena, "Plaza_Base"));
                if (basePlaza != null) Asignar(basePlaza, paseo, true);
                GameObject losas = Buscar(escena, "Pavimento_Plaza");
                if (losas != null)
                    foreach (Renderer r in losas.GetComponentsInChildren<Renderer>(true)) Asignar(r, plaza, true);
                GameObject llegada = Buscar(escena, "Plataforma_Llegada");
                if (llegada != null)
                    foreach (Renderer r in llegada.GetComponentsInChildren<Renderer>(true))
                        if (r.sharedMaterial != null && r.sharedMaterial.name == "M_Plaza") Asignar(r, plaza, true);
                for (int i = 1; i <= 4; i++)
                {
                    GameObject arbolPlaza = Buscar(escena, "Jardinera_" + i);
                    if (arbolPlaza != null) PintarFollaje(arbolPlaza, verde, verdeClaro, true);
                }
                Undo.CollapseUndoOperations(grupoUndo);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(escena);
                terminado = true;
                Selection.activeGameObject = conjunto;
                Debug.Log("Acabado urbano: materiales, seis aceras, bordes y 16 arboles creados. Materiales: " + carpeta + ".", conjunto);
                Aviso("Acabado urbano creado: 16 arboles, 6 aceras y bordes titanio/cian.");
            }
            catch (Exception error)
            {
                if (!terminado)
                {
                    if (grupoUndo >= 0) Undo.RevertAllDownToGroup(grupoUndo);
                    if (conjunto != null) UnityEngine.Object.DestroyImmediate(conjunto);
                }
                Debug.LogException(error);
                Aviso("No se pudo completar el acabado.\n\n" + error.Message);
            }
            finally
            {
                if (!terminado && !string.IsNullOrEmpty(carpeta)) AssetDatabase.DeleteAsset(carpeta);
            }
        }

        private static void Arbol(GameObject prefab, GameObject original, Transform padre,
            Scene escena, int numero, Vector3 posicion, Material verde, Material verdeClaro)
        {
            GameObject objeto = prefab != null
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena)
                : UnityEngine.Object.Instantiate(original);
            if (objeto == null) throw new InvalidOperationException("No se pudo crear el arbol " + numero);
            objeto.transform.SetParent(padre, false);
            objeto.name = "Arbol_Urbano_" + numero.ToString("00");
            objeto.transform.localPosition = posicion;
            objeto.transform.localRotation = Quaternion.identity;
            objeto.transform.localScale = Vector3.one * 1.15f;
            PintarFollaje(objeto, verde, verdeClaro, false);
            if (PrefabUtility.IsPartOfPrefabInstance(objeto))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(objeto);
                PrefabUtility.RecordPrefabInstancePropertyModifications(objeto.transform);
            }
        }

        private static void PintarFollaje(GameObject objeto, Material verde, Material claro, bool undo)
        {
            foreach (Renderer r in objeto.GetComponentsInChildren<Renderer>(true))
            {
                if (r.sharedMaterial == null) continue;
                string nombre = r.sharedMaterial.name;
                if (nombre == "M_Vegetacion" || nombre == "M_Verde_Urbano") Asignar(r, verde, undo);
                else if (nombre == "M_Vegetacion_Clara" || nombre == "M_Verde_Claro_Urbano") Asignar(r, claro, undo);
            }
        }

        private static void Asignar(Renderer renderer, Material material, bool undo)
        {
            if (undo) Undo.RecordObject(renderer, "TA5 - Material urbano");
            renderer.sharedMaterial = material;
            if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            EditorUtility.SetDirty(renderer);
        }

        private static Material MaterialNuevo(Material original, string carpeta, string nombre,
            string hex, float metalico, float suavidad)
        {
            Material material = new Material(original) { name = nombre, enableInstancing = true };
            if (!ColorUtility.TryParseHtmlString("#" + hex, out Color color)) throw new ArgumentException("Color: " + hex);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metalico);
            material.SetFloat("_Smoothness", suavidad);
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material, carpeta + "/" + nombre + ".mat");
            return material;
        }

        private static void Cubo(string nombre, Transform padre, Vector3 centro,
            Vector3 escala, Material material, bool colision)
        {
            GameObject objeto = GameObject.CreatePrimitive(PrimitiveType.Cube);
            objeto.name = nombre;
            objeto.transform.SetParent(padre, false);
            objeto.transform.localPosition = centro;
            objeto.transform.localScale = escala;
            objeto.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!colision) UnityEngine.Object.DestroyImmediate(objeto.GetComponent<BoxCollider>());
        }

        [MenuItem("TA5/Encuadrar ciudad completa", false, 140)]
        public static void Encuadrar()
        {
            if (!EscenaLista(out Scene escena)) return;
            GameObject objeto = Buscar(escena, "Main Camera");
            Camera camara = objeto != null ? objeto.GetComponent<Camera>() : null;
            if (camara == null) { Aviso("No se encontro Main Camera."); return; }
            Undo.IncrementCurrentGroup();
            int grupo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("TA5 - Encuadrar ciudad completa");
            Undo.RecordObject(camara.transform, "TA5 - Posicion de camara");
            Undo.RecordObject(camara, "TA5 - Campo de vision");
            camara.transform.position = V(0, 42, -66);
            camara.transform.rotation = Quaternion.Euler(31, 0, 0);
            camara.fieldOfView = 65;
            Undo.CollapseUndoOperations(grupo);
            EditorSceneManager.MarkSceneDirty(escena);
        }

        private static bool EscenaLista(out Scene escena)
        {
            escena = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            { Aviso("Operacion no disponible durante Play o la compilacion."); return false; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null || !escena.IsValid()
                || !escena.isLoaded || escena.name != "CiudadFuturista2050")
            { Aviso("Se requiere la escena CiudadFuturista2050 cargada fuera del modo Prefab."); return false; }
            return true;
        }
        private static Transform Hijo(string nombre, Transform padre)
        {
            Transform t = new GameObject(nombre).transform;
            t.SetParent(padre, false);
            return t;
        }
        private static GameObject Buscar(Scene escena, string nombre)
        {
            foreach (GameObject raiz in escena.GetRootGameObjects())
                foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                    if (t.name == nombre) return t.gameObject;
            return null;
        }
        private static Renderer RendererDe(GameObject objeto)
        { return objeto != null ? objeto.GetComponent<Renderer>() : null; }
        private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
        private static void Aviso(string texto)
        { EditorUtility.DisplayDialog("TA5 - Acabado urbano", texto, "Aceptar"); }
    }
}
#endif
