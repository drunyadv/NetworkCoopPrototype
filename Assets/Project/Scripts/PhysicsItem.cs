using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public sealed class PhysicsItem : NetworkBehaviour
{
    public const ulong NoHolder = ulong.MaxValue;

    public readonly NetworkVariable<ulong> HolderClientId = new(NoHolder);

    private Rigidbody body;
    private Collider itemCollider;
    private Transform holder;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        itemCollider = GetComponent<Collider>();

        GetComponent<NetworkObject>().DontDestroyWithOwner = true;
    }

    public override void OnNetworkSpawn()
    {
        HolderClientId.OnValueChanged += OnHolderChanged;
        OnHolderChanged(NoHolder, HolderClientId.Value);

        if (IsServer)
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void FixedUpdate()
    {
        if (!IsSpawned || !IsServer || holder == null)
            return;

        Vector3 target = holder.position
            + Vector3.up * 1.35f
            + holder.forward * 1.7f;

        body.MovePosition(target);
        body.MoveRotation(Quaternion.Euler(0f, holder.eulerAngles.y, 0f));
    }

    public bool TryPickUp(ulong clientId, Transform player, float maxDistance)
    {
        if (!IsServer || !IsSpawned || HolderClientId.Value != NoHolder)
            return false;

        Vector3 eyePosition = player.position + Vector3.up * 1.65f;

        if (Vector3.Distance(eyePosition, body.position) > maxDistance)
            return false;

        holder = player;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        body.useGravity = false;

        HolderClientId.Value = clientId;
        NetworkObject.ChangeOwnership(clientId);

        return true;
    }

    public void Release()
    {
        if (!IsServer || !IsSpawned || HolderClientId.Value == NoHolder)
            return;

        holder = null;
        HolderClientId.Value = NoHolder;

        body.isKinematic = false;
        body.useGravity = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        if (!NetworkObject.IsOwnedByServer)
        NetworkObject.RemoveOwnership();
        body.WakeUp();
    }

    public void Throw(Vector3 direction, float impulse)
    {
        if (!IsServer || HolderClientId.Value == NoHolder)
            return;

        Release();
        body.AddForce(direction.normalized * impulse, ForceMode.Impulse);
    }

    public void Delete()
    {
        if (IsServer && IsSpawned)
            NetworkObject.Despawn(true);
    }

    private void OnHolderChanged(ulong previous, ulong current)
    {
        if (itemCollider != null)
            itemCollider.enabled = current == NoHolder;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsServer && HolderClientId.Value == clientId)
            Release();
    }

    public override void OnNetworkDespawn()
    {
        HolderClientId.OnValueChanged -= OnHolderChanged;

        if (IsServer)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }
}
