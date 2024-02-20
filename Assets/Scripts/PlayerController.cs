using Unity.Netcode;
using UnityEngine;

/// <summary>
/// This code was provided by https://github.com/Matthew-J-Spencer/Unity-Netcode-Starter/blob/39b1d0861f825679bd1312eb4263de907aa4d33a/Assets/_Game/_Scripts/Player/PlayerController.cs#L7
/// </summary>
public class PlayerController : NetworkBehaviour
{
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _cam = Camera.main;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner) enabled = false;

    }

    private void Update()
    {
        _input = new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical"));
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleRotation();
    }

    #region Movement

    [SerializeField] private float _acceleration = 80;
    [SerializeField] private float _maxVelocity = 10;
    private Vector3 _input;
    private Rigidbody _rb;

    private void HandleMovement()
    {
        _rb.velocity += _input.normalized * (_acceleration * Time.deltaTime);
        _rb.velocity = Vector3.ClampMagnitude(_rb.velocity, _maxVelocity);
    }

    #endregion

    #region Rotation

    [SerializeField] private float _rotationSpeed = 450;
    private Plane _groundPlane = new(Vector3.up, Vector3.zero);
    private Camera _cam;

    private void HandleRotation()
    {
        var ray = _cam.ScreenPointToRay(Input.mousePosition);

        if (_groundPlane.Raycast(ray, out var enter))
        {
            var hitPoint = ray.GetPoint(enter);

            var dir = hitPoint - transform.position;
            var rot = Quaternion.LookRotation(dir);

            transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, _rotationSpeed * Time.deltaTime);
        }
    }

    #endregion
}
