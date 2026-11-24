using UnityEngine;

public class EffectController : MonoBehaviour
{
    public Sprite[] sprites;
    public GameObject effectPrefab;
    public GameObject panel;

    GameObjectPool pool;

    void Awake()
    {
        GameServices.Effects = this;
        if (effectPrefab != null)
            pool = new GameObjectPool(effectPrefab, 12);
    }

    public void SetEffect(Vector3 vec, int type)
    {
        if (panel == null || sprites == null || type < 0 || type >= sprites.Length)
            return;
        GameObject eff = pool != null ? pool.Get(panel.transform) : Instantiate(effectPrefab, panel.transform, false);
        EffectSetting setting = eff.GetComponent<EffectSetting>();
        eff.transform.position = vec;
        if (type == 0)
            setting.SetEffectColor(new Color(1f, 1f, 130f / 255f, 1f));
        else if (type == 1)
            setting.SetEffectColor(new Color(130f / 255f, 210f / 255f, 1f, 1f));
        setting.SetGrade(sprites[type]);
        setting.PlayAndReturn(type != 3);
    }
}
