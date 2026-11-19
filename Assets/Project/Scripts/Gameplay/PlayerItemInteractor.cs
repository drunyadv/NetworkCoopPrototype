using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerItemInteractor : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PhysicsItem itemPrefab;

    [Header("Input")]
    [SerializeField] private InputActionReference spawnAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference throwAction;
    [SerializeField] private InputActionReference deleteAction;

    [Header("Interaction")]
    [SerializeField] private float pickupDistance = 3.5f;
    [SerializeField] private float throwImpulse = 8f;

    private const float SpawnInterval = 0.4f;
    private const int MaxItems = 40;

    private PhysicsItem heldItem;
    private float nextSpawnTime;

    private void Update()
    {
        if (!IsSpawned || !IsOwner || !CanInteract())
            return;

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        if (spawnAction.action.WasPressedThisFrame())
            SpawnItemRpc();

        if (interactAction.action.WasPressedThisFrame())
        {
            ulong targetId = ulong.MaxValue;

            if (Physics.Raycast(
                    playerCamera.transform.position,
                    playerCamera.transform.forward,
                    out RaycastHit hit,
                    pickupDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                PhysicsItem item = hit.collider.GetComponentInParent<PhysicsItem>();

                if (item != null && item.IsSpawned)
                    targetId = item.NetworkObjectId;
            }

            InteractRpc(targetId);
        }

        if (throwAction.action.WasPressedThisFrame())
            ThrowItemRpc(playerCamera.transform.forward);

        if (deleteAction.action.WasPressedThisFrame())
            DeleteItemRpc();
    }

    private bool CanInteract()
    {
        return ConnectionService.Instance?.CanPlay == true;
    }

    private bool IsValidRequest(RpcParams rpcParams)
    {
        return IsServer &&
               IsSpawned &&
               rpcParams.Receive.SenderClientId == OwnerClientId &&
               CanInteract();
    }

    [Rpc(SendTo.Server)]
    private void SpawnItemRpc(RpcParams rpcParams = default)
    {
        if (!IsValidRequest(rpcParams) || itemPrefab == null)
            return;

        if (Time.time < nextSpawnTime)
            return;

        int itemCount = FindObjectsByType<PhysicsItem>(
            FindObjectsSortMode.None
        ).Length;

        if (itemCount >= MaxItems)
            return;

        nextSpawnTime = Time.time + SpawnInterval;

        Vector3 position = transform.position
            + Vector3.up * 1.5f
            + transform.forward * 2.25f;

        PhysicsItem item = Instantiate(
            itemPrefab,
            position,
            Quaternion.identity
        );

        item.NetworkObject.Spawn(true);
    }

    [Rpc(SendTo.Server)]
    private void InteractRpc(
        ulong targetId,
        RpcParams rpcParams = default)
    {
        if (!IsValidRequest(rpcParams))
            return;

        if (heldItem != null && heldItem.IsSpawned &&
            heldItem.HolderClientId.Value == OwnerClientId)
        {
            heldItem.Release();
            heldItem = null;
            return;
        }

        heldItem = null;

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(
                targetId, out NetworkObject target))
        {
            return;
        }

        if (!target.TryGetComponent(out PhysicsItem item))
            return;

        // Allow a small margin for replicated player positions.
        if (item.TryPickUp(OwnerClientId, transform, pickupDistance + 0.5f))
            heldItem = item;
    }

    [Rpc(SendTo.Server)]
    private void ThrowItemRpc(
        Vector3 direction,
        RpcParams rpcParams = default)
    {
        if (!IsValidRequest(rpcParams) ||
            heldItem == null ||
            !heldItem.IsSpawned ||
            heldItem.HolderClientId.Value != OwnerClientId)
        {
            return;
        }

        if (!IsValidDirection(direction))
            return;

        Vector3 horizontal = Vector3.ProjectOnPlane(
            direction,
            Vector3.up
        );

        if (horizontal.sqrMagnitude > 0.01f &&
            Vector3.Angle(transform.forward, horizontal) > 85f)
        {
            return;
        }

        heldItem.Throw(direction.normalized, throwImpulse);
        heldItem = null;
    }

    [Rpc(SendTo.Server)]
    private void DeleteItemRpc(RpcParams rpcParams = default)
    {
        if (!IsValidRequest(rpcParams) ||
            heldItem == null ||
            !heldItem.IsSpawned ||
            heldItem.HolderClientId.Value != OwnerClientId)
        {
            return;
        }

        heldItem.Delete();
        heldItem = null;
    }

    private static bool IsValidDirection(Vector3 direction)
    {
        if (float.IsNaN(direction.x) ||
            float.IsNaN(direction.y) ||
            float.IsNaN(direction.z) ||
            float.IsInfinity(direction.x) ||
            float.IsInfinity(direction.y) ||
            float.IsInfinity(direction.z))
        {
            return false;
        }

        return direction.sqrMagnitude > 0.5f &&
               direction.sqrMagnitude < 1.5f;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && heldItem != null && heldItem.IsSpawned)
            heldItem.Release();

        heldItem = null;
    }
}
