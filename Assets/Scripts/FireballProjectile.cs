using UnityEngine;

/// <summary>
/// Travels in a straight line, applies <see cref="AttackData"/> to enemies on contact, and expires at max range.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class FireballProjectile : MonoBehaviour
{
    static Sprite s_runtimeSprite;
    static Texture2D s_runtimeTexture;

    AttackData attackData;
    Actor owner;
    Vector3 direction;
    float speed;
    float maxDistance;
    float traveled;
    bool spent;
    SphereCollider sphere;

    public static FireballProjectile Spawn(
        Actor owner,
        AttackData data,
        Vector3 position,
        Vector3 directionXZ,
        float speed,
        float maxDistance,
        GameObject prefabOrNull)
    {
        directionXZ.y = 0f;
        if (directionXZ.sqrMagnitude < 0.0001f)
            directionXZ = Vector3.right;
        directionXZ.Normalize();

        GameObject go = prefabOrNull != null
            ? Instantiate(prefabOrNull, position, Quaternion.LookRotation(directionXZ))
            : new GameObject("Fireball");

        if (prefabOrNull == null)
            go.transform.position = position;

        var proj = go.GetComponent<FireballProjectile>();
        if (proj == null)
            proj = go.AddComponent<FireballProjectile>();

        proj.EnsureRuntimeVisual();
        proj.Configure(owner, data, directionXZ, speed, maxDistance);
        return proj;
    }

    void EnsureRuntimeVisual()
    {
        if (GetComponent<SpriteRenderer>() == null)
        {
            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateRuntimeSprite();
            sr.color = new Color(1f, 0.45f, 0.08f, 1f);
            sr.sortingOrder = 50;
        }

        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        rb.excludeLayers = LayerMask.GetMask("Default", "Friendly");
    }

    static Sprite GetOrCreateRuntimeSprite()
    {
        if (s_runtimeSprite != null)
            return s_runtimeSprite;

        const int size = 32;
        s_runtimeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        s_runtimeTexture.filterMode = FilterMode.Bilinear;
        s_runtimeTexture.wrapMode = TextureWrapMode.Clamp;

        float r = size * 0.45f;
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float a = d < r ? 1f : 0f;
                float core = d < r * 0.55f ? 1f : 0.85f;
                s_runtimeTexture.SetPixel(x, y, new Color(1f, core * 0.55f, 0.05f, a));
            }
        }
        s_runtimeTexture.Apply();

        s_runtimeSprite = Sprite.Create(
            s_runtimeTexture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        return s_runtimeSprite;
    }

    void Awake()
    {
        sphere = GetComponent<SphereCollider>();
        sphere.isTrigger = true;
        if (sphere.radius < 0.15f)
            sphere.radius = 0.28f;
    }

    void Configure(Actor ownerActor, AttackData data, Vector3 dir, float moveSpeed, float range)
    {
        owner = ownerActor;
        attackData = data;
        direction = dir;
        speed = moveSpeed;
        maxDistance = range;
        traveled = 0f;

        EnsureRuntimeVisual();
    }

    void FixedUpdate()
    {
        float step = speed * Time.fixedDeltaTime;
        transform.position += direction * step;
        traveled += step;
        if (traveled >= maxDistance)
            Destroy(gameObject);
    }

    bool IsOwnerHierarchy(Collider other)
    {
        if (owner == null)
            return false;
        Transform t = other.transform;
        while (t != null)
        {
            if (t == owner.transform)
                return true;
            t = t.parent;
        }
        return false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (spent)
            return;

        if (IsOwnerHierarchy(other))
            return;

        int layer = other.gameObject.layer;
        if (layer == LayerMask.NameToLayer("Powerup"))
            return;

        var actor = other.GetComponentInParent<Actor>();
        if (actor != null && actor != owner && actor.CanBeHit())
        {
            Vector3 hitVector = direction.sqrMagnitude > 0.0001f ? direction : (actor.transform.position - transform.position).normalized;
            hitVector.y = 0f;
            if (hitVector.sqrMagnitude < 0.0001f)
                hitVector = Vector3.right;

            Vector3 hitPoint = other.ClosestPoint(transform.position);
            actor.EvaluateAttackData(attackData, hitVector, hitPoint);
            spent = true;
            Destroy(gameObject);
            return;
        }

        if (layer == LayerMask.NameToLayer("Wall"))
        {
            spent = true;
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Hero"))
            return;
    }
}
