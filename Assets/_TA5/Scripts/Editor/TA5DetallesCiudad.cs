#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace TA5.EditorTools
{
    public static class TA5DetallesCiudad
    {
        private const string NombreGrupo = "05_Detalles_Ciudad";
        private const string NombreMenu = "TA5/Crear detalles de plaza y torre";
        private const float SueloPlaza = 0.085f;

        private sealed class Modelo
        {
            public readonly GameObject raiz;
            private readonly Mesh cubo;
            private readonly string carpeta;
            private readonly Dictionary<Material, List<CombineInstance>> partes =
                new Dictionary<Material, List<CombineInstance>>();

            public Modelo(string nombre, Mesh cuboBase, string destino, Transform padre)
            {
                raiz = new GameObject(nombre);
                raiz.transform.SetParent(padre, false);
                cubo = cuboBase;
                carpeta = destino;
            }

            public void Caja(Material material, Vector3 centro, Vector3 escala)
            {
                Agregar(material, cubo, centro, escala, Quaternion.identity);
            }

            public void Agregar(Material material, Mesh malla, Vector3 centro,
                Vector3 escala, Quaternion giro)
            {
                if (!partes.TryGetValue(material, out List<CombineInstance> lista))
                {
                    lista = new List<CombineInstance>();
                    partes.Add(material, lista);
                }
                lista.Add(new CombineInstance
                {
                    mesh = malla, subMeshIndex = 0,
                    transform = Matrix4x4.TRS(centro, giro, escala)
                });
            }

            public void Colision(string nombre, Vector3 centro, Vector3 escala)
            {
                GameObject objeto = new GameObject(nombre);
                objeto.transform.SetParent(raiz.transform, false);
                BoxCollider collider = objeto.AddComponent<BoxCollider>();
                collider.center = centro;
                collider.size = escala;
                collider.isTrigger = false;
            }

            public void Terminar()
            {
                foreach (KeyValuePair<Material, List<CombineInstance>> par in partes)
                {
                    Mesh malla = new Mesh { name = raiz.name + "_" + par.Key.name };
                    malla.CombineMeshes(par.Value.ToArray(), true, true, false);
                    malla.RecalculateBounds();
                    AssetDatabase.CreateAsset(malla, AssetDatabase.GenerateUniqueAssetPath(
                        carpeta + "/Mallas/" + malla.name + ".asset"));
                    GameObject pieza = new GameObject(par.Key.name);
                    pieza.transform.SetParent(raiz.transform, false);
                    pieza.AddComponent<MeshFilter>().sharedMesh = malla;
                    MeshRenderer renderer = pieza.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = par.Key;
                    renderer.shadowCastingMode = par.Key.IsKeywordEnabled("_EMISSION")
                        ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                partes.Clear();
            }
        }

        [MenuItem(NombreMenu, false, 110)]
        public static void Crear()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                Aviso("Operacion no disponible durante Play o la compilacion.");
                return;
            }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Aviso("Operacion no disponible en el modo de edicion de prefabs.");
                return;
            }
            Scene escena = SceneManager.GetActiveScene();
            if (!escena.IsValid() || !escena.isLoaded || escena.name != "CiudadFuturista2050")
            {
                Aviso("Se requiere la escena CiudadFuturista2050 cargada.");
                return;
            }
            HashSet<string> nombres = new HashSet<string>();
            foreach (GameObject raiz in escena.GetRootGameObjects())
                foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                    nombres.Add(t.name);
            if (nombres.Contains(NombreGrupo))
            {
                Aviso("Ya existe 05_Detalles_Ciudad. No se ha creado otra copia.");
                return;
            }
            if (!nombres.Contains("Plaza_Base") || !nombres.Contains("Torre_Cuerpo")
                || !nombres.Contains("04_Edificios"))
            {
                Aviso("Faltan elementos requeridos: Plaza_Base, Torre_Cuerpo o 04_Edificios.");
                return;
            }

            Material titanio = LeerMaterial("M_Titanio");
            Material fachada = LeerMaterial("M_Fachada");
            Material cian = LeerMaterial("M_Neon_Cian");
            Material camino = LeerMaterial("M_Camino");
            Material plaza = LeerMaterial("M_Plaza");
            if (titanio == null || fachada == null || cian == null || camino == null || plaza == null)
            {
                Aviso("Materiales requeridos no disponibles: M_Titanio, M_Fachada, M_Neon_Cian, M_Camino y M_Plaza en Assets/_TA5/Materials.");
                return;
            }
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (lit == null || unlit == null)
            {
                Aviso("No se encontraron los shaders de URP necesarios para esta escena.");
                return;
            }

            GameObject conjunto = null;
            GameObject temporales = null;
            string carpeta = null;
            bool completado = false;
            try
            {
                CrearCarpeta("Assets/_TA5/Prefabs");
                string libre = AssetDatabase.GenerateUniqueAssetPath("Assets/_TA5/Prefabs/DetallesCiudad");
                string guid = AssetDatabase.CreateFolder("Assets/_TA5/Prefabs",
                    libre.Substring(libre.LastIndexOf('/') + 1));
                if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("No se pudo crear la carpeta.");
                carpeta = AssetDatabase.GUIDToAssetPath(guid);
                CrearCarpeta(carpeta + "/Mallas");
                CrearCarpeta(carpeta + "/Materiales");
                CrearCarpeta(carpeta + "/Senales");

                Material madera = MaterialLit(lit, carpeta, "M_Madera_Calida", "946441", 0, 0.25f);
                Material tierra = MaterialLit(lit, carpeta, "M_Tierra", "302B25", 0, 0.1f);
                Material verde = MaterialLit(lit, carpeta, "M_Vegetacion", "357963", 0, 0.2f);
                Material verdeClaro = MaterialLit(lit, carpeta, "M_Vegetacion_Clara", "64A883", 0, 0.2f);
                Material vidrio = MaterialLit(lit, carpeta, "M_Cristal_Mirador", "173C52", 0.25f, 0.65f);
                Material losa = MaterialLit(lit, carpeta, "M_Losa_Plaza", "74889F", 0.05f, 0.3f);
                Material ambar = MaterialLit(lit, carpeta, "M_Farola_Ambar", "FFCE83", 0, 0.2f);
                ambar.EnableKeyword("_EMISSION");
                ambar.SetColor("_EmissionColor", ColorHex("FFCE83") * 1.5f);
                EditorUtility.SetDirty(ambar);

                temporales = new GameObject("TA5_Temporales_Detalles");
                GameObject cuboObjeto = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cuboObjeto.transform.SetParent(temporales.transform, false);
                Mesh cubo = cuboObjeto.GetComponent<MeshFilter>().sharedMesh;
                Mesh hojas = CrearFollaje();
                AssetDatabase.CreateAsset(hojas, carpeta + "/Mallas/Follaje_Base.asset");
                Mesh plano = CrearPlano();
                AssetDatabase.CreateAsset(plano, carpeta + "/Mallas/Plano_Senal.asset");
                Material cartelPlaza = CrearRotulo(unlit, carpeta, "PLAZA");
                Material cartelTorre = CrearRotulo(unlit, carpeta, "TORRE");

                conjunto = new GameObject(NombreGrupo);
                SceneManager.MoveGameObjectToScene(conjunto, escena);
                Transform zonaPlaza = Grupo("01_Plaza", conjunto.transform);
                Transform zonaTorre = Grupo("02_Torre", conjunto.transform);
                Transform zonaGuias = Grupo("03_Guias", conjunto.transform);
                Transform zonaSenales = Grupo("04_Senales", conjunto.transform);

                Modelo pavimento = new Modelo("Pavimento_Plaza", cubo, carpeta, zonaPlaza);
                for (int x = 0; x < 4; x++)
                    for (int z = 0; z < 4; z++)
                        pavimento.Caja(losa, V(-7.5f + 5 * x, 0.065f, -7.5f + 5 * z), V(4.94f, 0.04f, 4.94f));
                pavimento.Colision("Superficie_Plaza", V(0, 0.0625f, 0), V(20, 0.045f, 20));
                pavimento.Terminar();

                Modelo jardinera = new Modelo("Jardinera_Arbol", cubo, carpeta, temporales.transform);
                jardinera.Caja(titanio, V(0, 0.35f, 0), V(3.6f, 0.7f, 2.8f));
                jardinera.Caja(tierra, V(0, 0.72f, 0), V(3.2f, 0.06f, 2.4f));
                jardinera.Caja(madera, V(0, 1.35f, 0), V(0.22f, 1.2f, 0.22f));
                jardinera.Agregar(verde, hojas, V(0, 2.35f, 0), V(1.85f, 1.9f, 1.65f), Quaternion.Euler(0, 18, 0));
                jardinera.Agregar(verdeClaro, hojas, V(0.45f, 2.7f, 0.15f), V(1.05f, 1.1f, 1.05f), Quaternion.Euler(0, 42, 0));
                foreach (float x in new[] { -1.15f, 1.15f })
                    foreach (float z in new[] { -0.7f, 0.7f })
                        jardinera.Caja(verdeClaro, V(x, 0.88f, z), V(0.5f, 0.26f, 0.5f));
                foreach (float z in new[] { -1.415f, 1.415f })
                    jardinera.Caja(cian, V(0, 0.18f, z), V(3.25f, 0.055f, 0.025f));
                jardinera.Colision("Colision_Jardinera", V(0, 0.35f, 0), V(3.6f, 0.7f, 2.8f));
                jardinera.Colision("Colision_Tronco", V(0, 1.35f, 0), V(0.22f, 1.2f, 0.22f));
                GameObject prefabJardinera = GuardarPrefab(jardinera, carpeta);
                int numero = 0;
                foreach (float x in new[] { -7.3f, 7.3f })
                    foreach (float z in new[] { -6.8f, 6.8f })
                        Copia(prefabJardinera, zonaPlaza, escena, "Jardinera_" + (++numero), V(x, SueloPlaza, z), 0);

                Modelo banco = new Modelo("Banco_Modular", cubo, carpeta, temporales.transform);
                foreach (float x in new[] { -1.05f, 1.05f })
                    banco.Caja(titanio, V(x, 0.24f, 0), V(0.16f, 0.48f, 0.7f));
                for (int i = 0; i < 3; i++)
                    banco.Caja(madera, V(0, 0.54f, -0.24f + 0.24f * i), V(2.7f, 0.12f, 0.21f));
                banco.Caja(madera, V(0, 0.95f, 0.36f), V(2.7f, 0.6f, 0.12f));
                banco.Caja(titanio, V(0, 0.95f, 0.43f), V(2.7f, 0.08f, 0.08f));
                banco.Colision("Colision_Asiento", V(0, 0.3f, 0), V(2.7f, 0.6f, 0.72f));
                banco.Colision("Colision_Respaldo", V(0, 0.95f, 0.36f), V(2.7f, 0.6f, 0.12f));
                GameObject prefabBanco = GuardarPrefab(banco, carpeta);
                Copia(prefabBanco, zonaPlaza, escena, "Banco_Izquierdo", V(-7.5f, SueloPlaza, 0), -90);
                Copia(prefabBanco, zonaPlaza, escena, "Banco_Derecho", V(7.5f, SueloPlaza, 0), 90);

                Modelo farola = new Modelo("Farola_Modular", cubo, carpeta, temporales.transform);
                farola.Caja(titanio, V(0, 0.075f, 0), V(0.55f, 0.15f, 0.55f));
                farola.Caja(fachada, V(0, 1.75f, 0), V(0.16f, 3.2f, 0.16f));
                farola.Caja(titanio, V(0, 3.4f, -0.35f), V(0.6f, 0.18f, 1.2f));
                farola.Caja(ambar, V(0, 3.295f, -0.42f), V(0.5f, 0.03f, 0.9f));
                farola.Colision("Colision_Poste", V(0, 1.75f, 0), V(0.16f, 3.2f, 0.16f));
                farola.Colision("Colision_Base", V(0, 0.075f, 0), V(0.55f, 0.15f, 0.55f));
                Grupo("Punto_Luz", farola.raiz.transform).localPosition = V(0, 3.18f, -0.42f);
                GameObject prefabFarola = GuardarPrefab(farola, carpeta);
                numero = 0;
                foreach (float x in new[] { -9.25f, 9.25f })
                    foreach (float z in new[] { -3.8f, 3.8f })
                        Copia(prefabFarola, zonaPlaza, escena, "Farola_" + (++numero), V(x, SueloPlaza, z), x < 0 ? -90 : 90);

                CrearTorre(cubo, carpeta, zonaTorre, titanio, vidrio, cian, camino, plaza, plano, cartelTorre);
                CrearTotem("Panel_Plaza", V(-5.4f, SueloPlaza, -8.9f), cubo, carpeta,
                    zonaSenales, fachada, titanio, cian, plano, cartelPlaza);
                CrearTotem("Panel_Torre", V(5.4f, SueloPlaza, -8.9f), cubo, carpeta,
                    zonaSenales, fachada, titanio, cian, plano, cartelTorre);

                Guia("Guia_Portal_Izquierda", -3.85f, -20, 20, 0.07f, nombres, cubo, cian, carpeta, zonaGuias);
                Guia("Guia_Portal_Derecha", 3.85f, -20, 20, 0.07f, nombres, cubo, cian, carpeta, zonaGuias);
                Guia("Guia_Torre_Izquierda", -3.85f, 18, 16, 0.07f, nombres, cubo, cian, carpeta, zonaGuias);
                Guia("Guia_Torre_Derecha", 3.85f, 18, 16, 0.07f, nombres, cubo, cian, carpeta, zonaGuias);
                Guia("Guia_Plaza_Izquierda", -3.85f, 0, 20, 0.105f, nombres, cubo, cian, carpeta, zonaGuias);
                Guia("Guia_Plaza_Derecha", 3.85f, 0, 20, 0.105f, nombres, cubo, cian, carpeta, zonaGuias);
                Modelo flechas = new Modelo("Flechas_Recorrido", cubo, carpeta, zonaGuias);
                foreach (float z in new[] { -20f, 0, 14f })
                {
                    float y = z == 0 ? 0.105f : 0.07f;
                    flechas.Caja(cian, V(0, y, z), V(0.12f, 0.025f, 1.6f));
                    flechas.Agregar(cian, cubo, V(-0.28f, y, z + 0.52f), V(0.12f, 0.025f, 0.8f), Quaternion.Euler(0, 45, 0));
                    flechas.Agregar(cian, cubo, V(0.28f, y, z + 0.52f), V(0.12f, 0.025f, 0.8f), Quaternion.Euler(0, -45, 0));
                }
                flechas.Terminar();

                AssetDatabase.SaveAssets();
                Undo.IncrementCurrentGroup();
                int grupoUndo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("TA5 - Detalles de plaza y torre");
                Undo.RegisterCreatedObjectUndo(conjunto, "TA5 - Detalles de plaza y torre");
                Undo.CollapseUndoOperations(grupoUndo);
                EditorSceneManager.MarkSceneDirty(escena);
                completado = true;
                Selection.activeGameObject = conjunto;
                EditorGUIUtility.PingObject(conjunto);
                Debug.Log("Ciudad: plaza, mobiliario, torre, senales y guias creados. Recursos: "
                    + carpeta + ".", conjunto);
                Aviso("Detalles creados en 05_Detalles_Ciudad.\n\nIncluye 4 jardineras, 2 bancas, 4 farolas, 2 paneles, torre y guias.");
            }
            catch (Exception error)
            {
                if (!completado && conjunto != null) UnityEngine.Object.DestroyImmediate(conjunto);
                Debug.LogException(error);
                Aviso("No se pudo completar la creacion.\n\n" + error.Message);
            }
            finally
            {
                if (temporales != null) UnityEngine.Object.DestroyImmediate(temporales);
                if (!completado && !string.IsNullOrEmpty(carpeta)) AssetDatabase.DeleteAsset(carpeta);
            }
        }

        private static void CrearTorre(Mesh cubo, string carpeta, Transform padre,
            Material metal, Material vidrio, Material cian, Material camino, Material plaza,
            Mesh plano, Material cartel)
        {
            Modelo torre = new Modelo("Fachada_Mirador", cubo, carpeta, padre);
            torre.raiz.transform.localPosition = V(0, 0, 30);
            foreach (float x in new[] { -4.12f, 4.12f })
                foreach (float z in new[] { -4.12f, 4.12f })
                    torre.Caja(metal, V(x, 9.05f, z), V(0.24f, 17.8f, 0.24f));
            foreach (float signo in new[] { -1f, 1f })
            {
                torre.Caja(vidrio, V(0, 10, signo * 4.035f), V(5.8f, 14, 0.05f));
                torre.Caja(vidrio, V(signo * 4.035f, 10, 0), V(0.05f, 14, 5.8f));
                torre.Caja(vidrio, V(0, 19, signo * 6.04f), V(11.2f, 1.2f, 0.06f));
                torre.Caja(vidrio, V(signo * 6.04f, 19, 0), V(0.06f, 1.2f, 11.2f));
                torre.Caja(cian, V(0, 19.8f, signo * 6.085f), V(12, 0.09f, 0.04f));
                torre.Caja(cian, V(signo * 6.085f, 19.8f, 0), V(0.04f, 0.09f, 12));
                foreach (float a in new[] { -3.7f, 3.7f })
                {
                    torre.Caja(cian, V(a, 9, signo * 4.09f), V(0.07f, 17.4f, 0.035f));
                    torre.Caja(cian, V(signo * 4.09f, 9, a), V(0.035f, 17.4f, 0.07f));
                }
                foreach (float a in new[] { -4.48f, -2.24f, 0, 2.24f, 4.48f })
                {
                    torre.Caja(metal, V(a, 19, signo * 6.09f), V(0.07f, 1.2f, 0.035f));
                    torre.Caja(metal, V(signo * 6.09f, 19, a), V(0.035f, 1.2f, 0.07f));
                }
                foreach (float y in new[] { 5.3f, 9f, 12.7f, 16.4f })
                {
                    torre.Caja(metal, V(0, y, signo * 4.075f), V(5.8f, 0.065f, 0.035f));
                    torre.Caja(metal, V(signo * 4.075f, y, 0), V(0.035f, 0.065f, 5.8f));
                }
            }

            torre.Caja(vidrio, V(0, 1.6f, -4.08f), V(3.2f, 3.2f, 0.12f));
            foreach (float x in new[] { -1.7f, 1.7f })
                torre.Caja(metal, V(x, 1.7f, -4.15f), V(0.14f, 3.4f, 0.14f));
            torre.Caja(metal, V(0, 3.4f, -4.15f), V(3.54f, 0.14f, 0.14f));
            torre.Caja(metal, V(0, 1.6f, -4.155f), V(0.07f, 3.2f, 0.035f));
            torre.Caja(cian, V(0, 3.48f, -4.19f), V(3.3f, 0.055f, 0.035f));
            torre.Caja(metal, V(0, 4, -4.19f), V(3.54f, 0.99f, 0.08f));
            torre.Caja(metal, V(0, 20.075f, 0), V(0.8f, 0.15f, 0.8f));
            torre.Caja(metal, V(0, 21.05f, 0), V(0.14f, 1.8f, 0.14f));
            torre.Caja(cian, V(0, 22.05f, 0), V(0.32f, 0.2f, 0.32f));

            torre.Colision("Colision_Marco_Acceso", V(0, 1.7f, -4.11f), V(3.54f, 3.4f, 0.22f));
            torre.Terminar();
            Rotulo("Rotulo_Torre", torre.raiz.transform, plano, cartel, V(0, 4, -4.24f), 3.4f, 0.85f);

            Modelo llegada = new Modelo("Plataforma_Llegada", cubo, carpeta, padre);
            llegada.Caja(plaza, V(0, 0.055f, 23), V(12, 0.03f, 6));
            llegada.Colision("Superficie_Llegada", V(0, 0.055f, 23), V(12, 0.03f, 6));
            foreach (float x in new[] { -5.85f, 5.85f })
                llegada.Caja(cian, V(x, 0.09f, 23), V(0.07f, 0.025f, 5.8f));
            llegada.Caja(cian, V(0, 0.09f, 20.1f), V(11.7f, 0.025f, 0.07f));
            llegada.Caja(camino, V(0, 0.082f, 23), V(4, 0.02f, 2));
            foreach (float x in new[] { -2f, 2f })
                llegada.Caja(cian, V(x, 0.11f, 23), V(0.06f, 0.025f, 2));
            foreach (float z in new[] { 22f, 24f })
                llegada.Caja(cian, V(0, 0.11f, z), V(4, 0.025f, 0.06f));
            llegada.Terminar();
        }

        private static void CrearTotem(string nombre, Vector3 posicion, Mesh cubo,
            string carpeta, Transform padre, Material fondo, Material metal, Material cian,
            Mesh plano, Material cartel)
        {
            Modelo panel = new Modelo(nombre, cubo, carpeta, padre);
            panel.raiz.transform.localPosition = posicion;
            panel.Caja(metal, V(0, 0.06f, 0), V(1.1f, 0.12f, 0.65f));
            panel.Caja(fondo, V(0, 1.2f, 0), V(0.95f, 2.2f, 0.4f));
            foreach (float x in new[] { -0.48f, 0.48f })
                panel.Caja(metal, V(x, 1.2f, -0.225f), V(0.055f, 2.2f, 0.05f));
            panel.Caja(cian, V(0, 1.05f, -0.235f), V(0.075f, 0.55f, 0.025f));
            panel.Agregar(cian, cubo, V(-0.105f, 1.33f, -0.235f), V(0.075f, 0.3f, 0.025f), Quaternion.Euler(0, 0, -45));
            panel.Agregar(cian, cubo, V(0.105f, 1.33f, -0.235f), V(0.075f, 0.3f, 0.025f), Quaternion.Euler(0, 0, 45));
            panel.Caja(cian, V(0, 0.48f, -0.235f), V(0.65f, 0.035f, 0.025f));
            panel.Colision("Colision_Panel", V(0, 1.2f, 0), V(1.1f, 2.2f, 0.5f));
            panel.Colision("Colision_Pie", V(0, 0.06f, 0), V(1.1f, 0.12f, 0.65f));
            panel.Terminar();
            Rotulo("Rotulo", panel.raiz.transform, plano, cartel, V(0, 1.85f, -0.26f), 0.86f, 0.215f);
        }

        private static void Guia(string nombre, float x, float z, float largo, float y,
            HashSet<string> existentes, Mesh cubo, Material material, string carpeta, Transform padre)
        {
            if (existentes.Contains(nombre)) return;
            Modelo modelo = new Modelo(nombre, cubo, carpeta, padre);
            modelo.Caja(material, V(x, y, z), V(0.12f, 0.03f, largo));
            modelo.Terminar();
        }

        private static GameObject GuardarPrefab(Modelo modelo, string carpeta)
        {
            modelo.Terminar();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(modelo.raiz,
                carpeta + "/" + modelo.raiz.name + ".prefab", out bool guardado);
            if (!guardado || prefab == null) throw new InvalidOperationException("No se pudo guardar " + modelo.raiz.name);
            return prefab;
        }

        private static void Copia(GameObject prefab, Transform padre, Scene escena,
            string nombre, Vector3 posicion, float giro)
        {
            GameObject objeto = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
            if (objeto == null) throw new InvalidOperationException("No se pudo crear " + nombre);
            objeto.transform.SetParent(padre, false);
            objeto.name = nombre;
            objeto.transform.localPosition = posicion;
            objeto.transform.localRotation = Quaternion.Euler(0, giro, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(objeto);
            PrefabUtility.RecordPrefabInstancePropertyModifications(objeto.transform);
        }

        private static Material MaterialLit(Shader shader, string carpeta, string nombre,
            string color, float metalico, float suavidad)
        {
            Material material = new Material(shader) { name = nombre, enableInstancing = true };
            material.SetColor("_BaseColor", ColorHex(color));
            material.SetFloat("_Surface", 0);
            material.SetFloat("_Metallic", metalico);
            material.SetFloat("_Smoothness", suavidad);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            AssetDatabase.CreateAsset(material, carpeta + "/Materiales/" + nombre + ".mat");
            return material;
        }

        private static Material CrearRotulo(Shader shader, string carpeta, string palabra)
        {
            const int ancho = 256, alto = 64, escala = 6;
            Dictionary<char, string> letras = new Dictionary<char, string>
            {
                {'P', "11110/10001/10001/11110/10000/10000/10000"},
                {'L', "10000/10000/10000/10000/10000/10000/11111"},
                {'A', "01110/10001/10001/11111/10001/10001/10001"},
                {'Z', "11111/00001/00010/00100/01000/10000/11111"},
                {'T', "11111/00100/00100/00100/00100/00100/00100"},
                {'O', "01110/10001/10001/10001/10001/10001/01110"},
                {'R', "11110/10001/10001/11110/10100/10010/10001"},
                {'E', "11111/10000/10000/11110/10000/10000/11111"}
            };
            Color32 fondo = ColorHex("132634");
            Color32 tinta = ColorHex("D6F9FF");
            Color32 borde = ColorHex("12D8F0");
            Color32[] pixeles = new Color32[ancho * alto];
            for (int i = 0; i < pixeles.Length; i++) pixeles[i] = fondo;
            for (int y = 0; y < alto; y++)
                for (int x = 0; x < ancho; x++)
                    if (x < 2 || x >= ancho - 2 || y < 2 || y >= alto - 2)
                        pixeles[y * ancho + x] = borde;
            int inicioX = (ancho - (palabra.Length * 6 - 1) * escala) / 2;
            int inicioY = (alto - 7 * escala) / 2;
            for (int letra = 0; letra < palabra.Length; letra++)
            {
                string[] filas = letras[palabra[letra]].Split('/');
                for (int fila = 0; fila < 7; fila++)
                    for (int columna = 0; columna < 5; columna++)
                    {
                        if (filas[fila][columna] != '1') continue;
                        for (int py = 0; py < escala; py++)
                            for (int px = 0; px < escala; px++)
                            {
                                int x = inicioX + letra * 6 * escala + columna * escala + px;
                                int y = inicioY + (6 - fila) * escala + py;
                                pixeles[y * ancho + x] = tinta;
                            }
                    }
            }
            Texture2D textura = new Texture2D(ancho, alto, TextureFormat.RGBA32, true)
            {
                name = "Senal_" + palabra, filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            textura.SetPixels32(pixeles);
            textura.Apply(true, false);
            AssetDatabase.CreateAsset(textura, carpeta + "/Senales/" + textura.name + ".asset");
            Material material = new Material(shader) { name = "M_Senal_" + palabra };
            material.SetTexture("_BaseMap", textura);
            material.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(material, carpeta + "/Senales/" + material.name + ".mat");
            return material;
        }

        private static void Rotulo(string nombre, Transform padre, Mesh plano,
            Material material, Vector3 posicion, float ancho, float alto)
        {
            GameObject objeto = new GameObject(nombre);
            objeto.transform.SetParent(padre, false);
            objeto.transform.localPosition = posicion;
            objeto.transform.localScale = V(ancho, alto, 1);
            objeto.AddComponent<MeshFilter>().sharedMesh = plano;
            MeshRenderer renderer = objeto.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh CrearPlano()
        {
            Mesh malla = new Mesh { name = "Plano_Senal" };
            malla.vertices = new[] { V(-0.5f,-0.5f,0), V(0.5f,-0.5f,0), V(0.5f,0.5f,0), V(-0.5f,0.5f,0) };
            malla.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
            malla.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            malla.RecalculateNormals();
            malla.RecalculateBounds();
            return malla;
        }

        private static Mesh CrearFollaje()
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            Vector3[] puntos = { V(-1,t,0), V(1,t,0), V(-1,-t,0), V(1,-t,0),
                V(0,-1,t), V(0,1,t), V(0,-1,-t), V(0,1,-t), V(t,0,-1), V(t,0,1), V(-t,0,-1), V(-t,0,1) };
            int[] indices = { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            Vector3[] vertices = new Vector3[indices.Length];
            int[] triangulos = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                vertices[i] = puntos[indices[i]].normalized * 0.5f;
                triangulos[i] = i;
            }
            Mesh malla = new Mesh { name = "Follaje_Base" };
            malla.vertices = vertices;
            malla.triangles = triangulos;
            malla.RecalculateNormals();
            malla.RecalculateBounds();
            return malla;
        }

        private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }
        private static Transform Grupo(string nombre, Transform padre)
        {
            Transform t = new GameObject(nombre).transform;
            t.SetParent(padre, false);
            return t;
        }
        private static Material LeerMaterial(string nombre)
        {
            return AssetDatabase.LoadAssetAtPath<Material>("Assets/_TA5/Materials/" + nombre + ".mat");
        }
        private static Color ColorHex(string valor)
        {
            if (!ColorUtility.TryParseHtmlString("#" + valor, out Color color))
                throw new ArgumentException("Color no valido: " + valor);
            return color;
        }
        private static void CrearCarpeta(string ruta)
        {
            if (AssetDatabase.IsValidFolder(ruta)) return;
            int i = ruta.LastIndexOf('/');
            string padre = ruta.Substring(0, i);
            CrearCarpeta(padre);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(padre, ruta.Substring(i + 1))))
                throw new InvalidOperationException("No se pudo crear " + ruta);
        }
        private static void Aviso(string texto)
        {
            EditorUtility.DisplayDialog("TA5 - Detalles de ciudad", texto, "Aceptar");
        }
    }
}
#endif
