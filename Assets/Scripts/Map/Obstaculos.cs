using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Obstaculos : MonoBehaviour
{
    public GameObject obs;

    public GameObject whater;

    public float size = 1f;
    public int tamano = 10;


    public List<Vector2Int> whaterPlaces = new();

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
                Vector3 pos = Vector3.zero;
                GameObject newGo = null;
                if (whaterPlaces.Contains( new Vector2Int(i,j) ))
                    {
                    pos = new Vector3(i * size, -1.11f, j * size);
                    newGo = Instantiate(whater, pos, Quaternion.identity, transform);
                    newGo.name = "Water_" + i + "_" + j;
                    continue;
                }

                 pos = new Vector3(i * size, 3f, j * size);
                 newGo = Instantiate(obs, pos, Quaternion.identity, transform);
                newGo.name = "Pilar_" + i + "_" + j;
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
