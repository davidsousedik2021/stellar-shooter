using UnityEngine;
using Fusion;
using TMPro;
using System.Collections;

public class PlayerHealth : NetworkBehaviour
{
    private const string HitboxName = "Hitbox";

    [Networked] public float Health { get; set; }

    public TextMeshProUGUI healthText;

    public override void Spawned()
    {
        // Every local representation needs a collider so its Physics.Raycast can
        // detect remote players even though their CharacterController is disabled.
        EnsureHitbox();

        if (!HasStateAuthority) return;
        Health = 100f;
        Debug.Log($"[Health] {Object.Id} spawned with {Health} HP (StateAuthority: {Object.StateAuthority})");
    }

    void Update()
    {
        if (HasStateAuthority && healthText != null)
            healthText.text = $"HP: {Health:0}";
    }

    private void EnsureHitbox()
    {
        if (transform.Find(HitboxName) != null) return;

        GameObject hitboxObject = new GameObject(HitboxName);
        hitboxObject.layer = gameObject.layer;
        hitboxObject.transform.SetParent(transform, false);

        CapsuleCollider hitbox = hitboxObject.AddComponent<CapsuleCollider>();
        hitbox.isTrigger = true;
        hitbox.center = Vector3.zero;
        hitbox.height = 2f;
        hitbox.radius = 0.5f;
        hitbox.direction = 1; // Y axis
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage, RpcInfo info = default)
    {
        Debug.Log($"[RPC_TakeDamage] {Object.Id} received {damage} dmg from {info.Source} " +
                   $"(current HP: {Health})");

        if (Health <= 0)
        {
            Debug.Log($"[RPC_TakeDamage] {Object.Id} already at/below 0 HP, ignoring hit");
            return;
        }

        Health -= damage;
        Debug.Log($"[RPC_TakeDamage] {Object.Id} new HP: {Health}");

        if (Health <= 0)
        {
            Debug.Log($"[RPC_TakeDamage] {Object.Id} died — starting respawn sequence");
            StartCoroutine(RespawnSequence());
        }
    }

    private IEnumerator RespawnSequence()
    {
        Debug.Log($"[Health] {Object.Id} respawn sequence started");

        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        // Schování pod mapu
        transform.position = new Vector3(0f, -100f, 0f);

        yield return new WaitForSeconds(3f);

        Vector3 respawnPos = new Vector3(
            UnityEngine.Random.Range(-8f, 8f),
            2f,
            UnityEngine.Random.Range(-8f, 8f));

        // Fusion 2 – správná metoda je Teleport, ne TeleportToPosition
        GetComponent<NetworkTransform>().Teleport(respawnPos);
        transform.position = respawnPos;

        Health = 100f;
        if (controller != null) controller.enabled = true;

        Debug.Log($"[Health] {Object.Id} respawn complete at {respawnPos}, HP reset to {Health}");
    }
}
