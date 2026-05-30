using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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

        [Tooltip("Kéo prefab vào đây, đã đánh dấu Addressable")]
        public AssetReference MapPrefabRef;
    }

    [Header("Map Configurations")]
    public List<MapConfig> _MapConfigs = new List<MapConfig>();

    [Tooltip("Address của Player Prefab (đã Addressable)")]
    public AssetReference _PlayerPrefabRef;

    public string _InitialMapId = "Map1";
    public string _InitialPortalId = "M1_Spawn";

    [Header("Debug Info")]
    [SerializeField] private string _currentMapId;
    [SerializeField] private GameObject _currentMapInstance;

    private bool _isTransitioning = false;

    private Dictionary<string, GameObject> _instantiatedMaps = new Dictionary<string, GameObject>();
    private Dictionary<string, AsyncOperationHandle<GameObject>> _mapHandles
        = new Dictionary<string, AsyncOperationHandle<GameObject>>();

    private AsyncOperationHandle<GameObject> _playerHandle;
    private bool _playerHandleValid = false;

    public string CurrentMapId => _currentMapId;
    public GameObject CurrentMapInstance => _currentMapInstance;
    public Dictionary<string, GameObject> InstantiatedMaps => _instantiatedMaps;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LoadInitialMapAsync().Forget();
    }

    private async UniTaskVoid LoadInitialMapAsync()
    {
        await UniTask.Yield();

        if (string.IsNullOrEmpty(_InitialMapId)) return;

        if (SaveManager.Instance != null && SaveManager.Instance.HasSaveFile())
            SaveManager.Instance.LoadGame().Forget();
        else
            await LoadMapSequenceAsync(_InitialMapId, _InitialPortalId);
    }

    public void ClearAllInstantiatedMaps()
    {
        foreach (var kvp in _instantiatedMaps)
        {
            if (kvp.Value != null)
                Addressables.ReleaseInstance(kvp.Value); 
        }
        _instantiatedMaps.Clear();

        _mapHandles.Clear();

        _currentMapInstance = null;
        _currentMapId = null;
    }

    public async UniTask TransitionToPortalAsync(MapPortal sourcePortal)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        if (!string.IsNullOrEmpty(sourcePortal._TargetMapId))
        {
            await LoadMapSequenceAsync(sourcePortal._TargetMapId, sourcePortal._TargetPortalId);
            SaveManager.Instance?.Save();
        }

        _isTransitioning = false;
    }

    public async UniTask LoadMapSequenceAsync(string targetMapId, string spawnPortalId,
                                               Vector3? customSpawnPos = null)
    {
        UILoading loadingUI = UIManager_SSMB.Instance?.GetUI<UILoading>();

        if (UIManager_SSMB.Instance != null)
        {
            UIManager_SSMB.Instance.EnableGameplay(false);
            UIManager_SSMB.Instance.EnableLoad(true);
        }

        loadingUI?.SetProgress(0.05f, "Đang khởi động chuyển cảnh...");
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.4f));

        MapConfig targetConfig = _MapConfigs.Find(m => m.MapId == targetMapId);
        if (targetConfig == null || targetConfig.MapPrefabRef == null)
        {
            Debug.LogError($"[MapManager] Không tìm thấy MapConfig cho '{targetMapId}'!");
            UIManager_SSMB.Instance?.EnableLoad(false);
            return;
        }

        loadingUI?.SetProgress(0.2f, "Đang ẩn bản đồ cũ...");
        if (_currentMapInstance != null)
            _currentMapInstance.SetActive(false);

        loadingUI?.SetProgress(0.35f, $"Đang tải {targetMapId}...");

        GameObject targetMapInstance;

        if (_instantiatedMaps.TryGetValue(targetMapId, out GameObject cached) && cached != null)
        {
            targetMapInstance = cached;
            targetMapInstance.SetActive(true);
        }
        else
        {
            var handle = Addressables.InstantiateAsync(targetConfig.MapPrefabRef);
            await handle.ToUniTask(); 

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[MapManager] Addressables InstantiateAsync thất bại: {targetMapId}");
                UIManager_SSMB.Instance?.EnableLoad(false);
                return;
            }

            targetMapInstance = handle.Result;
            _instantiatedMaps[targetMapId] = targetMapInstance;
            _mapHandles[targetMapId] = handle;

            SaveManager.Instance?.ApplySavedStateToMap(targetMapId, targetMapInstance);
        }

        _currentMapInstance = targetMapInstance;
        _currentMapId = targetMapId;

        await UniTask.Yield();

        var mapLoader = _currentMapInstance.GetComponentInChildren<IMapLoader>();
        if (mapLoader != null)
        {
            while (!mapLoader.IsLoaded)
            {
                float p = 0.35f + mapLoader.Progress * 0.45f;
                loadingUI?.SetProgress(p, $"Đang tạo vật thể ({Mathf.RoundToInt(mapLoader.Progress * 100)}%)...");
                await UniTask.Yield();
            }
        }
        else
        {
            for (float p = 0.35f; p < 0.8f; p += 0.05f)
            {
                loadingUI?.SetProgress(p, "Đang sắp đặt tài nguyên...");
                await UniTask.Delay(System.TimeSpan.FromSeconds(0.05f));
            }
        }

        // ── Spawn / tìm Player ───────────────────────────────────────────
        loadingUI?.SetProgress(0.85f, "Đang định vị nhân vật...");

        PlayerController player = FindAnyObjectByType<PlayerController>();

        if (player == null)
            player = await SpawnPlayerAsync();

        if (player != null)
        {
            CameraFollow cam = FindAnyObjectByType<CameraFollow>();
            if (cam != null) cam.target = player.transform;

            PlacePlayer(player, spawnPortalId, customSpawnPos);
        }

        // ── Hoàn tất ────────────────────────────────────────────────────
        loadingUI?.SetProgress(1.0f, "Hoàn tất!");
        await UniTask.Delay(System.TimeSpan.FromSeconds(0.3f));

        if (UIManager_SSMB.Instance != null)
        {
            UIManager_SSMB.Instance.EnableLoad(false);
            UIManager_SSMB.Instance.EnableGameplay(true);
        }
    }

    // ── Spawn player bằng Addressables ──────────────────────────────────
    private async UniTask<PlayerController> SpawnPlayerAsync()
    {
        if (_PlayerPrefabRef == null)
        {
            Debug.LogWarning("[MapManager] _PlayerPrefabRef chưa được gán!");
            return null;
        }

        // Release player cũ nếu có
        if (_playerHandleValid)
        {
            Addressables.ReleaseInstance(_playerHandle);
            _playerHandleValid = false;
        }

        var handle = Addressables.InstantiateAsync(_PlayerPrefabRef);
        await handle.ToUniTask();

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError("[MapManager] Không thể spawn Player qua Addressables!");
            return null;
        }

        _playerHandle = handle;
        _playerHandleValid = true;

        return handle.Result.GetComponent<PlayerController>();
    }

    // ── Đặt vị trí player ───────────────────────────────────────────────
    private void PlacePlayer(PlayerController player, string spawnPortalId, Vector3? customPos)
    {
        Rigidbody rb = player.GetComponent<Rigidbody>();

        if (customPos.HasValue)
        {
            if (rb != null) { rb.position = customPos.Value; rb.linearVelocity = Vector3.zero; }
            player.transform.position = customPos.Value;
            return;
        }

        MapPortal destPortal = FindPortal(spawnPortalId);
        if (destPortal != null)
        {
            Transform spawn = destPortal._SpawnPoint != null ? destPortal._SpawnPoint : destPortal.transform;
            if (rb != null) { rb.position = spawn.position; rb.linearVelocity = Vector3.zero; }
            player.transform.position = spawn.position;
            player.transform.rotation = spawn.rotation;
        }
        else
        {
            Debug.LogWarning($"[MapManager] Không tìm thấy portal '{spawnPortalId}'!");
        }
    }

    private MapPortal FindPortal(string portalId)
    {
        foreach (MapPortal p in MapPortal.AllPortals)
            if (p._PortalId == portalId) return p;
        return null;
    }
}

public interface IMapLoader
{
    bool IsLoaded { get; }
    float Progress { get; }
}