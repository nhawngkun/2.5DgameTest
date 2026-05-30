using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class UIGameplay : UICanvas_SSMB
{
    [Header("Inventory Data")]
    public InventoryData _InventoryData;

    [Header("UI Elements")]
    public TextMeshProUGUI _MoneyText;
    public TextMeshProUGUI _CollectPromptText;
    public Button _InventoryButton;

    private int lastMoney = -1;
    private Vector3 originalPromptLocalPos;
    private bool isAnimatingPrompt = false;

    public override void Setup()
    {
        base.Setup();
        
        if (_InventoryButton != null)
        {
            _InventoryButton.onClick.RemoveAllListeners();
            _InventoryButton.onClick.AddListener(OnInventoryButtonClicked);
        }

        if (_CollectPromptText != null)
        {
            originalPromptLocalPos = _CollectPromptText.transform.localPosition;
            _CollectPromptText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (_InventoryData != null && _MoneyText != null)
        {
            if (_InventoryData.money != lastMoney)
            {
                lastMoney = _InventoryData.money;
                _MoneyText.text = lastMoney.ToString();
            }
        }
    }

    public void ShowCollectPrompt(bool show, string customText = "Nhấn J để nhặt")
    {
        if (_CollectPromptText == null) return;

        if (show)
        {
            if (!isAnimatingPrompt)
            {
                _CollectPromptText.gameObject.SetActive(true);
                _CollectPromptText.alpha = 1f;
                _CollectPromptText.transform.localPosition = originalPromptLocalPos;
                _CollectPromptText.text = customText;
            }
        }
        else
        {
            if (!isAnimatingPrompt)
            {
                _CollectPromptText.gameObject.SetActive(false);
            }
        }
    }

    public void PlayCollectFeedback(string textToShow)
    {
        if (_CollectPromptText == null) return;

        _CollectPromptText.DOKill();
        _CollectPromptText.transform.DOKill();

        _CollectPromptText.text = textToShow;
        _CollectPromptText.gameObject.SetActive(true);
        _CollectPromptText.alpha = 1f;
        _CollectPromptText.transform.localPosition = originalPromptLocalPos;

        isAnimatingPrompt = true;

        _CollectPromptText.transform.DOLocalMoveY(originalPromptLocalPos.y + 80f, 1f).SetEase(Ease.OutCubic);
        _CollectPromptText.DOFade(0f, 1f).OnComplete(() =>
        {
            _CollectPromptText.gameObject.SetActive(false);
            isAnimatingPrompt = false;
        });
    }

    public void OnInventoryButtonClicked()
    {
        if (UIManager_SSMB.Instance != null)
        {
            bool isOpened = UIManager_SSMB.Instance.IsUIOpened<UIIventory>();
            if (isOpened)
            {
                UIManager_SSMB.Instance.EnableInventory(false);
            }
            else
            {
                UIManager_SSMB.Instance.EnableInventory(true);
            }
        }
    }

   
    public void OnCollectPromptClicked()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            player.TryInteractWithPrompt();
        }
    }
}

