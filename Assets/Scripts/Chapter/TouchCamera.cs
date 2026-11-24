using UnityEngine;
using DG.Tweening;

public class TouchCamera : MonoBehaviour
{
    public bool canPan = true;
    public static bool Dragged { get; private set; }

    const float ChapterPlaneZ = 15f;
    const float DragPixels = 18f;
    const float FlickSpeed = 8f;

    Camera cam;
    bool dragging;
    Vector2 pressPos;
    Vector2 lastPos;
    readonly float[] speeds = new float[4];
    int speedCount;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Update()
    {
        if (VarSingleton.instance == null || VarSingleton.instance.chapterNum != -1)
        {
            dragging = false;
            return;
        }
        if (!canPan || cam == null)
            return;
        if (!ReadPointer(out bool down, out bool held, out bool up, out Vector2 pos))
        {
            if (dragging)
                EndDrag();
            return;
        }
        if (down)
            BeginDrag(pos);
        if (dragging && held)
            Drag(pos);
        if (up && dragging)
            EndDrag();
    }

    void BeginDrag(Vector2 pos)
    {
        dragging = true;
        Dragged = false;
        pressPos = lastPos = pos;
        speedCount = 0;
        transform.DOKill();
    }

    void Drag(Vector2 pos)
    {
        if ((pos - pressPos).sqrMagnitude > DragPixels * DragPixels)
            Dragged = true;
        float dx = WorldX(pos) - WorldX(lastPos);
        float previous = transform.position.x;
        float next = Mathf.Clamp(previous - dx, 0f, MaxX());
        PushSpeed((next - previous) / Mathf.Max(Time.deltaTime, 0.0001f));
        Vector3 at = transform.position;
        at.x = next;
        transform.position = at;
        lastPos = pos;
    }

    void EndDrag()
    {
        dragging = false;
        if (!Dragged)
            return;
        float gap = VarSingleton.instance.chapterGap;
        if (gap <= 0f)
            return;
        float index = transform.position.x / gap;
        float speed = AverageSpeed();
        int target;
        if (speed > FlickSpeed)
            target = Mathf.FloorToInt(index) + 1;
        else if (speed < -FlickSpeed)
            target = Mathf.CeilToInt(index) - 1;
        else
            target = Mathf.FloorToInt(index + 0.5f);
        int last = Mathf.Max(0, VarSingleton.instance.chapterCount - 1);
        target = Mathf.Clamp(target, 0, last);
        float dest = target * gap;
        float duration = Mathf.Clamp(Mathf.Abs(dest - transform.position.x) / gap * 0.3f, 0.12f, 0.32f);
        transform.DOKill();
        transform.DOMoveX(dest, duration).SetEase(Ease.OutCubic);
    }

    float WorldX(Vector2 screen)
    {
        float distance = ChapterPlaneZ - transform.position.z;
        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, distance)).x;
    }

    float MaxX()
    {
        int count = VarSingleton.instance.chapterCount;
        if (count <= 1)
            return 0f;
        return (count - 1) * VarSingleton.instance.chapterGap;
    }

    void PushSpeed(float speed)
    {
        speeds[speedCount % speeds.Length] = speed;
        speedCount++;
    }

    float AverageSpeed()
    {
        int count = Mathf.Min(speedCount, speeds.Length);
        if (count == 0)
            return 0f;
        float sum = 0f;
        for (int i = 0; i < count; i++)
            sum += speeds[i];
        return sum / count;
    }

    static bool ReadPointer(out bool down, out bool held, out bool up, out Vector2 pos)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            pos = touch.position;
            down = touch.phase == TouchPhase.Began;
            up = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            held = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            return true;
        }
        if (Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
        {
            pos = Input.mousePosition;
            down = Input.GetMouseButtonDown(0);
            up = Input.GetMouseButtonUp(0);
            held = Input.GetMouseButton(0);
            return true;
        }
        down = held = up = false;
        pos = Vector2.zero;
        return false;
    }
}
