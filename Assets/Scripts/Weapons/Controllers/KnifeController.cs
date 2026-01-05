using UnityEngine;

public class KnifeController : MonoBehaviour
{
    public WeaponData data;
    public Transform firePoint;

    private PlayerMovement playerMovement;
    private float timer;

    void Awake()
    {
        if (!firePoint) firePoint = transform;
        playerMovement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (!data || !data.projectilePrefab) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            FireKnife();
            timer = data.cooldown;
        }
    }

    void FireKnife()
    {
        Vector2 dir = playerMovement.lastMoveDir;

        if (dir.sqrMagnitude < 0.01f)
            dir = Vector2.right;

        // 🔒 HARD SNAP to 4 directions
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            dir = dir.x > 0 ? Vector2.right : Vector2.left;
        else
            dir = dir.y > 0 ? Vector2.up : Vector2.down;

        GameObject go = Instantiate(
            data.projectilePrefab,
            firePoint.position,
            data.projectilePrefab.transform.rotation // KEEP prefab rotation (-45)
        );

        // ✅ MOVE using direction (no rotation)
        var p = go.GetComponent<KnifeProjectile>();
        if (p)
        {
            p.SetDirection(dir);
            p.speed = data.projectileSpeed;
            p.damage = data.damage;
            p.lifetime = data.lifetime;
            p.pierce = data.pierce;
        }

        // ✅ VISUAL: SCALE ONLY
        SpriteRenderer sr = go.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            Vector3 s = Vector3.one;

            if (dir == Vector2.up)
                s = new Vector3(-1f, 1f, 1f);     // TOP = -1, 1
            else if (dir == Vector2.down)
                s = new Vector3(1f, -1f, 1f);     // BOTTOM = 1, -1
            else if (dir == Vector2.right)
                s = new Vector3(1f, 1f, 1f);      // keep right normal
            else if (dir == Vector2.left)
                s = new Vector3(-1f, -1f, 1f);     // simple left flip (change if you want)

            sr.transform.localScale = s;
        }
    }


}
