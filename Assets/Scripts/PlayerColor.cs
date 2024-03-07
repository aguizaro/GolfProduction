using Unity.Netcode;
using UnityEngine;

// code provided by https://github.com/Matthew-J-Spencer/Unity-Netcode-Starter/blob/main/Assets/_Game/_Scripts/Player/PlayerColor.cs

/// <summary>
/// Simple server-authoritative example.
/// Also shows reactive checks instead of per-frame checks
/// If you have questions, pop into discord and have a chat https://discord.gg/tarodev
/// </summary>
public class PlayerColor : NetworkBehaviour
{
    private readonly NetworkVariable<Color> _netColor = new();
    private readonly Color[] _colors = { Color.red, Color.blue, Color.green, Color.yellow, Color.black, Color.white, Color.magenta, Color.gray };
    private int _index;

    [SerializeField] private MeshRenderer _renderer;

    private void Awake()
    {
        // Subscribing to a change event. This is how the owner will change its color.
        // Could also be used for future color changes
        _netColor.OnValueChanged += OnValueChanged;
    }

    public override void OnDestroy()
    {
        _netColor.OnValueChanged -= OnValueChanged;
    }

    private void OnValueChanged(Color prev, Color next)
    {
        _renderer.material.color = next;
        Debug.Log("On Value Change: " + _renderer.material.color);
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log("onNetSpawn, value of _netColor : " + _netColor.Value);

        // Take note, RPCs are queued up to run.
        // If we tried to immediately set our color locally after calling this RPC it wouldn't have propagated
        if (IsOwner)
        {
            _index = (int)OwnerClientId;
            Debug.Log("Owner ID: " + _index);
            Color nextColor = GetNextColor();

            Debug.Log("Calling CommitNetworkColorServerRpc with " + nextColor);
            CommitNetworkColorServerRpc(nextColor);
        }
        else
        {
            Debug.Log("Non-Owner reading _netColor " + _netColor.Value);
            _renderer.material.color = _netColor.Value;
        }

        base.OnNetworkSpawn();
    }

    [ServerRpc]
    private void CommitNetworkColorServerRpc(Color color)
    {
        _netColor.Value = color;
        Debug.Log("Server set netColor to: " + _netColor.Value);
    }

    private Color GetNextColor()
    {
        return _colors[_index++ % _colors.Length];
    }
}