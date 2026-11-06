using UnityEngine;
using UnityEngine.InputSystem;

public class Obstaculos : MonoBehaviour
{
    public GameObject obs;

    public float size = 1f;
    public int tamano = 10;

    void Start()
    {
        GenMap();
    }

    private void GenMap()
    {
        foreach(Transform child in transform)
        {
            Destroy(child.gameObject);
        }
        for (int i = -tamano; i < tamano; i++)
        {
            for (int j = -tamano; j < tamano; j++)
            {
                if(j==0 && i==0 || j == 1 && i == 0 || j == -1 && i == 0 || j == 0 && i == 1 || j == 0 && i == -1)
                    {
                    continue;
                }

                Vector3 pos = new Vector3(i * size, 3f, j * size);
                Instantiate(obs, pos, Quaternion.identity, transform);
            }
        }
    }
    private void Update()
    {
         if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            GenMap();
        }
    }
}
