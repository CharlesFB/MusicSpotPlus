using UnityEngine;

public class TouchSpotNew : Spot
{
    public InfoManager speedInfo;
    public ResultManager resultManager;
    public EffectController effectController;

    Animator anim;
    AudioSource hitSource;

    protected override void Awake()
    {
        base.Awake();
        anim = GetComponent<Animator>();
        hitSource = GetComponent<AudioSource>();
        type = 1;
    }

    public override void Prepare(float songTime)
    {
        base.Prepare(songTime);
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
        if (elapsed >= deltaGap(3))
        {
            Miss();
            endAnim();
        }
        else if (!judged && elapsed >= deltaGap(2))
        {
            isOkForTouch = true;
        }
    }

    public void DoTouch()
    {
        if (judged || !isOkForTouch)
            return;
        judged = true;
        isOkForTouch = false;
        anim.SetBool("Touch", true);
        effectController.SetEffect(transform.position, 0);
        PlaySound();
        resultManager.ComboUp();
        resultManager.PerfectUp();
        resultManager.ScoreUp(GameInfoSingleton.instance.touchPerfectScore);
    }

    public void SetAnimSpeed(float s)
    {
        if (anim != null)
            anim.speed = s;
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

    float deltaGap(int i)
    {
        float scale = speedInfo != null ? speedInfo.speedFec : 1f;
        if (scale == 0f)
            scale = 1f;
        return GameInfoSingleton.instance.gaps[i] / scale;
    }
}
