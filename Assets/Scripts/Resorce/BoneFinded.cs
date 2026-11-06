using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Hueso dentro de un pilar. Cuando se termina de excavar, sube, se achica hasta desaparecer
/// y avisa con el callback. No tiene Update: la animación es una corrutina que sólo corre mientras se revela.
/// </summary>
public class BoneFinded : MonoBehaviour
{
    [Tooltip("Cuánto sube el hueso al encontrarlo.")]
    public float alturaSubida = 1.5f;
    [Tooltip("Segundos que tarda en subir y desaparecer.")]
    public float duracion = 1.5f;
    [Tooltip("Vueltas por segundo mientras sube.")]
    public float vueltasPorSegundo = 1f;
    [Tooltip("Parte final de la animación (0-1) en la que se achica hasta desaparecer.")]
    [Range(0.05f, 1f)] public float parteDesaparecer = 0.4f;

    public bool Revelando { get; private set; }

    /// <summary>Hace subir y desaparecer el hueso; al terminar llama a alTerminar.</summary>
    public void Revelar(Action alTerminar)
    {
        if (Revelando) return;
        gameObject.SetActive(true);
        StartCoroutine(Animar(alTerminar));
    }

    private IEnumerator Animar(Action alTerminar)
    {
        Revelando = true;
        Vector3 inicio = transform.position;
        Vector3 escalaInicial = transform.localScale;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duracion);
            float k = Mathf.Clamp01(t);

            // Sube con un poco de desaceleración
            float subida = 1f - (1f - k) * (1f - k);
            transform.position = inicio + Vector3.up * (alturaSubida * subida);
            transform.Rotate(Vector3.up, 360f * vueltasPorSegundo * Time.deltaTime, Space.World);

            // En la parte final se achica hasta 0
            float inicioDesaparecer = 1f - parteDesaparecer;
            float achique = k <= inicioDesaparecer ? 1f : 1f - (k - inicioDesaparecer) / parteDesaparecer;
            transform.localScale = escalaInicial * achique;

            yield return null;
        }

        // Primero restaurar y avisar (el callback lo devuelve al pilar y lo oculta).
        // No desactivar acá antes del callback: desactivar el objeto corta esta corrutina.
        transform.localScale = escalaInicial;
        Revelando = false;
        if (alTerminar != null) alTerminar();
        else gameObject.SetActive(false);
    }
}
