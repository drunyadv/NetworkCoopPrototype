using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerColor : NetworkBehaviour
{
    [SerializeField] private Renderer bodyRenderer;

    private readonly NetworkVariable<byte> colorIndex = new(0);

    private static readonly Color[] Colors =
    {
        new Color(0.25f, 0.55f, 0.95f),
        new Color(0.95f, 0.35f, 0.35f),
        new Color(0.3f, 0.8f, 0.5f),
        new Color(0.95f, 0.75f, 0.25f)
    };

    private MaterialPropertyBlock properties;

    private void Awake()
    {
        properties = new MaterialPropertyBlock();
    }

    public override void OnNetworkSpawn()
    {
        colorIndex.OnValueChanged += OnColorChanged;
        ApplyColor(colorIndex.Value);
    }

    private void Update()
    {
        if (!IsOwner || !IsSpawned || !CanChangeColor())
            return;

        if (Keyboard.current != null &&
            Keyboard.current.cKey.wasPressedThisFrame)
        {
            ChangeColorRpc();
        }
    }

    private bool CanChangeColor()
    {
        return ConnectionService.Instance?.CanPlay == true &&
               Cursor.lockState == CursorLockMode.Locked;
    }

    [Rpc(SendTo.Server)]
    private void ChangeColorRpc()
    {
        colorIndex.Value = (byte)((colorIndex.Value + 1) % Colors.Length);
    }

    private void OnColorChanged(byte previous, byte current)
    {
        ApplyColor(current);
    }

    private void ApplyColor(byte index)
    {
        bodyRenderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", Colors[index % Colors.Length]);
        bodyRenderer.SetPropertyBlock(properties);
    }

    public override void OnNetworkDespawn()
    {
        colorIndex.OnValueChanged -= OnColorChanged;
    }
}
