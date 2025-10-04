using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AbilityController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;

    [Header("Equip (2 slots)")]
    public AbilitySO slotA;   // ex: Dash
    public AbilitySO slotB;   // ex: Lasso/Grapple

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

    public System.Action OnAbilityStarted; // pour SimpleJump (reset jump)

    public bool MovementOverride => current != null && current.IsExclusive && current.IsActive;

    // Flags refill conditionnel
    bool slotASpentSinceGround, slotBSpentSinceGround;

    void Reset() { rb = GetComponent<Rigidbody2D>(); }
    void Awake() { if (!rb) rb = GetComponent<Rigidbody2D>(); }

    void Update()
    {
        if (current != null && !current.IsActive)
        {
            if (refillWhileGroundedOnAbilityEnd && isGrounded) RefillFor(currentSO);
            current = null; currentSO = null;
        }
    }

    void OnDisable()
    {
        if (current != null) { current.ForceCancelForTransfer(); current = null; currentSO = null; }
    }

    // ---------- Public API ----------
    public void TriggerSlotA(Vector2 aimDir) => TriggerSlot(slotA, ref slotASpentSinceGround, aimDir);
    public void TriggerSlotB(Vector2 aimDir) => TriggerSlot(slotB, ref slotBSpentSinceGround, aimDir);

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
                if (slotASpentSinceGround) { RefillAmmo(slotA); slotASpentSinceGround = false; }
                if (slotBSpentSinceGround) { RefillAmmo(slotB); slotBSpentSinceGround = false; }
            }
            if (resetCooldownOnGround)
            {
                ResetCooldown(slotA);
                ResetCooldown(slotB);
            }
        }
    }

    // ---------- Helpers ----------
    void TriggerSlot(AbilitySO so, ref bool spentFlag, Vector2 aimDir)
    {
        if (!so || !so.IsReady()) return;
        if (current != null) { current.ForceCancelForTransfer(); current = null; currentSO = null; }

        var rt = so.CreateRuntime(gameObject, this);
        rt.Use(aimDir);
        so.MarkUsed();
        spentFlag |= (so.ammoMax >= 0);

        current = rt; currentSO = so;
        OnAbilityStarted?.Invoke(); // reset jump
    }

    void RefillAmmo(AbilitySO so) { if (so != null && so.ammoMax >= 0) so.ammoCurrent = so.ammoMax; }
    void ResetCooldown(AbilitySO so) { if (so != null) so.lastUseAt = -999f; }

    void RefillFor(AbilitySO so)
    {
        if (!refillAmmoOnGround || so == null) return;
        if (so == slotA && slotASpentSinceGround) { RefillAmmo(so); slotASpentSinceGround = false; }
        if (so == slotB && slotBSpentSinceGround) { RefillAmmo(so); slotBSpentSinceGround = false; }
    }
}
