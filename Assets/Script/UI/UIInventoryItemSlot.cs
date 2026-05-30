using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIInventoryItemSlot : MonoBehaviour
{
    public Image _IconImage;
    public TextMeshProUGUI _QuantityText;
    public Button _SlotButton;

    private ItemType _itemType;
    private bool _hasItem = false;

    private void Awake()
    {
        ClearSlot();
    }

    public void Setup(ItemType type, string itemName, Sprite icon, int quantity)
    {
        _itemType = type;
        _hasItem = true;

        if (_IconImage != null)
        {
            _IconImage.sprite = icon;
            _IconImage.gameObject.SetActive(icon != null);
        }

        if (_QuantityText != null)
        {
            _QuantityText.text = quantity.ToString();
            _QuantityText.gameObject.SetActive(quantity > 0);
        }

        if (_SlotButton != null)
        {
            _SlotButton.onClick.RemoveAllListeners();
            _SlotButton.onClick.AddListener(OnSlotClicked);
        }
    }

    public void ClearSlot()
    {
        _hasItem = false;
        if (_IconImage != null)
        {
            _IconImage.sprite = null;
            _IconImage.gameObject.SetActive(false);
        }

        if (_QuantityText != null)
        {
            _QuantityText.text = "";
            _QuantityText.gameObject.SetActive(false);
        }

        if (_SlotButton != null)
        {
            _SlotButton.onClick.RemoveAllListeners();
        }
    }

    private void OnSlotClicked()
    {
        if (_hasItem)
        {
            Debug.Log($"[InventorySlot] Đã bấm vào vật phẩm: {_itemType}");
        }
    }
}
