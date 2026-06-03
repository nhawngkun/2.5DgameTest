using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class WaterRipple : MonoBehaviour
{
    [Header("Ripple Settings")]
    public float MaxRadius = 1.0f;
    public float Duration = 0.6f;
    public float StartWidth = 0.08f;
    public float EndWidth = 0.01f;
    public Color RippleColor = new Color(1f, 1f, 1f, 0.6f);

    private LineRenderer _lineRenderer;
    private float _elapsedTime = 0f;
    private const int POINTS_COUNT = 36; 

    private void Start()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = false;
        _lineRenderer.loop = true;
        _lineRenderer.positionCount = POINTS_COUNT;
        
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            _lineRenderer.material = new Material(spriteShader);
        }
        
        UpdateRipple(0f);
    }

    private void Update()
    {
        _elapsedTime += Time.deltaTime;
        float progress = Mathf.Clamp01(_elapsedTime / Duration);

        UpdateRipple(progress);

        if (progress >= 1.0f)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateRipple(float progress)
    {
        float currentRadius = progress * MaxRadius;
        float currentWidth = Mathf.Lerp(StartWidth, EndWidth, progress);
        
        _lineRenderer.startWidth = currentWidth;
        _lineRenderer.endWidth = currentWidth;

        for (int i = 0; i < POINTS_COUNT; i++)
        {
            float angle = i * Mathf.PI * 2f / (POINTS_COUNT - 1);
            float x = Mathf.Cos(angle) * currentRadius;
            float z = Mathf.Sin(angle) * currentRadius;
            
            _lineRenderer.SetPosition(i, new Vector3(x, 0.02f, z));
        }

        Color lerpedColor = RippleColor;
        lerpedColor.a = Mathf.Lerp(RippleColor.a, 0f, progress);
        
        _lineRenderer.startColor = lerpedColor;
        _lineRenderer.endColor = lerpedColor;
    }
}
