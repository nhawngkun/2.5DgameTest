using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;


public enum PanelAnimationType
{
    Fade,             // Mờ dần / đậm dần
    SlideFromTop,     // Cuộn từ trên xuống
    PopupScale,       // Phóng to từ tâm + nảy nhẹ
    SlideFromBottom,  // Trượt từ dưới lên (mobile sheet)
    FlipDown,         // Lật 3D theo trục X từ trên xuống
    Unfold,           // Mở ra như tờ giấy gấp (scaleY → scaleX)
    SpiralIn,         // Xoáy tròn 2 vòng trong khi phóng to
    Glitch,           // Nhiễu / rung rồi cố định (cyberpunk)
}


[System.Serializable]
public class PanelConfig
{
    public CanvasGroup canvasGroup;
    public PanelAnimationType animationType = PanelAnimationType.Fade;

    [HideInInspector] public float originalAnchoredY;
    [HideInInspector] public bool originalYCached;


    [HideInInspector] public Vector3 originalScale = Vector3.one;
    [HideInInspector] public bool originalScaleCached;
}

public class UIManager_SSMB : Singleton_SSMB<UIManager_SSMB>
{
    [SerializeField] private List<UICanvas_SSMB> uiCanvases;

    [Header("── First Open UI ──────────────────────")]
    [Tooltip("Kéo UICanvas muốn hiển thị ngay khi bắt đầu game (không bị ẩn đi rồi hiện lại)")]
    [SerializeField] private UICanvas_SSMB _FirstOpenUI;

    [Header("── Panel Configs ──────────────────────")]
    [SerializeField] private PanelConfig tutorial;
    [SerializeField] private PanelConfig home;
    [SerializeField] private PanelConfig winPopUp;
    [SerializeField] private PanelConfig lossPopUp;
    [SerializeField] private PanelConfig settingPanel;
    [SerializeField] private PanelConfig gamplayPanel;
    [SerializeField] private PanelConfig levelPanel;
    [SerializeField] private PanelConfig shopPanel;
    [SerializeField] private PanelConfig iventoryPanel;
    [SerializeField] private PanelConfig loadPanel;
    [SerializeField] private PanelConfig questPanel;

    [Header("── Thời gian animation (giây) ────────")]
    [SerializeField] private float animTime = 0.5f;

    [Header("── Slide offset (% chiều cao màn hình)")]
    [Tooltip("1.0 = đúng 1 màn hình, 0.5 = nửa màn hình")]
    [SerializeField][Range(0.3f, 2f)] private float slideHeightMultiplier = 1f;

    [Header("── FlipDown – góc bắt đầu (độ) ─────────")]
    [Tooltip("90 = lật hoàn toàn từ ngang, 45 = lật nhẹ")]
    [SerializeField][Range(30f, 120f)] private float flipStartAngle = 90f;

    public Transform _effects;
    private bool isPaused = false;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    public override void Awake()
    {
        base.Awake();

        if (uiCanvases == null)
        {
            uiCanvases = new List<UICanvas_SSMB>();
        }

        // Try to get QuestUI from questPanel config first
        QuestUI questUI = null;
        if (questPanel != null && questPanel.canvasGroup != null)
        {
            questUI = questPanel.canvasGroup.GetComponent<QuestUI>();
        }

        // If not found in questPanel, try to find any in the scene
        if (questUI == null)
        {
            questUI = FindAnyObjectByType<QuestUI>();
        }

        // Ensure registered in uiCanvases list
        if (questUI != null && !uiCanvases.Contains(questUI))
        {
            uiCanvases.Add(questUI);
        }

        InitializeUICanvases();
        CacheOriginalPositions();
    }

    void Start()
    {
        if (Instance == null)
        {
            return;
        }

        GetUI<UIIventory>()?._InventoryData?.RuntimeInit();

      
    }



