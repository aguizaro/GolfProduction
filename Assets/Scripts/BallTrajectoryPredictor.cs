using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(LineRenderer))]
public class TrajectoryPredictor : MonoBehaviour
{
    #region Members
    LineRenderer trajectoryLine;
    [SerializeField, Tooltip("The marker will show where the projectile will hit")]
    Transform hitMarker;
    int maxPoints = 250;
    float increment = 0.015f;
    float rayOverlap = 1.1f;
    #endregion

    PowerMeter playerPowerMeter;
    float swingForce = 50.0f;
    float verticalAngle = 0.50f;
    Vector3 targetDir;
    ProjectileProperties ballProperties;
    Rigidbody rb;

    private void Start()
    {
        if (trajectoryLine == null)
        {
            trajectoryLine = GetComponent<LineRenderer>();

            SetTrajectoryVisible(true);
        }

        rb = gameObject.GetComponent<Rigidbody>();
        targetDir = transform.forward;
        SetTrajectoryVisible(false);
    }

    private void Update()
    {
        if (trajectoryLine.enabled)
        {
            if (playerPowerMeter)
            {
                PredictTrajectory(UpdateBallProjectileProperties(playerPowerMeter.GetPowerValue()));
            }
        }
    }

    private ProjectileProperties UpdateBallProjectileProperties(float force)
    {
        ProjectileProperties ballProperties;

        ballProperties.direction = targetDir + new Vector3(0, verticalAngle, 0);
        ballProperties.initialPosition = transform.position;
        ballProperties.initialSpeed = force * swingForce;
        ballProperties.mass = rb.mass;
        ballProperties.drag = rb.drag;

        return ballProperties;
    }

    public void PredictTrajectory(ProjectileProperties projectile)
    {
        Vector3 velocity =  projectile.direction * (projectile.initialSpeed / projectile.mass);
        Vector3 position = projectile.initialPosition;
        Vector3 nextPosition;
        float overlap;

        UpdateLineRender(maxPoints, (0, position));

        for (int i = 1; i < maxPoints; i++)
        {
            // Estimate velocity and update next predicted position
            velocity = CalculateNewVelocity(velocity, projectile.drag, increment);
            nextPosition = position + velocity * increment;

            // Overlap our rays by small margin to ensure we never miss a surface
            overlap = Vector3.Distance(position, nextPosition) * rayOverlap;

            //When hitting a surface we want to show the surface marker and stop updating our line
            if (Physics.Raycast(position, velocity.normalized, out RaycastHit hit, overlap))
            {
                UpdateLineRender(i, (i - 1, hit.point));
                MoveHitMarker(hit);
                break;
            }

            //If nothing is hit, continue rendering the arc without a visual marker
            hitMarker.gameObject.SetActive(false);
            position = nextPosition;
            UpdateLineRender(maxPoints, (i, position)); //Unneccesary to set count here, but not harmful
        }
    }
    
    
    private void UpdateLineRender(int count, (int point, Vector3 pos) pointPos)
    {
        trajectoryLine.positionCount = count;
        trajectoryLine.SetPosition(pointPos.point, pointPos.pos);
    }

    private Vector3 CalculateNewVelocity(Vector3 velocity, float drag, float increment)
    {
        velocity += Physics.gravity * increment;
        velocity *= Mathf.Clamp01(1f - drag * increment);
        return velocity;
    }

    private void MoveHitMarker(RaycastHit hit)
    {
        hitMarker.gameObject.SetActive(true);

        // Offset marker from surface
        float offset = 0.025f;
        hitMarker.position = hit.point + hit.normal * offset;
        hitMarker.rotation = Quaternion.LookRotation(hit.normal, Vector3.up);
    }

    public void SetPowerMeterRef(PowerMeter power_meter)
    {
        if (power_meter)
        {
            playerPowerMeter = power_meter;
        }
    }

    public void SetDirection(Vector3 new_dir)
    {
        targetDir = new_dir;
    }

    public void SetTrajectoryVisible(bool visible)
    {
        trajectoryLine.enabled = visible;
        hitMarker.gameObject.SetActive(visible);
    }
}

public struct ProjectileProperties
{
    public Vector3 direction;
    public Vector3 initialPosition;
    public float initialSpeed;
    public float mass;
    public float drag;
}