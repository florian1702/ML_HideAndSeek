using System;
using System.Buffers;
using Unity.MLAgents;
using UnityEngine;

public class Interactable : MonoBehaviour
{

    [SerializeField] private new Rigidbody rigidbody = null;

    [SerializeField] private Material materialDefault = null;
    [SerializeField] private Material materialLockHider = null;
    [SerializeField] private Material materialLockSeeker = null;

    [SerializeField] private string tagDefault = "";
    [SerializeField] private string tagLockHider = "";
    [SerializeField] private string tagLockSeeker = "";

    private MeshRenderer meshRenderer = null;

    private AgentActions owner = null;
    private AgentActions lockOwner = null;

    private Vector3 startPosition;
    private Quaternion startRotation;


    public AgentActions Owner { get { return owner; } }
    public AgentActions LockOwner { get { return lockOwner; } }
    public Rigidbody Rigidbody { get { return rigidbody; } }

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        
    }

    public void TryLockUnlock(AgentActions agent, bool tryLock)
    {
        if (tryLock)
        {
            if (lockOwner == null && (owner == null || owner.IsHider == agent.IsHider))
            {
                rigidbody.isKinematic = true;
                owner?.ReleaseInteractable();
                owner = null;
                lockOwner = agent;
                meshRenderer.material = agent.IsHider ? materialLockHider : materialLockSeeker;
                tag = agent.IsHider ? tagLockHider : tagLockSeeker;
            }
        }
        else
        {
            if (lockOwner != null && lockOwner.IsHider == agent.IsHider)
            {
                rigidbody.isKinematic = false;
                lockOwner = null;
                meshRenderer.material = materialDefault;
                tag = tagDefault;
            }
        }
    }

    public bool TryGrab(AgentActions agent)
    {
        if(lockOwner != null || owner != null)
        {
            return false;
        }
        owner = agent;
        Debug.Log(true);
        return true;
    }

    public void Release()
    {
        owner = null;
    }

    public void Reset()
    {
        owner = null;
        lockOwner = null;
        tag = tagDefault;
        rigidbody.isKinematic = false;
        meshRenderer.material = materialDefault;
        transform.position = startPosition;
        transform.rotation = startRotation;
        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;
    }
}
