using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;
/*
public class AgentActions : MonoBehaviour
{

    [SerializeField] private new Rigidbody rigidbody = null;
    [SerializeField] private float runSpeed = 1f;
    [SerializeField] private float drag = 0.3f;
    [SerializeField] private float rotationSpeed = 360f;
    [SerializeField] private float grabDistance = 2f;
    [SerializeField] private float holdBreakDistance = 4f;
    [SerializeField] private bool isHiding = true;

    private Vector2 movementInput = Vector2.zero;
    private float rotationInput = 0f;

    private Interactable grabbedInteractable;
    private Quaternion targetRelativeRotation;

    private BehaviorParameters behaviorParameters = null;

    public Rigidbody Rigidbody { get { return rigidbody; } }
    //public HideAndSeekAgent HideAndSeekAgent { get { return hideAndSeekAgent; } }
    
    public bool IsHiding { get { return isHiding; } }

    public bool IsHolding { get { return grabbedInteractable != null; } }
    public bool WasCaptured { get; set; } = false;

    public GameManager GameManager { get; set; }


    private void Awake()
    {
        behaviorParameters = GetComponent<BehaviorParameters>();
    }

    void FixedUpdate()
    {
        if (!WasCaptured && (isHiding || GameManager.GracePeriodEnded))
        {
            Movement();
        }

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

    public void ReleaseBox()
    {
        if (grabbedInteractable != null)
        {
            grabbedInteractable.Release();
            grabbedInteractable = null;
        }
    }

    public void LockBox(bool tryLock)
    {
        if (Physics.Raycast(new Ray(transform.position, transform.forward), out RaycastHit hit))
        {
            if (hit.distance < grabDistance && IsMovable(hit.collider))
            {
                Interactable boxHolding = hit.collider.gameObject.GetComponent<Interactable>();
                boxHolding.TryLockUnlock(this, tryLock);
            }
        }
    }


    public void ResetAgent()
    {
        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
        grabbedInteractable = null;
        WasCaptured = false;
        gameObject.SetActive(true);
    }

    private bool IsInteractable(Collider collider)
    {
        return collider.CompareTag("Interactable") || collider.CompareTag("Interactable Hider Lock") || collider.CompareTag("Interactable Seeker Lock");
    }


    private void OnDrawGizmos()
    {
        if (grabbedInteractable != null && GameManager.DebugDrawBoxHold)
        {
            Gizmos.DrawLine(transform.position, grabbedInteractable.transform.position);
        }
    }

}
*/