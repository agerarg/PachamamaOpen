using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Click en un worker: lo selecciona y muestra el tip siguiéndolo (con lo que está haciendo).
/// Click en otro lado: deselecciona y oculta el tip.
/// Mouse encima de un elemento de UI con ToolTipInUi: muestra su ShowText() junto al mouse (tiene prioridad).
/// Va dentro del Canvas; el tip se posiciona sobre el worker convirtiendo su posición a pantalla.
/// </summary>
public class TipOnMouse : MonoBehaviour
{
    public TextMeshProUGUI whatDoingText;
    public GameObject tipObj;

    [Tooltip("Desplazamiento en el mundo respecto al worker (por ejemplo, arriba de la cabeza).")]
    public Vector3 offsetMundo = new Vector3(0f, 2f, 0f);
    [Tooltip("Desplazamiento extra en píxeles de pantalla.")]
    public Vector2 offsetPantalla = Vector2.zero;
    [Tooltip("Si el mouse se mueve más que esto (en píxeles) entre apretar y soltar, cuenta como arrastre de cámara y no como click.")]
    public float toleranciaClick = 6f;
    [Tooltip("Desplazamiento en píxeles del tip respecto al mouse cuando muestra un tooltip de UI.")]
    public Vector2 offsetMouseUI = new Vector2(16f, -16f);

    private Camera cam;
    private Worker seleccionado;
    private string textoActual;
    private Vector2 posAlApretar;
    private bool clickValido;
    private PointerEventData pointerData;
    private EventSystem pointerDataEventSystem; // con qué EventSystem se creó pointerData
    private readonly List<RaycastResult> resultadosUI = new List<RaycastResult>();

    /// <summary>El worker seleccionado (o null).</summary>
    public Worker Seleccionado => seleccionado;

    void Awake()
    {
        cam = Camera.main;
        if (tipObj == gameObject)
            Debug.LogWarning("TipOnMouse: tipObj no puede ser el mismo objeto que tiene este script (al ocultarlo se apagaría el script). Usá un hijo.", this);
        else if (tipObj != null)
            tipObj.SetActive(false);
    }

    void Update()
    {
        LeerClick();
    }

    // En LateUpdate para seguir al worker después de que se movió en este frame
    void LateUpdate()
    {
        // 1) Tooltip de UI (prioridad sobre el del worker)
        ToolTipInUi ui = UIConTooltipBajoMouse();
        if (ui != null && Mouse.current != null)
        {
            MostrarTexto(ui.ShowText());
            PosicionarEnPantalla(Mouse.current.position.ReadValue() + offsetMouseUI);
            return;
        }

        // 2) Tooltip del worker seleccionado
        if (seleccionado == null)
        {
            Ocultar();
            return;
        }

        if (!Posicionar(seleccionado.transform.position + offsetMundo))
        {
            // Detrás de la cámara: ocultar sin perder la selección
            if (tipObj != null && tipObj.activeSelf) tipObj.SetActive(false);
            return;
        }

        MostrarTexto(seleccionado.TextoEstado);
    }

    private void MostrarTexto(string texto)
    {
        if (tipObj != null && !tipObj.activeSelf) tipObj.SetActive(true);

        // Sólo tocar el texto si cambió, para no regenerar la malla de TMP cada frame
        if (whatDoingText != null && texto != textoActual)
        {
            textoActual = texto;
            whatDoingText.text = texto;
        }
    }

    // ---------- UI ----------

    // Busca entre los elementos de UI bajo el mouse el primero (o un padre) que implemente ToolTipInUi
    private ToolTipInUi UIConTooltipBajoMouse()
    {
        var es = EventSystem.current;
        if (es == null || Mouse.current == null) return null;

        if (pointerData == null || pointerDataEventSystem != es)
        {
            pointerData = new PointerEventData(es);
            pointerDataEventSystem = es;
        }
        pointerData.position = Mouse.current.position.ReadValue();

        resultadosUI.Clear();
        es.RaycastAll(pointerData, resultadosUI);

        foreach (RaycastResult r in resultadosUI)
        {
            if (r.gameObject == null) continue;
            if (tipObj != null && r.gameObject.transform.IsChildOf(tipObj.transform)) continue; // ignorar el propio tip
            ToolTipInUi t = r.gameObject.GetComponentInParent<ToolTipInUi>();
            if (t != null) return t;
        }
        return null;
    }

    public void Deseleccionar()
    {
        seleccionado = null;
        Ocultar();
    }

    private void Ocultar()
    {
        if (tipObj != null && tipObj.activeSelf) tipObj.SetActive(false);
        textoActual = null;
    }

    // ---------- Click ----------

    private void LeerClick()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            posAlApretar = mouse.position.ReadValue();
            // Clicks sobre la UI (botones, paneles) no seleccionan ni deseleccionan
            clickValido = !(EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
        }

        if (!mouse.leftButton.wasReleasedThisFrame || !clickValido) return;
        clickValido = false;

        Vector2 posAlSoltar = mouse.position.ReadValue();
        if ((posAlSoltar - posAlApretar).sqrMagnitude > toleranciaClick * toleranciaClick) return; // fue un arrastre

        Worker worker = WorkerBajo(posAlSoltar);
        if (worker != null)
        {
            seleccionado = worker;
            textoActual = null; // forzar actualizar el texto
        }
        else
        {
            Deseleccionar();
        }
    }

    private Worker WorkerBajo(Vector2 posPantalla)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return null;

        Ray ray = cam.ScreenPointToRay(posPantalla);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            return hit.collider.GetComponentInParent<Worker>();
        return null;
    }

    // ---------- Posición ----------

    // Pone el tip sobre ese punto del mundo. Devuelve false si el punto está detrás de la cámara.
    private bool Posicionar(Vector3 posMundo)
    {
        if (tipObj == null) return true;
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Vector3 pantalla = cam.WorldToScreenPoint(posMundo);
        if (pantalla.z < 0f) return false;

        PosicionarEnPantalla((Vector2)pantalla + offsetPantalla);
        return true;
    }

    // Pone el tip en esa posición de pantalla (en píxeles)
    private void PosicionarEnPantalla(Vector2 pantalla)
    {
        if (tipObj == null) return;
        Canvas canvas = tipObj.GetComponentInParent<Canvas>(true);
        if (canvas == null) return;
        Canvas raiz = canvas.rootCanvas;
        Camera camCanvas = raiz.renderMode == RenderMode.ScreenSpaceOverlay ? null : raiz.worldCamera;

        // Se convierte contra el RectTransform del Canvas raíz y se asigna la posición en el mundo,
        // así funciona aunque el padre del tip no sea un RectTransform
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)raiz.transform, pantalla, camCanvas, out Vector3 posUI))
        {
            tipObj.transform.position = posUI;
        }
    }
}