    private void CacheOriginalPositions()
    {
        CacheY(tutorial); CacheScale(tutorial);
        CacheY(home); CacheScale(home);
        CacheY(winPopUp); CacheScale(winPopUp);
        CacheY(lossPopUp); CacheScale(lossPopUp);
        CacheY(settingPanel); CacheScale(settingPanel);
        CacheY(gamplayPanel); CacheScale(gamplayPanel);
        CacheY(levelPanel); CacheScale(levelPanel);
        CacheY(shopPanel); CacheScale(shopPanel);
        CacheY(iventoryPanel); CacheScale(iventoryPanel);
        CacheY(loadPanel); CacheScale(loadPanel);
        CacheY(questPanel); CacheScale(questPanel);
    }

    private void CacheScale(PanelConfig config)
    {
        if (config?.canvasGroup == null || config.originalScaleCached) return;
        config.originalScale = config.canvasGroup.transform.localScale;
        config.originalScaleCached = true;
    }

    private void CacheY(PanelConfig config)
    {
        if (config?.canvasGroup == null || config.originalYCached) return;
        RectTransform rt = config.canvasGroup.GetComponent<RectTransform>();
        if (rt != null)
        {
            config.originalAnchoredY = rt.anchoredPosition.y;
            config.originalYCached = true;
        }
    }


    private UICanvas_SSMB GetUICanvas(PanelConfig config)
    {
        if (config?.canvasGroup == null) return null;
        return config.canvasGroup.GetComponent<UICanvas_SSMB>();
    }

    private Sequence ShakeAnchorPos(RectTransform rt, float duration,
                                    float strengthX, float strengthY, int steps = 8)
    {
        Vector2 origin = rt.anchoredPosition;
        Sequence s = DOTween.Sequence();
        float stepTime = duration / steps;
        for (int i = 0; i < steps; i++)
        {
            float progress = 1f - (float)i / steps;         
            float ox = Random.Range(-strengthX, strengthX) * progress;
            float oy = Random.Range(-strengthY, strengthY) * progress;
            s.Append(rt.DOAnchorPos(origin + new Vector2(ox, oy), stepTime)
                       .SetEase(Ease.Linear));
        }
        s.Append(rt.DOAnchorPos(origin, stepTime * 0.5f));  
        return s;
    }


    private void AnimatePanel(PanelConfig config, bool enable,
                              bool callSetup = true, bool callOpen = false,
                              float openDelay = 0f)
    {
        if (config?.canvasGroup == null) return;

        CanvasGroup cg = config.canvasGroup;
        UICanvas_SSMB canvas = GetUICanvas(config);

        if (enable)
        {
            if (callSetup && canvas != null) canvas.Setup();
            if (callOpen && canvas != null) canvas.Open();
            cg.blocksRaycasts = true;
            cg.interactable = true;
            PlayOpenAnimation(config, cg, openDelay);
        }
        else
        {
            cg.blocksRaycasts = false;
            cg.interactable = false;
            PlayCloseAnimation(config, cg);
        }
    }

    // ── Open animations ──────────────────────────────────────────────────────

    private void PlayOpenAnimation(PanelConfig config, CanvasGroup cg, float delay)
    {
        RectTransform rt = cg.GetComponent<RectTransform>();

        switch (config.animationType)
        {
            case PanelAnimationType.Fade:
                cg.alpha = 0f;
                cg.DOFade(1f, animTime).SetDelay(delay).Play();
                break;

            case PanelAnimationType.SlideFromTop:
                if (rt == null) { PlayFadeOpen(cg, delay); return; }
                CacheY(config);
                float topOffset = Screen.height * slideHeightMultiplier;
                cg.alpha = 0f;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
                                                  config.originalAnchoredY + topOffset);
                rt.DOAnchorPosY(config.originalAnchoredY, animTime)
                  .SetDelay(delay).SetEase(Ease.OutCubic).Play();
                cg.DOFade(1f, animTime * 0.4f).SetDelay(delay).Play();
                break;

            case PanelAnimationType.SlideFromBottom:
                if (rt == null) { PlayFadeOpen(cg, delay); return; }
                CacheY(config);
                float botOffset = Screen.height * slideHeightMultiplier;
                cg.alpha = 0f;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
                                                  config.originalAnchoredY - botOffset);
                rt.DOAnchorPosY(config.originalAnchoredY, animTime)
                  .SetDelay(delay).SetEase(Ease.OutQuint).Play();
                cg.DOFade(1f, animTime * 0.45f).SetDelay(delay).Play();
                break;

