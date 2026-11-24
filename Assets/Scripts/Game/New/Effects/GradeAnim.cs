using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class GradeAnim : MonoBehaviour
{
    RectTransform rect;
    Image image;
    Vector2 home;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        home = rect.anchoredPosition;
    }

    void OnEnable()
    {
        if (rect == null || image == null)
            return;
        rect.DOKill();
        image.DOKill();
        rect.anchoredPosition = home;
        Color color = image.color;
        color.a = 0f;
        image.color = color;
        rect.DOLocalMove(new Vector3(home.x, home.y + 20f, 0f), 0.5f);
        image.DOFade(1f, 0.1f);
        CancelInvoke(nameof(FadeOut));
        Invoke(nameof(FadeOut), 0.4f);
    }

    void OnDisable()
    {
        CancelInvoke(nameof(FadeOut));
        if (rect != null)
            rect.DOKill();
        if (image != null)
            image.DOKill();
    }

    void FadeOut()
    {
        if (image != null)
            image.DOFade(0f, 0.1f);
    }
}
