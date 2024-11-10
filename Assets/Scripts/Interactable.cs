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

    private Agent owner = null;
    private Agent lockOwner = null;

    private Vector3 startPosition;
    private Quaternion startRotation;


    public Agent Owner { get { return owner; } }
    public Agent LockOwner { get { return lockOwner; } }
    public Rigidbody Rigidbody { get { return rigidbody; } }

    void Start()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        
    }

    public void TryLockUnlock(Agent agent, bool tryLock)
    {
        if (tryLock)
        {
            if (lockOwner == null && (owner == null || agent.GetType() == lockOwner.GetType()))
            {
                rigidbody.isKinematic = true;
                //owner?.ReleaseBox();
                owner = null;
                lockOwner = agent;
                meshRenderer.material = agent.GetType() == typeof(HiderAgent) ? materialLockHider : materialLockSeeker;
                tag = agent.GetType() == typeof(HiderAgent) ? tagLockHider : tagLockSeeker;
            }
        }
        else
        {
            if (lockOwner != null && agent.GetType() == lockOwner.GetType())
            {
                rigidbody.isKinematic = false;
                lockOwner = null;
                meshRenderer.material = materialDefault;
                tag = tagDefault;
            }
        }
    }

    public bool TryGrab(Agent agent)
    {
        if(lockOwner != null || owner != null)
        {
            return false;
        }
        owner = agent;
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
