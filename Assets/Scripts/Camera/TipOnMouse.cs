using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class TipOnMouse : MonoBehaviour
{
    public TextMeshProUGUI whatDoingText;
    public GameObject tipObj;

    private Camera cam;
    private string textoActual;

    void Awake()
    {
        cam = Camera.main;
        if (tipObj == gameObject)
            Debug.LogWarning("TipOnMouse: tipObj no puede ser el mismo objeto que tiene este script (al ocultarlo se apagaría el script). Usá un hijo.", this);
        else if (tipObj != null)
            tipObj.SetActive(false);
    }

    void LateUpdate()
    {
        Worker worker = WorkerBajoMouse();

        if (worker == null)
        {
            if (tipObj != null && tipObj.activeSelf) tipObj.SetActive(false);
            textoActual = null;
            return;
        }

        if (tipObj != null && !tipObj.activeSelf) tipObj.SetActive(true);

        // Sólo tocar el texto si cambió, para no regenerar la malla de TMP cada frame
        string texto = worker.TextoEstado;
        if (whatDoingText != null && texto != textoActual)
        {
            textoActual = texto;
            whatDoingText.text = texto;
        }
    }

    private Worker WorkerBajoMouse()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || Mouse.current == null) return null;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            return hit.collider.GetComponentInParent<Worker>();
        return null;
    }
}
