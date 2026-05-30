using System.Collections.Generic;
using UnityEngine;

public class WoodPool : MonoBehaviour
{
    private static WoodPool _instance;
    public static WoodPool Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<WoodPool>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("WoodPool");
                    _instance = go.AddComponent<WoodPool>();
                }
            }
            return _instance;
        }
    }

    [Header("Pool Config")]
    [Tooltip("Prefab của khúc gỗ để tạo trước trong Pool")]
    public GameObject _WoodPrefab;

    [Tooltip("Số lượng khúc gỗ khởi tạo sẵn ban đầu")]
    public int _InitialSize = 30;

    private Queue<GameObject> _poolQueue = new Queue<GameObject>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    private void Start()
    {
        if (_WoodPrefab != null)
        {
            Prewarm(_InitialSize);
        }
    }

    public void Prewarm(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(_WoodPrefab, transform);
            obj.SetActive(false);
            _poolQueue.Enqueue(obj);
        }
    }

    public GameObject Spawn(Vector3 position, Quaternion rotation)
    {
        GameObject obj;
        if (_poolQueue.Count > 0)
        {
            obj = _poolQueue.Dequeue();
        }
        else
        {
            obj = Instantiate(_WoodPrefab);
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.transform.SetParent(null);
        obj.SetActive(true);
        
        return obj;
    }

    public void Recycle(GameObject obj)
    {
        if (obj == null) return;
        
        obj.SetActive(false);
        obj.transform.SetParent(transform);
        _poolQueue.Enqueue(obj);
    }
}
