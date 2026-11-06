using TMPro;
using UnityEngine;

public class Perk : MonoBehaviour
{
    public TextMeshProUGUI descriptionText;
    public PerkData perkData;
    public void SelectThis()
    {
        PerkManager.OnPerkSelected?.Invoke(perkData);
    }
    public void Setup()
    {
        descriptionText.SetText(perkData.description);
    }
}
