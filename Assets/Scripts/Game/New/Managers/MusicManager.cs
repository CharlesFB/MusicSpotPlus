using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class MusicManager : MonoBehaviour
{
    public Image blackAlpha;
    public RectTransform finalPanel;
    public AudioClip endMusic;
    public PauseManager pauseManager;

    private bool playing; //代表游戏曲目正在进行，暂停不会使其变为false；用于区分结束音乐播放

    private void Awake()
    {
        playing = false;
    }

    private void Start()
    {
        Invoke("AudioPlay", 3.5f);
    }

    private void Update()
    {
        var playback = GetComponent<ChartPlayback>();
        if (playing && playback != null && playback.HasEnded)
        {
            pauseManager.pauseButton.interactable = false;
            blackAlpha.gameObject.GetComponent<BlackAlpha>().isOkForClick = true;
            Invoke("GetBack", 3f);
            FinalPanelMoveIn();
            playing = false;
            var source = GetComponent<AudioSource>();
            source.clip = endMusic;
            source.volume = 0.6f;
            source.Play();
        }
    }

    public void GetBack()
    {
        blackAlpha.gameObject.GetComponent<BlackAlpha>().OnClickThis();
    }

    public void AudioPlay()
    {
        var playback = GetComponent<ChartPlayback>();
        if (playback != null)
            playback.Play();
        else
            GetComponent<AudioSource>().Play();
        playing = true;
    }

    public void AudioPause()
    {
        var playback = GetComponent<ChartPlayback>();
        if (playback != null)
            playback.Pause();
        else
            GetComponent<AudioSource>().Pause();
    }

    public void FinalPanelMoveIn()
    {
        finalPanel.DOLocalMove(new Vector3(0,0,0),0.8f);
        blackAlpha.DOFade(0.6f, 0.7f); //背景黑底
    }
}
