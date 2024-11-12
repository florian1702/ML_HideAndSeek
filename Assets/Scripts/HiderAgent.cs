using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class HiderAgent : Agent
{
    [Header("Sensors")]
    [SerializeField] private BufferSensorComponent teamBufferSensor = null;
    [SerializeField] private RayPerceptionSensorComponent3D rayPerceptionSensors = null;

    [Header("Movement")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float rotationSpeed = 500f;

    [Header("Interacting")]
    [SerializeField] private float grabDistance = 2f;
    [SerializeField] private float holdBreakDistance = 4f;
    [SerializeField] private float grabOffset = 2f;  
    [SerializeField] private float positionVelocityFactor = 10f;      // Stärke der Kraft, die das Objekt in Position hält
    [SerializeField] private float rotationVelocityFactor = 2f;       // Stärke des Drehmoments, das das Objekt in Rotation hält
    [SerializeField] private float positionTolerance = 0.1f;    // Toleranz für Position
    [SerializeField] private float rotationTolerance = 1f;      // Toleranz für Rotation in Grad

      

    private Vector2 movementInput = Vector2.zero;
    private float rotationInput = 0f;
    private float currentRotationVelocity;
    private Interactable grabbedInteractable;
    private Quaternion targetRelativeRotation;
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
        
        else if (actions.DiscreteActions[0] == 2 && IsHolding)
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
        sensor.AddObservation(characterController.velocity);

        IEnumerable<HiderAgent> teamAgents = GameManager.GetHiders();

        foreach (HiderAgent teamAgent in teamAgents)
        {
                float[] obs = new float[8];
                Vector3 teamAgentPosition = teamAgent.transform.position - platformCenter;
                obs[0] = teamAgentPosition.x;
                obs[1] = teamAgentPosition.y;
                obs[2] = teamAgentPosition.z;
                obs[3] = NormalizeAngle(teamAgent.transform.rotation.eulerAngles.y);
                obs[4] = teamAgent.characterController.velocity.x;
                obs[5] = teamAgent.characterController.velocity.y;
                obs[6] = teamAgent.characterController.velocity.z;

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
        rotationInput = 0;

        if(Input.GetKey(KeyCode.W)) movementInput += Vector2.up;
        if(Input.GetKey(KeyCode.S)) movementInput += Vector2.down;
        if(Input.GetKey(KeyCode.A)) movementInput += Vector2.left;
        if(Input.GetKey(KeyCode.D)) movementInput += Vector2.right;
        movementInput.Normalize();
        ApplyMovement(movementInput * Time.deltaTime);

        if(Input.GetKey(KeyCode.Q)) rotationInput = -1.0f;
        else if(Input.GetKey(KeyCode.E)) rotationInput = 1.0f;
        else rotationInput = 0.0f;
        ApplyRotation(rotationInput * Time.deltaTime);

        if(Input.GetKey(KeyCode.C)){
            GrabInteractable();
        }
        if(!Input.GetKey(KeyCode.C) && IsHolding) ReleaseInteractable();
        
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

    private void FixedUpdate()
    {
        Movement();
        Rotation();
        AdjustGrabbedObject();
    }



    private void Movement()
    {
        movementInput = Vector2.Lerp(movementInput, Vector2.zero, Time.fixedDeltaTime * 5f);
        Vector3 direction = new Vector3(movementInput.x, 0, movementInput.y).normalized;
        Vector3 moveDirection = transform.right * direction.x + transform.forward * direction.z;

        characterController.SimpleMove(moveDirection * moveSpeed);
    }

    private void Rotation()
    {
        // Calculate the target rotation angle in degrees
        float targetAngle = transform.eulerAngles.y + rotationInput * rotationSpeed * Time.fixedDeltaTime;

        // Smoothly adjust the angle using SmoothDampAngle
        float smoothedAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref currentRotationVelocity, 0.1f);

        // Apply the smoothed rotation
        transform.rotation = Quaternion.Euler(0f, smoothedAngle, 0f);
    
    }

    private void AdjustGrabbedObject()
    {
        if (grabbedInteractable != null)
        {
            // Zielposition und -rotation berechnen
            Vector3 targetPosition = transform.position + transform.forward * grabOffset;
            targetPosition.y = grabbedInteractable.Rigidbody.position.y; // Behalte Y-Position bei

            Quaternion targetRotation = transform.rotation * targetRelativeRotation;

            // Abstand zur Zielposition und Differenz zur Zielrotation berechnen
            Vector3 positionDelta = targetPosition - grabbedInteractable.Rigidbody.position;
            Vector3 angularDelta = ShortestPathFromTo(grabbedInteractable.Rigidbody.rotation, targetRotation);

            // Setze velocity nur, wenn das Objekt außerhalb der Positionstoleranz ist
            if (positionDelta.magnitude > positionTolerance)
            {
                grabbedInteractable.Rigidbody.linearVelocity = positionDelta.normalized * positionVelocityFactor;
            }
            else
            {
                grabbedInteractable.Rigidbody.linearVelocity = Vector3.zero; // Stoppe das Objekt, wenn es in Position ist
            }

            // Setze angularVelocity nur, wenn die Rotation außerhalb der Toleranz ist
            if (angularDelta.magnitude > rotationTolerance)
            {
                grabbedInteractable.Rigidbody.angularVelocity = angularDelta.normalized * rotationVelocityFactor;
            }
            else
            {
                grabbedInteractable.Rigidbody.angularVelocity = Vector3.zero; // Stoppe die Rotation, wenn sie im Ziel ist
            }

            // Griff lösen, falls das Objekt zu weit entfernt ist
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
        if (grabbedInteractable == null)
        {
            if (Physics.Raycast(new Ray(transform.position, transform.forward), out RaycastHit hit))
            {
                if (hit.distance < grabDistance && IsInteractable(hit.collider))
                {
                    Interactable interactable = hit.collider.gameObject.GetComponent<Interactable>();
                    Debug.Log(interactable);
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
        if (grabbedInteractable != null)
        {
            grabbedInteractable.Release();
            grabbedInteractable = null;
        }
    }

    public void LockInteractable(bool tryLock)
    {
        if (Physics.Raycast(new Ray(transform.position, transform.forward), out RaycastHit hit))
        {
            if (hit.distance < grabDistance && IsInteractable(hit.collider))
            {
                Interactable interactable = hit.collider.gameObject.GetComponent<Interactable>();
                interactable.TryLockUnlock(this, tryLock);
            }
        }
    }


    public void ResetAgent()
    {
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
