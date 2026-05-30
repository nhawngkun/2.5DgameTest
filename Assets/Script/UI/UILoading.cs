using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UILoading : UICanvas_SSMB
{
    [Header("UI References")]
    public Slider _ProgressBar;
    public TextMeshProUGUI _LoadingText;

    public override void Setup()
    {
        base.Setup();
        if (_ProgressBar != null)
            _ProgressBar.value = 0f;
        if (_LoadingText != null)
            _LoadingText.text = "Đang tải bản đồ...";
    }

    public void SetProgress(float progress, string message = "")
    {
        if (_ProgressBar != null)
            _ProgressBar.value = progress;
        
        if (_LoadingText != null && !string.IsNullOrEmpty(message))
            _LoadingText.text = message;
    }
}
