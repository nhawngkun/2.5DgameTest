using UnityEngine;

public class Singleton_SSMB<T> : MonoBehaviour where T : MonoBehaviour
{
    [SerializeField] bool dontDestroyOnload;
    private static T _instance;
    private static bool _applicationIsQuitting;

    public static T Instance
    {
        get
        {
            return _instance;
        }
    }

    public static bool IsInstanceValid()
    {
        return (_instance != null);
    }

    public virtual void Awake()
    {
        if (_instance != null)
        {
            Destroy(this.gameObject);
            return;
        }

        if (_instance == null)
            _instance = (T)(MonoBehaviour)this;

        if (_instance == null)
        {
            Debug.LogError("Awake xong van NULL " + typeof(T));
        }

        if (dontDestroyOnload)
            DontDestroyOnLoad(this);
    }

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }
}