using UnityEngine;

public class UICanvas_SSMB : MonoBehaviour
{
    [SerializeField] bool isDestroyOnClose = false;
    private CanvasGroup canvasGroup;

    protected virtual void Awake()
    {
        InitializeCanvasGroup();
    }

    void InitializeCanvasGroup()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
           
        }
    }

    public virtual void Setup()
    {
        if (canvasGroup == null)
        {
            InitializeCanvasGroup();
        }
    }

    public virtual void Open()
    {
        if (canvasGroup == null)
        {
            InitializeCanvasGroup();
        }

        if (canvasGroup == null)
        {
           
            return;
        }

        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

       
    }

    public virtual void Close(float time)
    {
        Invoke(nameof(CloseDirectly), time);
    }

    public virtual void CloseDirectly()
    {
        if (canvasGroup == null)
        {
            InitializeCanvasGroup();
        }

        if (isDestroyOnClose)
        {
            Destroy(gameObject);
        }
        else
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }
}