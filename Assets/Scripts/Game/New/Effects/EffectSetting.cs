using UnityEngine;
using UnityEngine.UI;

public class EffectSetting : MonoBehaviour
{
    public Image grade;
    public ParticleSystem effect;

    public void SetGrade(Sprite sprite)
    {
        grade.sprite = sprite;
    }

    public void SetEffectColor(Color color)
    {
        if (effect == null)
            return;
        ParticleSystem.MainModule main = effect.main;
        main.startColor = color;
    }

    public void PlayAndReturn(bool playParticles)
    {
        float wait = 0.6f;
        if (effect != null)
        {
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Clear(true);
            if (playParticles)
            {
                effect.time = 0f;
                effect.Play(true);
                ParticleSystem.MainModule main = effect.main;
                float lifetime = main.startLifetime.constantMax;
                if (lifetime <= 0f)
                    lifetime = main.startLifetime.constant;
                wait = Mathf.Max(0.6f, main.duration + lifetime);
            }
        }
        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), wait);
    }

    void ReturnToPool()
    {
        PooledToken token = GetComponent<PooledToken>();
        if (token != null && token.Pool != null)
            token.Pool.Release(gameObject);
        else
            Destroy(gameObject);
    }
}
