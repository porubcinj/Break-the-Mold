using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

public class AgentBase : Agent
{
    /* Constants */
    protected const float AIM_DEADZONE = 1f;
    protected const float ANGULAR_DAMPING = 0.025f;
    protected const float BOUNDS = 18.5f;
    protected const float BULLET_SPREAD_RECOVERY_FACTOR = 0.8f;
    protected int BULLET_LAYER_MASK;
    protected const int MAX_AMMO = 30;
    protected const float MAX_BULLET_SPREAD = 18f;
    protected const float MAX_RUN_FORCE = 50f;
    protected const float MAX_RUN_SPEED = 5f;
    protected const float MAX_TURN_SPEED = 330f;
    protected const float MAX_TURN_TORQUE = 7.2f;
    protected const float MIN_BULLET_SPREAD = 1f;
    protected const float RELOAD_COOLDOWN = 3f;
    protected const float RUN_BULLET_SPREAD_RATE = 0.6f;
    protected const float SHOOT_BULLET_SPREAD_INCREMENT = 3f;
    protected const float SHOOT_COOLDOWN = 0.08f;
    protected const float TURN_BULLET_SPREAD_RATE = 3f;
    protected int VISION_LAYER_MASK;

    /* Inputs */
    protected Vector2 AimInput;
    protected Vector2 MoveInput;
    protected bool ReloadInput;
    protected bool ShootInput;

    /* Heuristics */
    protected Vector2 AimCoordinates;

    /* State */
    protected int Ammo;
    protected float Cooldown;
    protected float BulletSpread;
    public int TeamId;

    /* Components */
    protected BufferSensorComponent AgentSensor;
    protected Light2D AimVisualizer;
    protected Light2D FieldOfViewLight;
    protected Camera PersonalCamera;
    protected Light2D ProximityLight;
    protected RayPerceptionSensorComponent2D[] RaySensors;
    protected AudioSource[] ReloadAudioSource;
    protected Rigidbody2D Rigidbody;
    protected AudioSource[] ShellAudioSource;
    protected AudioSource[] ShootAudioSource;

    /* Environment */
    protected readonly List<AgentBase> AgentsList = new();
    protected EnvironmentController Environment;
    protected AmmoController AmmoHUD;
    public string PlayerId = "Team0_0";
    protected Tilemap GridTilemap;
    protected CompositeCollider2D TilemapCollider;

    protected override void Awake()
    {
        base.Awake();
        AgentSensor = GetComponent<BufferSensorComponent>();
        AgentsList.AddRange(transform.parent.GetComponent<EnvironmentController>().AgentsList.Where(agent => agent != this));
        AimVisualizer = transform.Find("Aim Visualizer").GetComponent<Light2D>();
        BULLET_LAYER_MASK = LayerMask.GetMask("Default", "Team0", "Team1");
        if (PlayerId == "Team0_0")
        {
            AmmoHUD = GameObject.Find("Canvas/Ammo").GetComponent<AmmoController>();
            AmmoHUD.Capacity = MAX_AMMO;
        }
        Environment = gameObject.GetComponentInParent<EnvironmentController>();
        FieldOfViewLight = transform.Find("Spot Light 2D").GetComponent<Light2D>();
        GridTilemap = transform.parent.GetComponentInChildren<Tilemap>();
        PersonalCamera = GetComponent<PlayerInput>().camera = Camera.main;
        ProximityLight = transform.Find("Circle Light 2D").GetComponent<Light2D>();
        RaySensors = GetComponentsInChildren<RayPerceptionSensorComponent2D>();
        ReloadAudioSource = new AudioSource[]
        {
            transform.Find("Reload Audio Source").GetComponent<AudioSource>(),
        };
        Rigidbody = GetComponent<Rigidbody2D>();
        ShellAudioSource = new AudioSource[] 
        {
            transform.Find("Shell_0 Audio Source").GetComponent<AudioSource>(),
            transform.Find("Shell_1 Audio Source").GetComponent<AudioSource>(),
        };
        ShootAudioSource = new AudioSource[] 
        {
            transform.Find("Shoot_0 Audio Source").GetComponent<AudioSource>(),
            transform.Find("Shoot_1 Audio Source").GetComponent<AudioSource>(),
        };
        TilemapCollider = transform.parent.Find("Grid").GetComponentInChildren<CompositeCollider2D>();
        VISION_LAYER_MASK = LayerMask.GetMask("Default");
    }

