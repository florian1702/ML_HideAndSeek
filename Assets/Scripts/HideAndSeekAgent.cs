using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class HideAndSeekAgent : Agent
{
    [Header("Actions")]
    [SerializeField] private AgentActions agentActions = null;

    [Header("Sensors")]
    [SerializeField] private BufferSensorComponent teamBufferSensor = null;

    private const float POSITION_MIN = -12.5f;
    private const float POSITION_MAX = 12.5f;

    private const float VELOCITY_MIN =  -3f; // Example bounds for velocity
    private const float VELOCITY_MAX = 3f;

    private const float PREP_PHASE_MIN = 0f;
    private const float PREP_PHASE_MAX = 500f * 0.4f; // Example max prep phase time

    // Called at the beginning of each episode
    public override void OnEpisodeBegin()
    {
        // Randomly position the agent within a specified range
        // I need to do this becouse i dont know why but in the first episode all agents are spawned in the same position
       transform.localPosition = new Vector3(Random.Range(POSITION_MIN + 1,POSITION_MAX - 1), 0, Random.Range(POSITION_MIN + 1,POSITION_MAX - 1));
        
    }

    // Collect observations for the agent
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 platformCenter = agentActions.GameManager.transform.position;

        // Self observation: position, rotation, velocity, and time left in preperation Phase
        Vector3 normalizedPosition = NormalizeVector(transform.position - platformCenter, POSITION_MIN, POSITION_MAX);
        sensor.AddObservation(normalizedPosition);

        float normalizedAngle = NormalizeAngle(transform.rotation.eulerAngles.y);
        sensor.AddObservation(normalizedAngle);

        Vector3 normalizedVelocity = NormalizeVector(agentActions.Rigidbody.linearVelocity, VELOCITY_MIN, VELOCITY_MAX);
        sensor.AddObservation(normalizedVelocity);

        float normalizedTimeLeft = NormalizeValue(agentActions.GameManager.TimeLeftInPreparationPhase, PREP_PHASE_MIN, PREP_PHASE_MAX);
        sensor.AddObservation(normalizedTimeLeft);

        //sensor.AddObservation(transform.position - platformCenter);
        //sensor.AddObservation(NormalizeAngle(transform.rotation.eulerAngles.y));
        //sensor.AddObservation(agentActions.Rigidbody.linearVelocity);
        //sensor.AddObservation(agentActions.GameManager.TimeLeftInPreparationPhase);

        // Team observations: position, rotation, and velocity of team members
        IEnumerable<AgentActions> teamAgents = agentActions.IsHider
                                             ? agentActions.GameManager.GetHiders()
                                             : agentActions.GameManager.GetSeekers();

        foreach (AgentActions teamAgent in teamAgents)
        {
            float[] obs = new float[7];
            Vector3 teamAgentPosition = NormalizeVector(teamAgent.transform.position - transform.position, POSITION_MIN, POSITION_MAX);
            obs[0] = teamAgentPosition.x;
            obs[1] = teamAgentPosition.y;
            obs[2] = teamAgentPosition.z;

            obs[3] = NormalizeAngle(teamAgent.transform.rotation.eulerAngles.y);

            Vector3 teamAgentVelocity = NormalizeVector(teamAgent.Rigidbody.linearVelocity, VELOCITY_MIN, VELOCITY_MAX);
            obs[4] = teamAgentVelocity.x;
            obs[5] = teamAgentVelocity.y;
            obs[6] = teamAgentVelocity.z;

            teamBufferSensor.AppendObservation(obs);
        }
  
    }

    // Process actions received from the neural network or heuristic
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

    }

    // Define heuristic actions for manual control
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
        
        if (Input.GetKey(KeyCode.C)) agentActions.GrabInteractable();
        if (!Input.GetKey(KeyCode.C) && agentActions.IsHolding) agentActions.ReleaseInteractable();
        if (Input.GetKey(KeyCode.Alpha2)) agentActions.LockInteractable(true);
        else if (Input.GetKey(KeyCode.Alpha3)) agentActions.LockInteractable(false);
    }

      private float NormalizeValue(float currentValue, float minValue, float maxValue)
    {
        return (currentValue - minValue) / (maxValue - minValue);
    }

    private Vector3 NormalizeVector(Vector3 vector, float minValue, float maxValue)
    {
        return new Vector3(
            NormalizeValue(vector.x, minValue, maxValue),
            NormalizeValue(vector.y, minValue, maxValue),
            NormalizeValue(vector.z, minValue, maxValue)
        );
    }

    // Normalize angle to range [-pi, pi] (degrees to radians)
    private float NormalizeAngle(float angle)
    {
        angle = (angle + 180f) % 360f - 180f;
        angle *= Mathf.Deg2Rad;
        return angle;
    }

    void OnDrawGizmos()
    {
        if (agentActions.GameManager.DebugDrawIndividualReward)
        {
            float reward = GetCumulativeReward();
            Color rewardColor = Color.blue;

            if (reward > 0f) 
                rewardColor = Color.green;
            if (reward < 0f) 
                rewardColor = Color.red;

            Gizmos.color = rewardColor;

            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y + 2f, transform.position.z), 0.5f);
        }
    }


}
