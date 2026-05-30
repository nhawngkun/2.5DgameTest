using UnityEngine;
using UnityEngine.UI;

public class UIIventory : UICanvas_SSMB
{
    [Header("Inventory Data")]
    public InventoryData _InventoryData;

    [Header("UI References")]
    public Transform _ContentParent;
    public GameObject _ItemSlotPrefab;
    public Button _CloseButton;

    public override void Setup()
    {
        base.Setup();
        if (_CloseButton != null)
        {
            _CloseButton.onClick.RemoveAllListeners();
            _CloseButton.onClick.AddListener(OnCloseButtonClicked);
        }
    }

    private void OnEnable()
    {
        if (_InventoryData != null)
        {
            _InventoryData.OnInventoryChanged += RedrawInventory;
            Debug.Log($"[UIIventory] Subscribed to OnInventoryChanged (asset: {_InventoryData.name})");
        }
        else
        {
            Debug.LogError("[UIIventory] _InventoryData is NULL in OnEnable! Kéo asset vào Inspector.");
        }
    }

    private void OnDisable()
    {
        if (_InventoryData != null)
        {
            _InventoryData.OnInventoryChanged -= RedrawInventory;
        }
    }

    public override void Open()
    {
        if (_InventoryData != null)
        {
            _InventoryData.OnInventoryChanged -= RedrawInventory;
            _InventoryData.OnInventoryChanged += RedrawInventory;
        }
        base.Open();
        RedrawInventory();
    }

    public void RedrawInventory()
    {
        if (_ContentParent == null) return;

        UIInventoryItemSlot[] slots = _ContentParent.GetComponentsInChildren<UIInventoryItemSlot>(true);

        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.ClearSlot();
            }
        }

        if (_InventoryData == null) return;


        int activeSlotIndex = 0;
        foreach (var item in _InventoryData.items)
        {
            if (item.quantity <= 0) continue;

            if (activeSlotIndex < slots.Length)
            {
                UIInventoryItemSlot slot = slots[activeSlotIndex];
                if (slot != null)
                {
                    var def = _InventoryData.GetDefinition(item.type);
                    Sprite icon = (def != null) ? def.icon : null;
                    string slotName = (def != null) ? def.displayName : item.type.ToString();
                    slot.Setup(item.type, slotName, icon, item.quantity);
                }
                activeSlotIndex++;
            }
            else
            {
                Debug.LogWarning($"[UIIventory] Không đủ slots trống để hiển thị vật phẩm: {item.type}!");
            }
        }
    }

    private void OnCloseButtonClicked()
    {
        if (UIManager_SSMB.Instance != null)
        {
            UIManager_SSMB.Instance.EnableInventory(false);
        }
    }
}
