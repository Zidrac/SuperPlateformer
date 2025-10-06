using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Abilities/Grapple (Terraria-like)")]
public class GrappleAbilitySO : AbilitySO
{
    [Header("Cast")]
    public float maxDistance = 14f;
    public LayerMask attachMask;                 // 0 => fallback Everything
    public float castOriginYOffset = 0.15f;
    public bool showPreviewOnMiss = true;

    [Header("Pull (phase d’attraction)")]
    public float pullAcceleration = 60f;         // N·s/kg (Impulse * dt)
    public float maxPullSpeed = 18f;             // clamp de la vitesse vers l’ancre
    [Range(0f, 1f)] public float lateralDamping = 0.25f; // réduit l’oscillation pendant la traction
    public float snapDistance = 0.4f;            // quand on est assez proche, on “s’accroche”
    public float gravityDuringPull = 0.2f;       // mini gravité pendant le pull

    [Header("Accroche (phase latched)")]
    public float holdDistance = 0.25f;           // distance de repos de la corde une fois accroché
    public float gravityWhileLatched = 0f;       // 0 = reste collé/suspendu
    public bool freezeVelocityOnLatch = true;    // met la vélocité à 0 quand on s’accroche

    [Header("Rope Render")]
    public Material ropeMaterial;
    public float ropeWidth = 0.04f;
    public Color ropeColor = Color.white;
    public int ropeSortingLayerID = 0;
    public int ropeSortingOrder = 25;
    public float missPreviewWidth = 0.02f;
    public Color missPreviewColor = new Color(1f, 1f, 1f, 0.75f);
    public float missPreviewTime = 0.2f;

    public override AbilityRuntime CreateRuntime(GameObject owner, MonoBehaviour host)
        => new Runtime(this, owner, host);

    private class Runtime : AbilityRuntime
    {
        enum State { None, Pulling, Latched }
        readonly GrappleAbilitySO G;
        readonly PlayerController pc;

        State state = State.None;
        DistanceJoint2D joint;
        LineRenderer rope, preview;
        Coroutine loopCo, previewCo;

        Vector2 anchorPoint;
        float originalGravity;
        bool exclusive = true; // reste exclusif tant que le grappin est actif
        public override bool IsExclusive => exclusive;

        public Runtime(GrappleAbilitySO s, GameObject owner, MonoBehaviour host) : base(s, owner, host)
        {
            G = s;
            pc = owner.GetComponent<PlayerController>();
        }

        public override void Use(Vector2 aimDir)
        {
            // direction : comme le dash (stick droit / touches), fallback au facing
            if (aimDir.sqrMagnitude < 0.0001f)
            {
                var ac = host as AbilityController;
                int sgn = (ac != null) ? ac.FacingSign() : (owner.transform.localScale.x >= 0 ? 1 : -1);
                aimDir = new Vector2(sgn, 0f);
            }
            Vector2 dir = aimDir.normalized;

            Vector2 origin = rb.position + new Vector2(0f, G.castOriginYOffset);
            int mask = (G.attachMask.value == 0) ? ~0 : G.attachMask.value;

            var hit = Physics2D.Raycast(origin, dir, G.maxDistance, mask);
            if (!hit.collider)
            {
                // Miss + preview
                if (G.showPreviewOnMiss) ShowMissPreview(origin, origin + dir * G.maxDistance);
                // On reste inactif (l’AbilityController a déjà consommé l’usage si tu l’as configuré ainsi)
                return;
            }

            // Setup physique
            originalGravity = rb.gravityScale;
            rb.gravityScale = G.gravityDuringPull;

            // Rope renderer
            rope = CreateLR("GrappleRope", G.ropeWidth, G.ropeColor, G.ropeMaterial, G.ropeSortingLayerID, G.ropeSortingOrder);

            // DistanceJoint pour la phase latched (créé dès maintenant, mais on ajuste à la fin)
            joint = owner.AddComponent<DistanceJoint2D>();
            joint.enableCollision = false;
            joint.autoConfigureDistance = false;
            joint.connectedBody = null;
            joint.connectedAnchor = hit.point;
            joint.distance = Mathf.Max(G.holdDistance, 0.01f);
            joint.maxDistanceOnly = false;
            joint.enabled = false; // on n’active qu’à la phase latched

            anchorPoint = hit.point;
            state = State.Pulling;
            exclusive = true;
            active = true;

            loopCo = host.StartCoroutine(MainLoop());
        }

