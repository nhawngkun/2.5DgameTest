using UnityEngine;
using System.Collections.Generic;

public class DayNightCycle : MonoBehaviour
{
    private static DayNightCycle _instance;
    public static DayNightCycle Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<DayNightCycle>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("DayNightCycleManager");
                    _instance = go.AddComponent<DayNightCycle>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [Header("Time Settings")]
    [Range(0f, 24f)]
    public float TimeOfDay = 8f; // Start at 8 AM
    public float DayLengthInSeconds = 120f; // 2 minutes for full day/night cycle
    public bool PauseTime = false;

    [Header("Lighting Assets")]
    public Light DirectionalLight;
    public Gradient SunColor;
    public Gradient AmbientColor;
    public AnimationCurve SunIntensityCurve;

    [Header("2.5D Sprite Shading")]
    public Gradient SpriteLightColor;

    public bool IsNight => TimeOfDay < 6f || TimeOfDay > 18f;

    private struct TintedSprite
    {
        public SpriteRenderer Renderer;
        public Color OriginalColor;
    }

    private struct TintedMesh
    {
        public MeshRenderer Renderer;
        public Color OriginalColor;
        public string ColorPropertyName;
    }

    private List<TintedSprite> _tintedSprites = new List<TintedSprite>();
    private List<TintedMesh> _tintedMeshes = new List<TintedMesh>();
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Setup default gradients and curves if not configured in Inspector
        if (SunColor == null || SunColor.colorKeys.Length <= 2)
        {
            SetupDefaultGradients();
        }
    }

    private void Start()
    {
        _mpb = new MaterialPropertyBlock();

        // Force Ambient Mode to Flat Color for reliable scripting runtime control
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

        if (DirectionalLight == null)
        {
            // Try to find by name or type
            DirectionalLight = GameObject.Find("Directional Light")?.GetComponent<Light>();
            if (DirectionalLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (Light l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        DirectionalLight = l;
                        break;
                    }
                }
            }
        }
    }

    private void Update()
    {
        if (!PauseTime)
        {
            TimeOfDay += (Time.deltaTime / DayLengthInSeconds) * 24f;
            if (TimeOfDay >= 24f)
            {
                TimeOfDay -= 24f;
            }
        }

        UpdateLighting();
    }

    private void UpdateLighting()
    {
        float timePercent = TimeOfDay / 24f;

        // Rotate the sun directional light
        if (DirectionalLight != null)
        {
            // Calculate sun X angle: at 12:00 PM (noon), it should be 90 degrees (facing down)
            // at 6:00 AM (sunrise), X = 0
            // at 6:00 PM (sunset), X = 180
            float sunAngleX = timePercent * 360f - 90f;
            DirectionalLight.transform.rotation = Quaternion.Euler(sunAngleX, -30f, 0f);

            // Update color and intensity
            DirectionalLight.color = SunColor.Evaluate(timePercent);
            DirectionalLight.intensity = SunIntensityCurve.Evaluate(timePercent);
        }

        // Update ambient color flat tinting
        RenderSettings.ambientLight = AmbientColor.Evaluate(timePercent);

        // Tint sprites and meshes for 2.5D shading
        if (SpriteLightColor != null)
        {
            Color spriteTint = SpriteLightColor.Evaluate(timePercent);

            for (int i = _tintedSprites.Count - 1; i >= 0; i--)
            {
                var item = _tintedSprites[i];
                if (item.Renderer == null)
                {
                    _tintedSprites.RemoveAt(i);
                    continue;
                }
                item.Renderer.color = item.OriginalColor * spriteTint;
            }

            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            for (int i = _tintedMeshes.Count - 1; i >= 0; i--)
            {
                var item = _tintedMeshes[i];
                if (item.Renderer == null)
                {
                    _tintedMeshes.RemoveAt(i);
                    continue;
                }
                _mpb.Clear();
                item.Renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(item.ColorPropertyName, item.OriginalColor * spriteTint);
                item.Renderer.SetPropertyBlock(_mpb);
            }
        }
    }

    public void RegisterMap(GameObject mapInstance)
    {
        _tintedSprites.Clear();
        _tintedMeshes.Clear();

        if (mapInstance == null) return;

        // Register SpriteRenderers in map
        SpriteRenderer[] sprites = mapInstance.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sprite in sprites)
        {
            // Skip light beam and bulb sprites to prevent them from darkening at night
            StreetLight light = sprite.GetComponentInParent<StreetLight>();
            if (light != null)
            {
                if (sprite == light.BulbRenderer || (light.LightVisualObject != null && sprite.transform.IsChildOf(light.LightVisualObject.transform)))
                {
                    continue;
                }
            }

            _tintedSprites.Add(new TintedSprite
            {
                Renderer = sprite,
                OriginalColor = sprite.color
            });
        }

        // Register MeshRenderers in map
        MeshRenderer[] meshes = mapInstance.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var mesh in meshes)
        {
            // Skip light beam and bulb meshes to prevent them from darkening at night
            StreetLight light = mesh.GetComponentInParent<StreetLight>();
            if (light != null)
            {
                if (mesh == light.BulbRenderer || (light.LightVisualObject != null && mesh.transform.IsChildOf(light.LightVisualObject.transform)))
                {
                    continue;
                }
            }

            Material mat = mesh.sharedMaterial;
            if (mat != null)
            {
                string propName = null;
                if (mat.HasProperty("_Color")) propName = "_Color";
                else if (mat.HasProperty("_BaseColor")) propName = "_BaseColor";

                if (propName != null)
                {
                    Color origColor = mat.HasProperty(propName) ? mat.GetColor(propName) : Color.white;
                    _tintedMeshes.Add(new TintedMesh
                    {
                        Renderer = mesh,
                        OriginalColor = origColor,
                        ColorPropertyName = propName
                    });
                }
            }
        }
    }

    private void SetupDefaultGradients()
    {
        // Sun Color Gradient (Sunrise = warm orange, Noon = white, Sunset = red/orange, Night = dark blue)
        SunColor = new Gradient();
        GradientColorKey[] gck = new GradientColorKey[5];
        GradientAlphaKey[] gak = new GradientAlphaKey[5];

        gck[0] = new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 0f);      // Midnight
        gck[1] = new GradientColorKey(new Color(1f, 0.6f, 0.3f), 0.25f);       // Sunrise (6 AM)
        gck[2] = new GradientColorKey(new Color(1f, 1f, 0.9f), 0.5f);          // Noon (12 PM)
        gck[3] = new GradientColorKey(new Color(1f, 0.4f, 0.2f), 0.75f);       // Sunset (6 PM)
        gck[4] = new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 1f);      // Midnight

        gak[0] = new GradientAlphaKey(1f, 0f);
        gak[1] = new GradientAlphaKey(1f, 0.25f);
        gak[2] = new GradientAlphaKey(1f, 0.5f);
        gak[3] = new GradientAlphaKey(1f, 0.75f);
        gak[4] = new GradientAlphaKey(1f, 1f);

        SunColor.SetKeys(gck, gak);

        // Ambient Color Gradient
        AmbientColor = new Gradient();
        GradientColorKey[] agck = new GradientColorKey[5];
        GradientAlphaKey[] agak = new GradientAlphaKey[5];

        agck[0] = new GradientColorKey(new Color(0.02f, 0.02f, 0.05f), 0f);    // Midnight
        agck[1] = new GradientColorKey(new Color(0.35f, 0.25f, 0.2f), 0.25f);  // Sunrise
        agck[2] = new GradientColorKey(new Color(0.5f, 0.6f, 0.75f), 0.5f);     // Noon
        agck[3] = new GradientColorKey(new Color(0.3f, 0.2f, 0.25f), 0.75f);   // Sunset
        agck[4] = new GradientColorKey(new Color(0.02f, 0.02f, 0.05f), 1f);    // Midnight

        agak[0] = new GradientAlphaKey(1f, 0f);
        agak[1] = new GradientAlphaKey(1f, 0.25f);
        agak[2] = new GradientAlphaKey(1f, 0.5f);
        agak[3] = new GradientAlphaKey(1f, 0.75f);
        agak[4] = new GradientAlphaKey(1f, 1f);

        AmbientColor.SetKeys(agck, agak);

        // Sun Intensity Curve
        SunIntensityCurve = new AnimationCurve(
            new Keyframe(0f, 0.05f),   // Midnight (Moonlight intensity)
            new Keyframe(0.22f, 0.05f), // Just before dawn
            new Keyframe(0.3f, 0.6f),  // Morning
            new Keyframe(0.5f, 1.2f),  // Noon
            new Keyframe(0.7f, 0.6f),  // Afternoon
            new Keyframe(0.78f, 0.05f),// Dusk
            new Keyframe(1f, 0.05f)    // Midnight
        );

        // Sprite Light Tint Gradient Setup
        SpriteLightColor = new Gradient();
        GradientColorKey[] sgck = new GradientColorKey[5];
        GradientAlphaKey[] sgak = new GradientAlphaKey[5];

        sgck[0] = new GradientColorKey(new Color(0.15f, 0.15f, 0.25f), 0f);    // Midnight
        sgck[1] = new GradientColorKey(new Color(1f, 0.9f, 0.8f), 0.25f);      // Sunrise
        sgck[2] = new GradientColorKey(Color.white, 0.5f);                      // Noon
        sgck[3] = new GradientColorKey(new Color(1f, 0.8f, 0.7f), 0.75f);      // Sunset
        sgck[4] = new GradientColorKey(new Color(0.15f, 0.15f, 0.25f), 1f);    // Midnight

        sgak[0] = new GradientAlphaKey(1f, 0f);
        sgak[1] = new GradientAlphaKey(1f, 0.25f);
        sgak[2] = new GradientAlphaKey(1f, 0.5f);
        sgak[3] = new GradientAlphaKey(1f, 0.75f);
        sgak[4] = new GradientAlphaKey(1f, 1f);

        SpriteLightColor.SetKeys(sgck, sgak);
    }
}
