using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class MapManager : MonoBehaviour
{
    private static MapManager _instance;
    public static MapManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<MapManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("MapManager");
                    _instance = go.AddComponent<MapManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [System.Serializable]
    public class MapConfig
    {
        public string MapId;
        public GameObject MapPrefab;
        public GameObject MapVFX;
    }

    [Header("Map Prefab Configurations")]
    public List<MapConfig> _MapConfigs = new List<MapConfig>();
    public GameObject _PlayerPrefab;
    public GameObject _QuestNpcPrefab;
    public string _InitialMapId = "Map1";
    public string _InitialPortalId = "M1_Spawn";

    [Header("Debug Info")]
    [SerializeField] private string _currentMapId;
    [SerializeField] private GameObject _currentMapInstance;

    private bool _isTransitioning = false;
    private Dictionary<string, GameObject> _instantiatedMaps = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> _instantiatedVFX = new Dictionary<string, GameObject>();

    public string CurrentMapId => _currentMapId;
    public GameObject CurrentMapInstance => _currentMapInstance;
    public Dictionary<string, GameObject> InstantiatedMaps => _instantiatedMaps;
    public bool IsTransitioning => _isTransitioning;

    public void ClearAllInstantiatedMaps()
    {
        foreach (var kvp in _instantiatedMaps)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        _instantiatedMaps.Clear();

        foreach (var kvp in _instantiatedVFX)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }
        _instantiatedVFX.Clear();

        _currentMapInstance = null;
        _currentMapId = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        bool isPrefabAsset = false;
#if UNITY_EDITOR
        if (_currentMapInstance != null && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(_currentMapInstance))
        {
            isPrefabAsset = true;
        }
#endif
        if (_currentMapInstance != null && !isPrefabAsset && !string.IsNullOrEmpty(_currentMapInstance.scene.name) && _currentMapInstance.scene.IsValid())
        {
            if (!_instantiatedMaps.ContainsKey(_InitialMapId))
            {
                _instantiatedMaps[_InitialMapId] = _currentMapInstance;
                _currentMapId = _InitialMapId;
            }
        }
        else
        {
            _currentMapInstance = null;
        }

        LoadInitialMapAsync().Forget();
    }

    private async UniTaskVoid LoadInitialMapAsync()
    {
        await UniTask.Yield();

        if (string.IsNullOrEmpty(_InitialMapId))
        {
            return;
        }

        if (SaveManager.Instance != null && SaveManager.Instance.HasSaveFile())
        {
            SaveManager.Instance.LoadGame().Forget();
        }
        else
        {
            await LoadMapSequenceAsync(_InitialMapId, _InitialPortalId);
        }
    }

    public async UniTask TransitionToPortalAsync(MapPortal sourcePortal)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        string targetPortalId = sourcePortal._TargetPortalId;
        string targetMapId = sourcePortal._TargetMapId;

        if (string.IsNullOrEmpty(targetMapId))
        {
            _isTransitioning = false;
            return;
        }

        await LoadMapSequenceAsync(targetMapId, targetPortalId);

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.Save();
        }

        _isTransitioning = false;
    }

    public async UniTask LoadMapSequenceAsync(string targetMapId, string spawnPortalId, Vector3? customSpawnPos = null)
    {
        _isTransitioning = true;
        UILoading loadingUI = UIManager_SSMB.Instance != null ? UIManager_SSMB.Instance.GetUI<UILoading>() : null;

        if (UIManager_SSMB.Instance != null)
        {
            UIManager_SSMB.Instance.EnableGameplay(false);
            UIManager_SSMB.Instance.EnableLoad(true);
        }

        if (loadingUI != null)
        {
            loadingUI.SetProgress(0.05f, "Đang khởi động chuyển cảnh...");
        }
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.4f));

        MapConfig targetConfig = _MapConfigs.Find(m => m.MapId == targetMapId);
        if (targetConfig == null || targetConfig.MapPrefab == null)
        {
            if (UIManager_SSMB.Instance != null)
                UIManager_SSMB.Instance.EnableLoad(false);
            _isTransitioning = false;
            return;
        }

        if (loadingUI != null)
            loadingUI.SetProgress(0.2f, "Đang ẩn bản đồ cũ...");

        if (_currentMapInstance != null)
        {
            _currentMapInstance.SetActive(false);
        }

        if (loadingUI != null)
            loadingUI.SetProgress(0.35f, $"Đang chuẩn bị hiển thị {targetMapId}...");

        bool alreadyInstantiated = _instantiatedMaps.TryGetValue(targetMapId, out GameObject targetMapInstance);
        if (alreadyInstantiated && targetMapInstance != null)
        {
            _currentMapInstance = targetMapInstance;
            _currentMapInstance.SetActive(true);
        }
        else
        {
            _currentMapInstance = Instantiate(targetConfig.MapPrefab);
            _instantiatedMaps[targetMapId] = _currentMapInstance;

            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.ApplySavedStateToMap(targetMapId, _currentMapInstance);
            }
        }
        _currentMapId = targetMapId;

        UpdateMapVFX(targetMapId);
        ActivateChildVFX(_currentMapInstance);

        if (DayNightCycle.Instance != null)
        {
            DayNightCycle.Instance.RegisterMap(_currentMapInstance);
        }

        await UniTask.Yield();

        var mapLoader = _currentMapInstance.GetComponentInChildren<IMapLoader>();
        if (mapLoader != null)
        {
            while (!mapLoader.IsLoaded)
            {
                float progressValue = 0.35f + (mapLoader.Progress * 0.45f);
                if (loadingUI != null)
                {
                    loadingUI.SetProgress(progressValue, $"Đang tạo các vật thể trên bản đồ ({Mathf.RoundToInt(mapLoader.Progress * 100)}%)...");
                }
                await UniTask.Yield();
            }
        }
        else
        {
            float simulatedProgress = 0.35f;
            while (simulatedProgress < 0.8f)
            {
                simulatedProgress += 0.05f;
                if (loadingUI != null)
                {
                    loadingUI.SetProgress(simulatedProgress, "Đang sắp đặt tài nguyên bản đồ...");
                }
                await UniTask.Delay(System.TimeSpan.FromSeconds(0.05f));
            }
        }

        if (loadingUI != null)
            loadingUI.SetProgress(0.85f, "Đang định vị nhân vật...");

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null && _PlayerPrefab != null)
        {
            GameObject playerGo = Instantiate(_PlayerPrefab);
            player = playerGo.GetComponent<PlayerController>();
        }

        // Programmatically spawn QuestNPC if not present in the map scene
        QuestNPC npc = _currentMapInstance.GetComponentInChildren<QuestNPC>(true);
        if (npc == null && _QuestNpcPrefab != null)
        {
            Transform npcSpawn = FindNPCSpawnPoint(_currentMapInstance.transform);
            Vector3 spawnPos;
            if (npcSpawn != null)
            {
                spawnPos = npcSpawn.position;
            }
            else
            {
                // Fallback: spawn 8 units left of the destination portal
                MapPortal destPortal = FindPortal(spawnPortalId);
                Vector3 portalPos = destPortal != null ? destPortal.transform.position : Vector3.zero;
                spawnPos = portalPos + new Vector3(-8f, 0f, 0f);
            }

            GameObject npcGo = Instantiate(_QuestNpcPrefab, spawnPos, Quaternion.identity);
            npcGo.transform.SetParent(_currentMapInstance.transform);
            npc = npcGo.GetComponent<QuestNPC>();
        }

        if (player != null)
        {
            CameraFollow cam = FindAnyObjectByType<CameraFollow>();
            if (cam != null)
            {
                cam.target = player.transform;

                MapBounds mapBounds = _currentMapInstance.GetComponentInChildren<MapBounds>();
                cam.SetBounds(mapBounds);

               
            }

            if (customSpawnPos.HasValue)
            {
                Rigidbody rb = player.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.position = customSpawnPos.Value;
                    rb.linearVelocity = Vector3.zero;
                }
                player.transform.position = customSpawnPos.Value;
            }
            else
            {
                MapPortal destPortal = FindPortal(spawnPortalId);
                if (destPortal != null)
                {
                    Transform spawn = destPortal._SpawnPoint != null ? destPortal._SpawnPoint : destPortal.transform;

                    Rigidbody rb = player.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.position = spawn.position;
                        rb.linearVelocity = Vector3.zero;
                    }
                    player.transform.position = spawn.position;
                    player.transform.rotation = spawn.rotation;
                }
                else
                {
                    Debug.LogWarning($"[MapManager] Không tìm thấy cổng đích {spawnPortalId} trong map mới!");
                }
            }
        }

        if (loadingUI != null)
            loadingUI.SetProgress(1.0f, "Hoàn tất!");

        await UniTask.Delay(System.TimeSpan.FromSeconds(0.3f));

        if (UIManager_SSMB.Instance != null)
        {
            UIManager_SSMB.Instance.EnableLoad(false);
            UIManager_SSMB.Instance.EnableGameplay(true);
        }
        _isTransitioning = false;
    }

    private MapPortal FindPortal(string portalId)
    {
        foreach (MapPortal portal in MapPortal.AllPortals)
        {
            if (portal._PortalId == portalId)
                return portal;
        }
        return null;
    }

    private Transform FindNPCSpawnPoint(Transform parent)
    {
        if (parent.name == "NPCSpawnPoint") return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindNPCSpawnPoint(child);
            if (result != null) return result;
        }
        return null;
    }

    private void UpdateMapVFX(string activeMapId)
    {
        foreach (var config in _MapConfigs)
        {
            if (config.MapVFX == null) continue;

            bool isActiveMap = (config.MapId == activeMapId);

            // Check if it is a scene object or a prefab asset
            bool isSceneObject = config.MapVFX.scene.IsValid() && !string.IsNullOrEmpty(config.MapVFX.scene.name);

            if (isSceneObject)
            {
                config.MapVFX.SetActive(isActiveMap);
            }
            else
            {
                // Prefab asset
                if (isActiveMap)
                {
                    if (!_instantiatedVFX.TryGetValue(config.MapId, out GameObject vfxInstance) || vfxInstance == null)
                    {
                        vfxInstance = Instantiate(config.MapVFX);
                        if (_currentMapInstance != null)
                        {
                            vfxInstance.transform.SetParent(_currentMapInstance.transform, false);
                        }
                        _instantiatedVFX[config.MapId] = vfxInstance;
                    }
                    vfxInstance.SetActive(true);
                }
                else
                {
                    if (_instantiatedVFX.TryGetValue(config.MapId, out GameObject vfxInstance) && vfxInstance != null)
                    {
                        vfxInstance.SetActive(false);
                    }
                }
            }
        }
    }

    private void ActivateChildVFX(GameObject mapInstance)
    {
        if (mapInstance == null) return;
        FindAndActivateVFXTransforms(mapInstance.transform);
    }

    private void FindAndActivateVFXTransforms(Transform current)
    {
        if (current.name.ToUpper().Contains("VFX"))
        {
            current.gameObject.SetActive(true);
            ParticleSystem[] particles = current.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in particles)
            {
                ps.Play();
            }
        }
        foreach (Transform child in current)
        {
            FindAndActivateVFXTransforms(child);
        }
    }
}

public interface IMapLoader
{
    bool IsLoaded { get; }
    float Progress { get; }
}