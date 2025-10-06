using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D), typeof(AbilityController))]
public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private AbilityController abilityCtrl;

    [Header("Move")]
    public float maxRunSpeed = 8f;
    public float runAccel = 60f;
    public float runDecel = 45f;

    [Header("Keys (fallback)")]
    public KeyCode dashKey = KeyCode.LeftShift;
    public KeyCode lassoKey = KeyCode.E;
    public KeyCode cancelKey = KeyCode.R;

    [Header("Air Momentum")]
    [SerializeField] private bool preserveAirMomentumWhenNoInput = true; // garde la vitesse X en l'air sans input
    [SerializeField] private bool useAirDecel = false;                   // si tu préfères une décélération douce au lieu de préserver 100%
    [SerializeField] private float airDecel = 4f;

    float inputX;
    bool grapplePressed = false;

    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        abilityCtrl = GetComponent<AbilityController>();
    }

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        if (!abilityCtrl) abilityCtrl = GetComponent<AbilityController>();
    }

    void Update()
    {
        // --- MOUVEMENT H ---
        inputX = 0f;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) inputX -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) inputX += 1f;
        }
        if (Gamepad.current != null)
        {
            float lx = Gamepad.current.leftStick.ReadValue().x;
            if (Mathf.Abs(lx) > 0.1f) inputX = lx;
        }
#endif
        if (inputX == 0f)
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inputX -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputX += 1f;
        }

        // --- AIM 360° POUR LES ABILITIES ---
        Vector2 aim = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Gamepad.current != null)
        {
            Vector2 rs = Gamepad.current.rightStick.ReadValue();   // priorité au stick droit
            if (rs.sqrMagnitude > 0.04f) aim = rs;
        }
        if (aim == Vector2.zero && Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.isPressed || Keyboard.current.wKey.isPressed) aim.y += 1f;
            if (Keyboard.current.downArrowKey.isPressed || Keyboard.current.sKey.isPressed) aim.y -= 1f;
            if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) aim.x += 1f;
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) aim.x -= 1f;
        }
#endif
        if (aim == Vector2.zero) // Fallback old Input
        {
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) aim.y += 1f;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) aim.y -= 1f;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) aim.x += 1f;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) aim.x -= 1f;
        }
        if (aim == Vector2.zero) // si rien, utiliser le facing
            aim = new Vector2(transform.localScale.x >= 0f ? 1f : -1f, 0f);

        // --- TRIGGERS ---
        bool dashPressed = false, lassoPressed = false, cancelPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            dashPressed |= Keyboard.current.leftShiftKey.wasPressedThisFrame;
            lassoPressed |= Keyboard.current.eKey.wasPressedThisFrame;
            cancelPressed |= Keyboard.current.rKey.wasPressedThisFrame;
        }
        if (Gamepad.current != null)
        {
            dashPressed |= Gamepad.current.leftShoulder.wasPressedThisFrame;
            lassoPressed |= Gamepad.current.rightShoulder.wasPressedThisFrame;
            cancelPressed |= Gamepad.current.bButton.wasPressedThisFrame;
        }
#endif


#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            grapplePressed |= Keyboard.current.qKey.wasPressedThisFrame;
        }
        if (Gamepad.current != null)
        {
            // Par ex: stick droit clic (R3) ou LeftTrigger — choisis ton bouton
            grapplePressed |= Gamepad.current.rightStickButton.wasPressedThisFrame;
        }
#endif
        grapplePressed |= Input.GetKeyDown(KeyCode.Q);

        if (grapplePressed) abilityCtrl.TriggerGrapple(aim);

        dashPressed |= Input.GetKeyDown(dashKey);
        lassoPressed |= Input.GetKeyDown(lassoKey);
        cancelPressed |= Input.GetKeyDown(cancelKey);

        if (dashPressed) abilityCtrl.TriggerDash(aim);
        if (lassoPressed) abilityCtrl.TriggerLasso(aim);
        if (cancelPressed) abilityCtrl.CancelCurrent();

        // --- FACING / FLIP ---
        if (Mathf.Abs(inputX) > 0.01f)
        {
            bool faceRight = inputX > 0f;
            abilityCtrl.SetFacing(faceRight);
            var s = transform.localScale; s.x = Mathf.Abs(s.x) * (faceRight ? 1f : -1f); transform.localScale = s;
        }
    }


    private void FixedUpdate()
    {
        // Si une ability exclusive pilote la vitesse (dash/lasso), on ne touche à rien
        if (abilityCtrl != null && abilityCtrl.MovementOverride) return;

        bool grounded = abilityCtrl != null && abilityCtrl.IsGrounded;
        float dt = Time.fixedDeltaTime;

        // 1) Pas d'input horizontal ?
        if (Mathf.Abs(inputX) < 0.01f)
        {
            if (!grounded)
            {
                // EN L'AIR + SANS INPUT
                if (preserveAirMomentumWhenNoInput)
                {
                    // Laisse la vitesse X telle quelle (pas de stop net)
                    return;
                }
                else if (useAirDecel)
                {
                    // Décélération aérienne douce vers 0 (faible frottement)
                    float newX = Mathf.MoveTowards(rb.velocity.x, 0f, airDecel * dt);
                    rb.velocity = new Vector2(newX, rb.velocity.y);
                    return;
                }
                // Sinon, on tombera sur le bloc "mouvement standard" plus bas,
                // mais comme targetX sera 0, ça freinerait fort en l'air — à éviter.
                return;
            }
            else
            {
                // AU SOL + SANS INPUT -> décélération classique vers 0
                float newX = Mathf.MoveTowards(rb.velocity.x, 0f, runDecel * dt);
                rb.velocity = new Vector2(newX, rb.velocity.y);
                return;
            }
        }

        // 2) Il y a de l'input -> mouvement standard
        float targetX = Mathf.Clamp(inputX, -1f, 1f) * maxRunSpeed;
        float accel = runAccel; // même en l'air on pousse si le joueur tient une direction
        float movedX = Mathf.MoveTowards(rb.velocity.x, targetX, accel * dt);
        rb.velocity = new Vector2(movedX, rb.velocity.y);
    }


    // --- utilitaires appelés par le lasso ---
    public void AdditiveImpulse(Vector2 impulse)
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        rb.AddForce(impulse, ForceMode2D.Impulse);
    }
    public void SetSwinging(bool swinging, float a = 0f, float b = 0f) { /* facultatif: VFX/SFX/state */ }
}








