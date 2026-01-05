using UnityEngine;

public class KnifeProjectile : MonoBehaviour
{
    public float speed = 12f;
    public float damage = 10f;
    public float lifetime = 2f;
    public int pierce = 1;

    private float timer;

    // ✅ direction set by controller (no rotation needed)
    private Vector2 moveDir = Vector2.up;

    public void SetDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude > 0.0001f)
            moveDir = dir.normalized;
    }

    void Start()
    {
        timer = lifetime;
    }

    void Update()
    {
        // ✅ move straight using the direction vector
        transform.position += (Vector3)(moveDir * speed * Time.deltaTime);

        timer -= Time.deltaTime;
        if (timer <= 0f) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Enemy")) return;

        pierce--;
        if (pierce <= 0) Destroy(gameObject);
    }
}
