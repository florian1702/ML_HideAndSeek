using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class HideAndSeekAgent : Agent
{
    [SerializeField] private AgentActions agentActions = null;


    [Header("Sensors")]
    [SerializeField] private BufferSensorComponent teamBufferSensor = null;
    [SerializeField] private RayPerceptionSensorComponent3D rayPerceptionSensors = null;

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float moveZ = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float rotation = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        agentActions.ApplyMovement(new Vector2(moveX, moveZ));
        agentActions.ApplyRotation(rotation);

        if (actions.DiscreteActions[0] == 1)
        {
            agentActions.GrabInteractable();
        }
        
        else if (actions.DiscreteActions[0] == 2 && agentActions.IsHolding)
        {
            agentActions.ReleaseInteractable();
        }
        

        if (actions.DiscreteActions[1] == 1)
        {
            agentActions.LockInteractable(true);
        }
        else if (actions.DiscreteActions[1] == 2)
        {
            agentActions.LockInteractable(false);
        }

        if (agentActions.GameManager.DebugDrawIndividualReward)
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
        Vector3 platformCenter = agentActions.GameManager.transform.position;

        // self -- 7 floats
        sensor.AddObservation(transform.position - platformCenter);
        sensor.AddObservation(NormalizeAngle(transform.rotation.eulerAngles.y));
        sensor.AddObservation(agentActions.Rigidbody.linearVelocity);
        sensor.AddObservation(agentActions.IsHider);

        IEnumerable<AgentActions> teamAgents = agentActions.IsHider
                                             ? agentActions.GameManager.GetHiders()
                                             : agentActions.GameManager.GetSeekers();

        foreach (AgentActions teamAgent in teamAgents)
        {
                float[] obs = new float[8];
                Vector3 teamAgentPosition = teamAgent.transform.position - platformCenter;
                obs[0] = teamAgentPosition.x;
                obs[1] = teamAgentPosition.y;
                obs[2] = teamAgentPosition.z;
                obs[3] = NormalizeAngle(teamAgent.transform.rotation.eulerAngles.y);
                obs[4] = teamAgent.Rigidbody.linearVelocity.x;
                obs[5] = teamAgent.Rigidbody.linearVelocity.y;
                obs[6] = teamAgent.Rigidbody.linearVelocity.z;

                teamBufferSensor.AppendObservation(obs);
        }
    }

    public override void OnEpisodeBegin()
    {
        transform.localPosition = new Vector3(Random.Range(-8,8), 0, Random.Range(-8,8));
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
       
        Vector2 movementInput = Vector2.zero;
        float rotationInput = 0f;

        if (Input.GetKey(KeyCode.W)) movementInput += Vector2.up;
        if (Input.GetKey(KeyCode.S)) movementInput += Vector2.down;
        if (Input.GetKey(KeyCode.A)) movementInput += Vector2.left;
        if (Input.GetKey(KeyCode.D)) movementInput += Vector2.right;

        movementInput.Normalize();
        agentActions.ApplyMovement(movementInput * Time.fixedDeltaTime);

        if (Input.GetKey(KeyCode.Q)) rotationInput = 1f;
        else if (Input.GetKey(KeyCode.E)) rotationInput = -1f;

        agentActions.ApplyRotation(rotationInput);
        
        // Grabbing, releasing, and locking remains unchanged
        if (Input.GetKey(KeyCode.C)) agentActions.GrabInteractable();
        if (!Input.GetKey(KeyCode.C) && agentActions.IsHolding) agentActions.ReleaseInteractable();
        if (Input.GetKey(KeyCode.Alpha2)) agentActions.LockInteractable(true);
        else if (Input.GetKey(KeyCode.Alpha3)) agentActions.LockInteractable(false);
    }

    // Input: angle in degrees
    // Output: angle in radians in range [-pi; pi]
    private float NormalizeAngle(float angle)
    {
        angle = (angle + 180f) % 360f - 180f;
        angle *= Mathf.Deg2Rad;
        return angle;
    }

}
