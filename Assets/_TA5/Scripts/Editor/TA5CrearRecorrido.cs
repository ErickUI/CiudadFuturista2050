#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TA5.Navegacion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TA5.EditorTools
{
    public static class TA5CrearRecorrido
    {
        private const string Grupo = "08_Navegacion";

        [MenuItem("TA5/Crear recorrido PC y movil", false, 200)]
        public static void Crear()
        {
            if (!Lista(out Scene escena)) return;
            if (Buscar(escena, Grupo) != null)
            { Aviso("08_Navegacion ya existe. No se ha creado otra copia."); return; }
            GameObject general = Buscar(escena, "Main Camera");
            GameObject suelo = Buscar(escena, "Piso_Ciudad");
            Camera camaraGeneral = general != null ? general.GetComponent<Camera>() : null;
            Collider sueloCollider = suelo != null ? suelo.GetComponent<Collider>() : null;
            if (camaraGeneral == null || sueloCollider == null || !sueloCollider.enabled
                || !suelo.activeInHierarchy || sueloCollider.isTrigger)
            { Aviso("Se necesita Main Camera y un collider activo, sin Is Trigger, en Piso_Ciudad."); return; }

            if (!ValidarLimites(escena)) return;

            GameObject conjunto = null;
            int grupoUndo = -1;
            bool terminado = false;
            try
            {
                conjunto = new GameObject(Grupo);
                SceneManager.MoveGameObjectToScene(conjunto, escena);
                GameObject jugador = new GameObject("Jugador_PC_Movil");
                jugador.transform.SetParent(conjunto.transform, false);
                jugador.transform.localPosition = new Vector3(0, 0.08f, -38);
                CharacterController cuerpo = jugador.AddComponent<CharacterController>();
                cuerpo.height = 1.8f;
                cuerpo.radius = 0.3f;
                cuerpo.center = new Vector3(0, 0.9f, 0);
                cuerpo.stepOffset = 0.25f;
                cuerpo.slopeLimit = 45;
                cuerpo.skinWidth = 0.035f;
                cuerpo.minMoveDistance = 0;

                GameObject vista = UnityEngine.Object.Instantiate(general, jugador.transform, false);
                vista.name = "Camara_Recorrido";
                vista.tag = "MainCamera";
                vista.transform.localPosition = new Vector3(0, 1.65f, 0);
                vista.transform.localRotation = Quaternion.Euler(-8, 0, 0);
                vista.transform.localScale = Vector3.one;
                vista.SetActive(true);
                Camera camara = vista.GetComponent<Camera>();
                camara.enabled = true;
                camara.fieldOfView = 65;
                camara.nearClipPlane = 0.05f;
                camara.farClipPlane = 200;
                AudioListener audio = vista.GetComponent<AudioListener>();
                if (audio == null) audio = vista.AddComponent<AudioListener>();
                audio.enabled = true;
                TA5ControlRecorrido control = jugador.AddComponent<TA5ControlRecorrido>();
                control.camaraVista = camara;
                control.velocidad = 3;
                control.inclinacionInicial = -8;
                control.mostrarControlesEnPC = true;

                Undo.IncrementCurrentGroup();
                grupoUndo = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("TA5 - Crear recorrido PC y movil");
                Undo.RegisterCreatedObjectUndo(conjunto, "TA5 - Jugador y controles");
                Undo.RecordObject(general, "TA5 - Desactivar vista general");
                general.SetActive(false);
                Undo.CollapseUndoOperations(grupoUndo);
                EditorSceneManager.MarkSceneDirty(escena);
                terminado = true;
                Selection.activeGameObject = jugador;
                Debug.Log("Recorrido PC y movil creado en 08_Navegacion.", jugador);
                Aviso("Recorrido PC y movil creado en 08_Navegacion.");
            }
            catch (Exception error)
            {
                if (!terminado)
                {
                    if (grupoUndo >= 0) Undo.RevertAllDownToGroup(grupoUndo);
                    if (conjunto != null) UnityEngine.Object.DestroyImmediate(conjunto);
                }
                Debug.LogException(error);
                Aviso("No se pudo crear el recorrido.\n\n" + error.Message);
            }
        }

        [MenuItem("TA5/Navegacion/Mostrar vista general", false, 201)]
        public static void VistaGeneral() { CambiarModo(false); }

        [MenuItem("TA5/Navegacion/Activar recorrido PC y movil", false, 202)]
        public static void VistaRecorrido() { CambiarModo(true); }

        private static void CambiarModo(bool recorrido)
        {
            if (!Lista(out Scene escena)) return;
            GameObject general = Buscar(escena, "Main Camera");
            GameObject navegacion = Buscar(escena, Grupo);
            if (general == null || navegacion == null)
            { Aviso("Faltan objetos requeridos: Main Camera o 08_Navegacion."); return; }
            Undo.IncrementCurrentGroup();
            int grupoUndo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("TA5 - Cambiar vista");
            Undo.RecordObject(general, "TA5 - Vista general");
            Undo.RecordObject(navegacion, "TA5 - Vista de recorrido");
            general.SetActive(!recorrido);
            navegacion.SetActive(recorrido);
            Undo.CollapseUndoOperations(grupoUndo);
            EditorSceneManager.MarkSceneDirty(escena);
        }

        private static bool ValidarLimites(Scene escena)
        {
            string[] nombres = { "Limite_Izquierdo", "Limite_Derecho", "Limite_Entrada", "Limite_Fondo" };
            var problemas = new List<string>();
            foreach (string nombre in nombres)
            {
                GameObject limite = Buscar(escena, nombre);
                if (limite == null)
                { problemas.Add(nombre + ": no se encontro el objeto."); continue; }
                bool tieneColision = false;
                foreach (Collider colision in limite.GetComponentsInChildren<Collider>(true))
                    if (colision.enabled && colision.gameObject.activeInHierarchy && !colision.isTrigger)
                    { tieneColision = true; break; }
                if (!tieneColision)
                    problemas.Add(nombre + ": necesita un collider activo, sin Is Trigger.");
            }
            if (problemas.Count == 0) return true;
            Aviso("Barreras no validas:\n\n"
                + string.Join("\n", problemas.ToArray())
                + "\n\nNo se ha cambiado la escena.");
            return false;
        }

        private static bool Lista(out Scene escena)
        {
            escena = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            { Aviso("Operacion no disponible durante Play o la compilacion."); return false; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null || !escena.IsValid()
                || !escena.isLoaded || escena.name != "CiudadFuturista2050")
            { Aviso("Se requiere la escena CiudadFuturista2050 cargada fuera del modo Prefab."); return false; }
            return true;
        }
        private static GameObject Buscar(Scene escena, string nombre)
        {
            foreach (GameObject raiz in escena.GetRootGameObjects())
                foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                    if (t.name == nombre) return t.gameObject;
            return null;
        }
        private static void Aviso(string texto)
        { EditorUtility.DisplayDialog("TA5 - Recorrido PC y movil", texto, "Aceptar"); }
    }
}
#endif
