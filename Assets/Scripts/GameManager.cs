using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private Trainer trainer = Trainer.PPO;
    [SerializeField] private int episodeSteps = 500;
    [SerializeField] private float preparationPhaseFraction = 0.4f;
    [SerializeField] private float coneAngle = 67.5f;
    [SerializeField] private MapGenerator mapGenerator = null;

    [Serializable]
    public struct Reward
    {
        public enum Type { VisibilityTeam, OutsideArenaPenalty };
        public Type type;
        public float value;
    };

    public enum Trainer { PPO, MA_POCA };

    [Header("Game Rules")]
    [SerializeField] private List<Reward> rewards = null;
    [SerializeField] private float arenaSize = 25f;

    [Header("Debug")]
    [SerializeField] private bool debugDrawInteractableHolding = true;
    [SerializeField] private bool debugDrawAgentsFOV = true;
    [SerializeField] private bool debugDrawIndividualReward = true;
    [SerializeField] private bool debugDrawArenaBounds = true;

    private int episodeTimer = 0;
    private List<AgentActions> hiders;
    private List<AgentActions> seekers;
    private List<Interactable> boxes;
    private List<Interactable> ramps;
    private List<AgentActions> hiderInstances;
    private List<AgentActions> seekerInstances;
    private List<Interactable> boxInstances;
    private List<Interactable> rampInstances;
    private SimpleMultiAgentGroup hidersGroup;
    private SimpleMultiAgentGroup seekersGroup;
    private bool[,] visibilityMatrix;
    private bool allHidden;
    private int stepsHidden = 0;
    private StatsRecorder statsRecorder = null;

    public float ArenaSize{ get { return arenaSize; } }
    public float ConeAngle{ get { return coneAngle; } }
    public bool DebugDrawBoxHold => debugDrawInteractableHolding;
    public bool DebugDrawIndividualReward => debugDrawIndividualReward;
    public bool PreparationPhaseEnded
    {
        get { return episodeTimer >= episodeSteps * preparationPhaseFraction; }
    }
    public float TimeLeftInPreparationPhase
    {
        get 
        {
            float preparationPhaseDuration = episodeSteps * preparationPhaseFraction;
            float timeLeft = preparationPhaseDuration - episodeTimer;
            return Mathf.Max(timeLeft, 0f); // Ensure the value does not go negative
        }
    }

    private void Start()
    {
        hiderInstances = mapGenerator.GetInstantiatedHiders();
        seekerInstances = mapGenerator.GetInstantiatedSeekers();
        boxInstances = mapGenerator.GetInstantiatedBoxes();
        rampInstances = mapGenerator.GetInstantiatedRamps();
        hiderInstances.ForEach(hider => hider.GameManager = this);
        seekerInstances.ForEach(seeker => seeker.GameManager = this);
    
        hidersGroup = new SimpleMultiAgentGroup();
        seekersGroup = new SimpleMultiAgentGroup();
        
        // Initialize stats recorder
        statsRecorder = Academy.Instance.StatsRecorder;
        Academy.Instance.OnEnvironmentReset += ResetScene;
    }

    private void Update()
    {
        // Manuelly reset scene on 'Space' key press for debugging
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ResetScene();
        }
    }

    private void FixedUpdate()
    {
        episodeTimer++;

        // Calculate and record mean position of seekers
        Vector3 seekersMeanPosition = Vector3.zero;
        foreach (AgentActions seeker in seekers)
        {
            seekersMeanPosition += seeker.transform.position - transform.position;
        }
        seekersMeanPosition /= seekers.Count;
        statsRecorder.Add("Environment/SeekersMeanX", seekersMeanPosition.x);
        statsRecorder.Add("Environment/SeekersMeanZ", seekersMeanPosition.z);

        // Calculate and record mean position of hiders
        Vector3 hidersMeanPosition = Vector3.zero;
        foreach (AgentActions hider in hiders)
        {
            hidersMeanPosition += hider.transform.position - transform.position;
        }
        hidersMeanPosition /= hiders.Count;
        statsRecorder.Add("Environment/HidersMeanX", hidersMeanPosition.x);
        statsRecorder.Add("Environment/HidersMeanZ", hidersMeanPosition.z);

        UpdateRewards();

        if (episodeTimer >= episodeSteps)
        {
            EndEpisode();
        }
        else if (PreparationPhaseEnded)
        {
            FillVisibilityMatrix();
            stepsHidden += allHidden ? 1 : 0;
        }
    }

    public IEnumerable<AgentActions> GetHiders()
    {
        foreach(AgentActions agent in hiders)
        {
            yield return agent;
        }
    }

    public IEnumerable<AgentActions> GetSeekers()
    {
        foreach(AgentActions agent in seekers)
        {
            yield return agent;
        }
    }

    public IEnumerable<Interactable> GetBoxes()
    {
        foreach(Interactable box in boxInstances)
        {
            yield return box;
        }
    }

    public IEnumerable<Interactable> GetRamps()
    {
        foreach(Interactable ramp in rampInstances)
        {
            yield return ramp;
        }
    }

    private void EndEpisode()
    {
        // Calculate time hidden
        float timeHidden = PreparationPhaseEnded ? stepsHidden / Mathf.Ceil(episodeTimer - episodeSteps * preparationPhaseFraction) : 0.0f;
        statsRecorder.Add("Environment/TimeHidden", timeHidden);

        //Record locked interactables at the end of the episode
        foreach (Interactable box in boxes)
        {
            if (box.LockOwner != null)
            {
                statsRecorder.Add("Environment/LockedBoxes", 1);
            }
        }

        hidersGroup.EndGroupEpisode();
        seekersGroup.EndGroupEpisode();
        ResetScene();
    }

    private void ResetScene()
    {
        stepsHidden = 0;
        episodeTimer = 0;

        // Generate new map
        mapGenerator.Generate();

        // Initialize hiders, seekers and interacrables
        hiders = hiderInstances.Take(mapGenerator.NumHiders).ToList();
        seekers = seekerInstances.Take(mapGenerator.NumSeekers).ToList();
        boxes = boxInstances.Take(mapGenerator.NumBoxes).ToList();
        ramps = rampInstances.Take(mapGenerator.NumRamps).ToList();

        foreach (AgentActions hider in hiders)
        {
            hider.ResetAgent();
            hidersGroup.RegisterAgent(hider.HideAndSeekAgent);
        }
        foreach (AgentActions seeker in seekers)
        {
            seeker.ResetAgent();
            seekersGroup.RegisterAgent(seeker.HideAndSeekAgent);
        }
        foreach (Interactable box in boxes)
        {
            box.Reset();
        }
        foreach (Interactable ramp in ramps)
        {
            ramp.Reset();
        }

        // Activate/deactivate agents and interactables based on count
        for (int i = 0; i < hiderInstances.Count; i++)
        {
            hiderInstances[i].gameObject.SetActive(i < hiders.Count);
        }
        for (int i = 0; i < seekerInstances.Count; i++)
        {
            seekerInstances[i].gameObject.SetActive(i < seekers.Count);
        }
        for (int i = 0; i < boxInstances.Count; i++)
        {
            boxInstances[i].gameObject.SetActive(i < boxes.Count);
        }
        for (int i = 0; i < rampInstances.Count; i++)
        {
            rampInstances[i].gameObject.SetActive(i < ramps.Count);
        }

        visibilityMatrix = new bool[hiders.Count, seekers.Count];
        allHidden = false;
    }

    // Checks if agent1 can see agent2
    private bool AgentSeesAgent(AgentActions agent1, AgentActions agent2, out RaycastHit hit)
    {
        Vector3 direction = agent2.transform.position - agent1.transform.position;
        if (Vector3.Angle(direction, agent1.transform.forward) > coneAngle)
        {
            hit = new RaycastHit();
            return false;
        }

        Ray ray = new Ray(agent1.transform.position, direction);
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.gameObject == agent2.gameObject)
            {
                return true;
            }
        }
        return false;
    }

    private void FillVisibilityMatrix()
    {
        allHidden = true;

        // Update visibility matrix
        for (int i = 0; i < hiders.Count; i++)
        {
            for (int j = 0; j < seekers.Count; j++)
            {
                if (AgentSeesAgent(seekers[j], hiders[i], out RaycastHit hit))
                {
                    visibilityMatrix[i, j] = true;
                    allHidden = false;
                }
                else
                {
                    visibilityMatrix[i, j] = false;
                }
            }
        }
    }

    private void UpdateRewards()
    {
        // Update rewards based on visibility and out-of-bounds penalties
        foreach (Reward rewardInfo in rewards)
        {
            switch (rewardInfo.type)
            {
                case Reward.Type.VisibilityTeam:
                    if (!PreparationPhaseEnded) break;
                    float teamReward = allHidden ? rewardInfo.value : -rewardInfo.value;
                    //Add reward to hiders and penalty to seekers as a group when MA-POCA is used
                    if(trainer == Trainer.MA_POCA)
                    {
                        hidersGroup.AddGroupReward(teamReward);
                        seekersGroup.AddGroupReward(-teamReward);
                    }
                    //Add reward to hiders and penalty to seekers individually when PPO is used
                    else if(trainer == Trainer.PPO)
                    {
                        hiders.ForEach((AgentActions hider) => hider.HideAndSeekAgent.AddReward(teamReward));
                        seekers.ForEach((AgentActions seeker) => seeker.HideAndSeekAgent.AddReward(-teamReward));
                    }
                    
                    break;

                case Reward.Type.OutsideArenaPenalty:
                    foreach (AgentActions hider in hiders.Where((AgentActions agent) => IsOutsideArena(agent)))
                    {
                        hider.HideAndSeekAgent.AddReward(-rewardInfo.value);
                    }
                    foreach (AgentActions seeker in seekers.Where((AgentActions agent) => IsOutsideArena(agent)))
                    {
                        seeker.HideAndSeekAgent.AddReward(-rewardInfo.value);
                    }
                    break;
            }
        }
    }

    // Checks if agent is outside arena
    private bool IsOutsideArena(AgentActions agent)
    {
        return Mathf.Max(Mathf.Abs(agent.transform.position.x - transform.position.x),
                         Mathf.Abs(agent.transform.position.z - transform.position.z)) > arenaSize * 0.5f;
    }

    private void OnDrawGizmos()
    {
        // Draw visibility field of view for seekers
        if (debugDrawAgentsFOV)
        {
            if(seekers != null){
                foreach (var agent in seekers){
                    DrawFieldOfView(agent);
                }
            }
        }

        // Draw Arena bounds
        if (debugDrawArenaBounds)
        {
            DrawArenaBounds();
        }
    }

    // Draws the arena bounds
    private void DrawArenaBounds()
    {
        Vector3 center = transform.position;
        Color c = Gizmos.color;
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.5f);
        Gizmos.DrawCube(center + new Vector3(-arenaSize * 0.5f, 1f, 0f), new Vector3(0.25f, 2f, arenaSize));
        Gizmos.DrawCube(center + new Vector3(+arenaSize * 0.5f, 1f, 0f), new Vector3(0.25f, 2f, arenaSize));
        Gizmos.DrawCube(center + new Vector3(0f, 1f, -arenaSize * 0.5f), new Vector3(arenaSize, 2f, 0.25f));
        Gizmos.DrawCube(center + new Vector3(0f, 1f, +arenaSize * 0.5f), new Vector3(arenaSize, 2f, 0.25f));
        Gizmos.color = c;
    }

    // Draws the field of view (FoV) of an agent
    private void DrawFieldOfView(AgentActions agent)
    {
        if (agent == null || !seekers.Contains(agent)) return;

        // Agent's position and forward direction
        Vector3 position = agent.transform.position;
        Vector3 forward = agent.transform.forward;

        // Total field of view angle and radius
        float totalFovAngle = coneAngle * 2f; // Full field of view (double the half-angle)

        // Color for the field of view
        Color fovColor = Color.yellow;

        // Draw field of view boundaries with obstacle checks
        Gizmos.color = fovColor;

        // Number of steps to divide the field of view
        int numSteps = 20; // More steps = smoother visualization
        float angleStep = totalFovAngle / numSteps;

        for (int i = 0; i <= numSteps; i++)
        {
            // Calculate angle for the current step
            float angle = -coneAngle + i * angleStep;

            // Direction of the current ray
            Vector3 rayDirection = Quaternion.Euler(0, angle, 0) * forward;

            // Perform a raycast to check for obstacles
            if (Physics.Raycast(position, rayDirection, out RaycastHit hit))
            {
                // Draw a line to the obstacle
                Gizmos.DrawLine(position, hit.point);
            }
        }
    }
}
