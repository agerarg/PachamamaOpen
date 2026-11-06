using System;
using System.Collections.Generic;
using UnityEngine;

// Fuera de la clase para poder usarlo directo como PerkType desde cualquier script (PerkData, Perk, etc.)
public enum PerkType { WorkerSpeed, WokerMaxGold, WorkerMinGold, GoldDoble }

public class PerkManager : MonoBehaviour
{
    [Tooltip("Cuántas opciones se ofrecen cada vez que se encuentra un hueso.")]
    public int cantidadOpciones = 3;

    public GameObject perkWindow;

    public List<PerkData> perkDatas = new List<PerkData>();

    public static Action<PerkData> OnPerkSelected;

    public Transform perkHolder;
    public Perk perkPrefab;

    private void OnEnable()
    {
        Pilar.HuesoEncontrado += OnHuesoEncontrado;
        OnPerkSelected += PerkSelected;
    }
    private void OnDisable()
    {
        Pilar.HuesoEncontrado -= OnHuesoEncontrado;
        OnPerkSelected -= PerkSelected;

    }

    private void PerkSelected(PerkData data)
    {
        switch(data.perkType)
        {
            case PerkType.WorkerSpeed:
                ResourceManager.Instance.AddWokerSpeed();
            break;
            case PerkType.WokerMaxGold:
                ResourceManager.Instance.AddWorkerMaxGold();
            break;
            case PerkType.WorkerMinGold:
                ResourceManager.Instance.AddWorkerMinGold();
                break;
            case PerkType.GoldDoble:
                                ResourceManager.Instance.MutiplyGold(data.value);
                break;
        }


        Time.timeScale = 1f; 
        perkWindow.SetActive(false);
    }

    private void Start()
    {
        perkWindow.SetActive(false);
    }
    private void OnHuesoEncontrado(Pilar pilar)
    {
        // Borrar las opciones anteriores
        foreach (Transform t in perkHolder)
        {
            Destroy(t.gameObject);
        }

        // Copia de las opciones posibles (no tocamos perkDatas, para que sigan todas la próxima vez)
        List<PerkData> disponibles = new List<PerkData>(perkDatas);

        int cantidad = Mathf.Min(cantidadOpciones, disponibles.Count);
        for (int i = 0; i < cantidad; i++)
        {
            // Elegir una al azar y sacarla de la lista, así no se repite
            int indice = UnityEngine.Random.Range(0, disponibles.Count);
            PerkData elegido = disponibles[indice];
            disponibles.RemoveAt(indice);

            Perk perk = Instantiate(perkPrefab, perkHolder);
            perk.perkData = elegido;
            perk.Setup();
        }
        Time.timeScale = 0f; // Pausar el juego mientras se elige un perk
        perkWindow.SetActive(true);
    }

}

[Serializable]
public class PerkData
{
    public PerkType perkType;
    public int value;
    public string description;
}
