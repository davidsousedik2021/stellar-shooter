using UnityEngine;
using Fusion;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;

public class PlayerWeapon : NetworkBehaviour
{
    public Camera fpsCamera;
    public GameObject hitEffectPrefab;
    public Light muzzleFlashLight;
    public PlayerMovement playerMovement;  // propoj v Inspectoru – kvůli recoilu

    [Header("Statistiky")]
    public float damage = 35f;
    public float fireRate = 0.1f;   // sekund mezi výstřely (0.1 = 600 RPM)
    public float weaponRange = 150f;
    public float reloadTime = 1.5f;

    [Header("Zásobník")]
    public int magazineSize = 30;
    public int totalAmmoReserve = 90;

    [Header("Recoil")]
    public float recoilPerShot = 1.2f;   // stupňů kicku nahoru za výstřel
    public float maxRecoil = 8f;     // strop – kamera nevyletí do nebe
    public float recoilRecovery = 8f;     // jak rychle se vrátí (vyšší = rychlejší)

    [Header("Spread (nepřesnost)")]
    public float baseSpread = 0.01f;  // v klidu
    public float movingSpread = 0.04f;  // při pohybu
    public float sprintingSpread = 0.08f;  // při sprintu

    [Header("UI")]
    public TextMeshProUGUI ammoText;
    public TextMeshProUGUI reloadText;

    private int _currentAmmo, _reserveAmmo;
    private bool _isReloading;
    private float _nextFireTime;
    private float _currentRecoil;

    // Pohybový stav čteme přes referenci na Movement
    private bool _isMoving;
    private bool _isSprinting;

    public override void Spawned()
    {
        if (!HasStateAuthority) return;
        _currentAmmo = magazineSize;
        _reserveAmmo = totalAmmoReserve;
        UpdateAmmoUI();
        if (reloadText != null) reloadText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!HasStateAuthority) return;
        HandleShooting();
        HandleReloadInput();

        // Recoil recovery (pohybuje se v Movement.Update přes recoilOffset)
        _currentRecoil = Mathf.Lerp(_currentRecoil, 0f, Time.deltaTime * recoilRecovery);
        if (playerMovement != null)
            playerMovement.recoilOffset = _currentRecoil;
    }

    // ── Střelba ───────────────────────────────────────────────────

    private void HandleShooting()
    {
        if (_isReloading) return;
        if (Mouse.current == null) return;

        // isPressed = automatická střelba (drž LMB)
        if (!Mouse.current.leftButton.isPressed) return;

        if (_currentAmmo <= 0)
        {
            if (_reserveAmmo > 0)
            {
                Debug.Log("[HandleShooting] Out of ammo, auto-reloading");
                StartCoroutine(ReloadSequence());
            }
            return;
        }

        if (Time.time < _nextFireTime) return;

        Shoot();
        _nextFireTime = Time.time + fireRate;
    }

    private void Shoot()
    {
        _currentAmmo--;
        UpdateAmmoUI();
        RPC_ShowMuzzleFlash();

        // Recoil kick
        _currentRecoil = Mathf.Min(_currentRecoil + recoilPerShot, maxRecoil);

        // Spread – závisí na pohybu
        float spread = GetCurrentSpread();
        Vector3 direction = fpsCamera.transform.forward;
        direction += new Vector3(
            Random.Range(-spread, spread),
            Random.Range(-spread, spread),
            Random.Range(-spread, spread));
        direction.Normalize();

        Ray ray = new Ray(fpsCamera.transform.position, direction);

        // Visualize the shot in the Scene view for 1 second
        Debug.DrawRay(ray.origin, ray.direction * weaponRange, Color.red, 1f);

        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                weaponRange,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide))
        {
            Debug.Log($"[Shoot] Hit '{hit.transform.name}' " +
                       $"(layer: {LayerMask.LayerToName(hit.transform.gameObject.layer)}) " +
                       $"at {hit.point}, distance {hit.distance:F1}");

            if (hitEffectPrefab != null)
            {
                GameObject fx = Instantiate(
                    hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(fx, 2f);
            }

            // The hit collider may belong to a child body object; health is on the player root.
            PlayerHealth targetHealth = hit.collider.GetComponentInParent<PlayerHealth>();

            if (targetHealth == null)
            {
                Debug.LogWarning($"[Shoot] '{hit.transform.name}' has no PlayerHealth " +
                                  $"on the hit collider's own GameObject. " +
                                  $"Parent has one: {hit.transform.GetComponentInParent<PlayerHealth>() != null}");
            }
            else
            {
                if (targetHealth.Object.StateAuthority == PlayerRef.None)
                {
                    Debug.LogWarning($"[Shoot] Target {targetHealth.Object.Id} has no " +
                                      $"StateAuthority — RPC will not be delivered!");
                }

                Debug.Log($"[Shoot] Sending RPC_TakeDamage({damage}) to " +
                           $"{targetHealth.Object.Id} (StateAuthority: {targetHealth.Object.StateAuthority})");
                targetHealth.RPC_TakeDamage(damage);
            }
        }
        else
        {
            Debug.Log("[Shoot] Raycast hit nothing — ray passed clean, " +
                       "check DrawRay in Scene view or target's collider/layer state.");
        }
    }

    private float GetCurrentSpread()
    {
        if (playerMovement == null) return baseSpread;

        // Čteme pohyb z CharacterController velocity
        CharacterController cc = playerMovement.GetComponent<CharacterController>();
        if (cc == null) return baseSpread;

        float horizSpeed = new Vector3(cc.velocity.x, 0, cc.velocity.z).magnitude;

        if (horizSpeed > 12f) return sprintingSpread;  // sprint
        if (horizSpeed > 1f) return movingSpread;     // chůze
        return baseSpread;                             // stojí
    }

    // ── Přebíjení ─────────────────────────────────────────────────

    private void HandleReloadInput()
    {
        if (_isReloading || _currentAmmo == magazineSize || _reserveAmmo <= 0) return;
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Debug.Log("[HandleReloadInput] Manual reload triggered");
            StartCoroutine(ReloadSequence());
        }
    }

    private IEnumerator ReloadSequence()
    {
        Debug.Log("[ReloadSequence] Reload started");
        _isReloading = true;
        if (reloadText != null) { reloadText.gameObject.SetActive(true); reloadText.text = "Přebíjím..."; }

        yield return new WaitForSeconds(reloadTime);

        int needed = magazineSize - _currentAmmo;
        int refill = Mathf.Min(needed, _reserveAmmo);
        _currentAmmo += refill;
        _reserveAmmo -= refill;

        _isReloading = false;
        if (reloadText != null) reloadText.gameObject.SetActive(false);
        UpdateAmmoUI();
        Debug.Log($"[ReloadSequence] Reload complete: {_currentAmmo}/{_reserveAmmo}");
    }

    // ── UI ────────────────────────────────────────────────────────

    private void UpdateAmmoUI()
    {
        if (ammoText != null) ammoText.text = $"{_currentAmmo} / {_reserveAmmo}";
    }

    // ── RPC ───────────────────────────────────────────────────────

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ShowMuzzleFlash()
    {
        if (muzzleFlashLight != null) StartCoroutine(MuzzleFlashEffect());
    }

    private IEnumerator MuzzleFlashEffect()
    {
        muzzleFlashLight.enabled = true;
        yield return new WaitForSeconds(0.05f);
        muzzleFlashLight.enabled = false;
    }
}