        IEnumerator MainLoop()
        {
            var wait = new WaitForFixedUpdate();
            while (active)
            {
                // MAJ corde visuelle
                if (rope != null)
                {
                    rope.SetPosition(0, rb.position + new Vector2(0f, G.castOriginYOffset));
                    rope.SetPosition(1, (state == State.Pulling) ? anchorPoint : joint.connectedAnchor);
                }

                if (state == State.Pulling)
                {
                    // Force vers l’ancre
                    Vector2 toAnchor = (anchorPoint - rb.position);
                    float dist = toAnchor.magnitude;
                    Vector2 dir = (dist > 0.0001f) ? toAnchor / dist : Vector2.zero;

                    // Arrivé ?
                    if (dist <= Mathf.Max(G.snapDistance, G.holdDistance))
                    {
                        EnterLatched();
                        yield return wait;
                        continue;
                    }

                    // Accélère vers l’ancre
                    rb.AddForce(dir * G.pullAcceleration * Time.fixedDeltaTime, ForceMode2D.Impulse);

                    // Clamp vitesse parallèle
                    Vector2 v = rb.velocity;
                    float vPar = Vector2.Dot(v, dir);
                    if (vPar > G.maxPullSpeed)
                    {
                        float excess = vPar - G.maxPullSpeed;
                        rb.AddForce(-dir * excess, ForceMode2D.Impulse);
                    }

                    // Damping latéral
                    if (G.lateralDamping > 0f)
                    {
                        Vector2 vParVec = vPar * dir;
                        Vector2 vLat = v - vParVec;
                        rb.AddForce(-vLat * G.lateralDamping * Time.fixedDeltaTime, ForceMode2D.Impulse);
                    }
                }
                else if (state == State.Latched)
                {
                    // Accroché : immobilisé (MovementOverride), gravité réduite/0
                    if (G.freezeVelocityOnLatch) rb.velocity = Vector2.zero;
                    // On n’applique pas de forces : le joint maintient la position
                }

                yield return wait;
            }
        }

        void EnterLatched()
        {
            state = State.Latched;
            rb.gravityScale = G.gravityWhileLatched;
            if (G.freezeVelocityOnLatch) rb.velocity = Vector2.zero;

            if (joint != null)
            {
                joint.enabled = true;
                joint.distance = Mathf.Max(G.holdDistance, 0.01f);
                joint.connectedAnchor = anchorPoint;
            }
        }

        public override bool OnJumpPressed()
        {
            if (!active) return false;

            // Le saut “décroche” (comme demandé).
            Cancel();
            return true; // on consomme le saut (et laisse SimpleJump éventuellement sauter ensuite)
        }

        public override void ForceCancelForTransfer()
        {
            if (!active) return;
            Cancel();
        }

        void Cancel()
        {
            if (!active) return;
            active = false;

            if (loopCo != null) { host.StopCoroutine(loopCo); loopCo = null; }
            if (previewCo != null) { host.StopCoroutine(previewCo); previewCo = null; }

            rb.gravityScale = originalGravity;

            if (joint) Object.Destroy(joint);
            joint = null;

            if (rope) Object.Destroy(rope.gameObject);
            rope = null;

            if (preview) Object.Destroy(preview.gameObject);
            preview = null;

            state = State.None;
            exclusive = false;

            pc?.SetSwinging(false); // au cas où tu utilises des états visuels
        }

        // --- Helpers visuels ---
        void ShowMissPreview(Vector2 a, Vector2 b)
        {
            if (previewCo != null) { host.StopCoroutine(previewCo); previewCo = null; }
            if (preview) { Object.Destroy(preview.gameObject); preview = null; }

            preview = CreateLR("GrappleMiss", G.missPreviewWidth, G.missPreviewColor, G.ropeMaterial, G.ropeSortingLayerID, G.ropeSortingOrder + 1);
            preview.SetPosition(0, a);
            preview.SetPosition(1, b);

            previewCo = host.StartCoroutine(PreviewLife());
        }

        IEnumerator PreviewLife()
        {
            float t = 0f;
            var wait = new WaitForEndOfFrame();
            Color c0 = preview.startColor, c1 = preview.endColor;
            while (t < G.missPreviewTime && preview != null)
            {
                float a = Mathf.Clamp01(1f - t / G.missPreviewTime);
                preview.startColor = new Color(c0.r, c0.g, c0.b, a * c0.a);
                preview.endColor = new Color(c1.r, c1.g, c1.b, a * c1.a);
                t += Time.deltaTime;
                yield return wait;
            }
            if (preview) Object.Destroy(preview.gameObject);
            preview = null; previewCo = null;
        }

        LineRenderer CreateLR(string name, float width, Color col, Material mat, int sortingLayerID, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(owner.transform, false);
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
}