    protected bool CanSeeAgent(GameObject agent)
    {
        /* See if agent is not within proximity range */
        Vector2 agentPosition = agent.transform.position;
        if (Vector2.Distance(transform.position, agentPosition) - 1f > ProximityLight.pointLightOuterRadius)
        {
            /* See if agent is not within line of sight range */
            Vector2 eyePosition = FieldOfViewLight.transform.position;
            float distanceToAgent = Vector2.Distance(eyePosition, agentPosition);
            if (distanceToAgent > FieldOfViewLight.pointLightOuterRadius)
            {
                return false;
            }

            /* See if agent is not within field of view */
            Vector2 directionToAgent = agentPosition - eyePosition;
            if (2f * Vector2.Angle(transform.right, directionToAgent) > FieldOfViewLight.pointLightOuterAngle)
            {
                return false;
            }

            /* See if agent is occluded */
            RaycastHit2D lineOfSight = Physics2D.Linecast(eyePosition, agentPosition, VISION_LAYER_MASK);
            if (lineOfSight.collider == null || lineOfSight.collider.gameObject != agent)
            {
                return false;
            }
        }
        return true;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        /* Normalize observations to [-1, 1] */
        Vector2 normalizedLocalPosition = transform.localPosition / BOUNDS;
        normalizedLocalPosition.x = Mathf.Clamp(normalizedLocalPosition.x, -1f, 1f);
        normalizedLocalPosition.y = Mathf.Clamp(normalizedLocalPosition.y, -1f, 1f);
        Vector2 normalizedLocalRight = ((Vector2)transform.right).SquareNormalize();
        Vector2 normalizedMoveDirection = Rigidbody.linearVelocity.SquareNormalize();
        float normalizedMoveSpeed = Mathf.Clamp(Rigidbody.linearVelocity.magnitude / MAX_RUN_SPEED, -1f, 1f);
        float normalizedTurnVelocity = Mathf.Clamp(Rigidbody.angularVelocity / MAX_TURN_SPEED, -1f, 1f);
        float normalizedAmmo = Mathf.Clamp(2f * Ammo / MAX_AMMO - 1f, -1f, 1f);
        float normalizedBulletSpread = Mathf.Clamp(2f * Ammo / MAX_BULLET_SPREAD - 1f, -1f, 1f);
        float normalizedCooldown = Mathf.Clamp(2f * Cooldown / RELOAD_COOLDOWN - 1f, -1f, 1f);

        sensor.AddObservation(normalizedLocalPosition);
        sensor.AddObservation(normalizedLocalRight);
        sensor.AddObservation(normalizedMoveDirection);
        sensor.AddObservation(normalizedMoveSpeed);
        sensor.AddObservation(normalizedTurnVelocity);
        sensor.AddObservation(normalizedAmmo);
        sensor.AddObservation(normalizedBulletSpread);
        sensor.AddObservation(normalizedCooldown);

        /* Include observations from field of view to Buffer Sensor */
        foreach (AgentBase agent in AgentsList.Where(agent => agent.gameObject.activeSelf))
        {
            float[] observation = agent.GetObservableAttributes(
                FieldOfViewLight.transform.position,
                transform.localPosition,
                TeamId,
                CanSeeAgent(agent.gameObject)
            );
            AgentSensor.AppendObservation(observation);
        }
    }

