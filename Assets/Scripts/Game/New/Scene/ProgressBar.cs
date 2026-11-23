using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProgressBar : MonoBehaviour
{
    private int length;
    public AudioSource aud;

    // Start is called before the first frame update
    void Start()
    {
        length = Screen.width;
    }

    // Update is called once per frame
    void Update()
    {
        var playback = ChartPlayback.Instance;
        if (playback != null && playback.SongActive && aud != null && aud.clip != null && aud.clip.length > 0f)
        {
            RectTransform tr = this.GetComponent<RectTransform>();
            float progress = Mathf.Clamp01((float)(playback.SongTime / aud.clip.length));
            tr.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, length * progress);
        }
    }
}
