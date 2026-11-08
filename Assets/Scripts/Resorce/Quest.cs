using System;
using UnityEngine;
using UnityEngine.UI;

public class Quest : MonoBehaviour
{
    public Sprite questComplete;
    public Sprite questIncomplete;

    public static Action AllQuestComplete;

    public Image quest1;
    public Image quest2;
    public Image quest3;
    public Image quest4;

    private void OnEnable()
    {
        ResourceManager.GoldHasChange += OnGoldChange;
        ResourceManager.WorkersHasChange += OnWorkersChange;
        Pilar.HuesoEncontrado += OnHuesoEncontrado;
    }
    private void OnDisable()
    {
        ResourceManager.GoldHasChange -= OnGoldChange;
        ResourceManager.WorkersHasChange -= OnWorkersChange;
        Pilar.HuesoEncontrado -= OnHuesoEncontrado;
    }

    private void OnHuesoEncontrado(Pilar pilar)
    {
        if (ResourceManager.Instance.BonesCounter >= 5)
            SetQuest(quest3, questComplete);
    }

    private void Start()
    {
        quest1.sprite = questIncomplete;
        quest2.sprite = questIncomplete;
        quest3.sprite = questIncomplete;
        quest4.sprite = questIncomplete;
    }

    // Cambia el sprite sólo si es distinto al actual, y suena una sola vez al cambiar
    private void SetQuest(Image target, Sprite set)
    {
        if (target == null || target.sprite == set) return;

        target.sprite = set;
        if (AudioMaster.Instance != null)
            AudioMaster.Instance.TriggerSound(AudioMaster.SoundType.questComplete, Vector3.zero);

        if(quest1.sprite == questComplete && quest2.sprite == questComplete && quest3.sprite == questComplete && quest4.sprite == questComplete)
            AllQuestComplete?.Invoke();
    }

    private void OnWorkersChange(int obj)
    {
        if (obj >= 5)
            SetQuest(quest1, questComplete);
        if (obj >= 20)
            SetQuest(quest4, questComplete);
    }

    private void OnGoldChange(int obj)
    {
        if (obj >= 500)
            SetQuest(quest2, questComplete);
    }
}
