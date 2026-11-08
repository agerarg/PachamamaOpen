
using UnityEngine;

public class WinCondition : MonoBehaviour
{
    public GameObject winScreen;
    private void OnEnable()
    {
        Quest.AllQuestComplete += OnComplete;
    }
    private void OnDisable()
    {
        Quest.AllQuestComplete -= OnComplete;

    }
    private void Start()
    {
        winScreen.SetActive(false);
    }
    private void OnComplete()
    {
        winScreen.SetActive(true);
        Time.timeScale = 0;
    }
}
