using TMPro;
using UnityEngine;

public class Perk : MonoBehaviour
{
    public TextMeshProUGUI descriptionText;
    public PerkData perkData;
    public GameObject costoOroWindow;
    public TextMeshProUGUI goldCost;
    public int goldRequired = 0;
    public void SelectThis()
    {
        if(goldRequired<=ResourceManager.Instance.Oro)
            PerkManager.OnPerkSelected?.Invoke(perkData);
        else
            ResourceManager.PlayerHasNoGold?.Invoke(goldRequired);

    }
    public void Setup(int goldCost)
    {
        descriptionText.SetText(perkData.description);
        if (goldCost > 0)
        {
            goldRequired = goldCost;
            this.goldCost.SetText("Costo: <color=yellow>" + goldCost + "</color> oro");
            costoOroWindow.SetActive(true);
        }
        else
        {
            costoOroWindow.SetActive(false);
        }
    }
}
