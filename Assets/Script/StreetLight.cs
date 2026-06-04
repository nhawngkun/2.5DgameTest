using UnityEngine;
using System.Collections;

public class StreetLight : MonoBehaviour
{
    [Header("Components")]
    public Light LightComponent;          
    public GameObject LightVisualObject;   
    public Renderer BulbRenderer;          

    [Header("Settings")]
    public float TurnOnHour = 18f;
    public float TurnOffHour = 6f;  
    public Color WarmLightColor = new Color(1f, 0.92f, 0.75f);
    public float MaxIntensity = 12f;

    [Header("2D Sprite Settings")]
    public Color BulbOnColor = Color.white;
    public Color BulbOffColor = new Color(0.3f, 0.3f, 0.3f);

    private bool _isCurrentlyOn = false;
    private Material _bulbMaterialInstance;
    private bool _isSpriteBulb = false;

    private void Awake()
    {
        if (transform.childCount == 0)
        {
            Build3DStreetLightGeometry();
        }
    }

    private void Start()
    {
        if (LightComponent == null)
        {
            LightComponent = GetComponentInChildren<Light>();
        }

        if (LightVisualObject == null)
        {
            foreach (Transform child in transform)
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains("beam") || nameLower.Contains("glow") || nameLower.Contains("cone") || (nameLower.Contains("light") && (LightComponent == null || child.gameObject != LightComponent.gameObject)))
                {
                    LightVisualObject = child.gameObject;
                    break;
                }
            }
        }

        if (BulbRenderer == null)
        {
            BulbRenderer = GetComponentInChildren<SpriteRenderer>();
            if (BulbRenderer == null)
            {
                BulbRenderer = GetComponentInChildren<MeshRenderer>();
            }
        }

        if (BulbRenderer != null)
        {
            _isSpriteBulb = BulbRenderer is SpriteRenderer;
            if (!_isSpriteBulb)
            {
                _bulbMaterialInstance = BulbRenderer.material;
                _bulbMaterialInstance.EnableKeyword("_EMISSION");
            }
        }

        // Initialize state
        bool isNight = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
        _isCurrentlyOn = isNight;
        SetLightState(isNight, instant: true);
    }

    private void Update()
    {
        if (DayNightCycle.Instance == null) return;

        bool shouldBeOn = DayNightCycle.Instance.IsNight;

        if (shouldBeOn != _isCurrentlyOn)
        {
            _isCurrentlyOn = shouldBeOn;
            StartCoroutine(TransitionLightState(shouldBeOn));
        }
    }

    private IEnumerator TransitionLightState(bool turnOn)
    {
        if (turnOn)
        {
            yield return new WaitForSeconds(Random.Range(0.1f, 1.2f));

            // Small quick flicker
            SetLightState(false, instant: true);
            yield return new WaitForSeconds(0.1f);
            SetLightState(true, instant: true);
            yield return new WaitForSeconds(0.15f);
            SetLightState(false, instant: true);
            yield return new WaitForSeconds(0.08f);
            SetLightState(true, instant: false);
        }
        else
        {
            SetLightState(false, instant: false);
        }
    }

    private void SetLightState(bool turnOn, bool instant)
    {

        if (LightComponent != null)
        {
            LightComponent.enabled = turnOn;
            LightComponent.intensity = turnOn ? MaxIntensity : 0f;
        }

        if (LightVisualObject != null)
        {
            LightVisualObject.SetActive(turnOn);
        }

        if (BulbRenderer != null)
        {
            if (_isSpriteBulb)
            {
                ((SpriteRenderer)BulbRenderer).color = turnOn ? BulbOnColor : BulbOffColor;
            }
            else if (_bulbMaterialInstance != null)
            {
                if (turnOn)
                {
                    _bulbMaterialInstance.SetColor("_EmissionColor", WarmLightColor * 4f);
                    _bulbMaterialInstance.EnableKeyword("_EMISSION");
                }
                else
                {
                    _bulbMaterialInstance.SetColor("_EmissionColor", Color.black);
                    _bulbMaterialInstance.DisableKeyword("_EMISSION");
                }
            }
        }
    }

    private void Build3DStreetLightGeometry()
    {
        if (GetComponent<Collider>() == null)
        {
            CapsuleCollider cc = gameObject.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, 2.5f, 0f);
            cc.radius = 0.25f;
            cc.height = 5.0f;
        }

        Material poleMat = CreateLitMaterial(new Color(0.2f, 0.2f, 0.22f), 0.7f, 0.4f);
        Material bulbMat = CreateLitMaterial(Color.white, 0f, 0.9f);
        bulbMat.EnableKeyword("_EMISSION");
        bulbMat.SetColor("_EmissionColor", Color.black);

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pole.GetComponent<Collider>());
        pole.name = "Pole";
        pole.transform.SetParent(transform);
        pole.transform.localPosition = new Vector3(0f, 2.0f, 0f);
        pole.transform.localScale = new Vector3(0.15f, 2.0f, 0.15f);
        pole.GetComponent<MeshRenderer>().material = poleMat;

        GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(arm.GetComponent<Collider>());
        arm.name = "Arm";
        arm.transform.SetParent(transform);
        arm.transform.localPosition = new Vector3(0f, 3.95f, 0.4f);
        arm.transform.localScale = new Vector3(0.1f, 0.4f, 0.1f);
        arm.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        arm.GetComponent<MeshRenderer>().material = poleMat;

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(head.GetComponent<Collider>());
        head.name = "Head";
        head.transform.SetParent(transform);
        head.transform.localPosition = new Vector3(0f, 3.95f, 0.8f);
        head.transform.localScale = new Vector3(0.45f, 0.15f, 0.45f);
        head.GetComponent<MeshRenderer>().material = poleMat;

        GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(bulb.GetComponent<Collider>());
        bulb.name = "Bulb";
        bulb.transform.SetParent(transform);
        bulb.transform.localPosition = new Vector3(0f, 3.85f, 0.8f);
        bulb.transform.localScale = new Vector3(0.25f, 0.2f, 0.25f);
        BulbRenderer = bulb.GetComponent<MeshRenderer>();
        BulbRenderer.material = bulbMat;

        GameObject lightGo = new GameObject("LampLight");
        lightGo.transform.SetParent(transform);
        lightGo.transform.localPosition = new Vector3(0f, 3.7f, 0.8f);
        lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        LightComponent = lightGo.AddComponent<Light>();
        LightComponent.type = LightType.Spot;
        LightComponent.range = 10f;
        LightComponent.spotAngle = 70f;
        LightComponent.innerSpotAngle = 30f;
        LightComponent.intensity = MaxIntensity;
        LightComponent.color = WarmLightColor;
        LightComponent.shadows = LightShadows.Soft;
    }

    private Material CreateLitMaterial(Color color, float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    private void OnDestroy()
    {
        if (_bulbMaterialInstance != null)
        {
            Destroy(_bulbMaterialInstance);
        }
    }
}
