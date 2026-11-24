using UnityEngine;

public class Spot : MonoBehaviour
{
    public bool isOkForTouch;

    /*
     * type = 0: single spot
     * type = 1: touch spot
     * type = 2: press spot 1/4倍上下屏距
     * type = 3: press spot 1/2倍上下屏距
     * type = 4: press spot 1倍上下屏距
     */
    public int type;
    public float animSpeed = 1;
    public float spawnSongTime;
    public Collider2D hitCollider;

    protected bool judged;
    bool released;
    float baseAnimSpeed;

    protected virtual void Awake()
    {
        baseAnimSpeed = animSpeed;
        if (hitCollider == null)
            hitCollider = GetComponent<Collider2D>();
    }

    public virtual void Prepare(float songTime)
    {
        spawnSongTime = songTime;
        isOkForTouch = false;
        judged = false;
        released = false;
        animSpeed = baseAnimSpeed;
    }

    public virtual void Tick(float songTime)
    {
    }

    public bool Contains(Vector2 worldPos)
    {
        return hitCollider != null && hitCollider.OverlapPoint(worldPos);
    }

    protected virtual void endAnim()
    {
        ReleaseSelf();
    }

    protected void ReleaseSelf()
    {
        if (released)
            return;
        released = true;
        isOkForTouch = false;
        if (GameServices.Board != null)
            GameServices.Board.Remove(this);
        PooledToken token = GetComponent<PooledToken>();
        if (token != null && token.Pool != null)
            token.Pool.Release(gameObject);
        else
            Destroy(gameObject);
    }
}
