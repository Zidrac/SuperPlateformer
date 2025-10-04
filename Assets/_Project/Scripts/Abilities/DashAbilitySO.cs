using UnityEngine;
using System.Collections;

[CreateAssetMenu(menuName = "Abilities/Dash")]
public class DashAbilitySO : AbilitySO
{
    [Header("Dash")]
    public float dashSpeed = 18f;
    public float dashDuration = 0.16f;

    [Header("Physique pendant le dash")]
    public float gravityDuringDash = 0f;
    public bool lockSpeedConstant = true;
    public bool cutVerticalOnBegin = true;

    [Tooltip("À la fin NATURELLE du dash (pas via jump), met Y=0 s'il était >0 pour éviter un micro-bump.")]
    public bool zeroVerticalOnExit = true;

    [Header("Jump pendant dash → BUMP de sortie")]
    public bool jumpAddsBump = true;
    public float bumpImpulse = 10f;

    [Range(0f, 1f)]
    public float bumpHorizontalBias = 0.25f;

    [Header("Affinage sol / air")]
    public float bumpVerticalBonusIfGrounded = 1.2f;
    public float bumpVerticalBonusInAir = 0.6f;

    [Range(0f, 1f)]
    public float airUpwardCorrectBias = 0.35f;

    [Tooltip("Vitesse max après bump (0 = pas de clamp).")]
    public float bumpMaxResultSpeed = 24f;

    [Header("Fenêtre de grâce (post-dash)")]
    [Tooltip("Durée pendant laquelle on peut encore appuyer Saut APRÈS la fin du dash pour déclencher le bump si le joueur est en l’air.")]
    public float postDashJumpCoyoteTime = 0.15f;

    public override AbilityRuntime CreateRuntime(GameObject owner, MonoBehaviour host)
        => new DashRuntime(this, owner, host);

    class DashRuntime : AbilityRuntime
    {
        readonly DashAbilitySO D;
        Coroutine co;
        float originalGravity;
        Vector2 dir;                 // direction 360°
        bool exclusive = true;       // exclusif pendant le dash, pas pendant la grâce
        bool inGrace = false;        // phase de fenêtre post-dash
        float graceLeft = 0f;

        public override bool IsExclusive => exclusive;

        public DashRuntime(DashAbilitySO d, GameObject owner, MonoBehaviour host) : base(d, owner, host) { D = d; }

        public override void Use(Vector2 aimDir)
        {
            if (aimDir.sqrMagnitude < 0.0001f)
            {
                var ac = host as AbilityController;
                int s = (ac != null) ? ac.FacingSign() : (owner.transform.localScale.x >= 0 ? 1 : -1);
                aimDir = new Vector2(s, 0f);
            }
            dir = aimDir.normalized;

            originalGravity = rb.gravityScale;
            rb.gravityScale = D.gravityDuringDash;

            if (D.cutVerticalOnBegin && rb.velocity.y > 0f)
                rb.velocity = new Vector2(rb.velocity.x, 0f);

            rb.velocity = dir * D.dashSpeed;

            inGrace = false;
            exclusive = true;
            active = true;

            co = host.StartCoroutine(DashLoop());
        }

        IEnumerator DashLoop()
        {
            var wait = new WaitForFixedUpdate();
            float t = 0f;

            while (t < D.dashDuration)
            {
                if (D.lockSpeedConstant) rb.velocity = dir * D.dashSpeed;
                t += Time.fixedDeltaTime;
                yield return wait;
            }

            // Fin NATURELLE du dash → on rend la gravité, optionnellement on coupe Y>0,
            // puis on entre en "grace window" pour capter un jump un peu après.
            rb.gravityScale = originalGravity;
            if (D.zeroVerticalOnExit && rb.velocity.y > 0f)
                rb.velocity = new Vector2(rb.velocity.x, 0f);

            EnterGracePhase();
        }

        void EnterGracePhase()
        {
            inGrace = true;
            graceLeft = Mathf.Max(0f, D.postDashJumpCoyoteTime);
            exclusive = false; // plus de MovementOverride
            // On garde active=true pour que AbilityController continue de router Jump ici.
            if (co != null) host.StopCoroutine(co);
            co = host.StartCoroutine(GraceLoop());
        }

