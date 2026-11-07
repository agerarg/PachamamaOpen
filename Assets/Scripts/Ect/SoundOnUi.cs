using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Ponelo en un botón o imagen de la UI: hace un sonido cuando el mouse pasa por encima.
/// El objeto necesita un Graphic (Image, texto...) con Raycast Target activado.
/// </summary>
public class SoundOnUi : MonoBehaviour, IPointerEnterHandler
{
    [Tooltip("Sonido que suena al pasar el mouse por encima.")]
    public AudioMaster.SoundType sonido = AudioMaster.SoundType.select;

    [Tooltip("Si está en un botón desactivado (no interactuable), no suena.")]
    public bool soloSiEsInteractuable = true;

    private UnityEngine.UI.Selectable selectable;

    void Awake()
    {
        selectable = GetComponent<UnityEngine.UI.Selectable>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (soloSiEsInteractuable && selectable != null && !selectable.IsInteractable()) return;
        if (AudioMaster.Instance == null) return;

        // Sonido de interfaz: se reproduce en la cámara para que se escuche igual en cualquier lado
        Camera cam = Camera.main;
        Vector3 pos = cam != null ? cam.transform.position : Vector3.zero;
        AudioMaster.Instance.TriggerSound(sonido, pos);
    }
}