    public float[] GetObservableAttributes(Vector2 observerPosition, Vector3 observerLocalPosition, int observerTeamId, bool canSeeAgent)
    {
        Vector2 normalizedLocalPosition = transform.localPosition / BOUNDS;
        normalizedLocalPosition.x = Mathf.Clamp(normalizedLocalPosition.x, -1f, 1f);
        normalizedLocalPosition.y = Mathf.Clamp(normalizedLocalPosition.y, -1f, 1f);
        float normalizedDistance = 0f;
        Vector2 normalizedDisplacementDirection = Vector2.zero;
        Vector2 normalizedMoveDirection = Vector2.zero;
        float normalizedMoveSpeed = 0f;
        float isTeammate = 0f;
        Vector2 normalizedLocalRight = Vector2.zero;
        float normalizedTurnVelocity = 0f;
        float normalizedCooldown = 0f;

        /* Team outline is always observable.
         * Provides position and velocity information. */
        if (canSeeAgent || TeamId == observerTeamId)
        {
            normalizedDistance = Mathf.Clamp(2f * (Vector2.Distance(observerPosition, transform.position) - 1f) / (FieldOfViewLight.pointLightOuterRadius - 0.5f) - 1f, -1f, 1f);
            normalizedDisplacementDirection = ((Vector2)(transform.localPosition - observerLocalPosition)).SquareNormalize();
            normalizedMoveDirection = Rigidbody.linearVelocity.SquareNormalize();
            normalizedMoveSpeed = Mathf.Clamp(Rigidbody.linearVelocity.magnitude / MAX_RUN_SPEED, -1f, 1f);
            isTeammate = TeamId == observerTeamId ? 1f : -1f;
        }

        /* Seeing an agent provides even more information. */
        if (canSeeAgent)
        {
            normalizedLocalRight = ((Vector2)transform.right).SquareNormalize();
            normalizedTurnVelocity = Mathf.Clamp(Rigidbody.angularVelocity / MAX_TURN_SPEED, -1f, 1f);
            normalizedCooldown = Mathf.Clamp(2f * Cooldown / RELOAD_COOLDOWN - 1f, -1f, 1f);
        }

        return new float[]
        {
            normalizedLocalPosition.x,
            normalizedLocalPosition.y,
            normalizedDistance,
            normalizedDisplacementDirection.x,
            normalizedDisplacementDirection.y,
            normalizedMoveDirection.x,
            normalizedMoveDirection.y,
            normalizedMoveSpeed,
            isTeammate,
            normalizedLocalRight.x,
            normalizedLocalRight.y,
            normalizedTurnVelocity,
            normalizedCooldown
        };
    }

