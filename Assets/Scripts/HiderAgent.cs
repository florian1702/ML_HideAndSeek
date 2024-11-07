using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class HiderAgent : Agent
{
    [SerializeField] private BufferSensorComponent teamBufferSensor = null;
    [SerializeField] private RayPerceptionSensorComponent3D rayPerceptionSensors = null;
    [SerializeField] private new Rigidbody rigidbody = null;
    [SerializeField] private float runSpeed = 1f;
    [SerializeField] private float drag = 0.3f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float grabDistance = 2f;
    [SerializeField] private float holdBreakDistance = 4f;

    private Vector2 movementInput = Vector2.zero;
    private float rotationInput = 0f;
    private Interactable grabbedInteractable;
    private Quaternion targetRelativeRotation;
    public Rigidbody Rigidbody { get { return rigidbody; } }    
    public bool IsHolding { get { return grabbedInteractable != null; } }
    public GameManager GameManager { get; set; }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float rotation = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        ApplyMovement(new Vector2(moveX, moveZ));
        ApplyRotation(rotation);

        if (actions.DiscreteActions[0] == 1)
        {
            GrabInteractable();
        }
        else if (IsHolding)
        {
            ReleaseInteractable();
        }

        if (actions.DiscreteActions[1] == 1)
        {
            LockInteractable(true);
        }
        else if (actions.DiscreteActions[1] == 2)
        {
            LockInteractable(false);
        }

        if (GameManager.DebugDrawIndividualReward)
        {
            float reward = GetCumulativeReward();
            Color rewardColor = Color.blue;
            if (reward > 0f) rewardColor = Color.green;
            if (reward < 0f) rewardColor = Color.red;
            Debug.DrawRay(transform.position, 20f * Mathf.Abs(reward) * Vector3.up, rewardColor);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 platformCenter = GameManager.transform.position;

        // self -- 7 floats
        sensor.AddObservation(transform.position - platformCenter);
        sensor.AddObservation(NormalizeAngle(transform.rotation.eulerAngles.y));
        sensor.AddObservation(Rigidbody.linearVelocity);

        IEnumerable<HiderAgent> teamAgents = GameManager.GetHiders();

        foreach (HiderAgent teamAgent in teamAgents)
        {
                float[] obs = new float[8];
                Vector3 teamAgentPosition = teamAgent.transform.position - platformCenter;
                obs[0] = teamAgentPosition.x;
                obs[1] = teamAgentPosition.y;
                obs[2] = teamAgentPosition.z;
                obs[3] = NormalizeAngle(teamAgent.transform.rotation.eulerAngles.y);
                obs[4] = teamAgent.rigidbody.linearVelocity.x;
                obs[5] = teamAgent.rigidbody.linearVelocity.y;
                obs[6] = teamAgent.rigidbody.linearVelocity.z;

                teamBufferSensor.AppendObservation(obs);
        }
    }

    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(Random.Range(-8,8), 0, Random.Range(-8,8));
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
       
        movementInput = Vector2.zero;
        //rotationInput = Vector2.zero;

        if(Input.GetKey(KeyCode.W)) movementInput += Vector2.up;
        if(Input.GetKey(KeyCode.S)) movementInput += Vector2.down;
        if(Input.GetKey(KeyCode.A)) movementInput += Vector2.left;
        if(Input.GetKey(KeyCode.D)) movementInput += Vector2.right;
        movementInput.Normalize();
        ApplyMovement(movementInput * Time.deltaTime);

        /*if(Input.GetKey(KeyCode.Q))
            continiousActionsOut[2] = -1.0f;
        else if(Input.GetKey(KeyCode.E))
            continiousActionsOut[2] = 1.0f;
        else
            continiousActionsOut[2] = 0.0f;
*/
        if(Input.GetKey(KeyCode.Alpha1))
            GrabInteractable();
        if(!Input.GetKey(KeyCode.Alpha1) && IsHolding)
            ReleaseInteractable();
        else if(Input.GetKey(KeyCode.Alpha2))
            LockInteractable(true);
        else if(Input.GetKey(KeyCode.Alpha3))
            LockInteractable(false);
    }

    // Input: angle in degrees
    // Output: angle in radians in range [-pi; pi]
    private float NormalizeAngle(float angle)
    {
        angle = (angle + 180f) % 360f - 180f;
        angle *= Mathf.Deg2Rad;
        return angle;
    }

    void FixedUpdate()
    {
        Movement();
        movementInput = Vector2.zero;
        rotationInput = 0f;
    }


    private void Movement()
    {
        // Apply movement
        Vector2 direction = movementInput.normalized;
        Vector3 force = direction.x * transform.right + direction.y * transform.forward;
        rigidbody.AddForce(force * runSpeed, ForceMode.Impulse);

        // Additional movement drag
        Vector3 currentVel = new Vector3(rigidbody.linearVelocity.x, Mathf.Max(0f, rigidbody.linearVelocity.y), rigidbody.linearVelocity.z);
        Vector3 dragForce = -currentVel * drag;
        rigidbody.AddForce(dragForce, ForceMode.Impulse);

        float delta = rotationInput * rotationSpeed * Time.fixedDeltaTime;
        rigidbody.MoveRotation(rigidbody.rotation * Quaternion.AngleAxis(delta, Vector3.up));

        // Adjust grabbed object position / rotation
        if (grabbedInteractable != null)
        {
            // Adjust position
            Vector3 targetPosition = transform.position + transform.forward * grabDistance;
            Vector3 towards = targetPosition - grabbedInteractable.Rigidbody.position;
            grabbedInteractable.Rigidbody.linearVelocity = towards * 10f;

            // Adjust rotation
            Quaternion targetRotation = transform.rotation * targetRelativeRotation;
            Vector3 angularTowards = ShortestPathFromTo(grabbedInteractable.transform.rotation, targetRotation);
            grabbedInteractable.Rigidbody.angularVelocity = angularTowards * 0.1f;

            // Break in case the object is too far from holder
            if (Vector3.Distance(grabbedInteractable.Rigidbody.position, transform.position) > holdBreakDistance)
            {
                grabbedInteractable.Release();
                grabbedInteractable = null;
            }
        }
    }


    // Shortest path rotation from one quaternion to another, returned as euler angles
    private Vector3 ShortestPathFromTo(Quaternion from, Quaternion to)
    {
        Quaternion q = Quaternion.Inverse(from) * to;
        Vector3 v = q.eulerAngles;
        float x = v.x > 180f ? v.x - 360f : v.x;
        float y = v.y > 180f ? v.y - 360f : v.y;
        float z = v.z > 180f ? v.z - 360f : v.z;
        return new Vector3(x, y, z);
    }

    public void ApplyMovement(Vector2 input)
    {
        movementInput += input;
    }

    public void ApplyRotation(float delta)
    {
        rotationInput += delta;
    }


    public void GrabInteractable()
    {
        Debug.Log("grab");
        if (grabbedInteractable == null)
        {
            if (Physics.Raycast(new Ray(transform.position, transform.forward), out RaycastHit hit))
            {
                if (hit.distance < grabDistance && IsInteractable(hit.collider))
                {
                    Interactable interactable = hit.collider.gameObject.GetComponent<Interactable>();
                    if (interactable.TryGrab(this))
                    {
                        grabbedInteractable = interactable;
                        // Keep rotation of the object relative to the agent
                        targetRelativeRotation = Quaternion.Inverse(transform.rotation) * grabbedInteractable.transform.rotation;
                    }
                }
            }
        }
    }

    public void ReleaseInteractable()
    {
        Debug.Log("release");
        if (grabbedInteractable != null)
        {
            grabbedInteractable.Release();
            grabbedInteractable = null;
        }
    }

    public void LockInteractable(bool tryLock)
    {
        if(tryLock)Debug.Log("Lock");
        else Debug.Log("Unlock");
        if (Physics.Raycast(new Ray(transform.position, transform.forward), out RaycastHit hit))
        {
            if (hit.distance < grabDistance && IsInteractable(hit.collider))
            {
                Debug.Log("hit interactable");
                Interactable interactable = hit.collider.gameObject.GetComponent<Interactable>();
                interactable.TryLockUnlock(this, tryLock);
            }
        }
    }


    public void ResetAgent()
    {
        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        grabbedInteractable = null;
        gameObject.SetActive(true);
    }

    private bool IsInteractable(Collider collider)
    {
        return collider.CompareTag("Box") || collider.CompareTag("Box Hider Lock") || collider.CompareTag("Box Seeker Lock") ||
                collider.CompareTag("Ramp") || collider.CompareTag("Ramp Hider Lock") || collider.CompareTag("Ramp Seeker Lock");
    }


    private void OnDrawGizmos()
    {
        if (grabbedInteractable != null && GameManager.DebugDrawBoxHold)
        {
            Gizmos.DrawLine(transform.position, grabbedInteractable.transform.position);
        }
    }
}