            case PanelAnimationType.PopupScale:
                if (rt == null) { PlayFadeOpen(cg, delay); return; }
                cg.alpha = 1f;
                rt.localScale = Vector3.zero;
                rt.DOScale(config.originalScale, animTime)
                  .SetDelay(delay).SetEase(Ease.OutBack).Play();
                cg.DOFade(1f, animTime * 0.5f).SetDelay(delay).Play();
                break;

            case PanelAnimationType.FlipDown:
                if (rt == null) { PlayFadeOpen(cg, delay); return; }
                cg.alpha = 1f;
                rt.pivot = new Vector2(0.5f, 1f);
                rt.localRotation = Quaternion.Euler(-flipStartAngle, 0f, 0f);
                rt.DOLocalRotate(Vector3.zero, animTime)
                  .SetDelay(delay).SetEase(Ease.OutBack).Play();
                cg.DOFade(1f, animTime * 0.4f).SetDelay(delay).Play();
                break;

            case PanelAnimationType.Unfold:
                if (rt == null) { PlayFadeOpen(cg, delay); return; }
                rt.localScale = new Vector3(0f, 0f, 1f);
                cg.alpha = 1f;
                float halfOpen = animTime * 0.5f;
                Sequence unfoldOpen = DOTween.Sequence().SetDelay(delay);
                unfoldOpen.Append(rt.DOScaleY(config.originalScale.y, halfOpen)
                                    .SetEase(Ease.OutCubic));
                unfoldOpen.Append(rt.DOScaleX(config.originalScale.x, halfOpen)
                                    .SetEase(Ease.OutBack));
                unfoldOpen.Play();
                break;

            case PanelAnimationType.SpiralIn:
                if (rt == null) { PlayFadeOpen(cg, delay); return; }
                cg.alpha = 0f;
                rt.localScale = Vector3.zero;
                rt.localRotation = Quaternion.Euler(0f, 0f, 720f);
                float spiralIn = animTime * 1.2f;
                rt.DOScale(config.originalScale, spiralIn)
                  .SetDelay(delay).SetEase(Ease.OutQuart).Play();
                rt.DOLocalRotate(Vector3.zero, spiralIn)
                  .SetDelay(delay).SetEase(Ease.OutQuart).Play();
                cg.DOFade(1f, spiralIn * 0.4f).SetDelay(delay).Play();
                break;

