using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inicio suave: la pantalla negra se desvanece y recién ahí arranca el juego (GameStarted).
/// Los demás scripts escuchan GameStart.GameStarted o consultan GameStart.Iniciado.
/// </summary>
public class GameStart : MonoBehaviour
{
    public GameObject canvas;
    public Image blackScreen;

    [Tooltip("Segundos con la pantalla negra antes de empezar el fade.")]
    public float esperaInicial = 0.5f;
    [Tooltip("Segundos que tarda el fade de negro a transparente.")]
    public float duracionFade = 1.5f;
    [Tooltip("Segundos desde que carga la escena hasta que empieza la música (independiente del fade).")]
    public float segundosHastaMusica = 0.5f;

    /// <summary>Se dispara cuando tiene que empezar la música (a los 'segundosHastaMusica').</summary>
    public static Action MusicStarted;

    /// <summary>Se dispara cuando empieza el fade (la pantalla todavía está negra).</summary>
    public static Action FadeStarted;

    /// <summary>Se dispara una vez, cuando termina el fade y empieza el juego.</summary>
    public static Action GameStarted;

    private static GameStart instancia;
    private static bool iniciado;

    /// <summary>True cuando el juego ya empezó (o si no hay GameStart en la escena, para no trabar nada).</summary>
    public static bool Iniciado => iniciado || instancia == null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ReiniciarEstatico() { instancia = null; iniciado = false; GameStarted = null; FadeStarted = null; MusicStarted = null; }

    void Awake()
    {
        canvas.SetActive(true);
        instancia = this;
        iniciado = false;

        if (blackScreen != null)
        {
            blackScreen.gameObject.SetActive(true);
            SetAlpha(1f);
            blackScreen.raycastTarget = true; // bloquea clicks mientras está negra
        }
    }

    void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    void Start()
    {
        StartCoroutine(Fade());
        StartCoroutine(Musica());
    }

    private IEnumerator Musica()
    {
        if (segundosHastaMusica > 0f) yield return new WaitForSecondsRealtime(segundosHastaMusica);
        MusicStarted?.Invoke();
    }

    private IEnumerator Fade()
    {
        // Tiempo sin escala: funciona aunque el juego esté pausado
        yield return new WaitForSecondsRealtime(esperaInicial);

        FadeStarted?.Invoke();

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duracionFade);
            // Suavizado: empieza y termina despacio
            SetAlpha(1f - Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        if (blackScreen != null) blackScreen.gameObject.SetActive(false);

        iniciado = true;
        GameStarted?.Invoke();
    }

    private void SetAlpha(float a)
    {
        if (blackScreen == null) return;
        Color c = blackScreen.color;
        c.a = a;
        blackScreen.color = c;
    }
}
