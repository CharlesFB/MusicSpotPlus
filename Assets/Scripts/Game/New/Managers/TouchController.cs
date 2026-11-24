using UnityEngine;

public class TouchController : MonoBehaviour
{
    public int maxCount = 10;
    public GameObject spotsPanel;

    Camera cam;
    readonly Vector2[] pointerPos = new Vector2[11];
    readonly bool[] pointerBegan = new bool[11];
    int pointerCount;

    void Awake()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (GameInfoSingleton.instance.isPaused)
            return;
        NoteBoard board = GameServices.Board;
        if (board == null)
            return;
        if (cam == null)
            cam = GameServices.Camera != null ? GameServices.Camera : Camera.main;

        CollectPointers();
        if (pointerCount == 0)
        {
            ReleasePresses(board);
            return;
        }

        var active = board.Active;
        for (int i = 0; i < pointerCount; i++)
        {
            Vector3 world = cam.ScreenToWorldPoint(pointerPos[i]);
            Vector2 world2 = world;
            for (int j = 0; j < active.Count; j++)
            {
                Spot spot = active[j];
                if (spot == null || !spot.isOkForTouch || !spot.Contains(world2))
                    continue;
                if (spot is SingleSpotNew single)
                {
                    if (!pointerBegan[i])
                        continue;
                    single.DoTouch();
                    break;
                }
                if (spot is TouchSpotNew touch)
                {
                    touch.DoTouch();
                    break;
                }
                if (spot is PressSpotNew press && !press.isPressing)
                {
                    press.DoPress();
                    break;
                }
            }
        }

        ReleasePresses(board);
    }

    void CollectPointers()
    {
        pointerCount = 0;
        int touches = Input.touchCount;
        if (touches > maxCount)
            touches = maxCount;
        for (int i = 0; i < touches && pointerCount < pointerPos.Length; i++)
        {
            Touch touch = Input.GetTouch(i);
            pointerPos[pointerCount] = touch.position;
            pointerBegan[pointerCount] = touch.phase == TouchPhase.Began;
            pointerCount++;
        }
        if (pointerCount == 0 && Input.GetMouseButton(0))
        {
            pointerPos[0] = Input.mousePosition;
            pointerBegan[0] = Input.GetMouseButtonDown(0);
            pointerCount = 1;
        }
    }

    void ReleasePresses(NoteBoard board)
    {
        var active = board.Active;
        for (int i = 0; i < active.Count; i++)
        {
            if (!(active[i] is PressSpotNew press) || !press.isPressing)
                continue;
            bool held = false;
            for (int p = 0; p < pointerCount; p++)
            {
                Vector3 world = cam.ScreenToWorldPoint(pointerPos[p]);
                if (press.Contains(world))
                {
                    held = true;
                    break;
                }
            }
            if (!held)
                press.DoRelease();
        }
    }
}
