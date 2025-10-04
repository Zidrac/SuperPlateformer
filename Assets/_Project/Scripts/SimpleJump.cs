using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D), typeof(AbilityController))]
public class SimpleJump : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private AbilityController abilityCtrl;

    [Header("Tune by Height")]
    public bool useDesiredHeight = true;
    public float desiredJumpHeight = 3f;

    [Header("Jump")]
    public float jumpImpulse = 12f;
    public int maxAirJumps = 0;

    [Header("Assist")]
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.12f;
    public bool cutUpwardOnRelease = true;
    public float cutFactor = 0.5f;

    float lastGroundedTime = -999f;
    float lastJumpPressedTime = -999f;
    int airJumpsUsed = 0;

    void Reset() { rb = GetComponent<Rigidbody2D>(); abilityCtrl = GetComponent<AbilityController>(); }

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!abilityCtrl) abilityCtrl = GetComponent<AbilityController>();
        abilityCtrl.OnGroundedChanged += OnGroundedChanged;
        abilityCtrl.OnAbilityStarted += ResetJumpUsage; // ← reset quand une ability démarre
        RecomputeImpulseFromHeight();
    }

    void OnDestroy()
    {
        if (abilityCtrl != null)
        {
            abilityCtrl.OnGroundedChanged -= OnGroundedChanged;
            abilityCtrl.OnAbilityStarted -= ResetJumpUsage;
        }
    }

    void RecomputeImpulseFromHeight()
    {
        if (!useDesiredHeight) return;
        float g = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;
        float v = Mathf.Sqrt(2f * g * Mathf.Max(0.01f, desiredJumpHeight));
        jumpImpulse = v * rb.mass;
    }

    void ResetJumpUsage()
    {
        // réinitialise le système de jump (tu “récupères” ta possibilité de sauter)
        airJumpsUsed = 0;
        lastJumpPressedTime = -999f; // purge le buffer
        // on ne modifie pas lastGroundedTime ici pour garder le coyote “réel”
    }

    void OnGroundedChanged(bool grounded)
    {
        if (grounded)
        {
            lastGroundedTime = Time.time;
            airJumpsUsed = 0;
        }
    }

    void Update()
    {
        // --- LECTURE SAUT: UNIQUEMENT SPACE (+ Gamepad A). Up/W ont été retirés. ---
        bool pressed = false, released = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            pressed |= Keyboard.current.spaceKey.wasPressedThisFrame;
            released |= Keyboard.current.spaceKey.wasReleasedThisFrame;
        }
        if (Gamepad.current != null)
        {
            pressed |= Gamepad.current.aButton.wasPressedThisFrame;
            released |= Gamepad.current.aButton.wasReleasedThisFrame;
        }
#endif
        // Fallback (ancien système): uniquement Space
        pressed |= Input.GetKeyDown(KeyCode.Space);
        released |= Input.GetKeyUp(KeyCode.Space);

        if (pressed)
        {
            // Si une ability souhaite consommer le saut (dash/lasso), on ne saute pas ici
            if (abilityCtrl != null && abilityCtrl.HandleJumpPressed()) return;
            lastJumpPressedTime = Time.time;
        }

        if (released && cutUpwardOnRelease && rb.velocity.y > 0f)
            rb.velocity = new Vector2(rb.velocity.x, rb.velocity.y * (1f - Mathf.Clamp01(cutFactor)));

        TryConsumeJump();
    }

    void TryConsumeJump()
    {
        bool grounded = abilityCtrl != null && abilityCtrl.IsGrounded;
        bool withinCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
        bool buffered = (Time.time - lastJumpPressedTime) <= jumpBufferTime;
        if (!buffered) return;

        if (grounded || withinCoyote)
        {
            DoJump(); lastJumpPressedTime = -999f; return;
        }

        if (maxAirJumps > 0 && airJumpsUsed < maxAirJumps)
        {
            DoJump(); airJumpsUsed++; lastJumpPressedTime = -999f;
        }
    }

    void DoJump()
    {
        if (rb.velocity.y < 0f) rb.velocity = new Vector2(rb.velocity.x, 0f);
        rb.AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
    }
}





