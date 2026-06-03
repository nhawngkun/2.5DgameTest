using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class SaveManager : MonoBehaviour
{
    private static SaveManager _instance;
    public static SaveManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<SaveManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("SaveManager");
                    _instance = go.AddComponent<SaveManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [Header("Inventory Ref (Optional lookup if not on Player)")]
    public InventoryData _InventoryData;

    private SaveData _currentSaveData = null;
    public SaveData CurrentSaveData => _currentSaveData;

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, "savegame.json");

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

    private void Update()
    {
      
      
    }

    public bool HasSaveFile()
    {
        return File.Exists(SaveFilePath);
    }

    public void Save()
    {
        try
        {
            SaveData data = new SaveData();

            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                data.playerPosition = new Vector3Save(player.transform.position);
            }
            else
            {
                data.playerPosition = new Vector3Save(Vector3.zero);
            }

            data.currentMapId = MapManager.Instance.CurrentMapId;

            InventoryData inv = _InventoryData;
            if (inv == null && player != null)
            {
                inv = player._InventoryData;
            }

            if (inv != null)
            {
                data.money = inv.money;
                data.inventoryItems = new List<InventoryItemSave>();
                foreach (var item in inv.items)
                {
                    data.inventoryItems.Add(new InventoryItemSave
                    {
                        type = item.type,
                        quantity = item.quantity
                    });
                }
            }

            data.mapStates = new List<MapSaveData>();
            foreach (var kvp in MapManager.Instance.InstantiatedMaps)
            {
                string mapId = kvp.Key;
                GameObject mapGo = kvp.Value;
                if (mapGo == null) continue;

                MapSaveData mapData = new MapSaveData();
                mapData.mapId = mapId;

                ChoppableTree[] trees = mapGo.GetComponentsInChildren<ChoppableTree>(true);
                mapData.trees = new List<TreeSaveData>();
                foreach (var tree in trees)
                {
                    string treeId = $"Tree_{tree.transform.localPosition.x:F2}_{tree.transform.localPosition.y:F2}_{tree.transform.localPosition.z:F2}";
                    mapData.trees.Add(new TreeSaveData
                    {
                        treeId = treeId,
                        currentHits = tree.CurrentHits,
                        isChopped = tree.IsChopped
                    });
                }

                WoodLoot[] woodLoots = mapGo.GetComponentsInChildren<WoodLoot>(true);
                mapData.woodLoots = new List<WoodLootSaveData>();
                foreach (var wood in woodLoots)
                {
                    if (wood.gameObject.activeSelf)
                    {
                        mapData.woodLoots.Add(new WoodLootSaveData
                        {
                            position = new Vector3Save(wood.transform.position)
                        });
                    }
                }

                Campfire[] campfires = mapGo.GetComponentsInChildren<Campfire>(true);
                mapData.campfires = new List<CampfireSaveData>();
                foreach (var cf in campfires)
                {
                    string cfId = $"Campfire_{cf.transform.localPosition.x:F2}_{cf.transform.localPosition.y:F2}_{cf.transform.localPosition.z:F2}";
                    mapData.campfires.Add(new CampfireSaveData
                    {
                        campfireId = cfId,
                        isBurning = cf.IsBurningVal,
                        burnTimer = cf.BurnTimer
                    });
                }
                // Save AI NPC position
                QuestNPC npc = mapGo.GetComponentInChildren<QuestNPC>(true);
                mapData.ais = new List<AISaveData>();
                if (npc != null)
                {
                    mapData.ais.Add(new AISaveData
                    {
                        aiId = "QuestNPC",
                        position = new Vector3Save(npc.transform.position)
                    });
                }

                data.mapStates.Add(mapData);
            }

            // Save Quests
            data.questStates = new List<QuestSaveData>();
            if (QuestManager.Instance != null)
            {
                foreach (var q in QuestManager.Instance.AllQuests)
                {
                    data.questStates.Add(new QuestSaveData
                    {
                        mapId = q.MapId,
                        description = q.Description,
                        type = q.Type,
                        targetAmount = q.TargetAmount,
                        currentAmount = q.CurrentAmount,
                        isCompleted = q.IsCompleted,
                        isAccepted = q.IsAccepted,
                        startAmount = q.StartAmount,
                        hasBeenIntroduced = q.HasBeenIntroduced,
                        rewardMoney = q.RewardMoney,
                        rewardClaimed = q.RewardClaimed
                    });
                }
            }

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SaveFilePath, json);
            _currentSaveData = data;

        
        }
        catch (Exception ex)
        {
        }
    }

    public async UniTaskVoid LoadGame()
    {
        if (!HasSaveFile())
        {
           
            return;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            _currentSaveData = data;

            MapManager.Instance.ClearAllInstantiatedMaps();

            PlayerController player = FindAnyObjectByType<PlayerController>();
            InventoryData inv = _InventoryData;
            if (inv == null && player != null)
            {
                inv = player._InventoryData;
            }

            if (inv != null)
            {
                inv.Clear();
                foreach (var itemSave in data.inventoryItems)
                {
                    inv.AddItem(itemSave.type, itemSave.quantity);
                }
                inv.money = data.money;
                inv.AddMoney(0); 
            }

            if (QuestManager.Instance != null && data.questStates != null)
            {
                QuestManager.Instance.AllQuests.Clear();
                foreach (var qs in data.questStates)
                {
                    QuestManager.Instance.AllQuests.Add(new ActiveQuest
                    {
                        MapId = qs.mapId,
                        Description = qs.description,
                        Type = qs.type,
                        TargetAmount = qs.targetAmount,
                        CurrentAmount = qs.currentAmount,
                        IsCompleted = qs.isCompleted,
                        IsAccepted = qs.isAccepted,
                        StartAmount = qs.startAmount,
                        HasBeenIntroduced = qs.hasBeenIntroduced,
                        RewardMoney = qs.rewardMoney,
                        RewardClaimed = qs.rewardClaimed
                    });
                }

                if (!string.IsNullOrEmpty(data.currentMapId))
                {
                    QuestManager.Instance.InitializeQuestForMap(data.currentMapId);
                }
            }

            if (!string.IsNullOrEmpty(data.currentMapId))
            {
                await MapManager.Instance.LoadMapSequenceAsync(data.currentMapId, null, data.playerPosition.ToVector3());
            }

           
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Lỗi khi tải game: {ex.Message}");
        }
    }

    public void ApplySavedStateToMap(string mapId, GameObject mapInstance)
    {
        if (_currentSaveData == null)
        {
            if (HasSaveFile())
            {
                try
                {
                    string json = File.ReadAllText(SaveFilePath);
                    _currentSaveData = JsonUtility.FromJson<SaveData>(json);
                }
                catch
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

        if (_currentSaveData == null || _currentSaveData.mapStates == null) return;

        MapSaveData mapSave = _currentSaveData.mapStates.Find(m => m.mapId == mapId);
        if (mapSave == null) return;

        ChoppableTree[] trees = mapInstance.GetComponentsInChildren<ChoppableTree>(true);
        foreach (var tree in trees)
        {
            string treeId = $"Tree_{tree.transform.localPosition.x:F2}_{tree.transform.localPosition.y:F2}_{tree.transform.localPosition.z:F2}";
            TreeSaveData treeSave = mapSave.trees.Find(t => t.treeId == treeId);
            if (treeSave != null)
            {
                tree.LoadState(treeSave.currentHits, treeSave.isChopped);
            }
        }

        WoodLoot[] existingLoots = mapInstance.GetComponentsInChildren<WoodLoot>(true);
        foreach (var loot in existingLoots)
        {
            if (WoodPool.Instance != null)
                WoodPool.Instance.Recycle(loot.gameObject);
            else
                Destroy(loot.gameObject);
        }

        foreach (var lootSave in mapSave.woodLoots)
        {
            Vector3 pos = lootSave.position.ToVector3();
            GameObject woodObj = null;

            if (WoodPool.Instance != null && WoodPool.Instance._WoodPrefab != null)
            {
                woodObj = WoodPool.Instance.Spawn(pos, Quaternion.Euler(45f, 0f, 0f));
            }
            else
            {
                if (trees.Length > 0 && trees[0].woodPrefab != null)
                {
                    woodObj = Instantiate(trees[0].woodPrefab, pos, Quaternion.Euler(45f, 0f, 0f));
                }
            }

            if (woodObj != null)
            {
                woodObj.transform.SetParent(mapInstance.transform);
                
                WoodLoot woodLoot = woodObj.GetComponent<WoodLoot>();
                if (woodLoot != null)
                {
                    woodLoot.InitializeLanded(pos);
                }
            }

            if (mapSave.campfires != null)
            {
                Campfire[] campfires = mapInstance.GetComponentsInChildren<Campfire>(true);
                foreach (var cf in campfires)
                {
                    string cfId = $"Campfire_{cf.transform.localPosition.x:F2}_{cf.transform.localPosition.y:F2}_{cf.transform.localPosition.z:F2}";
                    CampfireSaveData cfSave = mapSave.campfires.Find(c => c.campfireId == cfId);
                    if (cfSave != null)
                    {
                        cf.LoadState(cfSave.isBurning, cfSave.burnTimer);
                    }
                }
            }

        }

        if (mapSave.ais != null && mapSave.ais.Count > 0)
        {
            QuestNPC npc = mapInstance.GetComponentInChildren<QuestNPC>(true);
            if (npc != null)
            {
                var aiData = mapSave.ais.Find(a => a.aiId == "QuestNPC");
                if (aiData != null)
                {
                    npc.LoadPosition(aiData.position.ToVector3());
                }
            }
        }
    }

    private void ShowFeedback(string message)
    {
        if (UIManager_SSMB.Instance != null)
        {
            var gameplayUI = UIManager_SSMB.Instance.GetUI<UIGameplay>();
            gameplayUI?.PlayCollectFeedback(message);
        }
    }
}


[System.Serializable]
public class QuestSaveData
{
    public string mapId;
    public string description;
    public QuestType type;
    public int targetAmount;
    public int currentAmount;
    public bool isCompleted;
    public bool isAccepted;
    public int startAmount;
    public bool hasBeenIntroduced;
    public int rewardMoney;
    public bool rewardClaimed;
}

[System.Serializable]
public class SaveData
{
    public string currentMapId;
    public Vector3Save playerPosition;
    public List<InventoryItemSave> inventoryItems = new List<InventoryItemSave>();
    public int money;
    public List<MapSaveData> mapStates = new List<MapSaveData>();
    public List<QuestSaveData> questStates = new List<QuestSaveData>();
}

[System.Serializable]
public class Vector3Save
{
    public float x;
    public float y;
    public float z;

    public Vector3Save(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public Vector3 ToVector3() => new Vector3(x, y, z);
}

[System.Serializable]
public class InventoryItemSave
{
    public ItemType type;
    public int quantity;
}

[System.Serializable]
public class MapSaveData
{
    public string mapId;
    public List<TreeSaveData> trees = new List<TreeSaveData>();
    public List<WoodLootSaveData> woodLoots = new List<WoodLootSaveData>();
    public List<CampfireSaveData> campfires = new List<CampfireSaveData>();
    public List<AISaveData> ais = new List<AISaveData>();
}

[System.Serializable]
public class CampfireSaveData
{
    public string campfireId;
    public bool isBurning;
    public float burnTimer;
}

[System.Serializable]
public class TreeSaveData
{
    public string treeId;
    public int currentHits;
    public bool isChopped;
}

[System.Serializable]
public class WoodLootSaveData
{
    public Vector3Save position;
}

[System.Serializable]
public class AISaveData
{
    public string aiId;
    public Vector3Save position;
}
