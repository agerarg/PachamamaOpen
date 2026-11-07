using System;
using System.Collections.Generic;
using UnityEngine;

public class AudioMaster : MonoBehaviour
{
    public AudioSource musicScene;
    public float musicVol = 0.5f;
    public float fxVol = 1.0f;
    public static AudioMaster Instance { get; private set; }
    private void Awake()
    {
        Instance = this;
        // La música arranca recién cuando empieza el juego (después del fade)
        if (musicScene != null)
        {
            musicScene.playOnAwake = false;
            musicScene.Stop();
        }
    }
    // La música arranca cuando GameStart lo indica ('Segundos Hasta Musica' en su Inspector)
    private void OnEnable()  { 
        GameStart.MusicStarted += OnGameStarted;
        Settings.OnSettingsChanged += OnSettingsChange;


    }
    private void OnDisable() { GameStart.MusicStarted -= OnGameStarted;

        Settings.OnSettingsChanged -= OnSettingsChange;
    }

    private void OnSettingsChange()
    {
        musicScene.volume = musicVol;
    }

    private void Start()
    {
        if (musicScene != null) musicScene.volume = musicVol;
        // Si el juego ya empezó (o no hay GameStart en la escena), arrancar la música ya
        if (GameStart.Iniciado) OnGameStarted();
    }

    private void OnGameStarted()
    {
        if (musicScene != null && !musicScene.isPlaying) musicScene.Play();
    }


    [SerializeField]
    public Dictionary<SoundType, SoudData> audioLib = new();
    
    public enum SoundType { workerPop, goldGain, goldSpend, cavar, lavar , selectPerk, errorWindow, showPerks,select,questComplete, settings };

    public void TriggerSound(SoundType type, Vector3 pos )
    {
        if(audioLib.ContainsKey(type))
        {
           SoundInstance si =  Sound_Pooling.instance.Get();
           si.Setup(pos, audioLib[type].clip, fxVol * audioLib[type].volume, UnityEngine.Random.Range(0.9f, 1f));
        }
    }
}
[Serializable]
public class SoudData
{
    public AudioClip clip;
    public float volume = 1f;
}