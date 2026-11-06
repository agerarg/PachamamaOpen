using System;
using UnityEngine;
using UnityEngine.UI;

public class Quest : MonoBehaviour
{
    public Sprite questComplete;
    public Sprite questIncomplete;


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
       if(ResourceManager.Instance.BonesCounter >= 5)
        {
                       quest3.sprite = questComplete;
        }
    }

    private void Start()
    {
        quest1.sprite = questIncomplete;
        quest2.sprite = questIncomplete;
        quest3.sprite = questIncomplete;
        quest4.sprite = questIncomplete;
    }

    private void OnWorkersChange(int obj)
    {
        if (obj >= 5)
            {
            quest1.sprite = questComplete;
        }
        if (obj >= 20)
        {
            quest4.sprite = questComplete;
        }
    }

    private void OnGoldChange(int obj)
    {
        if (obj >= 500)
        {
            quest2.sprite = questComplete;
        }
    }
}
