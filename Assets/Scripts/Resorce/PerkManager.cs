using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

// Fuera de la clase para poder usarlo directo como PerkType desde cualquier script (PerkData, Perk, etc.)
public enum PerkType { WorkerSpeed, WokerMaxGold, WorkerMinGold, GoldDoble, NewWorker, reduceToxixidad, reduceToxicPerLavado, chanceDobleOro }

public class PerkManager : MonoBehaviour
{
    [Tooltip("Cuántas opciones se ofrecen cada vez que se encuentra un hueso.")]
    public int cantidadOpciones = 3;

    public GameObject perkWindow;

    public List<PerkData> perkDatas = new List<PerkData>();

    public static Action<PerkData> OnPerkSelected;

    public Transform perkHolder;
    public Perk perkPrefab;

    // Huesos encontrados que todavía no se convirtieron en perk (si salen varios juntos, se eligen uno tras otro)
    private int perksPendientes;

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
        // Descontar el límite. PerkData es una clase, así que 'data' es el mismo objeto que está en perkDatas.
        // -1 = sin límite; al llegar a 0 ya no se ofrece.
        if (data.limite > 0) data.limite--;

        switch(data.perkType)
        {
            case PerkType.WorkerSpeed:
                ResourceManager.Instance.AddWokerSpeed();
            break;
            case PerkType.WokerMaxGold:
                ResourceManager.Instance.AddWorkerMaxGold(data.value);
            break;
            case PerkType.WorkerMinGold:
                ResourceManager.Instance.AddWorkerMinGold(data.value);
                break;
            case PerkType.GoldDoble:
                                ResourceManager.Instance.MutiplyGold(data.value);
                break;
            case PerkType.NewWorker:
                int CostoDeTrabajador = ResourceManager.Instance.CostoDeTrabajador / 2;
                ResourceManager.Instance.SustractGold(CostoDeTrabajador);
                ResourceManager.Instance.SpownTrabajador();
                break;
            case PerkType.reduceToxixidad:
                int costoDeLimpiarRoxinas = ResourceManager.Instance.costoDeLimpiarRoxinas / 2;
                ResourceManager.Instance.SustractGold(costoDeLimpiarRoxinas);
                ResourceManager.Instance.ReduceToxicidad();
                break;
            case PerkType.reduceToxicPerLavado:
                ResourceManager.Instance.ReduceToxicidadPerLavado();
                break;
            case PerkType.chanceDobleOro:
                ResourceManager.Instance.ChanceDobleOroAdd(data.value);
                break;
        }
        AudioMaster.Instance.TriggerSound(AudioMaster.SoundType.selectPerk, Vector3.zero);

        // Si hay más huesos esperando, mostrar otras opciones sin despausar
        perksPendientes = Mathf.Max(0, perksPendientes - 1);
        if (perksPendientes > 0 && MostrarOpciones()) return;

        perksPendientes = 0;
        Time.timeScale = 1f;
        perkWindow.SetActive(false);
    }

    private void Start()
    {
        perkWindow.SetActive(false);
    }
    private void OnHuesoEncontrado(Pilar pilar)
    {
        perksPendientes++;
        // Si la ventana ya está abierta (eligiendo otro perk), éste queda en cola para después
        if (perkWindow.activeSelf) return;
        if (!MostrarOpciones()) perksPendientes = 0;
    }

    // Arma 3 opciones nuevas y abre la ventana. Devuelve false si no queda ningún perk para ofrecer.
    private bool MostrarOpciones()
    {
        // Borrar las opciones anteriores
        foreach (Transform t in perkHolder)
        {
            Destroy(t.gameObject);
        }
        AudioMaster.Instance.TriggerSound(AudioMaster.SoundType.showPerks, Vector3.zero);

        // Opciones posibles: todas las que no agotaron su límite (-1 = sin límite)
        List<PerkData> disponibles = perkDatas.FindAll(d => d.limite != 0);

        // Si ya no queda ningún perk para ofrecer, no abrir la ventana
        if (disponibles.Count == 0) return false;

        int cantidad = Mathf.Min(cantidadOpciones, disponibles.Count);
        for (int i = 0; i < cantidad; i++)
        {
            // Elegir una al azar y sacarla de la lista, así no se repite
            int indice = UnityEngine.Random.Range(0, disponibles.Count);
            PerkData elegido = disponibles[indice];
            disponibles.RemoveAt(indice);

            int goldRec = 0;
            switch (elegido.perkType)
            {
                case PerkType.WorkerMinGold:
                    elegido.value = UnityEngine.Random.Range(1, ResourceManager.Instance.oroMinimo);
                    elegido.description = "Trabajadores consiguen +"+ elegido.value + " oro como minimo";
                break;
                case PerkType.WokerMaxGold:
                    elegido.value = UnityEngine.Random.Range(2, ResourceManager.Instance.oroMaximo);
                    elegido.description = "Trabajadores pueden conseguir +"+ elegido.value +" de oro como maximo";
                    break;
                case PerkType.NewWorker:
                    goldRec = ResourceManager.Instance.CostoDeTrabajador / 2;
                    break;
                case PerkType.reduceToxixidad:
                    goldRec = ResourceManager.Instance.costoDeLimpiarRoxinas / 2;

                    break;
            }

            Perk perk = Instantiate(perkPrefab, perkHolder);
            perk.perkData = elegido;

            perk.Setup(goldRec);
        }
        Time.timeScale = 0f; // Pausar el juego mientras se elige un perk
        perkWindow.SetActive(true);
        return true;
    }

}

[Serializable]
public class PerkData
{
    public PerkType perkType;
    public int value;
    public string description;
    [Tooltip("Cuántas veces se puede elegir. -1 = sin límite. Al llegar a 0 ya no aparece como opción.")]
    public int limite = -1;
}
