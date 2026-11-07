using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class CamaraRTS : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad de desplazamiento en unidades por segundo.")]
    [SerializeField] private float velocidad = 20f;
    [Tooltip("Multiplicador al mantener Shift.")]
    [SerializeField] private float multiplicadorShift = 2f;

    [Header("Bordes de pantalla")]
    [SerializeField] private bool moverConBordes = true;
    [Tooltip("Distancia en píxeles al borde de la pantalla para empezar a mover.")]
    [SerializeField] private float margenBorde = 15f;

    [Header("Arrastre con click izquierdo")]
    [SerializeField] private bool moverConClickIzquierdo = true;
    [Tooltip("Sensibilidad del arrastre.")]
    [SerializeField] private float sensibilidadArrastre = 1f;
    [Tooltip("Invertir dirección (mover la cámara hacia donde va el mouse en vez de 'agarrar' el mapa).")]
    [SerializeField] private bool invertirArrastre = false;

    [Header("Rotación con click derecho")]
    [SerializeField] private bool rotarConClickDerecho = true;
    [Tooltip("Grados por píxel de movimiento del mouse (horizontal).")]
    [SerializeField] private float sensibilidadRotacion = 0.25f;
    [Tooltip("Permitir inclinar la cámara moviendo el mouse vertical.")]
    [SerializeField] private bool permitirInclinar = true;
    [Tooltip("Grados por píxel de movimiento del mouse (vertical).")]
    [SerializeField] private float sensibilidadInclinacion = 0.15f;
    [SerializeField] private float inclinacionMinima = 25f;
    [SerializeField] private float inclinacionMaxima = 85f;
    [Tooltip("Altura del piso: la cámara gira alrededor del punto del piso que está mirando.")]
    [SerializeField] private float alturaPiso = 0f;

    [Header("Rotación con Q / E")]
    [SerializeField] private bool rotarConTeclado = true;
    [Tooltip("Grados por segundo al mantener Q o E.")]
    [SerializeField] private float velocidadRotacionTeclado = 90f;

    [Header("Zoom")]
    [SerializeField] private float velocidadZoom = 2f;
    [SerializeField] private float alturaMinima = 5f;
    [SerializeField] private float alturaMaxima = 40f;
    [Tooltip("Suavizado del zoom (más alto = más rápido).")]
    [SerializeField] private float suavizadoZoom = 10f;

    [Header("Límites del mapa (XZ)")]
    [SerializeField] private bool limitar = false;
    [SerializeField] private Vector2 limiteMin = new Vector2(-50f, -50f);
    [SerializeField] private Vector2 limiteMax = new Vector2(50f, 50f);

    private float alturaObjetivo;
    // Sólo arrastrar/rotar si el click empezó fuera de la UI
    private bool arrastreActivo;
    private bool rotacionActiva;

    void Start()
    {
        alturaObjetivo = Mathf.Clamp(transform.position.y, alturaMinima, alturaMaxima);
    }

    void Update()
    {
        bool arrastrando = Arrastrar();
        bool rotando = Rotar();
        RotarConTeclado();
        Mover(arrastrando || rotando);
        Zoom();
    }

    private static bool SobreUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    // Mantener click izquierdo y mover el mouse para desplazar la cámara
    private bool Arrastrar()
    {
        var mouse = Mouse.current;
        if (!moverConClickIzquierdo || mouse == null) return false;

        if (mouse.leftButton.wasPressedThisFrame) arrastreActivo = !SobreUI();
        if (!mouse.leftButton.isPressed) arrastreActivo = false;
        if (!arrastreActivo) return false;

        Vector2 delta = mouse.delta.ReadValue();
        if (delta == Vector2.zero) return true;

        Vector3 adelante = transform.forward; adelante.y = 0f; adelante.Normalize();
        Vector3 derecha  = transform.right;   derecha.y = 0f;  derecha.Normalize();

        // Escalar por la altura para que el mapa siga al cursor a cualquier zoom
        float escala = sensibilidadArrastre * transform.position.y / Screen.height * 2f;
        float signo = invertirArrastre ? 1f : -1f;

        Vector3 mov = (derecha * delta.x + adelante * delta.y) * escala * signo;
        transform.position = Limitar(transform.position + mov);
        return true;
    }

    // Mantener click derecho y mover el mouse para girar la cámara alrededor del punto que mira
    private bool Rotar()
    {
        var mouse = Mouse.current;
        if (!rotarConClickDerecho || mouse == null) return false;

        if (mouse.rightButton.wasPressedThisFrame) rotacionActiva = !SobreUI();
        if (!mouse.rightButton.isPressed) rotacionActiva = false;
        if (!rotacionActiva) return false;

        Vector2 delta = mouse.delta.ReadValue();
        if (delta == Vector2.zero) return true;

        Vector3 pivote = PuntoMirado();

        // Giro horizontal alrededor del eje Y
        transform.RotateAround(pivote, Vector3.up, delta.x * sensibilidadRotacion);

        // Inclinación (con límites)
        if (permitirInclinar)
        {
            float actual = transform.eulerAngles.x;
            if (actual > 180f) actual -= 360f;
            float nueva = Mathf.Clamp(actual - delta.y * sensibilidadInclinacion, inclinacionMinima, inclinacionMaxima);
            float diferencia = nueva - actual;
            if (Mathf.Abs(diferencia) > 0.0001f)
                transform.RotateAround(pivote, transform.right, diferencia);
        }

        // Mantener la altura del zoom aunque la inclinación la cambie
        alturaObjetivo = Mathf.Clamp(transform.position.y, alturaMinima, alturaMaxima);
        transform.position = Limitar(transform.position);
        return true;
    }

    // Q gira a la izquierda y E a la derecha, alrededor del punto del piso que mira la cámara
    private void RotarConTeclado()
    {
        var teclado = Keyboard.current;
        if (!rotarConTeclado || teclado == null) return;

        float dir = 0f;
        if (teclado.qKey.isPressed) dir -= 1f;
        if (teclado.eKey.isPressed) dir += 1f;
        if (dir == 0f) return;

        transform.RotateAround(PuntoMirado(), Vector3.up, dir * velocidadRotacionTeclado * Time.deltaTime);
        transform.position = Limitar(transform.position);
    }

    // Punto del piso en el centro de la pantalla
    private Vector3 PuntoMirado()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        Plane piso = new Plane(Vector3.up, new Vector3(0f, alturaPiso, 0f));
        if (piso.Raycast(ray, out float d)) return ray.GetPoint(d);
        return transform.position + transform.forward * 10f;
    }

    private void Mover(bool arrastrando)
    {
        Vector2 dir = Vector2.zero;
        var teclado = Keyboard.current;

        if (teclado != null)
        {
            if (teclado.upArrowKey.isPressed    || teclado.wKey.isPressed) dir.y += 1f;
            if (teclado.downArrowKey.isPressed  || teclado.sKey.isPressed) dir.y -= 1f;
            if (teclado.rightArrowKey.isPressed || teclado.dKey.isPressed) dir.x += 1f;
            if (teclado.leftArrowKey.isPressed  || teclado.aKey.isPressed) dir.x -= 1f;
        }

        var mouse = Mouse.current;
        if (moverConBordes && !arrastrando && mouse != null && Application.isFocused)
        {
            Vector2 p = mouse.position.ReadValue();
            // Sólo si el mouse está dentro de la ventana
            if (p.x >= 0 && p.y >= 0 && p.x <= Screen.width && p.y <= Screen.height)
            {
                if (p.x <= margenBorde) dir.x -= 1f;
                if (p.x >= Screen.width - margenBorde) dir.x += 1f;
                if (p.y <= margenBorde) dir.y -= 1f;
                if (p.y >= Screen.height - margenBorde) dir.y += 1f;
            }
        }

        if (dir == Vector2.zero) return;
        dir = Vector2.ClampMagnitude(dir, 1f);

        // Mover en el plano XZ según hacia dónde mira la cámara (ignorando la inclinación)
        Vector3 adelante = transform.forward; adelante.y = 0f; adelante.Normalize();
        Vector3 derecha  = transform.right;   derecha.y = 0f;  derecha.Normalize();

        float vel = velocidad;
        if (teclado != null && teclado.leftShiftKey.isPressed) vel *= multiplicadorShift;
        // Más alto = se mueve más rápido, para que se sienta igual en cualquier zoom
        vel *= Mathf.Max(0.5f, transform.position.y / 20f);

        Vector3 pos = transform.position + (adelante * dir.y + derecha * dir.x) * vel * Time.deltaTime;
        transform.position = Limitar(pos);
    }

    private void Zoom()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                alturaObjetivo -= Mathf.Sign(scroll) * velocidadZoom;
        }

        var teclado = Keyboard.current;
        if (teclado != null)
        {
            if (teclado.equalsKey.isPressed || teclado.numpadPlusKey.isPressed)  alturaObjetivo -= velocidadZoom * 10f * Time.deltaTime;
            if (teclado.minusKey.isPressed  || teclado.numpadMinusKey.isPressed) alturaObjetivo += velocidadZoom * 10f * Time.deltaTime;
        }

        alturaObjetivo = Mathf.Clamp(alturaObjetivo, alturaMinima, alturaMaxima);

        float alturaActual = transform.position.y;
        float nuevaAltura = Mathf.Lerp(alturaActual, alturaObjetivo, 1f - Mathf.Exp(-suavizadoZoom * Time.deltaTime));
        float deltaAltura = nuevaAltura - alturaActual;
        if (Mathf.Abs(deltaAltura) < 0.0001f) return;

        // Acercarse/alejarse sobre la línea de visión de la cámara, para que el centro de la vista no se corra
        Vector3 f = transform.forward;
        if (f.y < -0.01f)
            transform.position = Limitar(transform.position + f * (deltaAltura / f.y));
        else
            transform.position = Limitar(transform.position + Vector3.up * deltaAltura);
    }

    private Vector3 Limitar(Vector3 pos)
    {
        if (!limitar) return pos;
        pos.x = Mathf.Clamp(pos.x, limiteMin.x, limiteMax.x);
        pos.z = Mathf.Clamp(pos.z, limiteMin.y, limiteMax.y);
        return pos;
    }
}
