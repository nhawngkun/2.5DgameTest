using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class VariableJoystick_SilkyWoods : MonoBehaviour, IDragHandler, IPointerUpHandler, IPointerDownHandler
{
    [Header("Joystick Components")]
    public RectTransform background;
    public RectTransform handle;

    [Header("Settings")]
    public float handleRange = 1f;
    public float deadZone = 0.1f;

    [Header("Animation")]
    public float resetSpeed = 10f; // Tốc độ handle về giữa

    private bool isResetting = false;

    [Header("Debug")]
    public bool showDebug = true;

    private Vector2 input = Vector2.zero;
    private Canvas canvas;
    private Camera cam;

    public float Horizontal => input.x;
    public float Vertical => input.y;
    public Vector2 Direction => new Vector2(Horizontal, Vertical);

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
           
            return;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            cam = canvas.worldCamera;

        if (background == null)
        {
            
            background = GetComponent<RectTransform>();
        }

        if (handle == null)
        {
            
        }
        else
        {
          
            ResetHandle();
        }

        
    }

    void ResetHandle()
    {
        if (handle == null) return;

      
        handle.anchorMin = new Vector2(0.5f, 0.5f);
        handle.anchorMax = new Vector2(0.5f, 0.5f);
        handle.pivot = new Vector2(0.5f, 0.5f);

        
        handle.anchoredPosition = Vector2.zero;
        handle.localPosition = new Vector3(0, 0, handle.localPosition.z);

       
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (showDebug)
            Debug.Log("[Joystick] OnPointerDown");

        isResetting = false; 
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isResetting) return;

        Vector2 position;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background,
            eventData.position,
            cam,
            out position))
        {
            
            position.x = (position.x / background.sizeDelta.x);
            position.y = (position.y / background.sizeDelta.y);

            float x = position.x * 2;
            float y = position.y * 2;

            input = new Vector2(x, y);
            input = (input.magnitude > 1.0f) ? input.normalized : input;

            
            if (input.magnitude < deadZone)
                input = Vector2.zero;

           
            if (handle != null)
            {
                handle.anchoredPosition = new Vector2(
                    input.x * (background.sizeDelta.x / 2f) * handleRange,
                    input.y * (background.sizeDelta.y / 2f) * handleRange
                );
            }

           
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
     
        input = Vector2.zero;

      
        if (handle != null)
        {
            handle.anchoredPosition = Vector2.zero;
        }

      
        isResetting = false; 
    }
    void Update()
    {
      
        if (isResetting && handle != null)
        {
           
            if (showDebug)
              

            handle.anchoredPosition = Vector2.Lerp(
                handle.anchoredPosition,
                Vector2.zero,
                resetSpeed * Time.deltaTime
            );

           
            if (handle.anchoredPosition.magnitude < 0.1f)
            {
                handle.anchoredPosition = Vector2.zero;
                isResetting = false;

               
            }
        }
    }

    
}