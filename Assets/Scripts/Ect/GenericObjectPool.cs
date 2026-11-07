using System.Collections.Generic;
using UnityEngine;
namespace Pool
{
    public abstract class GenericObjectPool<T> : MonoBehaviour where T : Component
    {

        public T prefab;
        public int prewarm = 20;
        public static GenericObjectPool<T> instance { get; private set; }
        private Queue<T> objects = new Queue<T>();

        private void Awake()
        {
            instance = this;
        }
        private void Start()
        {
            for(int i=0;i< prewarm;i++)
            {
                var newObject = GameObject.Instantiate(prefab, transform.position, Quaternion.identity, transform);
                newObject.gameObject.SetActive(false);
                objects.Enqueue(newObject);
            }
        }
        public T Get()
        {
            if (objects.Count == 0)
                AddObject();
            return objects.Dequeue();
        }

        public void ReturnToPool(T objectToReturn)
        {
            if (objectToReturn != null)
            {
                objectToReturn.gameObject.SetActive(false);
                objects.Enqueue(objectToReturn);
            }
        }

        public void AddObject()
        {
            var newObject = GameObject.Instantiate(prefab, transform.position, Quaternion.identity, transform);
            newObject.gameObject.SetActive(true);
            objects.Enqueue(newObject);
        }
    }
}