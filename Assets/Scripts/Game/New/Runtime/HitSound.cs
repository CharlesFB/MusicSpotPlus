using UnityEngine;

public class HitSound : MonoBehaviour
{
    AudioSource source;

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
    }

    public void Play(AudioClip clip)
    {
        if (clip != null && source != null)
            source.PlayOneShot(clip);
    }
}
