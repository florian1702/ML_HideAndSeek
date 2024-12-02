using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
public class AgentActions : MonoBehaviour
{

    [SerializeField] private bool isHider = true;
    [SerializeField] private HideAndSeekAgent hideAndSeekAgent = null;
    [SerializeField] private Animator animator = null;

    [Header("Movement")]
    [SerializeField] private Rigidbody rigidbody;
    [SerializeField] private float moveSpeed = 1f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float drag = 0.3f;


    [Header("Interacting")]
    [SerializeField] private float grabDistance = 2.5f;
    [SerializeField] private float holdBreakDistance = 4f;
    [SerializeField] private float grabOffset = 2.5f;  
    [SerializeField] private float positionVelocityFactor = 20f;      // Stärke der Kraft, die das Objekt in Position hält
    [SerializeField] private float rotationVelocityFactor = 7f;       // Stärke des Drehmoments, das das Objekt in Rotation hält
    [SerializeField] private float positionTolerance = 0.2f;    // Toleranz für Position
    [SerializeField] private float rotationTolerance = 0.5f;      // Toleranz für Rotation in Grad
    
    private BehaviorParameters behaviorParameters = null;
    public Rigidbody Rigidbody { get { return rigidbody; } }

    private Vector2 movementInput = Vector2.zero;
    private float rotationInput = 0f;
    private float currentRotationVelocity;
    private Interactable grabbedInteractable;
    private Quaternion targetRelativeRotation;
    public HideAndSeekAgent HideAndSeekAgent { get { return hideAndSeekAgent; } }
    public bool IsHider { get { return isHider; } }
    public bool IsHolding { get { return grabbedInteractable != null; } }
    public bool IsMoving { get { return rigidbody.linearVelocity.magnitude > 0.1f;  } }
    public GameManager GameManager { get; set; }

    private void Awake()
    {
        behaviorParameters = GetComponent<BehaviorParameters>();
    }

    void FixedUpdate()
    {
        if (isHider || GameManager.PreparationPhaseEnded)
        {
            Movement();
            Rotation();
            AdjustGrabbedObject();
        }

        UpdateAnimations();

        movementInput = Vector2.zero;
        rotationInput = 0f;

        // Apply damping when no input is provided
        if (movementInput.magnitude == 0)
        {
            Vector3 velocity = rigidbody.linearVelocity;
            velocity.x = Mathf.Lerp(velocity.x, 0f, Time.fixedDeltaTime * 5f); // Adjust the damping factor
            velocity.z = Mathf.Lerp(velocity.z, 0f, Time.fixedDeltaTime * 5f);
            rigidbody.linearVelocity = velocity;
        }

    }

    private void Movement()
    {

        // Apply movement
        Vector2 direction = movementInput.normalized;
        Vector3 force = direction.x * transform.right + direction.y * transform.forward;
        rigidbody.AddForce(force * moveSpeed, ForceMode.Impulse);

        // Additional movement drag
        Vector3 currentVel = new Vector3(rigidbody.linearVelocity.x, Mathf.Max(0f, rigidbody.linearVelocity.y), rigidbody.linearVelocity.z);
        Vector3 dragForce = -currentVel * drag;
        rigidbody.AddForce(dragForce, ForceMode.Impulse);
        
    }

    private void Rotation()
    {
        // Rotate only when rotationInput is non-zero
        if (rotationInput != 0f)
        {
            float delta = rotationInput * rotationSpeed * Time.fixedDeltaTime;
            rigidbody.MoveRotation(rigidbody.rotation * Quaternion.AngleAxis(delta, Vector3.up));
        }

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


    private void UpdateAnimations(){
        if(animator != null){
            animator.SetBool("IsMoving", IsMoving);
            animator.SetBool("IsHolding", IsHolding);
        }
    }

    private void OnDrawGizmos()
    {
        if (grabbedInteractable != null && GameManager.DebugDrawBoxHold)
        {
            Gizmos.DrawLine(transform.position, grabbedInteractable.transform.position);
        }
    }
}