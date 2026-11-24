using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class PressSpotNew : Spot
{
    public GameObject bar;
    public GameObject back;
    public GameObject button;
    public InfoManager speedInfo;
    public ResultManager resultManager;
    public EffectController effectController;

    public int dir;
    public int spd;
    public int holdTicks;
    public bool isPressing;

    Animator buttonAnim;
    AudioSource hitSource;
    Image barImage;
    Image backImage;
    Image buttonImage;
    RectTransform barRect;
    RectTransform backRect;
    RectTransform rootRect;
    Tweener backLoading;
    float loadTime = 0.6f;
    float desSize;
    bool loadOn = true;
    bool disappearOn = true;
    float gameWidth;
    float gameHeight;
    Vector2 barBase;
    Vector2 backBase;
    Quaternion baseRotation;
    float baseButtonSpeed;
    static float GAME_SCREEN_VERTICAL_FEC = 0.64f;
    static float GAME_SCREEN_HORIZ_FEC = 0.9f;

    protected override void Awake()
    {
        base.Awake();
        if (hitCollider == null)
            hitCollider = GetComponent<BoxCollider2D>();
        buttonAnim = button.GetComponent<Animator>();
        hitSource = GetComponent<AudioSource>();
        barImage = bar.GetComponent<Image>();
        backImage = back.GetComponent<Image>();
        buttonImage = button.GetComponent<Image>();
        barRect = bar.GetComponent<RectTransform>();
        backRect = back.GetComponent<RectTransform>();
        rootRect = GetComponent<RectTransform>();
        barBase = barRect.sizeDelta;
        backBase = backRect.sizeDelta;
        baseRotation = rootRect.localRotation;
        baseButtonSpeed = buttonAnim.speed;
    }

    public override void Prepare(float songTime)
    {
        base.Prepare(songTime);
        StopMotion();
        isPressing = false;
        loadOn = true;
        disappearOn = true;
        speedInfo = GameServices.Info;
        resultManager = GameServices.Results;
        effectController = GameServices.Effects;
        gameWidth = Screen.width * GAME_SCREEN_HORIZ_FEC / 2f;
        gameHeight = Screen.height * GAME_SCREEN_VERTICAL_FEC / 2f;
        barRect.sizeDelta = barBase;
        backRect.sizeDelta = backBase;
        rootRect.localScale = Vector3.one;
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.localScale = Vector3.one;
        buttonRect.localEulerAngles = Vector3.zero;
        buttonRect.localRotation = Quaternion.identity;
        ClearSpin(rootRect, baseRotation);
        BarInit();
        SpdInit();
        if (buttonAnim != null)
        {
            buttonAnim.Rebind();
            buttonAnim.Update(0f);
            buttonAnim.speed = baseButtonSpeed * (speedInfo != null ? speedInfo.speedFec : 1f);
        }
        barImage.color = new Color(barImage.color.r, barImage.color.g, barImage.color.b, 0f);
        barImage.DOFade(1f, 0.8f);
    }

    public override void Tick(float songTime)
    {
        float elapsed = songTime - spawnSongTime;
        if (holdTicks > 0)
            LayoutHold(songTime);
        bool finished = holdTicks > 0 ? HoldFraction(songTime) >= 1f : elapsed >= deltaGap(2) + loadTime;
        bool late = holdTicks > 0 ? HoldFraction(songTime) > 1f / 3f : elapsed > deltaGap(2) + loadTime / 3f;
        if (elapsed > deltaGap(2) + loadTime + 1f)
        {
            endAnim();
        }
        else if (elapsed > deltaGap(2) && finished)
        {
            if (isPressing)
                DoRelease();
            DoDisappear();
        }
        else if (elapsed > deltaGap(2) && late)
        {
            Miss();
        }
        else if (elapsed >= deltaGap(2))
        {
            if (!judged)
                isOkForTouch = true;
            if (holdTicks <= 0)
                BackLoadingAnim();
        }
    }

    void SpdInit()
    {
        if (holdTicks > 0 && ChartSession.Tempo != null)
        {
            loadTime = ChartGrid.SecondsPerTick(ChartSession.Tempo.BpmAt(spawnSongTime)) * holdTicks;
            return;
        }
        loadTime = (speedInfo != null ? speedInfo.secPerBeat : 0.6f) * 2f;
        float rate = 1f;
        switch (spd)
        {
            case 0:
                rate = 0.5f;
                break;
            case 2:
                rate = 2f;
                break;
            case 3:
                rate = 4f;
                break;
        }
        loadTime *= rate;
    }

    void BarInit()
    {
        if (holdTicks > 0)
        {
            LayoutHold(spawnSongTime);
            return;
        }
        switch (type)
        {
            case 2:
                desSize = backRect.sizeDelta.y + (1f / 4f) * gameHeight;
                barRect.sizeDelta = new Vector2(barRect.sizeDelta.x, barRect.sizeDelta.y + (1f / 4f) * gameHeight);
                break;
            case 3:
                desSize = backRect.sizeDelta.y + (1f / 2f) * gameHeight;
                barRect.sizeDelta = new Vector2(barRect.sizeDelta.x, barRect.sizeDelta.y + (1f / 2f) * gameHeight);
                break;
            case 4:
                desSize = backRect.sizeDelta.y + gameHeight;
                barRect.sizeDelta = new Vector2(barRect.sizeDelta.x, barRect.sizeDelta.y + gameHeight);
                break;
            default:
                desSize = backRect.sizeDelta.y;
                break;
        }
    }

    void LayoutHold(float songTime)
    {
        TempoMap map = ChartSession.Tempo;
        float bpm = map != null ? map.BpmAt(songTime) : ChartGrid.ReferenceBpm;
        float body = holdTicks * ChartGrid.RowPitchPixels(Screen.height * 0.5f, bpm);
        desSize = backBase.y + body;
        barRect.sizeDelta = new Vector2(barBase.x, desSize);
        float shown = backBase.y + body * Mathf.Clamp01(HoldFraction(songTime));
        backRect.sizeDelta = new Vector2(backBase.x, shown);
    }

    float HoldFraction(float songTime)
    {
        TempoMap map = ChartSession.Tempo;
        if (holdTicks <= 0 || map == null)
            return 0f;
        float done = map.TickAt(songTime) - map.TickAt(spawnSongTime);
        if (done <= 0f)
            return 0f;
        return done / holdTicks;
    }

    public void ApplyDirection()
    {
        DirInit();
    }

    void DirInit()
    {
        if (dir == 1)
            rootRect.Rotate(0f, 0f, 180f);
    }

    static void ClearSpin(RectTransform rect, Quaternion rotation)
    {
        rect.localEulerAngles = Vector3.zero;
        rect.localRotation = rotation;
    }

    public void DoPress()
    {
        if (judged || isPressing || !isOkForTouch)
            return;
        isPressing = true;
        button.GetComponent<RectTransform>().localScale = new Vector3(1.25f, 1.25f, 1.25f);
        buttonAnim.SetBool("Pressing", true);
        backImage.DOFade(0.7f, 0.05f).SetId<Tween>("game");
    }

    public void DoRelease()
    {
        if (judged)
            return;
        judged = true;
        isPressing = false;
        isOkForTouch = false;
        if (backLoading != null && backLoading.IsPlaying())
            backLoading.Pause();
        button.GetComponent<RectTransform>().localScale = Vector3.one;
        buttonAnim.SetBool("Pressing", false);

        Vector3 effectPos = backRect.position;
        effectPos.y += dir == 1 ? -backRect.sizeDelta.y : backRect.sizeDelta.y;
        float desPercentage = desSize > 0f ? backRect.sizeDelta.y / desSize : 0f;
        if (desPercentage >= 0.85f)
            PerfectClick(effectPos);
        else if (desPercentage >= 0.7f)
            GreatClick(effectPos);
        else
            BadClick(effectPos);
    }

    public void DoDisappear()
    {
        if (!disappearOn)
            return;
        disappearOn = false;
        barImage.DOFade(0f, 0.17f).SetId<Tween>("game");
        backImage.DOFade(0f, 0.17f).SetId<Tween>("game");
        buttonAnim.SetBool("Disappear", true);
    }

    public void SetAnimSpeed(float s)
    {
        if (buttonAnim != null)
            buttonAnim.speed = s;
    }

    void BackLoadingAnim()
    {
        if (!loadOn)
            return;
        loadOn = false;
        backLoading = backRect.DOSizeDelta(new Vector2(backRect.sizeDelta.x, barRect.sizeDelta.y), loadTime).SetEase(Ease.Linear).SetId("game");
    }

    void PerfectClick(Vector3 effectPos)
    {
        effectController.SetEffect(effectPos, 0);
        PlaySound();
        resultManager.ComboUp();
        resultManager.PerfectUp();
        resultManager.ScoreUp(GameInfoSingleton.instance.pressPerfectScore);
    }

    void GreatClick(Vector3 effectPos)
    {
        effectController.SetEffect(effectPos, 1);
        PlaySound();
        resultManager.ComboUp();
        resultManager.GreatUp();
        resultManager.ScoreUp((int)(GameInfoSingleton.instance.pressPerfectScore * 0.6f));
    }

    void BadClick(Vector3 effectPos)
    {
        effectController.SetEffect(effectPos, 2);
        resultManager.ComboReset();
        resultManager.BadUp();
        resultManager.ScoreUp((int)(GameInfoSingleton.instance.pressPerfectScore * 0.14f));
    }

    void Miss()
    {
        if (!isOkForTouch || isPressing || judged)
            return;
        judged = true;
        isOkForTouch = false;
        barImage.DOFade(0.4f, 0.05f).SetId("game");
        buttonImage.DOFade(0.4f, 0.05f).SetId("game");
        resultManager.ComboReset();
        resultManager.MissUp();
    }

    void PlaySound()
    {
        if (GameServices.Hits != null && hitSource != null)
            GameServices.Hits.Play(hitSource.clip);
    }

    protected override void endAnim()
    {
        StopMotion();
        base.endAnim();
    }

    void StopMotion()
    {
        if (backLoading != null)
        {
            backLoading.Kill();
            backLoading = null;
        }
        barImage.DOKill();
        backImage.DOKill();
        buttonImage.DOKill();
        barRect.DOKill();
        backRect.DOKill();
    }

    float deltaGap(int i)
    {
        float scale = speedInfo != null ? speedInfo.speedFec : 1f;
        if (scale == 0f)
            scale = 1f;
        return GameInfoSingleton.instance.gaps[i] / scale;
    }
}
