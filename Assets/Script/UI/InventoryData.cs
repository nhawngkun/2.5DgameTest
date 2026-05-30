using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Wood
}

[CreateAssetMenu(fileName = "InventoryData", menuName = "Inventory/InventoryData")]
public class InventoryData : ScriptableObject
{
    [Serializable]
    public class ItemDefinition
    {
        public ItemType type;
        public string displayName;
        public Sprite icon;
    }

    [Serializable]
    public class InventoryItem
    {
        public ItemType type;
        public int quantity;
    }

    [Header("Item Definitions")]
    public List<ItemDefinition> itemDefinitions = new List<ItemDefinition>();

    [Header("Inventory Status")]
    public List<InventoryItem> items = new List<InventoryItem>();
    public int money = 0;

    [Header("Play Mode")]
    [Tooltip("Tự động xoá dữ liệu cũ khi vào Play Mode (tránh data thừa từ session trước)")]
    public bool resetOnPlay = true;

    public event Action OnInventoryChanged;

   
    public void RuntimeInit()
    {
        if (resetOnPlay)
        {
            items.Clear();
            money = 0;
        }
    }

    public void AddItem(ItemType type, int quantity)
    {
        InventoryItem existing = items.Find(i => i.type == type);
        if (existing != null)
        {
            existing.quantity += quantity;
        }
        else
        {
            items.Add(new InventoryItem { type = type, quantity = quantity });
        }
        OnInventoryChanged?.Invoke();
    }

    public void AddMoney(int amount)
    {
        money += amount;
        OnInventoryChanged?.Invoke();
    }

    public ItemDefinition GetDefinition(ItemType type)
    {
        return itemDefinitions.Find(d => d.type == type);
    }

    public void Clear()
    {
        items.Clear();
        money = 0;
        OnInventoryChanged?.Invoke();
    }
}