        IEnumerator GraceLoop()
        {
            var wait = new WaitForFixedUpdate();
            while (graceLeft > 0f)
            {
                graceLeft -= Time.fixedDeltaTime;
                yield return wait;
            }
            // Fin de la fenêtre: on se termine vraiment
            active = false;
            inGrace = false;
            exclusive = false;
            co = null;
        }

        public override void ForceCancelForTransfer()
        {
            if (!active) return;
            if (co != null) host.StopCoroutine(co);
            // Restaure la gravité, et termine toute phase (dash ou grâce)
            rb.gravityScale = originalGravity;
            active = false;
            inGrace = false;
            exclusive = false;
            co = null;
        }

        public override bool OnJumpPressed()
        {
            if (!active) return false;

            var ac = host as AbilityController;
            bool groundedNow = (ac != null && ac.IsGrounded);

            // Cas 1: on est encore en plein dash (exclusive=true)
            if (!inGrace && exclusive)
            {
                // Annule immédiatement le dash en CONSERVANT la vélocité
                if (co != null) host.StopCoroutine(co);
                Vector2 vBefore = rb.velocity;
                rb.gravityScale = originalGravity;
                rb.velocity = vBefore;

                exclusive = false; // on ne pilote plus le mouvement
                active = true;     // reste actif pour consommer l'input (mais on va bump maintenant)
                inGrace = false;   // on bump tout de suite, pas de grâce après un bump volontaire
                co = null;

                ApplyBump(groundedNow);
                active = false;    // terminé après le bump
                return true;
            }

            // Cas 2: dans la fenêtre de grâce post-dash
            if (inGrace)
            {
                // Si on est au sol, on laisse SimpleJump faire un saut normal.
                if (groundedNow)
                {
                    // Fin immédiate de la grâce: on laisse la responsabilité à SimpleJump
                    active = false;
                    inGrace = false;
                    exclusive = false;
                    if (co != null) { host.StopCoroutine(co); co = null; }
                    return false; // ne consomme pas: SimpleJump prendra le relais
                }

                // En l'air → applique le même bump que pendant le dash
                ApplyBump(false);

                // Fin de la grâce après bump
                active = false;
                inGrace = false;
                exclusive = false;
                if (co != null) { host.StopCoroutine(co); co = null; }
                return true;
            }

            return false;
        }

        void ApplyBump(bool groundedNow)
        {
            if (!D.jumpAddsBump) return;

            // Direction de base: trajectoire actuelle; fallback: dir du dash, sinon facing
            var ac = host as AbilityController;

            Vector2 trajDir =
                (rb.velocity.sqrMagnitude > 0.0001f) ? rb.velocity.normalized :
                (dir.sqrMagnitude > 0.0001f ? dir :
                 new Vector2((ac != null && ac.FacingSign() < 0) ? -1f : 1f, 0f));

            // Biais horizontal pour “sentir” le saut
            if (D.bumpHorizontalBias > 0f)
            {
                float sx = Mathf.Sign(trajDir.x == 0f ? ((ac != null && ac.FacingSign() < 0) ? -1f : 1f) : trajDir.x);
                Vector2 horiz = new Vector2(sx, 0f);
                trajDir = Vector2.Lerp(trajDir, horiz, D.bumpHorizontalBias).normalized;
            }

            // En l'air: si la trajectoire pointe vers le bas, on la redresse un peu
            if (!groundedNow && trajDir.y < 0f && D.airUpwardCorrectBias > 0f)
            {
                Vector2 upOrHoriz = new Vector2(
                    Mathf.Sign(trajDir.x == 0f ? ((ac != null && ac.FacingSign() < 0) ? -1f : 1f) : trajDir.x),
                    0.35f
                ).normalized;
                trajDir = Vector2.Lerp(trajDir, upOrHoriz, D.airUpwardCorrectBias).normalized;
            }

            // Impulsion + lift (sol / air)
            Vector2 impulse = trajDir * D.bumpImpulse;
            float lift = groundedNow ? D.bumpVerticalBonusIfGrounded : D.bumpVerticalBonusInAir;
            if (lift > 0f) impulse += Vector2.up * lift;

            rb.AddForce(impulse, ForceMode2D.Impulse);

            // Clamp
            if (D.bumpMaxResultSpeed > 0f)
            {
                float spd = rb.velocity.magnitude;
                if (spd > D.bumpMaxResultSpeed)
                    rb.velocity = rb.velocity.normalized * D.bumpMaxResultSpeed;
            }
        }
    }
}




















