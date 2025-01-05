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
    [SerializeField] private BufferSensorComponent enemiesBufferSensor = null;
    [SerializeField] private BufferSensorComponent boxesBufferSensor = null;
    [SerializeField] private BufferSensorComponent rampsBufferSensor = null;


    // Collect observations for the agent
    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 platformCenter = agentActions.GameManager.transform.position;

        // Self observation: position, rotation, velocity, and time left in preparation Phase
        sensor.AddObservation(transform.position - platformCenter);
        sensor.AddObservation(NormalizeAngle(transform.rotation.eulerAngles.y));
        sensor.AddObservation(agentActions.Rigidbody.linearVelocity);
        sensor.AddObservation(agentActions.GameManager.TimeLeftInPreparationPhase);

        // Entity observations: position, rotation, and velocity
        if(agentActions.IsHider){
            CollectEntityObservations(teamBufferSensor, agentActions.GameManager.GetHiders());
            CollectEntityObservations(enemiesBufferSensor, agentActions.GameManager.GetSeekers());
        }
        else{
            CollectEntityObservations(teamBufferSensor, agentActions.GameManager.GetSeekers());
            CollectEntityObservations(enemiesBufferSensor, agentActions.GameManager.GetHiders());
        }
        CollectEntityObservations(boxesBufferSensor, agentActions.GameManager.GetBoxes());
        CollectEntityObservations(rampsBufferSensor, agentActions.GameManager.GetRamps());

    }

    private void CollectEntityObservations<T>(BufferSensorComponent sensor, IEnumerable<T> entities) where T : MonoBehaviour
    {
        foreach (var entity in entities)
        {
            if (AgentSeesEntity(entity.gameObject, out RaycastHit hit))
            {
                float[] obs = new float[10];
                Vector3 relativePosition = entity.transform.position - transform.position;
                obs[0] = relativePosition.x;
                obs[1] = relativePosition.y;
                obs[2] = relativePosition.z;
                obs[3] = NormalizeAngle(entity.transform.rotation.eulerAngles.y);
                obs[4] = entity.GetComponent<Rigidbody>()?.linearVelocity.x ?? 0f;
                obs[5] = entity.GetComponent<Rigidbody>()?.linearVelocity.y ?? 0f;
                obs[6] = entity.GetComponent<Rigidbody>()?.linearVelocity.z ?? 0f;
                
                Vector3 entityScale = entity.transform.localScale;
                obs[7] = entityScale.x;
                obs[8] = entityScale.y;
                obs[9] = entityScale.z;
                sensor.AppendObservation(obs);
            }
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
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = movementInput.x;
        continuousActions[1] = movementInput.y;

        if (Input.GetKey(KeyCode.Q)) rotationInput = 1f;
        else if (Input.GetKey(KeyCode.E)) rotationInput = -1f;

        continuousActions[2] = rotationInput;

        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = Input.GetKey(KeyCode.C) ? 1 : 0;
        discreteActions[1] = Input.GetKey(KeyCode.Alpha2) ? 1 : Input.GetKey(KeyCode.Alpha3) ? 2 : 0;
    }

    // Normalize angle to range [-pi, pi] (degrees to radians)
    private float NormalizeAngle(float angle)
    {
        angle = (angle + 180f) % 360f - 180f;
        angle *= Mathf.Deg2Rad;
        return angle;
    }

    private bool AgentSeesEntity(GameObject entity, out RaycastHit hit)
    {
        Vector3 direction = entity.transform.position - transform.position;
        if (Vector3.Angle(direction, transform.forward) > agentActions.GameManager.ConeAngle)
        {
            hit = new RaycastHit();
            return false;
        }

        Ray ray = new Ray(transform.position, direction);
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.gameObject == entity)
            {
                return true;
            }
        }
        return false;
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
