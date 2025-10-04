using UnityEngine;

public abstract class AbilitySO : ScriptableObject
{
    [Header("Common")]
    public float cooldown = 0f;        // secondes (0 = pas de CD)
    public int ammoMax = -1;           // -1 = infini

    // ÉTAT GLOBAL (par asset) — évite le reset à chaque nouvelle instance
    [System.NonSerialized] public float lastUseAt = -999f;
    [System.NonSerialized] public int ammoCurrent = int.MinValue;

    public bool IsReady()
    {
        if (ammoMax >= 0)
        {
            if (ammoCurrent == int.MinValue) ammoCurrent = ammoMax; // init lazy
            if (ammoCurrent <= 0) return false;
        }
        if (cooldown > 0f && Time.time < lastUseAt + cooldown) return false;
        return true;
    }

    public void MarkUsed()
    {
        lastUseAt = Time.time;
        if (ammoMax >= 0)
        {
            if (ammoCurrent == int.MinValue) ammoCurrent = ammoMax;
            if (ammoCurrent > 0) ammoCurrent--;
        }
    }

    public abstract AbilityRuntime CreateRuntime(GameObject owner, MonoBehaviour host);
}

public abstract class AbilityRuntime
{
    protected readonly AbilitySO data;
    protected readonly GameObject owner;
    protected readonly MonoBehaviour host;
    protected readonly Rigidbody2D rb;

    protected bool active;

    public virtual bool IsExclusive => false;
    public virtual bool IsActive => active;

    protected AbilityRuntime(AbilitySO data, GameObject owner, MonoBehaviour host)
    {
        this.data = data;
        this.owner = owner;
        this.host = host;
        rb = owner.GetComponent<Rigidbody2D>();
    }

    public virtual void Use(Vector2 aimDir) { }
    public virtual Vector2 GetTransferImpulse() => Vector2.zero;
    public virtual void ForceCancelForTransfer() { }
    public virtual bool OnJumpPressed() => false;
}









