using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private int episodeSteps = 240;
    [SerializeField] private float preparationPhaseFraction = 0.4f;
    [SerializeField] private float coneAngle = 67.5f;
    [SerializeField] private MapGenerator mapGenerator = null;
    [SerializeField] private Trainer trainer = Trainer.PPO;

    [Serializable]
    public struct RewardInfo
    {
        public enum Type { VisibilityTeam, OobPenalty };
        public Type type;
        public float weight;
    };

    public enum WinCondition { None, LineOfSight };

    public enum Trainer { PPO, MA_POCA };


    [Header("Game Rules")]
    [SerializeField] private List<RewardInfo> rewards = null;
    [SerializeField] private WinCondition winCondition = WinCondition.None;
    [SerializeField] private float winConditionReward = 1.0f;
    [SerializeField] private float arenaSize = 20f;

    [Header("Debug")]
    [SerializeField] private bool debugDrawBoxHold = true;
    [SerializeField] private bool debugDrawVisibility = true;
    [SerializeField] private bool debugDrawIndividualReward = true;
    [SerializeField] private bool debugDrawPlayAreaBounds = true;
    [SerializeField] private bool debugLogMatchResult = true;

    private int episodeTimer = 0;
    private List<AgentActions> hiders;
    private List<AgentActions> seekers;
    private List<AgentActions> hiderInstances;
    private List<AgentActions> seekerInstances;
    private SimpleMultiAgentGroup hidersGroup;
    private SimpleMultiAgentGroup seekersGroup;
    private List<Interactable> interactables;

    private bool[,] visibilityMatrix;
    private bool[] visibilityHiders;
    private bool[] visibilitySeekers;
    private bool allHidden;

    private int stepsHidden = 0;
    private bool hidersPerfectGame = true;
    private StatsRecorder statsRecorder = null;

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

    public bool DebugDrawBoxHold => debugDrawBoxHold;
    public bool DebugDrawIndividualReward => debugDrawIndividualReward;

    private void Start()
    {
        // Initialize map and agents and interactables
        mapGenerator.Initialize();

        hiderInstances = mapGenerator.GetInstantiatedHiders();
        seekerInstances = mapGenerator.GetInstantiatedSeekers();
        hiderInstances.ForEach(hider => hider.GameManager = this);
        seekerInstances.ForEach(seeker => seeker.GameManager = this);
    
        hidersGroup = new SimpleMultiAgentGroup();
        seekersGroup = new SimpleMultiAgentGroup();

        interactables = FindObjectsByType<Interactable>(0).ToList();
        
        // Initialize stats recorder
        statsRecorder = Academy.Instance.StatsRecorder;
        // Reset the scene
        ResetScene();
    }

    private void Update()
    {
        // Manuelly reset scene on 'P' key press for debugging
        if (Input.GetKeyDown(KeyCode.P))
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
            if (!allHidden)
            {
                hidersPerfectGame = false;
            }
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

    private void EndEpisode()
    {
        // Calculate time hidden
        float timeHidden = PreparationPhaseEnded ? stepsHidden / Mathf.Ceil(episodeTimer - episodeSteps * preparationPhaseFraction) : 0.0f;
        statsRecorder.Add("Environment/TimeHidden", timeHidden);

        // Determine win condition
        if (winCondition != WinCondition.None)
        {
            bool hidersWon = false;
            if (winCondition == WinCondition.LineOfSight && hidersPerfectGame)
            {
                hidersWon = true;
            }


            hidersGroup.AddGroupReward(hidersWon ? winConditionReward : -winConditionReward);
            seekersGroup.AddGroupReward(hidersWon ? -winConditionReward : winConditionReward);
            statsRecorder.Add("Environment/HiderWinRatio", hidersWon ? 1 : 0);
        
            if (debugLogMatchResult)
            {
                Debug.LogFormat("Team {0} won; Time percentage hidden - {1}", hidersWon ? "hiders" : "seekers", timeHidden * 100f);
            }
        }

        hidersGroup.EndGroupEpisode();
        seekersGroup.EndGroupEpisode();
        ResetScene();
    }

    private void ResetScene()
    {
        stepsHidden = 0;
        hidersPerfectGame = true;
        episodeTimer = 0;

        // Reset interactables if not instantiated
        if (!mapGenerator.InstantiatesInteractables())
        {
            foreach (Interactable interactable in interactables)
            {
                interactable.Reset();
            }
        }

        // Generate new map
        mapGenerator.Generate();

        // Initialize hiders and seekers
        hiders = hiderInstances.Take(mapGenerator.NumHiders).ToList();
        seekers = seekerInstances.Take(mapGenerator.NumSeekers).ToList();
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

        // Activate/deactivate agents based on count
        for (int i = 0; i < hiderInstances.Count; i++)
        {
            hiderInstances[i].gameObject.SetActive(i < hiders.Count);
        }
        for (int i = 0; i < seekerInstances.Count; i++)
        {
            seekerInstances[i].gameObject.SetActive(i < seekers.Count);
        }

        visibilityMatrix = new bool[hiders.Count, seekers.Count];
        visibilityHiders = new bool[hiders.Count];
        visibilitySeekers = new bool[seekers.Count];
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
        Array.Fill(visibilityHiders, false);
        Array.Fill(visibilitySeekers, false);

        // Update visibility matrix
        for (int i = 0; i < hiders.Count; i++)
        {
            for (int j = 0; j < seekers.Count; j++)
            {
                if (AgentSeesAgent(seekers[j], hiders[i], out RaycastHit hit))
                {
                    visibilityMatrix[i, j] = true;
                    visibilityHiders[i] = true;
                    visibilitySeekers[j] = true;
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
        foreach (RewardInfo rewardInfo in rewards)
        {
            switch (rewardInfo.type)
            {
                case RewardInfo.Type.VisibilityTeam:
                    if (!PreparationPhaseEnded) break;
                    float teamReward = allHidden ? rewardInfo.weight : -rewardInfo.weight;
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

                case RewardInfo.Type.OobPenalty:
                    foreach (AgentActions hider in hiders.Where((AgentActions agent) => IsOoB(agent)))
                    {
                        hider.HideAndSeekAgent.AddReward(-rewardInfo.weight);
                    }
                    foreach (AgentActions seeker in seekers.Where((AgentActions agent) => IsOoB(agent)))
                    {
                        seeker.HideAndSeekAgent.AddReward(-rewardInfo.weight);
                    }
                    break;
            }
        }
    }

    // Checks if agent is out of bounds
    private bool IsOoB(AgentActions agent)
    {
        return Mathf.Max(Mathf.Abs(agent.transform.position.x - transform.position.x),
                         Mathf.Abs(agent.transform.position.z - transform.position.z)) > arenaSize * 0.5f;
    }


    private void OnDrawGizmos()
    {
        // Draw visibility field of view for seekers
        if (debugDrawVisibility)
        {
            if(seekers != null){
                foreach (var agent in seekers){
                    DrawFieldOfView(agent);
                }
            }
        }

        // Draw play area bounds
        if (debugDrawPlayAreaBounds)
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