    protected void HandleWeaponInput()
    {
        if (Cooldown <= 0f)
        {
            if (Ammo <= 0)
            {
                Reload();
            }
            else if (ShootInput)
            {
                Shoot();
            }
            else if (ReloadInput && Ammo < MAX_AMMO)
            {
                Reload();
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;

        Vector2 AimInput = ((Vector2)(PersonalCamera.ScreenToWorldPoint(AimCoordinates) - transform.position)).SquareNormalize();
        continuousActions[0] = AimInput.x;
        continuousActions[1] = AimInput.y;

        discreteActions[0] = Mathf.RoundToInt(MoveInput.x) + 1;
        discreteActions[1] = Mathf.RoundToInt(MoveInput.y) + 1;
        discreteActions[2] = ShootInput ? 1 : 0;
        discreteActions[3] = ReloadInput ? 1 : 0;
    }

    protected void Move()
    {
        Vector2 moveForce = MoveInput.sqrMagnitude > 1f ? MoveInput.normalized : MoveInput;
        moveForce *= MAX_RUN_FORCE;
        Rigidbody.AddForce(moveForce);
        BulletSpread += Rigidbody.linearVelocity.magnitude / MAX_RUN_SPEED * RUN_BULLET_SPREAD_RATE;
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        /* Digital aim joystick */
        AimInput = new Vector2(
            Mathf.Clamp(actionBuffers.ContinuousActions[0], -1f, 1f),
            Mathf.Clamp(actionBuffers.ContinuousActions[1], -1f, 1f)
        );

        /* Branch 0 */
        /* -1 = left, 0 = no input, 1 = right */
        MoveInput.x = actionBuffers.DiscreteActions[0] - 1;

        /* Branch 1 */
        /* -1 = down, 0 = no input, 1 = up */
        MoveInput.y = actionBuffers.DiscreteActions[1] - 1;

        /* Branch 2 */
        /* 0 = no shoot, 1 = shoot */
        ShootInput = actionBuffers.DiscreteActions[2] == 1;

        /* Branch 3 */
        /* 0 = no reload, 1 = reload */
        ReloadInput = actionBuffers.DiscreteActions[3] == 1;


        /* FixedUpdate loop */
        Move();
        Rotate();
        HandleWeaponInput();
        BulletSpread = Mathf.Max(MIN_BULLET_SPREAD, BulletSpread * BULLET_SPREAD_RECOVERY_FACTOR);
        AimVisualizer.pointLightInnerAngle = AimVisualizer.pointLightOuterAngle = BulletSpread;
        if (PlayerId == "Team0_0")
        {
            UpdateTilemapVisibility();
        }
        Cooldown = Mathf.Max(0f, Cooldown - Time.fixedDeltaTime);
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        AimCoordinates = context.ReadValue<Vector2>();
    }

    public override void OnEpisodeBegin()
    {
        /* Randomize local starting position and rotation */
        Vector3 localPosition = TeamId == 0 ?
            Random.insideUnitCircle.SquareNormalize() * Academy.Instance.EnvironmentParameters.GetWithDefault("spawn_radius", BOUNDS) :
            Random.insideUnitCircle * (BOUNDS - 5f);
        while (transform.GetComponent<CircleCollider2D>().IsTouching(TilemapCollider))
        {
            localPosition = TeamId == 0 ?
                Random.insideUnitCircle.SquareNormalize() * Academy.Instance.EnvironmentParameters.GetWithDefault("spawn_radius", BOUNDS) :
                Random.insideUnitCircle * (BOUNDS - 5f);
        }
        Quaternion rotation = Quaternion.Euler(0f, 0f, Random.Range(-180f, 180f));
        transform.SetLocalPositionAndRotation(localPosition, rotation);

        /* Reset Rigidbody2D attributes */
        Rigidbody.linearVelocity = Vector2.zero;
        Rigidbody.angularVelocity = 0f;

        /* Reset Agent attributes */
        AimInput = MoveInput = Vector2.zero;
        MoveInput = Vector2.zero;
        Ammo = MAX_AMMO;
        if (AmmoHUD != null)
        {
            AmmoHUD.Amount = Ammo;
        }
        BulletSpread = MIN_BULLET_SPREAD;
        Cooldown = 0f;
        ReloadInput = ShootInput = false;

        /* Reset Tilemap */
        if (PlayerId == "Team0_0")
        {
            foreach (Vector3Int tile in GridTilemap.cellBounds.allPositionsWithin)
            {
                if (GridTilemap.HasTile(tile))
                {
                    GridTilemap.SetColor(tile, Color.black);
                }
            }
        }
    }

    public void OnHit()
    {
        AddReward(-1);
        gameObject.SetActive(false);
        if (TeamId == 0)
        {
            --Environment.NumTeam0AgentsRemaining;
        }
        else
        {
            --Environment.NumTeam1AgentsRemaining;
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    public void OnReload(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            ReloadInput = true;
        }
        else if (context.canceled)
        {
            ReloadInput = false;
        }
    }

    public void OnShoot(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            ShootInput = true;
        }
        else if (context.canceled)
        {
            ShootInput = false;
        }
    }

    protected void Reload()
    {
        Cooldown = RELOAD_COOLDOWN;
        ReloadAudioSource[Random.Range(0, ReloadAudioSource.Length)].Play();
        Ammo = MAX_AMMO;
        if (AmmoHUD != null)
        {
            AmmoHUD.Amount = Ammo;
        }
    }

    protected void Rotate()
    {
        if (AimInput != Vector2.zero)
        {
            float angle = Vector2.SignedAngle(transform.right, AimInput);
            float torque = angle - Rigidbody.angularVelocity * ANGULAR_DAMPING;
            torque = Mathf.Clamp(torque, -MAX_TURN_TORQUE, MAX_TURN_TORQUE);
            Rigidbody.AddTorque(torque);
            BulletSpread += Mathf.Abs(Rigidbody.angularVelocity) / MAX_TURN_SPEED * TURN_BULLET_SPREAD_RATE;
        }
    }

    public void SetPlayerId(string playerId)
    {
        /* PlayerId = TeamX_Y
         * TeamId = X */
        PlayerId = playerId;
        TeamId = GetComponent<BehaviorParameters>().TeamId = PlayerId[4] - '0';

        /* Set sprite based on team */
        string teamName = PlayerId[..5];
        gameObject.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>(teamName);

        foreach (Light2D light in GetComponentsInChildren<Light2D>())
        {
            if (light.gameObject.name == "Outline")
            {
                /* Outline visible to team */
                light.gameObject.layer = LayerMask.NameToLayer(teamName);
            }
            else
            {
                /* Field of view light visible to self */
                light.gameObject.layer = LayerMask.NameToLayer(PlayerId);
            }
        }

        /* Set outline color based on team */
        GameObject outline = transform.Find("Outline").gameObject;
        outline.GetComponent<SpriteRenderer>().color = outline.GetComponent<Light2D>().color = TeamId switch
        {
            0 => Color.blue,
            _ => Color.red,
        };
    }

    protected void Shoot()
    {
        Cooldown = SHOOT_COOLDOWN;

        float spreadAngle = Random.Range(-BulletSpread / 2f, BulletSpread / 2f);
        Transform muzzleTransform = AimVisualizer.transform;
        Vector2 shootDirection = Quaternion.Euler(0f, 0f, spreadAngle) * muzzleTransform.up;
        RaycastHit2D bullet = Physics2D.Raycast(muzzleTransform.position, shootDirection, Mathf.Infinity, BULLET_LAYER_MASK);
        if (bullet.collider != null)
        {
            // TODO: Draw raycast
            if (bullet.collider.CompareTag("Agent"))
            {
                AgentBase agent = bullet.collider.gameObject.GetComponent<AgentBase>();
                AddReward(TeamId == agent.TeamId ? 1f : -1f);
                agent.OnHit();
            }
        }

        BulletSpread += SHOOT_BULLET_SPREAD_INCREMENT;
        ShootAudioSource[Random.Range(0, ShootAudioSource.Length)].Play();
        ShellAudioSource[Random.Range(0, ShellAudioSource.Length)].Play();

        --Ammo;
        if (AmmoHUD != null)
        {
            AmmoHUD.Amount = Ammo;
        }
    }

    protected void UpdateTilemapVisibility()
    {
        foreach (RayPerceptionOutput.RayOutput rayOutput in RaySensors.SelectMany(sensor => sensor.RaySensor.RayPerceptionOutput.RayOutputs))
        {
            /* Extend ray cast slightly to penetrate walls for algorithm to work */
            Vector2 endPoint = Vector2.Lerp(rayOutput.StartPositionWorld, rayOutput.EndPositionWorld, rayOutput.HitFraction + 0.000625f);
            Vector3Int tile = new(
                Mathf.FloorToInt(endPoint.x),
                Mathf.FloorToInt(endPoint.y)
            );

            if (GridTilemap.HasTile(tile))
            {
                GridTilemap.SetColor(tile, Color.white);
            }
        }
    }

    public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
    {
        /* Shoot */
        actionMask.SetActionEnabled(2, 1, Cooldown <= 0f && Ammo > 0);

        /* Reload */
        actionMask.SetActionEnabled(3, 1, Cooldown <= 0f && Ammo < MAX_AMMO);
    }
}
