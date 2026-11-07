using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Agua donde los workers lavan el oro antes de llevarlo al centro.
/// No tiene Update: sólo se registra en la lista para que los workers la encuentren.
/// </summary>
public class Whater : MonoBehaviour
{
    /// <summary>Todas las aguas activas en la escena.</summary>
    public static readonly List<Whater> Todos = new List<Whater>();

    private Collider col;
    private Renderer rend;

    void Awake()
    {
        col = GetComponent<Collider>();
        rend = GetComponentInChildren<Renderer>();
    }

    void OnEnable()
    {
        Todos.Add(this);
        ResourceManager.WhaterHasChange += CambiarMaterial;
        // Al aparecer (ej. al regenerar el mapa) toma el material del nivel de contaminación actual
        if (ResourceManager.Instance != null) CambiarMaterial(ResourceManager.Instance.MaterialAguaActual);
    }

    void OnDisable()
    {
        Todos.Remove(this);
        ResourceManager.WhaterHasChange -= CambiarMaterial;
    }

    private void CambiarMaterial(Material m)
    {
        if (rend != null && m != null) rend.sharedMaterial = m;
    }

    /// <summary>Punto del borde del agua más cercano a una posición.</summary>
    public Vector3 PuntoMasCercano(Vector3 desde)
    {
        return col != null && col.enabled ? col.ClosestPoint(desde) : transform.position;
    }
}
