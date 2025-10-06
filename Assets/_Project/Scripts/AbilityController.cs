using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AbilityController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private PlayerController player; // optionnel

    [Header("Abilities")]
    public AbilitySO dashAbility;
    public AbilitySO lassoAbility;
    public AbilitySO grappleAbility;                // NOUVEAU

    [Header("Refill")]
    [SerializeField] private bool refillAmmoOnGround = true;
    [SerializeField] private bool resetCooldownOnGround = false;
    [SerializeField] private bool refillWhileGroundedOnAbilityEnd = true;

    AbilityRuntime current;
    AbilitySO currentSO;
    bool facingRight = true;

    [SerializeField] private bool isGrounded;
    public bool IsGrounded => isGrounded;
    public System.Action<bool> OnGroundedChanged;

    public System.Action OnAbilityStarted; // reset jump côté SimpleJump

    public bool MovementOverride => current != null && current.IsExclusive && current.IsActive;

    // Spent flags (recharge conditionnelle)
    bool dashSpentSinceGround, lassoSpentSinceGround, grappleSpentSinceGround;

    void Reset() { rb = GetComponent<Rigidbody2D>(); player = GetComponent<PlayerController>(); }
    void Awake() { if (!rb) rb = GetComponent<Rigidbody2D>(); if (!player) player = GetComponent<PlayerController>(); }

    void Update()
    {
        if (current != null && !current.IsActive)
        {
            if (refillWhileGroundedOnAbilityEnd && isGrounded) RefillFor(currentSO);
            current = null;
            currentSO = null;
        }
    }

    void OnDisable()
    {
        if (current != null) { current.ForceCancelForTransfer(); current = null; currentSO = null; }
    }

    // --------- Triggers ----------
    public void TriggerDash(Vector2 aimDir)
    {
        if (!dashAbility || !dashAbility.IsReady()) return;
        CancelCurrent();

        var rt = dashAbility.CreateRuntime(gameObject, this);
        rt.Use(aimDir);
        dashAbility.MarkUsed();
        dashSpentSinceGround |= (dashAbility.ammoMax >= 0);

        current = rt; currentSO = dashAbility;
        OnAbilityStarted?.Invoke();
    }

    public void TriggerLasso(Vector2 aimDir)
    {
        if (!lassoAbility || !lassoAbility.IsReady()) return;
        CancelCurrent();

        var rt = lassoAbility.CreateRuntime(gameObject, this);
        rt.Use(aimDir);
        lassoAbility.MarkUsed();
        lassoSpentSinceGround |= (lassoAbility.ammoMax >= 0);

        current = rt; currentSO = lassoAbility;
        OnAbilityStarted?.Invoke();
    }

    public void TriggerGrapple(Vector2 aimDir) // NEW
    {
        if (!grappleAbility || !grappleAbility.IsReady()) return;
        CancelCurrent();

        var rt = grappleAbility.CreateRuntime(gameObject, this);
        rt.Use(aimDir);
        grappleAbility.MarkUsed();
        grappleSpentSinceGround |= (grappleAbility.ammoMax >= 0);

        current = rt; currentSO = grappleAbility;
        OnAbilityStarted?.Invoke();
    }

    public void CancelCurrent()
    {
        if (current != null) { current.ForceCancelForTransfer(); current = null; currentSO = null; }
    }

    public bool HandleJumpPressed()
    {
        if (current != null && current.OnJumpPressed()) return true;
        return false;
    }

    public void SetFacing(bool right) => facingRight = right;
    public int FacingSign() => facingRight ? 1 : -1;

    public void NotifyGrounded(bool grounded)
    {
        if (isGrounded == grounded) return;
        isGrounded = grounded;
        OnGroundedChanged?.Invoke(grounded);

        if (grounded)
        {
            if (refillAmmoOnGround)
            {
                if (dashSpentSinceGround) { RefillAmmo(dashAbility); dashSpentSinceGround = false; }
                if (lassoSpentSinceGround) { RefillAmmo(lassoAbility); lassoSpentSinceGround = false; }
                if (grappleSpentSinceGround) { RefillAmmo(grappleAbility); grappleSpentSinceGround = false; }
            }
            if (resetCooldownOnGround)
            {
                ResetCooldown(dashAbility);
                ResetCooldown(lassoAbility);
                ResetCooldown(grappleAbility);
            }
        }
    }

    // --------- Helpers ----------
    void RefillAmmo(AbilitySO so) { if (so != null && so.ammoMax >= 0) so.ammoCurrent = so.ammoMax; }
    void ResetCooldown(AbilitySO so) { if (so != null) so.lastUseAt = -999f; }

    void RefillFor(AbilitySO so)
    {
        if (!refillAmmoOnGround || so == null) return;
        if (so == dashAbility && dashSpentSinceGround) { RefillAmmo(so); dashSpentSinceGround = false; }
        if (so == lassoAbility && lassoSpentSinceGround) { RefillAmmo(so); lassoSpentSinceGround = false; }
        if (so == grappleAbility && grappleSpentSinceGround) { RefillAmmo(so); grappleSpentSinceGround = false; }
    }
}
















