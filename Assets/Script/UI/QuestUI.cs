using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class QuestUI : UICanvas_SSMB
{
    [Header("Quest UI References")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descText;
    [SerializeField] private TextMeshProUGUI _progressText;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TextMeshProUGUI _buttonText;

    private void Start()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestUpdated += RefreshText;
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(CloseQuestUI);
            _closeButton.onClick.AddListener(CloseQuestUI);
        }
    }

    public override void Open()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.CurrentQuest != null)
        {
            QuestManager.Instance.CurrentQuest.HasBeenIntroduced = true;
        }
        RefreshText();
        base.Open();
    }

    private void CloseQuestUI()
    {
        if (QuestManager.Instance != null && QuestManager.Instance.CurrentQuest != null)
        {
            var q = QuestManager.Instance.CurrentQuest;
            if (q.IsCompleted && !q.RewardClaimed)
            {
                QuestManager.Instance.ClaimCurrentQuestReward();
            }
            else if (!q.IsAccepted)
            {
                QuestManager.Instance.AcceptCurrentQuest();
            }
        }

        if (UIManager_SSMB.Instance != null)
        {
            UIManager_SSMB.Instance.EnableQuestUI(false);
        }
        else
        {
            CloseDirectly();
        }
    }

    public void RefreshText()
    {
        if (QuestManager.Instance == null || QuestManager.Instance.CurrentQuest == null) 
            return;

        var q = QuestManager.Instance.CurrentQuest;
        if (_titleText != null) _titleText.text = "NHIỆM VỤ";
        if (_descText != null) _descText.text = q.Description;

        if (_progressText != null)
        {
            if (q.RewardClaimed)
            {
                _progressText.text = "ĐÃ NHẬN THƯỞNG!";
                _progressText.color = new Color(0.18f, 0.8f, 0.44f);
            }
            else if (q.IsCompleted)
            {
                _progressText.text = "ĐÃ HOÀN THÀNH!";
                _progressText.color = new Color(0.18f, 0.8f, 0.44f);
            }
            else if (!q.IsAccepted)
            {
                _progressText.text = $"Mục tiêu: {q.TargetAmount}";
                _progressText.color = new Color(0.95f, 0.6f, 0.07f);
            }
            else
            {
                _progressText.text = $"Tiến độ: {q.CurrentAmount} / {q.TargetAmount}";
                _progressText.color = new Color(0.95f, 0.6f, 0.07f);
            }
        }

        if (_buttonText != null)
        {
            if (q.RewardClaimed)
            {
                _buttonText.text = "ĐÓNG";
            }
            else if (q.IsCompleted)
            {
                _buttonText.text = "NHẬN THƯỞNG";
            }
            else if (!q.IsAccepted)
            {
                _buttonText.text = "ĐỒNG Ý";
            }
            else
            {
                _buttonText.text = "ĐÓNG";
            }
        }
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestUpdated -= RefreshText;
        }
    }
}
