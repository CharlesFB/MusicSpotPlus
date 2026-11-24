using UnityEngine;

public class SingleSpotNew : Spot
{
    public InfoManager speedInfo;
    public ResultManager resultManager;
    public EffectController effectController;

    Animator anim;
    AudioSource hitSource;
    bool open;

    protected override void Awake()
    {
        base.Awake();
        anim = GetComponent<Animator>();
        hitSource = GetComponent<AudioSource>();
        type = 0;
    }

    public override void Prepare(float songTime)
    {
        base.Prepare(songTime);
        open = false;
        speedInfo = GameServices.Info;
        resultManager = GameServices.Results;
        effectController = GameServices.Effects;
        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f);
            anim.speed = animSpeed * (speedInfo != null ? speedInfo.speedFec : 1f);
        }
    }

    public override void Tick(float songTime)
    {
        float elapsed = songTime - spawnSongTime;
        if (elapsed >= deltaGap(5))
        {
            Miss();
            endAnim();
        }
        else if (!judged && elapsed >= deltaGap(0))
        {
            isOkForTouch = true;
            open = true;
        }
    }

    public void DoTouch()
    {
        if (judged || !open)
            return;
        judged = true;
        isOkForTouch = false;
        float elapsed = CurrentElapsed();
        if (elapsed >= deltaGap(2) && elapsed < deltaGap(3))
            PerfectClick();
        else if (elapsed >= deltaGap(1) && elapsed < deltaGap(4))
            GreatClick();
        else if (elapsed >= deltaGap(0) && elapsed < deltaGap(5))
            BadClick();
    }

    public void SetAnimSpeed(float s)
    {
        if (anim != null)
            anim.speed = s;
    }

    void PerfectClick()
    {
        anim.SetInteger("Click", 1);
        effectController.SetEffect(transform.position, 0);
        PlaySound();
        resultManager.ComboUp();
        resultManager.PerfectUp();
        resultManager.ScoreUp(GameInfoSingleton.instance.singlePerfectScore);
    }

    void GreatClick()
    {
        anim.SetInteger("Click", 1);
        effectController.SetEffect(transform.position, 1);
        PlaySound();
        resultManager.ComboUp();
        resultManager.GreatUp();
        resultManager.ScoreUp((int)(GameInfoSingleton.instance.singlePerfectScore * 0.6f));
    }

    void BadClick()
    {
        anim.SetInteger("Click", 2);
        effectController.SetEffect(transform.position, 2);
        resultManager.ComboReset();
        resultManager.BadUp();
        resultManager.ScoreUp((int)(GameInfoSingleton.instance.singlePerfectScore * 0.14f));
    }

    void Miss()
    {
        if (!isOkForTouch)
            return;
        judged = true;
        isOkForTouch = false;
        effectController.SetEffect(transform.position, 3);
        resultManager.ComboReset();
        resultManager.MissUp();
    }

    void PlaySound()
    {
        if (GameServices.Hits != null && hitSource != null)
            GameServices.Hits.Play(hitSource.clip);
    }

    float CurrentElapsed()
    {
        ChartPlayback playback = ChartPlayback.Instance;
        if (playback == null)
            return 0f;
        return (float)playback.SongTime - spawnSongTime;
    }

    float deltaGap(int i)
    {
        float scale = speedInfo != null ? speedInfo.speedFec : 1f;
        if (scale == 0f)
            scale = 1f;
        return GameInfoSingleton.instance.gaps[i] / scale;
    }
}
