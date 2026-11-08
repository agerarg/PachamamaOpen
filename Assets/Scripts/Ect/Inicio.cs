using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Inicio : MonoBehaviour
{
    public Image blackimgCoverAll;

    [Tooltip("Música del menú que se va apagando al apretar Play. Si queda vacío, usa la del AudioMaster.")]
    public AudioSource musica;

    [Tooltip("Segundos que tarda la pantalla en ponerse negra (y la música en apagarse).")]
    public float duracionFade = 1.5f;
    [Tooltip("Segundos extra con la pantalla negra antes de cargar la escena.")]
    public float esperaDespuesDelFade = 0.5f;
    [Tooltip("Escena que carga al apretar Play.")]
    public string escenaJuego = "Pachamama";

    private bool cargando;

    void Start()
    {
        Time.timeScale = 1f;
        // Arranca transparente y sin bloquear los botones
        if (blackimgCoverAll != null)
        {
            SetAlpha(0f);
            blackimgCoverAll.raycastTarget = false;
        }
    }

    public void Salir()
    {
        Application.Quit();
    }

    public void Play()
    {
        if (cargando) return; // que no se pueda apretar dos veces
        cargando = true;
        StartCoroutine(FadeYCargar());
    }

    private IEnumerator FadeYCargar()
    {
        AudioSource m = musica;
        if (m == null && AudioMaster.Instance != null) m = AudioMaster.Instance.musicScene;
        float volumenInicial = m != null ? m.volume : 0f;

        if (blackimgCoverAll != null)
        {
            blackimgCoverAll.gameObject.SetActive(true);
            blackimgCoverAll.raycastTarget = true; // bloquea clicks mientras se oscurece
        }

        // Tiempo real: funciona aunque el juego esté pausado
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duracionFade);
            float k = Mathf.SmoothStep(0f, 1f, t);
            SetAlpha(k);
            if (m != null) m.volume = Mathf.Lerp(volumenInicial, 0f, k);
            yield return null;
        }

        SetAlpha(1f);
        if (m != null) m.volume = 0f;

        yield return new WaitForSecondsRealtime(esperaDespuesDelFade);

        Time.timeScale = 1f; // por si se venía de una pausa
        SceneManager.LoadScene(escenaJuego);
    }

    private void SetAlpha(float a)
    {
        if (blackimgCoverAll == null) return;
        Color c = blackimgCoverAll.color;
        c.a = a;
        blackimgCoverAll.color = c;
    }
}
