using System;
using System.Buffers;
using JetBrains.Annotations;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.UIElements;

public class Interactable : MonoBehaviour
{
    // Serialized fields for setting in the Unity editor
    [SerializeField] private  Rigidbody rb = null;
    [SerializeField] private  bool lockable = true;
    [SerializeField] private Material materialDefault = null;
    [SerializeField] private Material materialLockHider = null;
    [SerializeField] private Material materialLockSeeker = null;
    [SerializeField] private string tagDefault = "";
    [SerializeField] private string tagLockHider = "";
    [SerializeField] private string tagLockSeeker = "";

    // Private fields for internal use
    private MeshRenderer meshRenderer = null;
    private AgentActions owner = null;
    private AgentActions lockOwner = null;
    private Vector3 startPosition;
    private Quaternion startRotation;

    // Public properties for accessing private fields
    public AgentActions Owner { get { return owner; } }
    public AgentActions LockOwner { get { return lockOwner; } }
    public Rigidbody Rigidbody { get { return rb; } }

    void Start()
    {
        // Initialize meshRenderer and store the initial position and rotation
        meshRenderer = GetComponent<MeshRenderer>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        
    }

    // Method to lock or unlock the interactable object
    public void TryLockUnlock(AgentActions agent, bool tryLock)
    {
        if(lockable){
            if (tryLock)
            {
                // Lock the object if it is not already locked and the agent is allowed to lock it
                if (lockOwner == null && (owner == null || owner.IsHider == agent.IsHider))
                {
                    rb.isKinematic = true;
                    owner?.ReleaseInteractable();
                    owner = null;
                    lockOwner = agent;
                    meshRenderer.material = agent.IsHider ? materialLockHider : materialLockSeeker;
                    tag = agent.IsHider ? tagLockHider : tagLockSeeker;
                }
            }
            else
            {
                // Unlock the object if it is locked by the same type of agent
                if (lockOwner != null && lockOwner.IsHider == agent.IsHider)
                {
                    rb.isKinematic = false;
                    lockOwner = null;
                    meshRenderer.material = materialDefault;
                    tag = tagDefault;
                }
            }
        }
    }

    // Method to grab the interactable object
    public bool TryGrab(AgentActions agent)
    {
        if(lockOwner != null || owner != null)
        {
            return false;
        }
        owner = agent;
        rb.isKinematic = false;
        return true;
    }

    // Method to release the interactable object
    public void Release()
    {
        rb.isKinematic = true;
        transform.rotation = Quaternion.Euler(0, transform.rotation.eulerAngles.y, 0);
        transform.position = new Vector3(transform.position.x, startPosition.y, transform.position.z);
        owner = null;
    }
    
    // Method to reset the interactable object to its initial state
    public void Reset()
    {
        owner = null;
        lockOwner = null;
        tag = tagDefault;
        rb.isKinematic = true;
        meshRenderer.material = materialDefault;
        transform.position = startPosition;
        transform.rotation = startRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}
