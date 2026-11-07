using System;
using TMPro;
using UnityEngine;

public class ErrorWindow : MonoBehaviour
{
    public GameObject errorWindow;
    public TextMeshProUGUI errorText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnEnable()
    {
        ResourceManager.PlayerHasNoGold += OnNoGold;
        ResourceManager.WorkerIsDead += OnWokerDied;
    }
    private void OnDisable()
    {
        ResourceManager.PlayerHasNoGold -= OnNoGold;
        ResourceManager.WorkerIsDead -= OnWokerDied;


    }

    private void DisplayErr(string err)
    {
               errorWindow.SetActive(true);
        errorText.SetText(err);
        AudioMaster.Instance.TriggerSound(AudioMaster.SoundType.errorWindow, Vector3.zero);

    }

    private void OnWokerDied()
    {

        DisplayErr("Un trabajador ha muerto por toxicidad del agua.");
    }

    private void Start()
    {
        errorWindow.SetActive(false);
    }
    private void OnNoGold(int obj)
    {
        DisplayErr("No tienes suficiente oro, se requiere " + obj + " de oro!.");
    }
}
