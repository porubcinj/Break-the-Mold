using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Demonstrations;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputSystem;

public class EnvironmentController : MonoBehaviour
{
    private Transform GridTilemap;
    private int ResetTimer;
    private readonly SimpleMultiAgentGroup[] Teams = new SimpleMultiAgentGroup[2];

    public GameObject AgentPrefab;
    [HideInInspector] public readonly List<AgentBase> AgentsList = new();
    public int[] CameraTarget = new int[2];
    public bool Heuristic = false;
    [Tooltip("Max Environment Steps")] public int MaxEnvironmentSteps = 24000;
    [Tooltip("Number of Team0 Agents"), Range(1, 12)] public int NumTeam0Agents = 1;
    [Tooltip("Number of Team1 Agents"), Range(1, 12)] public int NumTeam1Agents = 1;
    [HideInInspector] public int NumTeam0AgentsRemaining, NumTeam1AgentsRemaining;
    public bool Record = false;

    private void Start()
    {
        Assert.IsNotNull(AgentPrefab);
        Assert.IsTrue(NumTeam0Agents >= 1);
        Assert.IsTrue(NumTeam1Agents >= 1);

        Teams[0] = new SimpleMultiAgentGroup();
        Teams[1] = new SimpleMultiAgentGroup();
        GridTilemap = transform.Find("Tilemap");

        AgentPrefab.SetActive(false);
        {
            GameObject agent = Instantiate(AgentPrefab, transform);
            agent.GetComponent<BehaviorParameters>().BehaviorType = Heuristic ? BehaviorType.HeuristicOnly : BehaviorType.Default;
            agent.GetComponent<PlayerInput>().enabled = Heuristic;
            agent.GetComponent<DemonstrationRecorder>().Record = Heuristic && Record;
            GameObject.Find("Cinemachine Brain").GetComponent<Camera>().cullingMask = LayerMask.GetMask("Default", $"Team{CameraTarget[0]}", $"Team{CameraTarget[0]}_{CameraTarget[1]}", "UI", "Tilemap");
            AgentsList.Add(agent.GetComponent<AgentBase>());
        }
        for (int i = 1; i < NumTeam0Agents; ++i)
        {
            AgentBase agent = Instantiate(AgentPrefab, transform).GetComponent<AgentBase>();
            agent.SetPlayerId($"Team0_{i}");
            AgentsList.Add(agent);
        }
        NumTeam0AgentsRemaining = NumTeam0Agents;

        for (int i = 0; i < NumTeam1Agents; ++i)
        {
            AgentBase agent = Instantiate(AgentPrefab, transform).GetComponent<AgentBase>();
            agent.SetPlayerId($"Team1_{i}");
            AgentsList.Add(agent);
        }
        NumTeam1AgentsRemaining = NumTeam1Agents;

        /* Register the agents to the groups */
        AgentPrefab.SetActive(true);
        foreach (AgentBase agent in AgentsList)
        {
            Teams[agent.TeamId].RegisterAgent(agent);
        }

        ResetScene();
    }

    private void FixedUpdate()
    {
        if (NumTeam0AgentsRemaining == 0)
        {
            Teams[0].AddGroupReward(-1f);
            Teams[1].AddGroupReward(1f);
            Teams[0].EndGroupEpisode();
            Teams[1].EndGroupEpisode();
            ResetScene();
        }
        else if (NumTeam1AgentsRemaining == 0)
        {
            Teams[0].AddGroupReward(1f);
            Teams[1].AddGroupReward(-1f);
            Teams[0].EndGroupEpisode();
            Teams[1].EndGroupEpisode();
            ResetScene();
        }
        else if (++ResetTimer > MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            Teams[0].GroupEpisodeInterrupted();
            Teams[1].GroupEpisodeInterrupted();
            ResetScene();
        }
    }

    public void ResetScene()
    {
        ResetTimer = 0;

        /*float zRotation = Random.Range(0, 4) switch
        {
            0 => 0f,
            1 => 90f,
            2 => 180f,
            _ => -90f,
        };
        GridTilemap.Rotate(0f, 0f, zRotation);*/

        foreach (AgentBase agent in AgentsList)
        {
            agent.gameObject.SetActive(true);
            Teams[agent.TeamId].RegisterAgent(agent);
        }
        NumTeam0AgentsRemaining = NumTeam0Agents;
        NumTeam1AgentsRemaining = NumTeam1Agents;
    }
}
