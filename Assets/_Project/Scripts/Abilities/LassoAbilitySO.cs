using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Abilities/Lasso")]
public class LassoAbilitySO : AbilitySO
{
    [Header("Portée & accrochage")]
    public float maxLength = 12f;
    public float minLength = 1.5f;
    public LayerMask attachMask;                 // 0 => fallback Everything
    public bool fallbackUpIfMiss = true;

    [Header("Corde (rendu)")]
    public Material ropeMaterial;
    public float ropeWidth = 0.04f;
    public Color ropeColor = Color.white;
    public int ropeSortingLayerID = 0;
    public int ropeSortingOrder = 20;

    [Header("Prévisualisation si on rate")]
    public bool showPreviewOnMiss = true;
    public float previewDuration = 0.25f;
    public float previewWidth = 0.025f;
    public Color previewColor = new Color(1f, 1f, 1f, 0.8f);
    public bool applyCooldownOnMiss = false;     // ATTENTION: avec la nouvelle archi, le CD se gère côté Controller

    [Header("Pendule naturel")]
    public float gravityMultWhileSwing = 1.15f;
    public float minTangentialSpeedAtAttach = 10f;

    [Header("Limite d’angle (anti-360)")]
    [Range(40f, 179f)] public float hardArcDeg = 140f;
    public float angleBufferDeg = 0.5f;

    [Header("Jump: impulsion purement horizontale à la sortie")]
    public float jumpHorizontalImpulse = 10f;    // + fort & horizontal

    [Header("Jump → Bump le long de la tangente (trajectoire)")]
    public bool jumpAddsBump = true;
    public float bumpImpulse = 7f;               // un peu plus costaud que le dash, à ton goût
    [Range(0f, 1f)] public float bumpVelocityBias = 0.25f; // 0 = 100% tangente, 1 = tire vers vélocité actuelle
    public float bumpMaxResultSpeed = 22f;       // clamp (0 = off)

    public override AbilityRuntime CreateRuntime(GameObject owner, MonoBehaviour host)
        => new LassoRuntime(this, owner, host);
}

public class LassoRuntime : AbilityRuntime
{
    readonly LassoAbilitySO L;
    readonly PlayerController pc;

    DistanceJoint2D joint;
    LineRenderer rope, preview;
    Coroutine ropeCo, previewCo;

    float savedGravity;

    public override bool IsExclusive => true;

    public LassoRuntime(LassoAbilitySO data, GameObject owner, MonoBehaviour host) : base(data, owner, host)
    {
        L = data;
        pc = owner.GetComponent<PlayerController>();
    }

    public override void Use(Vector2 aimDir)
    {
        // Direction diagonale selon l'orientation
        float s = (aimDir.x >= 0f) ? +1f : -1f;
        Vector2 diag = new Vector2(s, 1f).normalized;

        Vector2 from = rb.position;
        int mask = (L.attachMask.value == 0) ? ~0 : L.attachMask.value;

        var hit = Physics2D.Raycast(from, diag, L.maxLength, mask);
        if (!hit.collider && L.fallbackUpIfMiss)
            hit = Physics2D.Raycast(from, Vector2.up, L.maxLength, mask);

        if (hit.collider)
        {
            Attach(hit.point, hit.distance, aimDir);
        }
        else
        {
            // Avec l’archi actuelle, le cooldown/munitions sont gérés par AbilityController.
            // Optionnel: si tu veux “punir” le miss, applique le CD depuis le Controller.
            if (L.showPreviewOnMiss) ShowPreview(from, from + diag * L.maxLength);
        }
    }

    public override bool OnJumpPressed()
    {
        if (!active) return false;

        // Tangente à la corde (trajectoire du swing)
        Vector2 fromAnchor = (rb.position - joint.connectedAnchor);
        if (fromAnchor.sqrMagnitude < 0.0001f) fromAnchor = Vector2.right; // fallback
        Vector2 ropeDir = fromAnchor.normalized;
        Vector2 tangent = new Vector2(-ropeDir.y, ropeDir.x);

        // Choisir le sens de la tangente en fonction de la vélocité actuelle
        float sign = Mathf.Sign(Vector2.Dot(rb.velocity, tangent));
        if (Mathf.Abs(sign) < 0.001f) sign = (rb.velocity.x >= 0f) ? 1f : -1f;
        Vector2 trajDir = tangent * sign; // direction de trajectoire

        // Petit biais vers la vélocité actuelle si demandé
        if (L.bumpVelocityBias > 0f && rb.velocity.sqrMagnitude > 0.0001f)
        {
            trajDir = Vector2.Lerp(trajDir, rb.velocity.normalized, L.bumpVelocityBias).normalized;
        }

        // Cancel du lasso AVANT de pousser (pour rendre la main à la physique tout de suite)
        Cancel();

        if (L.jumpAddsBump)
        {
            rb.AddForce(trajDir * L.bumpImpulse, ForceMode2D.Impulse);
            if (L.bumpMaxResultSpeed > 0f)
            {
                float spd = rb.velocity.magnitude;
                if (spd > L.bumpMaxResultSpeed)
                    rb.velocity = rb.velocity.normalized * L.bumpMaxResultSpeed;
            }
        }
        return true; // on "consomme" le saut
    }

