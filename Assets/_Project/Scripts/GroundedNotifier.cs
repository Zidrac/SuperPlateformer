using UnityEngine;

[DefaultExecutionOrder(-50)]
public class GroundedNotifier : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private AbilityController abilityCtrl;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Settings")]
    [SerializeField] private float groundRadius = 0.18f;

    bool lastGrounded;

    void Reset()
    {
        abilityCtrl = GetComponentInParent<AbilityController>();
        groundCheck = transform;
    }

    void Awake()
    {
        if (!abilityCtrl) abilityCtrl = GetComponentInParent<AbilityController>();
        if (!groundCheck) groundCheck = transform;
    }

    void FixedUpdate()
    {
        bool grounded = Physics2D.OverlapCircle(groundCheck.position, groundRadius, groundLayers);
        if (grounded != lastGrounded)
        {
            lastGrounded = grounded;
            abilityCtrl?.NotifyGrounded(grounded);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        var pos = groundCheck ? groundCheck.position : transform.position;
        Gizmos.DrawWireSphere(pos, groundRadius);
    }
#endif
}