            case PanelAnimationType.Glitch:
                if (rt == null) { PlayFadeOpen(cg, delay); return; }
                CacheY(config);
                cg.alpha = 0f;
                rt.localScale = config.originalScale;
                Sequence glitchOpen = DOTween.Sequence().SetDelay(delay);
                glitchOpen.Append(cg.DOFade(0.9f, 0.06f));
                glitchOpen.Append(cg.DOFade(0.1f, 0.05f));
                glitchOpen.Append(cg.DOFade(1.0f, 0.06f));
                glitchOpen.Append(cg.DOFade(0.2f, 0.04f));
                glitchOpen.Append(cg.DOFade(0.9f, 0.05f));
                glitchOpen.Append(cg.DOFade(0.0f, 0.04f));
                glitchOpen.AppendCallback(() =>
                    ShakeAnchorPos(rt, animTime * 0.5f, 18f, 10f, steps: 10)
                       .OnComplete(() => rt.anchoredPosition =
                           new Vector2(rt.anchoredPosition.x, config.originalAnchoredY))
                       .Play());
                glitchOpen.Append(cg.DOFade(1f, animTime * 0.35f).SetEase(Ease.OutQuart));
                glitchOpen.Play();
                break;
        }
    }

    private void PlayFadeOpen(CanvasGroup cg, float delay)
    {
        cg.alpha = 0f;
        cg.DOFade(1f, animTime).SetDelay(delay).Play();
    }


    private void PlayCloseAnimation(PanelConfig config, CanvasGroup cg)
    {
        RectTransform rt = cg.GetComponent<RectTransform>();

        switch (config.animationType)
        {
            case PanelAnimationType.Fade:
                cg.DOFade(0f, animTime)
                  .OnComplete(() => cg.alpha = 0f).Play();
                break;

            case PanelAnimationType.SlideFromTop:
                if (rt == null) { PlayFadeClose(cg); return; }
                float topOff = Screen.height * slideHeightMultiplier;
                cg.DOFade(0f, animTime * 0.5f).Play();
                rt.DOAnchorPosY(config.originalAnchoredY + topOff, animTime)
                  .SetEase(Ease.InCubic)
                  .OnComplete(() =>
                  {
                      cg.alpha = 0f;
                      rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
                                                        config.originalAnchoredY);
                  }).Play();
                break;

            case PanelAnimationType.SlideFromBottom:
                if (rt == null) { PlayFadeClose(cg); return; }
                float botOff = Screen.height * slideHeightMultiplier;
                cg.DOFade(0f, animTime * 0.5f).Play();
                rt.DOAnchorPosY(config.originalAnchoredY - botOff, animTime)
                  .SetEase(Ease.InQuint)
                  .OnComplete(() =>
                  {
                      cg.alpha = 0f;
                      rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
                                                        config.originalAnchoredY);
                  }).Play();
                break;

            case PanelAnimationType.PopupScale:
                if (rt == null) { PlayFadeClose(cg); return; }
                cg.DOFade(0f, animTime * 0.6f).Play();
                rt.DOScale(Vector3.zero, animTime)
                  .SetEase(Ease.InBack)
                  .OnComplete(() =>
                  {
                      cg.alpha = 0f;
                      rt.localScale = config.originalScale;
                  }).Play();
                break;

            case PanelAnimationType.FlipDown:
                if (rt == null) { PlayFadeClose(cg); return; }
                cg.DOFade(0f, animTime * 0.55f).Play();
                rt.DOLocalRotate(new Vector3(-flipStartAngle, 0f, 0f), animTime)
                  .SetEase(Ease.InBack)
                  .OnComplete(() =>
                  {
                      cg.alpha = 0f;
                      rt.localRotation = Quaternion.identity;
                      rt.pivot = new Vector2(0.5f, 0.5f);
                  }).Play();
                break;

            case PanelAnimationType.Unfold:
                if (rt == null) { PlayFadeClose(cg); return; }
                float halfClose = animTime * 0.45f;
                Sequence unfoldClose = DOTween.Sequence();
                unfoldClose.Append(rt.DOScaleX(0f, halfClose).SetEase(Ease.InCubic));
                unfoldClose.Append(rt.DOScaleY(0f, halfClose).SetEase(Ease.InCubic));
                unfoldClose.OnComplete(() =>
                {
                    cg.alpha = 0f;
                    rt.localScale = config.originalScale;
                });
                unfoldClose.Play();
                break;

            case PanelAnimationType.SpiralIn:
                if (rt == null) { PlayFadeClose(cg); return; }
                float spiralOut = animTime * 0.85f;
                cg.DOFade(0f, spiralOut * 0.5f).Play();
                rt.DOScale(Vector3.zero, spiralOut).SetEase(Ease.InQuart).Play();
                rt.DOLocalRotate(new Vector3(0f, 0f, -360f), spiralOut)
                  .SetEase(Ease.InQuart)
                  .OnComplete(() =>
                  {
                      cg.alpha = 0f;
                      rt.localScale = config.originalScale;
                      rt.localRotation = Quaternion.identity;
                  }).Play();
                break;

            case PanelAnimationType.Glitch:
                if (rt == null) { PlayFadeClose(cg); return; }
                Sequence glitchClose = DOTween.Sequence();
                glitchClose.AppendCallback(() =>
                    ShakeAnchorPos(rt, 0.18f, 14f, 8f, steps: 8).Play());
                glitchClose.AppendInterval(0.05f);
                glitchClose.Append(cg.DOFade(0.0f, 0.05f));
                glitchClose.Append(cg.DOFade(0.7f, 0.04f));
                glitchClose.Append(cg.DOFade(0.0f, 0.04f));
                glitchClose.Append(cg.DOFade(0.4f, 0.03f));
                glitchClose.Append(cg.DOFade(0.0f, 0.06f));
                glitchClose.OnComplete(() =>
                {
                    cg.alpha = 0f;
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x,
                                                      config.originalAnchoredY);
                });
                glitchClose.Play();
                break;
        }
    }

    private void PlayFadeClose(CanvasGroup cg)
    {
        cg.DOFade(0f, animTime).OnComplete(() => cg.alpha = 0f).Play();
    }

    public void EnableTutorial(bool enable) => AnimatePanel(tutorial, enable);
    public void EnableInventory(bool enable) => AnimatePanel(iventoryPanel, enable, callSetup: true, callOpen: true);
    public void EnableLoad(bool enable) => AnimatePanel(loadPanel, enable);


    public void EnableLoss(bool enable) => AnimatePanel(lossPopUp, enable);
    public void EnableHome(bool enable) => AnimatePanel(home, enable);
    public void EnableSettingPanel(bool enable) => AnimatePanel(settingPanel, enable);
    public void EnableShopPanel(bool enable) => AnimatePanel(shopPanel, enable, callSetup: true, callOpen: true);

    public void EnableWin(bool enable)
    {
        AnimatePanel(winPopUp, enable, callSetup: true, callOpen: true, openDelay: enable ? 0.5f : 0f);
    }

    public void EnableLevelPanel(bool enable)
    {
        AnimatePanel(levelPanel, enable, callSetup: true, callOpen: true);
    }

    public bool IsQuestPanelConfigured()
    {
        return questPanel != null && questPanel.canvasGroup != null;
    }

    public void EnableQuestUI(bool enable)
    {
        if (IsQuestPanelConfigured())
        {
            AnimatePanel(questPanel, enable, callSetup: true, callOpen: true);
        }
        else
        {
            if (enable)
            {
                OpenUI<QuestUI>();
            }
            else
            {
                CloseUIDirectly<QuestUI>();
            }
        }
    }

    public void EnableGameplay(bool enable)
    {
        if (gamplayPanel?.canvasGroup == null)
        {
            
            return;
        }

        UICanvas_SSMB canvas = GetUICanvas(gamplayPanel);
        if (canvas == null)
        {
           
            return;
        }

        if (enable)
        {
            canvas.gameObject.SetActive(true);
           
        }

        AnimatePanel(gamplayPanel, enable, callSetup: true, callOpen: true);

       ;
    }

    // ── UICanvas List Management ─────────────────────────────────────────────

    private void InitializeUICanvases()
    {
        foreach (var canvas in uiCanvases)
        {
            CanvasGroup canvasGroup = canvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            canvas.gameObject.SetActive(true);

            if (canvas == _FirstOpenUI)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                canvas.Setup();
                canvas.Open();
            }
            else
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                canvas.Setup();
            }
        }
    }

    public T OpenUI<T>() where T : UICanvas_SSMB
    {
        T canvas = GetUI<T>();
        if (canvas != null)
        {
            canvas.Setup();
            canvas.Open();
        }
        return canvas;
    }

    public void CloseUI<T>(float t) where T : UICanvas_SSMB
    {
        T canvas = GetUI<T>();
        canvas?.Close(t);
    }

    public void CloseUIDirectly<T>() where T : UICanvas_SSMB
    {
        T canvas = GetUI<T>();
        canvas?.CloseDirectly();
    }

    public bool IsUIOpened<T>() where T : UICanvas_SSMB
    {
        T canvas = GetUI<T>();
        if (canvas == null) return false;
        CanvasGroup cg = canvas.GetComponent<CanvasGroup>();
        return cg != null && (cg.alpha > 0f || cg.blocksRaycasts);
    }

    public T GetUI<T>() where T : UICanvas_SSMB
    {
        return uiCanvases.Find(c => c is T) as T;
    }

    public void CloseAll()
    {
        foreach (var canvas in uiCanvases)
        {
            CanvasGroup cg = canvas.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha > 0f)
                canvas.Close(0);
        }
    }

    // ── Game State ───────────────────────────────────────────────────────────

    public void PauseGame()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0 : 1;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}