    public override void ForceCancelForTransfer()
    {
        if (!active) return;
        Cancel();
    }

    void Attach(Vector2 anchor, float hitDist, Vector2 aimDir)
    {
        float ropeLen = Mathf.Clamp(hitDist, L.minLength, L.maxLength);

        joint = owner.AddComponent<DistanceJoint2D>();
        joint.enableCollision = false;
        joint.autoConfigureDistance = false;
        joint.connectedBody = null;
        joint.connectedAnchor = anchor;
        joint.distance = ropeLen;
        joint.maxDistanceOnly = false;

        rope = CreateLineRenderer("LassoRope", L.ropeWidth, L.ropeColor, L.ropeMaterial, L.ropeSortingLayerID, L.ropeSortingOrder);

        savedGravity = rb.gravityScale;
        rb.gravityScale = Mathf.Max(0.01f, savedGravity * L.gravityMultWhileSwing);

        pc?.SetSwinging(true, 0f, 0f);

        // Assure une vitesse tangentielle mini à l'accroche
        Vector2 toAnchor = (anchor - rb.position);
        Vector2 ropeDir = toAnchor.normalized;
        Vector2 tangent = new Vector2(-ropeDir.y, ropeDir.x);
        float vt = Vector2.Dot(rb.velocity, tangent);

        float sign = (Mathf.Abs(vt) > 0.01f) ? Mathf.Sign(vt)
                   : (Mathf.Abs(aimDir.x) > 0.1f ? Mathf.Sign(aimDir.x) : 1f);

        float vtAbs = Mathf.Abs(vt);
        if (vtAbs < L.minTangentialSpeedAtAttach)
            rb.velocity += tangent * (sign * (L.minTangentialSpeedAtAttach - vtAbs));

        active = true;
        ropeCo = host.StartCoroutine(RopeLoop());
    }

    IEnumerator RopeLoop()
    {
        var wait = new WaitForFixedUpdate();
        float halfHard = Mathf.Clamp(L.hardArcDeg * 0.5f, 1f, 89.5f);
        float threshold = Mathf.Max(0.1f, halfHard - L.angleBufferDeg);

        while (active && joint != null)
        {
            // MAJ corde
            if (rope != null)
            {
                rope.SetPosition(0, rb.position);
                rope.SetPosition(1, joint.connectedAnchor);
            }

            // Anti-360 (blocage doux dans un cône)
            Vector2 fromAnchor = (rb.position - joint.connectedAnchor);
            Vector2 u = fromAnchor.normalized;
            Vector2 tCCW = Vector2.Perpendicular(u);
            float angle = Vector2.SignedAngle(Vector2.down, u);

            if (Mathf.Abs(angle) >= threshold)
            {
                Vector2 tInc = (angle >= 0f) ? tCCW : -tCCW;
                float vAlongInc = Vector2.Dot(rb.velocity, tInc);
                if (vAlongInc > 0f) rb.velocity -= tInc * vAlongInc;
            }

            yield return wait;
        }
        Cleanup();
    }

    void Cancel()
    {
        if (!active) return;
        active = false;
        if (ropeCo != null) { host.StopCoroutine(ropeCo); ropeCo = null; }

        // Restaure la gravité avant de supprimer le joint
        rb.gravityScale = savedGravity;

        Cleanup();
        pc?.SetSwinging(false);
    }

    void Cleanup()
    {
        if (joint) Object.Destroy(joint);
        joint = null;

        if (rope) Object.Destroy(rope.gameObject);
        rope = null;

        if (preview) Object.Destroy(preview.gameObject);
        preview = null; previewCo = null;
    }

    void ShowPreview(Vector2 start, Vector2 end)
    {
        if (!L.showPreviewOnMiss) return;

        if (previewCo != null) { host.StopCoroutine(previewCo); previewCo = null; }
        if (preview) { Object.Destroy(preview.gameObject); preview = null; }

        preview = CreateLineRenderer("LassoPreview", L.previewWidth, L.previewColor, L.ropeMaterial, L.ropeSortingLayerID, L.ropeSortingOrder + 1);
        preview.SetPosition(0, start);
        preview.SetPosition(1, end);
        previewCo = host.StartCoroutine(PreviewLife());
    }

    IEnumerator PreviewLife()
    {
        float t = 0f;
        var wait = new WaitForEndOfFrame();
        Color c0 = preview.startColor, c1 = preview.endColor;
        while (t < L.previewDuration && preview != null)
        {
            float a = Mathf.Clamp01(1f - (t / L.previewDuration));
            preview.startColor = new Color(c0.r, c0.g, c0.b, a * c0.a);
            preview.endColor = new Color(c1.r, c1.g, c1.b, a * c1.a);
            t += Time.deltaTime;
            yield return wait;
        }
        if (preview) Object.Destroy(preview.gameObject);
        preview = null; previewCo = null;
    }

    LineRenderer CreateLineRenderer(string name, float width, Color col, Material mat, int sortingLayerID, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(owner.transform, worldPositionStays: false);
        go.transform.localPosition = Vector3.zero;
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.material = mat ? mat : new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = lr.endWidth = width;
        lr.startColor = lr.endColor = col;
        lr.numCapVertices = 4;
        lr.sortingLayerID = sortingLayerID;
        lr.sortingOrder = sortingOrder;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }
}





















