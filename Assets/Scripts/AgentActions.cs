using UnityEngine;
public class AgentActions : MonoBehaviour
{
    [SerializeField] private bool isHider = true;
    [SerializeField] private HideAndSeekAgent hideAndSeekAgent = null;

    [Header("Movement")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float airMultiplier = 0.2f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float groundDrag = 10f;
    [SerializeField] private float airDrag = 1f;

    [Header("Ground Detection")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundDistance = 0.1f;
    [SerializeField] private float playerHeight = 2f;

    [Header("Interacting")]
    [SerializeField] private float grabDistance = 2.5f;
    [SerializeField] private float breakDistace = 4f;
    
    private Vector2 movementInput = Vector2.zero;
    private Vector3 moveDirection;
    private Vector3 slopeMoveDirection;
    private float rotationInput = 0f;
    private Interactable grabbedInteractable;
    private Quaternion targetRelativeRotation;
    private RaycastHit slopeHit;

    public Rigidbody Rigidbody { get { return rb; } }
    public HideAndSeekAgent HideAndSeekAgent { get { return hideAndSeekAgent; } }
    public bool IsHider { get { return isHider; } }
    public bool isGrounded { get; private set; }
    public bool IsHolding { get { return grabbedInteractable != null; } }
    public bool IsMoving { get { return rb.linearVelocity.magnitude > 0.1f;  } }
    public GameManager GameManager { get; set; }

   
    void FixedUpdate()
    {
        if (isHider || GameManager.PreparationPhaseEnded)
        {
            // Check if the agent is grounded
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            ControlDrag();
            Movement();
            Rotation();
            AdjustGrabbedObject();

            // Adjust movement direction based on slope
            slopeMoveDirection = Vector3.ProjectOnPlane(moveDirection, slopeHit.normal);
        }

        // Reset movement and rotation input
        movementInput = Vector2.zero;
        rotationInput = 0f;
    }

    private void Movement()
    {
        // Calculate movement direction
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
            // Airborne: Reduce horizontal velocity faster
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.linearVelocity -= horizontalVelocity * Time.fixedDeltaTime * 5f; // Increased damping rate for faster reduction

            // Apply reduced airborne movement force
            rb.AddForce(moveDirection.normalized * moveSpeed * airMultiplier, ForceMode.Force);

            // Stronger downward force to push to ground
            rb.AddForce(Vector3.down * (Physics.gravity.y * -6f), ForceMode.Acceleration); // Increase downward force
        }
    }

    void ControlDrag()
    {
        // Adjust drag based on whether the agent is grounded
        if (isGrounded)
        {
            // Additional movement drag
            Vector3 currentVel = new Vector3(rb.linearVelocity.x, Mathf.Max(0f, rb.linearVelocity.y), rb.linearVelocity.z);
            Vector3 dragForce = -currentVel * groundDrag;
            rb.AddForce(dragForce, ForceMode.Impulse);
        }
        else
        {
            rb.linearDamping = airDrag;
        }
    }

    private void Rotation()
    {
        // Apply rotation based on input
        float deltaRotation = rotationInput * rotationSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.AngleAxis(deltaRotation, Vector3.up));
    }

    private void AdjustGrabbedObject()
    {
        // Adjust grabbed object position and rotation
        if (grabbedInteractable != null)
        {
            Vector3 targetPosition = transform.position + transform.forward * grabDistance;
            Vector3 towards = targetPosition - grabbedInteractable.Rigidbody.position;
            grabbedInteractable.Rigidbody.linearVelocity = towards * 10f;

             // Adjust rotation using Slerp
            Quaternion targetRotation = transform.rotation * targetRelativeRotation;
            grabbedInteractable.transform.rotation = Quaternion.Slerp(
                grabbedInteractable.transform.rotation, 
                targetRotation, 
                Time.fixedDeltaTime * rotationSpeed
            );

            // Release object if it is too far from the agent
            if (Vector3.Distance(grabbedInteractable.Rigidbody.position, transform.position) > breakDistace)
            {
                grabbedInteractable.Release();
                grabbedInteractable = null;
            }
        }
    }

    // Checks if the agent is on a slope
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
        // Attempt to grab an interactable object
        if (grabbedInteractable == null)
        {
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
                            targetRelativeRotation = Quaternion.Inverse(transform.rotation) * grabbedInteractable.transform.rotation;
                        }
                    }
                }
            }
        }
    }

    public void ReleaseInteractable()
    {
        if (IsHolding)
        {
            grabbedInteractable.Release();
            grabbedInteractable = null;
        }
    }

    public void LockInteractable(bool tryLock)
    {
        // Attempt to lock or unlock an interactable object
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
        // Reset the agent's state
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        grabbedInteractable = null;
        gameObject.SetActive(true);
    }

    private bool IsInteractable(Collider collider)
    {
        // Check if the collider has an interactable tag
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