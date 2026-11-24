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

    public void PlayAndReturn()
    {
        if (effect != null)
        {
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            effect.Play();
        }
        CancelInvoke(nameof(ReturnToPool));
        Invoke(nameof(ReturnToPool), 0.6f);
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
