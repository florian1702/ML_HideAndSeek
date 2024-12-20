using UnityEngine;
public class AgentActions : MonoBehaviour
{

    [SerializeField] private bool isHider = true;
    [SerializeField] private HideAndSeekAgent hideAndSeekAgent = null;

    [Header("Movement")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] float airMultiplier = 0.4f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] float groundDrag = 6f;
    [SerializeField] float airDrag = 2f;

    [Header("Ground Detection")]
    [SerializeField] Transform groundCheck;
    [SerializeField] LayerMask groundMask;
    [SerializeField] float groundDistance = 0.2f;
    [SerializeField] float playerHeight = 2f;
    public bool isGrounded { get; private set; }

    [Header("Interacting")]
    [SerializeField] private float grabDistance = 2.5f;
    [SerializeField] private float holdBreakDistance = 4f;
    
    private Vector2 movementInput = Vector2.zero;

    Vector3 moveDirection;
    Vector3 slopeMoveDirection;
    private float rotationInput = 0f;
    private Interactable grabbedInteractable;
    private Quaternion targetRelativeRotation;
    public Rigidbody Rigidbody { get { return rb; } }
    public HideAndSeekAgent HideAndSeekAgent { get { return hideAndSeekAgent; } }
    public bool IsHider { get { return isHider; } }
    public bool IsHolding { get { return grabbedInteractable != null; } }
    public bool IsMoving { get { return rb.linearVelocity.magnitude > 0.1f;  } }
    public GameManager GameManager { get; set; }

    private RaycastHit slopeHit;
   
    void FixedUpdate()
    {
        if (isHider || GameManager.PreparationPhaseEnded)
        {
            // ground check
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            ControlDrag();
            Movement();
            Rotation();
            AdjustGrabbedObject();

            slopeMoveDirection = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal);
        }

        movementInput = Vector2.zero;
        rotationInput = 0f;
    }

    private void Movement()
    {
        // Apply movement
        Vector2 direction = movementInput;
        moveDirection = direction.x * transform.right + direction.y * transform.forward;


        if (isGrounded)
        {
            if (OnSlope())
            {
                // Adjust for slope movement
                slopeMoveDirection = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal).normalized;
                rb.AddForce(slopeMoveDirection * moveSpeed, ForceMode.Impulse);
            }
            else
            {
                // Normal ground movement
                rb.AddForce(moveDirection.normalized * moveSpeed, ForceMode.Impulse);
            }
        }
        else
        {
            // Airborne movement with stronger gravity pull
            rb.AddForce(moveDirection.normalized * moveSpeed * airMultiplier, ForceMode.Force);

            // Stronger gravity to pull agent to the ground
            rb.AddForce(Vector3.down * (Physics.gravity.y * -1.5f), ForceMode.Acceleration);
        }

       

    }
    void ControlDrag()
    {
        if (isGrounded)
        {
            rb.linearDamping = groundDrag;
        }
        else
        {
            rb.linearDamping = airDrag;
        }
    }

    private void Rotation()
    {
            float delta = rotationInput * rotationSpeed * Time.fixedDeltaTime;
            rb.MoveRotation(rb.rotation * Quaternion.AngleAxis(delta, Vector3.up));
    }

    private void AdjustGrabbedObject()
    {
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

    private bool OnSlope()
{
    if (Physics.Raycast(transform.position + (transform.forward * 0.2f), Vector3.down, out slopeHit, playerHeight / 2 + 0.5f))
    {
        return Vector3.Angle(Vector3.up, slopeHit.normal) > 0.1f; // Schräge erkannt
    }
    return false;
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
            // Nur Grab erlauben, wenn die Phase vorbei ist oder der Agent ein Hider ist
            if (GameManager.PreparationPhaseEnded || isHider)
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
        // Nur Lock/Unlock erlauben, wenn die Phase vorbei ist oder der Agent ein Hider ist
        if (GameManager.PreparationPhaseEnded || isHider)
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
    }

    public void ResetAgent()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
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