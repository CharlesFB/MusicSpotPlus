using System.Collections.Generic;
using UnityEngine;

public class GameObjectPool
{
    readonly GameObject prefab;
    readonly Transform root;
    readonly Stack<GameObject> idle = new Stack<GameObject>();

    public GameObjectPool(GameObject prefab, int prewarm)
    {
        this.prefab = prefab;
        var host = new GameObject(prefab.name + " Pool");
        root = host.transform;
        for (int i = 0; i < prewarm; i++)
            idle.Push(Create());
    }

    GameObject Create()
    {
        GameObject go = Object.Instantiate(prefab, root);
        go.SetActive(false);
        PooledToken token = go.GetComponent<PooledToken>();
        if (token == null)
            token = go.AddComponent<PooledToken>();
        token.Pool = this;
        token.Idle = true;
        return go;
    }

    public GameObject Get(Transform parent)
    {
        GameObject go = idle.Count > 0 ? idle.Pop() : Create();
        PooledToken token = go.GetComponent<PooledToken>();
        token.Idle = false;
        go.transform.SetParent(parent, false);
        go.SetActive(true);
        return go;
    }

    public void Release(GameObject go)
    {
        if (go == null)
            return;
        PooledToken token = go.GetComponent<PooledToken>();
        if (token == null || token.Pool != this || token.Idle)
            return;
        token.Idle = true;
        go.SetActive(false);
        go.transform.SetParent(root, false);
        idle.Push(go);
    }
}

public class PooledToken : MonoBehaviour
{
    public GameObjectPool Pool;
    public bool Idle;
}
