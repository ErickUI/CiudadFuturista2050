using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using ToqueMejorado = UnityEngine.InputSystem.EnhancedTouch.Touch;
using SoporteToques = UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport;
#endif

namespace TA5.Navegacion
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class TA5ControlRecorrido : MonoBehaviour
    {
        [Header("Camara y movimiento")]
        public Camera camaraVista;
        [Range(1f, 5f)] public float velocidad = 3f;
        [Range(0.03f, 0.4f)] public float sensibilidadRaton = 0.12f;
        [Range(90f, 360f)] public float sensibilidadTactil = 180f;
        [Range(-30f, 30f)] public float inclinacionInicial = -8f;
        [Header("Controles en pantalla")]
        public bool mostrarControlesEnPC = true;

        private const int RatonId = -2;
        private const float ZonaMuerta = 0.12f;
        private CharacterController controlador;
        private Vector3 inicio;
        private float giroInicial, giro, inclinacion, velocidadVertical;
        private Vector2 joystick, teclado, mirada;
        private int dedoMovimiento = -1, dedoMirada = -1;
        private bool reiniciar, mirandoConRaton, ignorarDerecho;
        private Rect zonaSegura, zonaJoystick, zonaMirada, botonVolver;
        private Vector2 centroJoystick, ultimaPosicionRaton;
        private float radioJoystick, escalaUI;
        private int anchoAnterior, altoAnterior;
        private Texture2D disco, anillo;
        private GUIStyle titulo, texto, centrado;
#if ENABLE_INPUT_SYSTEM
        private bool soporteAdquirido;
#endif

        private bool ControlesVisibles { get { return Application.isMobilePlatform || mostrarControlesEnPC; } }

        private void Awake()
        {
            controlador = GetComponent<CharacterController>();
            if (camaraVista == null) camaraVista = GetComponentInChildren<Camera>(true);
            if (camaraVista == null)
            {
                Debug.LogError("Recorrido: Camara Vista no esta asignada.", this);
                enabled = false;
                return;
            }
            inicio = transform.position;
            giroInicial = transform.eulerAngles.y;
            giro = giroInicial;
            inclinacion = inclinacionInicial;
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.LandscapeLeft;
            disco = CrearCirculo(false);
            anillo = CrearCirculo(true);
            ActualizarZonas();
            camaraVista.transform.localRotation = Quaternion.Euler(inclinacion, 0, 0);
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            SoporteToques.Enable();
            soporteAdquirido = true;
#elif !ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogError("Recorrido: no hay un sistema de entrada habilitado en Active Input Handling.", this);
#endif
            LimpiarEntradas();
        }

        private void OnDisable()
        {
            LimpiarEntradas();
#if ENABLE_INPUT_SYSTEM
            if (soporteAdquirido) { SoporteToques.Disable(); soporteAdquirido = false; }
#endif
        }

        private void OnDestroy()
        {
            if (disco != null) Destroy(disco);
            if (anillo != null) Destroy(anillo);
        }

        private void OnApplicationFocus(bool foco)
        {
            if (!foco) LimpiarEntradas();
        }

        private void OnApplicationPause(bool pausa)
        {
            if (pausa) LimpiarEntradas();
        }

        private void Update()
        {
            if (controlador == null || camaraVista == null || !controlador.enabled) return;
            if (!Application.isFocused) { LimpiarEntradas(); return; }
            if (Screen.width != anchoAnterior || Screen.height != altoAnterior)
                LimpiarEntradas();
            ActualizarZonas();
            mirada = Vector2.zero;
            teclado = Vector2.zero;
            reiniciar = false;
            bool hayToques = LeerToques();
            if (!Application.isMobilePlatform && !hayToques) LeerPC();
            else if (mirandoConRaton) LiberarRaton();

            if (reiniciar) { VolverAlPortal(); return; }
            giro = Mathf.Repeat(giro + Mathf.Clamp(mirada.x, -90, 90), 360);
            inclinacion = Mathf.Clamp(inclinacion - Mathf.Clamp(mirada.y, -90, 90), -75, 75);
            transform.rotation = Quaternion.Euler(0, giro, 0);
            camaraVista.transform.localRotation = Quaternion.Euler(inclinacion, 0, 0);

            Vector2 entrada = Vector2.ClampMagnitude(teclado + joystick, 1);
            Vector3 horizontal = transform.right * entrada.x + transform.forward * entrada.y;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (controlador.isGrounded && velocidadVertical < 0) velocidadVertical = -2f;
            velocidadVertical = Mathf.Max(velocidadVertical - 18f * dt, -30f);
            CollisionFlags colisiones = controlador.Move((horizontal * velocidad
                + Vector3.up * velocidadVertical) * dt);
            if ((colisiones & CollisionFlags.Below) != 0) velocidadVertical = -2f;
            if (transform.position.y < -5)
            {
                Debug.LogWarning("Recorrido: jugador fuera del suelo; retorno al portal.", this);
                VolverAlPortal();
            }
        }

        private bool LeerToques()
        {
            bool vioMovimiento = false, vioMirada = false;
            int cantidad = 0;
#if ENABLE_INPUT_SYSTEM
            foreach (var toque in ToqueMejorado.activeTouches)
            {
                cantidad++;
                bool comienza = toque.phase == UnityEngine.InputSystem.TouchPhase.Began;
                bool termina = toque.phase == UnityEngine.InputSystem.TouchPhase.Ended
                    || toque.phase == UnityEngine.InputSystem.TouchPhase.Canceled;
                ProcesarPuntero(toque.touchId, toque.screenPosition, toque.delta, comienza, termina);
                if (toque.touchId == dedoMovimiento) vioMovimiento = true;
                if (toque.touchId == dedoMirada) vioMirada = true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch toque = Input.GetTouch(i);
                cantidad++;
                bool comienza = toque.phase == TouchPhase.Began;
                bool termina = toque.phase == TouchPhase.Ended || toque.phase == TouchPhase.Canceled;
                ProcesarPuntero(toque.fingerId, toque.position, toque.deltaPosition, comienza, termina);
                if (toque.fingerId == dedoMovimiento) vioMovimiento = true;
                if (toque.fingerId == dedoMirada) vioMirada = true;
            }
#endif

            if (dedoMovimiento >= 0 && !vioMovimiento) { dedoMovimiento = -1; joystick = Vector2.zero; }
            if (dedoMirada >= 0 && !vioMirada) dedoMirada = -1;
            return cantidad > 0;
        }

        private void LeerPC()
        {
            Vector2 posicion = Vector2.zero, delta = Vector2.zero;
            bool izquierdaInicio = false, izquierda = false, izquierdaFin = false, derecha = false, escape = false;
#if ENABLE_INPUT_SYSTEM
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                teclado.x = ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1 : 0)
                    - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1 : 0);
                teclado.y = ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1 : 0)
                    - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1 : 0);
                reiniciar |= k.rKey.wasPressedThisFrame;
                escape = k.escapeKey.wasPressedThisFrame;
            }
            Mouse m = Mouse.current;
            if (m != null)
            {
                posicion = m.position.ReadValue();
                delta = m.delta.ReadValue();
                izquierdaInicio = m.leftButton.wasPressedThisFrame;
                izquierda = m.leftButton.isPressed;
                izquierdaFin = m.leftButton.wasReleasedThisFrame;
                derecha = m.rightButton.isPressed;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            teclado.x = ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ? 1 : 0)
                - ((Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ? 1 : 0);
            teclado.y = ((Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1 : 0)
                - ((Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) ? 1 : 0);
            reiniciar |= Input.GetKeyDown(KeyCode.R);
            escape = Input.GetKeyDown(KeyCode.Escape);
            posicion = Input.mousePosition;

            delta = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
            izquierdaInicio = Input.GetMouseButtonDown(0);
            izquierda = Input.GetMouseButton(0);
            izquierdaFin = Input.GetMouseButtonUp(0);
            derecha = Input.GetMouseButton(1);
#endif
            if (!derecha) ignorarDerecho = false;
            if (escape) { ignorarDerecho = true; LimpiarPunteros(); LiberarRaton(); }
            if (derecha && !ignorarDerecho)
            {
                LimpiarPunteros();
                if (!mirandoConRaton)
                {
                    mirandoConRaton = true;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else mirada += delta * sensibilidadRaton;
            }
            else
            {
                if (mirandoConRaton) LiberarRaton();
                Vector2 desplazamiento = posicion - ultimaPosicionRaton;
                if (izquierdaInicio || izquierda || izquierdaFin)
                    ProcesarPuntero(RatonId, posicion, desplazamiento, izquierdaInicio, izquierdaFin);
                if (!izquierda && !izquierdaInicio) SoltarPuntero(RatonId);
            }
            ultimaPosicionRaton = posicion;
        }

        private void ProcesarPuntero(int id, Vector2 posicion, Vector2 delta, bool comienza, bool termina)
        {
            if (termina) { SoltarPuntero(id); return; }
            if (comienza)
            {
                if (botonVolver.Contains(posicion)) { reiniciar = true; return; }
                if (ControlesVisibles && zonaJoystick.Contains(posicion) && dedoMovimiento == -1)
                    dedoMovimiento = id;
                else if (zonaMirada.Contains(posicion) && dedoMirada == -1)
                    dedoMirada = id;
            }
            if (id == dedoMovimiento)
            {
                Vector2 valor = Vector2.ClampMagnitude((posicion - centroJoystick) / radioJoystick, 1);
                float fuerza = valor.magnitude;
                joystick = fuerza <= ZonaMuerta ? Vector2.zero
                    : valor.normalized * ((fuerza - ZonaMuerta) / (1 - ZonaMuerta));
            }
            if (id == dedoMirada && !comienza)
                mirada += delta * (sensibilidadTactil / Mathf.Max(zonaSegura.height, 1));
        }

        private void SoltarPuntero(int id)
        {
            if (id == dedoMovimiento) { dedoMovimiento = -1; joystick = Vector2.zero; }
            if (id == dedoMirada) dedoMirada = -1;
        }

        public void VolverAlPortal()
        {
            if (controlador == null) return;
            controlador.enabled = false;
            transform.SetPositionAndRotation(inicio, Quaternion.Euler(0, giroInicial, 0));
            controlador.enabled = true;
            giro = giroInicial;
            inclinacion = inclinacionInicial;
            velocidadVertical = 0;
            camaraVista.transform.localRotation = Quaternion.Euler(inclinacion, 0, 0);
            LimpiarEntradas();
            ignorarDerecho = true;
        }

        private void LimpiarPunteros()
        {
            dedoMovimiento = dedoMirada = -1;
            joystick = Vector2.zero;
        }

        private void LimpiarEntradas()
        {
            LimpiarPunteros();
            teclado = mirada = Vector2.zero;
            LiberarRaton();
        }

        private void LiberarRaton()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            mirandoConRaton = false;
        }

        private void ActualizarZonas()
        {
            anchoAnterior = Screen.width;
            altoAnterior = Screen.height;
            zonaSegura = Screen.safeArea;
            if (zonaSegura.width <= 0 || zonaSegura.height <= 0)
                zonaSegura = new Rect(0, 0, Screen.width, Screen.height);
            escalaUI = Mathf.Clamp(Mathf.Min(zonaSegura.width / 1280f, zonaSegura.height / 720f), 0.65f, 2f);
            float margen = 22 * escalaUI;
            radioJoystick = Mathf.Clamp(Mathf.Min(zonaSegura.width, zonaSegura.height) * 0.115f, 48, 130);
            centroJoystick = new Vector2(zonaSegura.xMin + radioJoystick + margen,
                zonaSegura.yMin + radioJoystick + margen + 16 * escalaUI);
            zonaJoystick = new Rect(centroJoystick.x - radioJoystick * 1.35f,
                centroJoystick.y - radioJoystick * 1.35f, radioJoystick * 2.7f, radioJoystick * 2.7f);
            zonaMirada = new Rect(zonaSegura.xMin + zonaSegura.width * 0.45f,
                zonaSegura.yMin, zonaSegura.width * 0.55f, zonaSegura.height);
            botonVolver = new Rect(zonaSegura.xMax - 180 * escalaUI - margen,
                zonaSegura.yMax - 46 * escalaUI - margen, 180 * escalaUI, 46 * escalaUI);
        }

        private void OnGUI()
        {
            if (camaraVista == null || !camaraVista.isActiveAndEnabled || anillo == null) return;
            if (Event.current.type != EventType.Repaint) return;
            if (titulo == null)
            {
                titulo = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                texto = new GUIStyle(GUI.skin.label);
                centrado = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            }
            titulo.fontSize = Mathf.RoundToInt(18 * escalaUI);
            texto.fontSize = Mathf.RoundToInt(15 * escalaUI);
            centrado.fontSize = Mathf.RoundToInt(16 * escalaUI);
            titulo.normal.textColor = texto.normal.textColor = centrado.normal.textColor = Color.white;
            Color anterior = GUI.color;
            float margen = 22 * escalaUI;
            float anchoHud = Mathf.Min(500 * escalaUI, zonaSegura.width - botonVolver.width - 3 * margen);
            Rect hud = new Rect(zonaSegura.xMin + margen, zonaSegura.yMax - margen - 74 * escalaUI,
                Mathf.Max(200 * escalaUI, anchoHud), 74 * escalaUI);
            Fondo(hud, new Color(0.04f, 0.09f, 0.14f, 0.82f));
            Rect h = ParaGUI(hud);
            GUI.color = Color.white;
            GUI.Label(new Rect(h.x + 12 * escalaUI, h.y + 8 * escalaUI, h.width - 24 * escalaUI, 28 * escalaUI),
                "PORTAL  >  PLAZA  >  TORRE", titulo);
            GUI.Label(new Rect(h.x + 12 * escalaUI, h.y + 38 * escalaUI, h.width - 24 * escalaUI, 26 * escalaUI),
                Application.isMobilePlatform ? "Joystick: caminar | Derecha: mirar" : "WASD: caminar | Clic derecho: mirar | R: volver", texto);
            Fondo(botonVolver, new Color(0.06f, 0.31f, 0.39f, 0.92f));
            GUI.color = Color.white;
            GUI.Label(ParaGUI(botonVolver), "Volver al portal", centrado);

            if (ControlesVisibles)
            {
                Rect baseStick = new Rect(centroJoystick.x - radioJoystick, centroJoystick.y - radioJoystick,
                    radioJoystick * 2, radioJoystick * 2);
                GUI.DrawTexture(ParaGUI(baseStick), anillo);
                float r = radioJoystick * 0.38f;
                Vector2 centro = centroJoystick + joystick * radioJoystick * 0.7f;
                GUI.DrawTexture(ParaGUI(new Rect(centro.x - r, centro.y - r, r * 2, r * 2)), disco);
                GUI.Label(ParaGUI(new Rect(centroJoystick.x - radioJoystick, zonaSegura.yMin + 4 * escalaUI,
                    radioJoystick * 2, 24 * escalaUI)), "CAMINAR", centrado);
                Rect mirar = new Rect(zonaSegura.xMax - 240 * escalaUI - margen,
                    zonaSegura.yMin + margen, 240 * escalaUI, 58 * escalaUI);
                Fondo(mirar, new Color(0.04f, 0.09f, 0.14f, 0.55f));
                GUI.color = Color.white;
                GUI.Label(ParaGUI(mirar), "MIRAR\nArrastra en la mitad derecha", centrado);
            }
            GUI.color = anterior;
        }

        private static Rect ParaGUI(Rect r) { return new Rect(r.x, Screen.height - r.yMax, r.width, r.height); }
        private static void Fondo(Rect r, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(ParaGUI(r), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        private static Texture2D CrearCirculo(bool esAnillo)
        {
            const int n = 128;
            Texture2D textura = new Texture2D(n, n, TextureFormat.RGBA32, false);
            textura.name = esAnillo ? "TA5_Joystick_Base" : "TA5_Joystick_Mando";
            textura.wrapMode = TextureWrapMode.Clamp;
            textura.filterMode = FilterMode.Bilinear;
            Color32[] pixeles = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float distancia = new Vector2(x - 63.5f, y - 63.5f).magnitude / 63.5f;
                    if (distancia > 0.98f) pixeles[y * n + x] = new Color32(0, 0, 0, 0);
                    else if (!esAnillo || distancia > 0.9f) pixeles[y * n + x] = new Color32(30, 218, 239, 205);
                    else pixeles[y * n + x] = new Color32(13, 30, 45, 150);
                }
            textura.SetPixels32(pixeles);
            textura.Apply(false, true);
            return textura;
        }
    }
}
