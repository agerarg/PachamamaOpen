using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Settings : MonoBehaviour
{
    public GameObject settingsPanel;
    public GameObject mainMenu;

    public Slider sfxVolumeSlider;
    public Slider musicVolumeSlider;

    public static Action OnSettingsChanged;

    [Tooltip("Pausar el juego mientras el menú está abierto.")]
    public bool pausarAlAbrir = true;

    private float timeScaleAntes = 1f;

    void Start()
    {
        settingsPanel.SetActive(false);

        sfxVolumeSlider.value = PlayerPrefs.GetFloat("SoundVolume", 1);
        musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        SaveSetings();
    }

    void Update()
    {
        // Esc abre o cierra el menú
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (settingsPanel.activeSelf) VolverAlJuego();
            else AbrirSettings();
        }
    }

    public void AbrirSettings()
    {
        if (settingsPanel.activeSelf) return;
        settingsPanel.SetActive(true);
        if(mainMenu != null)
            mainMenu.SetActive(false);
        if (pausarAlAbrir)
        {
            AudioMaster.Instance.TriggerSound(AudioMaster.SoundType.settings, Vector3.zero);
            // Guardar la pausa que había (por ejemplo si estaba abierta la ventana de perks)
            timeScaleAntes = Time.timeScale;
            Time.timeScale = 0f;
        }
    }

    private void SaveSetings()
    {
        AudioMaster.Instance.fxVol = sfxVolumeSlider.value;
        AudioMaster.Instance.musicVol = musicVolumeSlider.value;
        PlayerPrefs.SetFloat("SoundVolume", sfxVolumeSlider.value);
        PlayerPrefs.SetFloat("MusicVolume", musicVolumeSlider.value);
        OnSettingsChanged?.Invoke();
    }

    public void VolverAlJuego()
    {
        if (mainMenu != null)
            mainMenu.SetActive(true);
        if (!settingsPanel.activeSelf) return;
        settingsPanel.SetActive(false);
        if (pausarAlAbrir) Time.timeScale = timeScaleAntes;
    }
    public void VolverAlMenu()
    {
        SceneManager.LoadScene("Inicio");
    }
    public void ExitGame()
    {
               Application.Quit();
    }

    public void OnChangedVol()
    {
        SaveSetings();
    }

}
