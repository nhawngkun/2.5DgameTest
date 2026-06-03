using UnityEngine;
using System;

public enum QuestType
{
    ChopTrees,
    CollectWood,
    EarnMoney
}

[System.Serializable]
public class ActiveQuest
{
    public string MapId;
    public string Description;
    public QuestType Type;
    public int TargetAmount;
    public int CurrentAmount;
    public bool IsCompleted;
    public bool IsAccepted;
    public int StartAmount;
    public bool HasBeenIntroduced;
    public int RewardMoney;
    public bool RewardClaimed;
}

[System.Serializable]
public class QuestConfig
{
    public string MapId;
    public string Description;
    public QuestType Type;
    public int TargetAmount;
    public int RewardMoney = 100;
}

public class QuestManager : MonoBehaviour
{
    private static QuestManager _instance;
    public static QuestManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<QuestManager>();
            }
            return _instance;
        }
    }

    [Header("Quest Setup Configuration (Setup Thủ Công)")]
    public System.Collections.Generic.List<QuestConfig> QuestConfigs = new System.Collections.Generic.List<QuestConfig>()
    {
        new QuestConfig() { MapId = "bien", Description = "Nhiệm vụ: Chặt đổ 2 cái Cây", Type = QuestType.ChopTrees, TargetAmount = 2, RewardMoney = 100 },
        new QuestConfig() { MapId = "nong trai", Description = "Nhiệm vụ: Thu thập 5 khúc Gỗ", Type = QuestType.CollectWood, TargetAmount = 5, RewardMoney = 150 }
    };

    public ActiveQuest CurrentQuest;
    public System.Collections.Generic.List<ActiveQuest> AllQuests = new System.Collections.Generic.List<ActiveQuest>();
    public event Action OnQuestUpdated;

    private InventoryData _cachedInventory;
    private bool _isListeningToInventory = false;

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
        TrySubscribeToPlayerInventory();
    }

    private void Update()
    {
        if (!_isListeningToInventory)
        {
            TrySubscribeToPlayerInventory();
        }
    }

    public ActiveQuest GetQuestForMap(string mapId)
    {
        return AllQuests.Find(q => q.MapId == mapId);
    }

    public bool IsQuestIntroduced(string mapId)
    {
        var q = GetQuestForMap(mapId);
        return q != null && q.HasBeenIntroduced;
    }

    public bool IsQuestAccepted(string mapId)
    {
        var q = GetQuestForMap(mapId);
        return q != null && q.IsAccepted;
    }

    public void InitializeQuestForMap(string mapId)
    {
        ActiveQuest existing = GetQuestForMap(mapId);
        if (existing != null)
        {
            CurrentQuest = existing;
            OnQuestUpdated?.Invoke();
            return;
        }

        ActiveQuest newQuest = new ActiveQuest();
        newQuest.MapId = mapId;
        newQuest.IsCompleted = false;
        newQuest.IsAccepted = false;
        newQuest.HasBeenIntroduced = false;
        newQuest.StartAmount = 0;
        newQuest.CurrentAmount = 0;

        string normalizedId = mapId.ToLower();

        QuestConfig config = QuestConfigs.Find(c => normalizedId.Contains(c.MapId.ToLower()) || c.MapId.ToLower().Contains(normalizedId));
        if (config != null)
        {
            newQuest.Description = config.Description;
            newQuest.Type = config.Type;
            newQuest.TargetAmount = config.TargetAmount;
            newQuest.RewardMoney = config.RewardMoney;
        }
        else
        {
            newQuest.Description = "Nhiệm vụ: Kiếm được 100 Tiền";
            newQuest.Type = QuestType.EarnMoney;
            newQuest.TargetAmount = 100;
            newQuest.RewardMoney = 100;
        }

        AllQuests.Add(newQuest);
        CurrentQuest = newQuest;
        OnQuestUpdated?.Invoke();

    }

    public void AcceptCurrentQuest()
    {
        if (CurrentQuest == null || CurrentQuest.IsAccepted) return;

        CurrentQuest.IsAccepted = true;
        CurrentQuest.CurrentAmount = 0;

        if (CurrentQuest.Type == QuestType.CollectWood)
        {
            CurrentQuest.StartAmount = GetCurrentWoodCount();
        }
        else if (CurrentQuest.Type == QuestType.EarnMoney)
        {
            CurrentQuest.StartAmount = GetCurrentMoneyCount();
        }
        else
        {
            CurrentQuest.StartAmount = 0;
        }

        CheckCompletion();
        OnQuestUpdated?.Invoke();
    }

    public void ClaimCurrentQuestReward()
    {
        if (CurrentQuest == null || !CurrentQuest.IsCompleted || CurrentQuest.RewardClaimed) return;

        CurrentQuest.RewardClaimed = true;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null && player._InventoryData != null)
        {
            player._InventoryData.AddMoney(CurrentQuest.RewardMoney);

            var gameplayUI = UIManager_SSMB.Instance != null ? UIManager_SSMB.Instance.GetUI<UIGameplay>() : null;
            gameplayUI?.PlayCollectFeedback($"+ {CurrentQuest.RewardMoney} Tiền (Thưởng Nhiệm Vụ)");
        }

        OnQuestUpdated?.Invoke();
    }

    private void TrySubscribeToPlayerInventory()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null && player._InventoryData != null)
        {
            _cachedInventory = player._InventoryData;
            _cachedInventory.OnInventoryChanged += HandleInventoryChanged;
            _isListeningToInventory = true;
            
            if (CurrentQuest != null && CurrentQuest.IsAccepted && !CurrentQuest.IsCompleted)
            {
                if (CurrentQuest.Type == QuestType.CollectWood)
                {
                    int delta = GetCurrentWoodCount() - CurrentQuest.StartAmount;
                    UpdateProgress(Mathf.Max(0, delta));
                }
                else if (CurrentQuest.Type == QuestType.EarnMoney)
                {
                    int delta = GetCurrentMoneyCount() - CurrentQuest.StartAmount;
                    UpdateProgress(Mathf.Max(0, delta));
                }
            }
        }
    }

    private void HandleInventoryChanged()
    {
        if (CurrentQuest == null || !CurrentQuest.IsAccepted || CurrentQuest.IsCompleted) return;

        if (CurrentQuest.Type == QuestType.CollectWood)
        {
            int delta = GetCurrentWoodCount() - CurrentQuest.StartAmount;
            UpdateProgress(Mathf.Max(0, delta));
        }
        else if (CurrentQuest.Type == QuestType.EarnMoney)
        {
            int delta = GetCurrentMoneyCount() - CurrentQuest.StartAmount;
            UpdateProgress(Mathf.Max(0, delta));
        }
    }

    private int GetCurrentWoodCount()
    {
        if (_cachedInventory == null) return 0;
        var woodItem = _cachedInventory.items.Find(i => i.type == ItemType.Wood);
        return woodItem != null ? woodItem.quantity : 0;
    }

    private int GetCurrentMoneyCount()
    {
        if (_cachedInventory == null) return 0;
        return _cachedInventory.money;
    }

    public void OnTreeChopped()
    {
        if (CurrentQuest == null || !CurrentQuest.IsAccepted || CurrentQuest.IsCompleted || CurrentQuest.Type != QuestType.ChopTrees) 
            return;

        UpdateProgress(CurrentQuest.CurrentAmount + 1);
    }

    public void OnWoodCollected()
    {
        if (CurrentQuest == null || !CurrentQuest.IsAccepted || CurrentQuest.IsCompleted || CurrentQuest.Type != QuestType.CollectWood) 
            return;

        int delta = GetCurrentWoodCount() - CurrentQuest.StartAmount;
        UpdateProgress(Mathf.Max(0, delta));
    }

    private void UpdateProgress(int newAmount)
    {
        if (CurrentQuest == null || !CurrentQuest.IsAccepted || CurrentQuest.IsCompleted) return;

        CurrentQuest.CurrentAmount = Mathf.Min(newAmount, CurrentQuest.TargetAmount);
        
        CheckCompletion();
        OnQuestUpdated?.Invoke();
    }

    private void CheckCompletion()
    {
        if (CurrentQuest == null || !CurrentQuest.IsAccepted || CurrentQuest.IsCompleted) return;

        if (CurrentQuest.CurrentAmount >= CurrentQuest.TargetAmount)
        {
            CurrentQuest.IsCompleted = true;
            Debug.Log($"[QuestManager] Quest Completed: {CurrentQuest.Description}");
            
            UIGameplay gameplayUI = UIManager_SSMB.Instance != null ? UIManager_SSMB.Instance.GetUI<UIGameplay>() : null;
            gameplayUI?.PlayCollectFeedback("Nhiệm Vụ Đã Hoàn Thành!");
        }
    }

    private void OnDestroy()
    {
        if (_cachedInventory != null)
        {
            _cachedInventory.OnInventoryChanged -= HandleInventoryChanged;
        }
    }
}
