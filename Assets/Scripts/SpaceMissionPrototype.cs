// =============================================================================
// SPACE MISSION PROTOTYPE - DARK STARRY SPACE & VISUAL RISK/REWARD SIMULATOR
// =============================================================================
// Features:
//   1. Dark Space Theme with Twinkling Starfield & Sleek Dark-Mode UI Cards
//   2. Visual Mission Corridor in Center:
//      - Earth Launchpad (Bottom) -> Flight Trajectory -> Target Planet Orbit (Top)
//      - 3 Animated Obstacle Zones (Max-Q Turbulence, Radiation Belt, Space Debris)
//      - 3 Orbiting Science Reward Nodes + Planetary Eclipse Shadow Zone
//   3. Real Risk vs. Reward Trade-Offs:
//      - Higher-risk destinations (Moon, Mars) have harsher obstacles & lower
//        solar power, but multiply Science & Funding Rewards (up to 3.0x)!
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

public class SpaceMissionPrototype : MonoBehaviour
{
    public enum MissionPhase { TitleScreen, Assembly, Countdown, Launching, FlightCrisis, OrbitSimulation, Completed }

    [Serializable]
    public class PartSpec
    {
        public string partTitle;
        public string category;
        public float massKg;
        public float costMillion;
        public float powerOutputW;
        public float powerDrawW;
        public float sciencePoints;
        public bool isInstalled;

        public Vector3 catalogPosition;
        public Vector3 busLocalOffset;
        public Vector3 catalogScale;
        public Vector3 mountedScale;
        public Color installedColor;

        public Transform partTransform;
        public Renderer partRenderer;
        public Renderer slotCardRenderer;
        public Renderer slotShadowRenderer;
        public TextMesh labelMesh;
    }

    [Serializable]
    public class FlightCrisis
    {
        public string title;
        public string realWorldWhy;
        public string optionAText;
        public string optionBText;
    }

    [Header("Mission Phase & Destination Risk/Reward")]
    public MissionPhase currentPhase = MissionPhase.TitleScreen;
    public int selectedTargetIndex = 0;
    public string[] targetNames = { "Low Earth Orbit (LEO)", "Lunar Polar Orbit", "Mars Deep Space Orbit" };
    public float[] targetDeltaVReq = { 1600f, 2800f, 3800f };
    public float[] targetSolarMultiplier = { 1.0f, 0.85f, 0.45f };
    public float[] targetRewardMultiplier = { 1.0f, 1.8f, 3.0f };
    public string[] targetRiskRating = { "LOW RISK (Protected by Earth Magnetosphere)", "MEDIUM RISK (Deep Space Radiation & Debris)", "EXTREME RISK (45% Sunlight, High Debris & Delay)" };

    public int selectedRocketIndex = 1;
    public string[] rocketNames = { "Minotaur IV Lite ($40M | 1800kg)", "Falcon 9 NASA-LSP ($80M | 3100kg)", "NASA SLS Block 1B ($150M | 4500kg)" };
    public float[] rocketCostsM = { 40f, 80f, 150f };
    public float[] rocketMaxMassKg = { 1800f, 3100f, 4500f };

    [Header("Budget & Mission Requirements")]
    public float[] targetBudgetMillion = { 1000f, 1400f, 1850f };
    public float[] targetScienceGoals = { 80f, 120f, 180f };
    public float maxBudgetMillion = 1000f;
    public float remainingBudgetMillion = 267.5f;
    public float targetSciencePts = 80f;

    [Header("Live Resources & Telemetry")]
    public float dryMassKg;
    public float fuelMassKg = 350f;
    public int repairKits = 2;
    public float hullIntegrity = 100f;
    public float missionFailureRiskPct = 15f;
    public float totalMassKg;
    public float totalCostMillion;
    public float powerGenW;
    public float powerDrawW;
    public float netPowerW;
    public float batteryCapacityWh;
    public float currentBatteryWh;
    public float calculatedDeltaV;
    public float potentialSciencePts;
    public float transmittedSciencePts;

    [Header("Spacecraft Parts List")]
    public List<PartSpec> parts = new List<PartSpec>();

    [Header("Visual Mission Map & Obstacle References")]
    public Transform assemblyRoot;
    public Transform missionCorridorMap;
    public Transform spacecraftHub;
    public Transform partsCatalogContainer;
    public Transform targetPlanetVisual;
    public Transform orbitRingRoot;
    public LineRenderer orbitRingRenderer;
    public Transform[] obstacleZones;
    public Transform[] debrisParticles;
    public Transform[] radiationParticles;
    public Transform[] scienceRewardNodes;
    public Transform[] backgroundStars;
    public Renderer crisisHighlightRing;

    public TextMesh topBarDisplay;
    public TextMesh telemetryDisplay;
    public TextMesh statusDisplay;
    public TextMesh configDisplay;
    public TextMesh mapTargetLabel;
    public TextMesh btnTargetLabel;
    public TextMesh btnRocketLabel;
    public Renderer statusCardRenderer;

    private float simTimer = 0f;
    private float orbitAngle = 0f;
    private bool inEclipse = false;
    private FlightCrisis[] realisticCrises;
    private GameObject titleScreenGroup;
    private GameObject manualModalObj;
    private Transform earthCloudsGroup;
    private Transform earthContinentsGroup;
    private Transform earthNightLightsGroup;
    private Transform moonCratersGroup;
    private Transform marsFeaturesGroup;
    private Transform downlinkPulse0;
    private Transform downlinkPulse1;
    private float downlinkTimer = 0f;
    private Transform maxQVaporCone;
    private Transform ionPlasmaPlume;
    private string NL { get { return Environment.NewLine; } }

    [Header("Categorized Rocket Assembly System")]
    public int activePartsTab = 0; // 0: Stages, 1: Power & Bus, 2: Science & Comms
    private readonly int[][] tabPartIndices = new int[][]
    {
        new int[] { 7, 8, 9, 10, 11 }, // Tab 0: STAGES (Booster, SRBs, Upper Stage, Grid Fins, Fairing)
        new int[] { 12, 0, 1, 2 },      // Tab 1: POWER & BUS (Satellite Bus, ROSA Solar, MMRTG, Battery)
        new int[] { 3, 5, 4, 6, 13 }    // Tab 2: SCIENCE & COMMS (Ka-Band Dish, Ion Thruster, HiRISE Camera, THEMIS IR, Magnetometer)
    };
    private GameObject leftCategorizedPanelObj;
    private GameObject[] tabSlotObjects = new GameObject[5];
    private Renderer[] tabSlotBgRenderers = new Renderer[5];
    private Renderer[] tabSlotBorderRenderers = new Renderer[5];
    private TextMesh[] tabSlotLabelMeshes = new TextMesh[5];
    private Renderer[] tabButtonRenderers = new Renderer[3];
    private TextMesh[] tabButtonLabelMeshes = new TextMesh[3];
    private Transform satelliteBusTransform;
    private Transform srbGroup;
    private Transform upperStageGroup;
    private Transform gridFinsGroup;
    private Transform magnetometerTransform;

    [Header("Active Pilot Flight Console")]
    private GameObject flightConsoleGroup;
    private Renderer throttle100Renderer;
    private Renderer throttle65Renderer;
    private Renderer throttle0Renderer;
    private Renderer flightStageRenderer;
    private TextMesh flightStageLabelMesh;
    private TextMesh flightGimbalLabelMesh;
    private Renderer flightAdvisoryBoxRenderer;
    private TextMesh flightAdvisoryTitleMesh;
    private TextMesh flightAdvisoryQMesh;
    private TextMesh flightAdvisoryCueMesh;
    private GameObject flightRcsVisual;
    private Transform rcsPlumeL;
    private Transform rcsPlumeR;
    private int activeRcsSide = 0; // -1 = yaw left (fire right jet), +1 = yaw right (fire left jet)
    private float rcsPlumeTimer = 0f;
    private float debrisHitCooldown = 0f;
    private string lastStatusBannerHeader = "";
    private string activeFlightDirectorHeader = "MISSION CONTROL";
    private string activeFlightDirectorCallout = "Ascent nominal. Maintain 100% thrust through lower atmosphere.";
    private Color activeFlightDirectorColor = new Color(0.345f, 0.800f, 0.008f);

    private float flightThrottle = 1.0f; // 1.0 = Full, 0.65 = Safe-Q, 0.0 = Idle/Cutoff
    private float flightGimbalOffset = 0f;
    private bool stage1Separated = false;

    // Dark-Mode Duolingo / Space UI Palette
    private readonly Color DuoGreen      = new Color(0.345f, 0.800f, 0.008f); // #58CC02
    private readonly Color DuoGreenDark  = new Color(0.200f, 0.480f, 0.020f);
    private readonly Color DuoBlue       = new Color(0.110f, 0.690f, 0.965f); // #1CB0F6
    private readonly Color DuoGold       = new Color(1.000f, 0.784f, 0.000f); // #FFC800
    private readonly Color DuoOrange     = new Color(1.000f, 0.588f, 0.000f); // #FF9600
    private readonly Color DuoRed        = new Color(1.000f, 0.294f, 0.294f); // #FF4B4B
    private readonly Color CardDarkBg    = new Color(0.102f, 0.165f, 0.208f); // #1A2A35
    private readonly Color CardDarkEdge  = new Color(0.200f, 0.290f, 0.355f); // #334A5B
    private readonly Color CardEquipped  = new Color(0.125f, 0.290f, 0.165f); // Dark Green Card

    // Key Y positions along the vertical mission trajectory
    private readonly float LaunchpadY = -0.55f;
    private readonly float[] ObstacleYPositions = { 7.0f, 13.5f, 20.0f };
    private readonly float OrbitCenterY = 26.5f;
    private readonly float OrbitRadius = 1.35f;

    void Start()
    {
        // Automatically finds Mission_Corridor_Map even when it is unchecked/hidden!
        missionCorridorMap = transform.Find("Mission_Corridor_Map");
        if (missionCorridorMap != null) missionCorridorMap.gameObject.SetActive(false);

        // Permanently destroy any legacy scrollbar/thumb/track remnants from the scene
        Transform legacyCat = transform.Find("Assembly_And_Catalog_Group");
        if (legacyCat != null)
        {
            for (int i = legacyCat.childCount - 1; i >= 0; i--)
            {
                Transform c = legacyCat.GetChild(i);
                if (c != null && (c.name.Contains("Scrollbar") || c.name.Contains("Thumb") || c.name.Contains("Track") || c.name.Contains("Selectable") || c.name == "Catalog_Header"))
                {
                    c.gameObject.SetActive(false);
                    Destroy(c.gameObject);
                }
            }
        }
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c != null && (c.name.Contains("Scrollbar") || c.name.Contains("Slider")))
            {
                c.gameObject.SetActive(false);
                Destroy(c.gameObject);
            }
        }

        InitializeAllSpaceParts();
        EnsureFairingHalvesCreated();
        EnsureRocketHardwareObjects();
        EnsureCategorizedPartsPanel();
        EnsureFlightConsoleCreated();
        SetupAttachedSprites();
        UpdateAllRocketHardwareVisuals();

        if (obstacleZones != null && obstacleZones.Length >= 3)
        {
            obstacleZones[0].localPosition = new Vector3(-0.3f, ObstacleYPositions[0], 0.3f);
            obstacleZones[1].localPosition = new Vector3(-0.3f, ObstacleYPositions[1], 0.3f);
            obstacleZones[2].localPosition = new Vector3(-0.3f, ObstacleYPositions[2], 0.3f);
        }
        if (orbitRingRoot != null)
        {
            orbitRingRoot.localPosition = new Vector3(-0.3f, OrbitCenterY, 0.25f);
            orbitRingRoot.localScale = new Vector3(1.4f, 1.4f, 1f);
        }
        Transform traj = missionCorridorMap != null ? missionCorridorMap.Find("Flight_Trajectory_Line") : null;
        if (traj != null && traj.GetComponent<LineRenderer>() != null)
        {
            traj.GetComponent<LineRenderer>().SetPosition(0, new Vector3(-0.3f, -1.44f, 0.4f));
            traj.GetComponent<LineRenderer>().SetPosition(1, new Vector3(-0.3f, OrbitCenterY - 1.2f, 0.4f));
        }
        
        EnsureAutoPresetButtonCreated();
        EnsureHomeButtonCreated();
        EnsureManualModalCreated();
        EnsureTitleScreenCreated();
        InitProceduralAudio();
        InitCrises();
        ResetToAssembly();
        ShowTitleScreen();
    }

    private void SetupAttachedSprites()
    {
        // 1. Hide old 3D placeholder cubes on the main hub
        string[] oldBoxes = { "Spacecraft_Core_Outline", "Spacecraft_Core_Body", "Spacecraft_Gold_Foil", "NASA_Rocket_Visuals" };
        foreach (string s in oldBoxes)
        {
            Transform old = spacecraftHub != null ? spacecraftHub.Find(s) : null;
            if (old != null) old.gameObject.SetActive(false);
        }

        // 2. Align Bus and Booster seamlessly with no gap
        if (spacecraftHub != null)
        {
            foreach (SpriteRenderer sr in spacecraftHub.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.name.Contains("Booster"))
                {
                    Sprite bSpr = Resources.Load<Sprite>("Part_Booster_Lower_Booster_Stage");
                    if (bSpr != null) sr.sprite = bSpr;
                    sr.sortingOrder = 8;
                    lowerBoosterStage = sr.transform;
                    sr.transform.localPosition = new Vector3(0f, -0.48f, -0.05f);
                    sr.transform.localScale = new Vector3(0.115f, 0.115f, 1f);
                }
                else if (sr.name.Contains("Bus"))
                {
                    Sprite busSpr = Resources.Load<Sprite>("Part_Bus_Sat_Gold_Bus");
                    if (busSpr != null) sr.sprite = busSpr;
                    sr.sortingOrder = 10;
                    sr.transform.localPosition = new Vector3(0f, 0.12f, -0.08f);
                    sr.transform.localScale = new Vector3(0.125f, 0.125f, 1f);
                }
            }
        }

        string[] partSpriteNames = {
            "Part_ROSA_PART_MESH_0",
            "Part_MMRTG_PART_MESH_1",
            "Part_Battery_PART_MESH_2",
            "Part_Dish_PART_MESH_3",
            "Part_Camera_PART_MESH_4",
            "Part_Ion_PART_MESH_5",
            "Part_THEMIS_PART_MESH_6"
        };

        // Clean 2D front-orthographic hardpoints centered around the Satellite Bus (Y = 0.18)
        Vector3[] cleanBusPositions = {
            new Vector3(0f, 0.18f, -0.06f),      // 0: ROSA Solar Wings
            new Vector3(-0.24f, 0.18f, -0.09f),  // 1: MMRTG Nuclear Core (Left mount)
            new Vector3(0f, 0.14f, -0.11f),      // 2: EaglePicher Battery (Core bay)
            new Vector3(0f, 0.38f, -0.09f),      // 3: DSN Parabolic Dish (Top deck)
            new Vector3(0f, 0.22f, -0.12f),      // 4: HiRISE Optical Camera (Center optics)
            new Vector3(0f, 0.05f, -0.07f),      // 5: NEXT-C Ion Thruster (Bus base adapter)
            new Vector3(0.24f, 0.18f, -0.12f)    // 6: THEMIS IR Spectrometer (Right mount)
        };

        float[] targetVisualScales = { 0.16f, 0.065f, 0.06f, 0.085f, 0.06f, 0.065f, 0.06f };

        if (parts != null)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].partTransform == null) continue;
                if (parts[i].partRenderer != null) parts[i].partRenderer.enabled = false;

                bool isOnRocket = (spacecraftHub != null && parts[i].partTransform.parent == spacecraftHub);

                if (isOnRocket && i < cleanBusPositions.Length)
                {
                    parts[i].partTransform.localPosition = cleanBusPositions[i];
                }

                SpriteRenderer sr = parts[i].partTransform.GetComponentInChildren<SpriteRenderer>(true);
                if (sr == null)
                {
                    GameObject sprObj = new GameObject("Part_Sprite_" + i);
                    sprObj.transform.SetParent(parts[i].partTransform, false);
                    sr = sprObj.AddComponent<SpriteRenderer>();
                }

                if (i < partSpriteNames.Length)
                {
                    sr.sprite = Resources.Load<Sprite>(partSpriteNames[i]);
                }

                if (i == 0) sr.sortingOrder = 7;
                else if (i == 5) sr.sortingOrder = 9;
                else sr.sortingOrder = 12;

                sr.transform.localPosition = new Vector3(0f, 0f, -0.1f);

                Vector3 pScale = parts[i].partTransform.localScale;
                float sx = Mathf.Abs(pScale.x) > 0.001f ? pScale.x : 1f;
                float sy = Mathf.Abs(pScale.y) > 0.001f ? pScale.y : 1f;
                float desired = isOnRocket ? targetVisualScales[i] : 0.065f;

                sr.transform.localScale = new Vector3(desired / sx, desired / sy, 1f);
            }
        }
    }

    private Transform lowerBoosterStage;
    private Transform fairingShell;
    private Transform fairingHalfLeft;
    private Transform fairingHalfRight;
    private bool fairingJettisoned = false;
    private float fairingJettisonTimer = 0f;
    private float solarDeployProgress = 1.0f;
    private bool boosterSeparated = false;
    private float boosterSepTimer = 0f;
    private Vector3 boosterBaseLocalPos = new Vector3(0f, -0.48f, -0.05f);
    private float currentAtmosphericStarAlpha = 1.0f;
    private ParticleSystem loxVaporPS;
    private ParticleSystem padLiftoffSmokePS;
    private AudioClip stagingPopClip;
    private AudioClip solarDeployClip;

    private GameObject CreateFairingPiece(Transform parent, string pieceName, Vector3 pos, Vector3 scale, float rotZ, Color col)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = pieceName;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        q.transform.localScale = scale;
        Destroy(q.GetComponent<Collider>());
        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        Material m = new Material(sh);
        m.color = col;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        Renderer r = q.GetComponent<Renderer>();
        r.material = m;
        r.sortingOrder = 15; // Renders on top of satellite bus and payload instruments
        return q;
    }

    private void SetFairingOpacity(float alpha)
    {
        if (fairingHalfLeft != null)
        {
            foreach (Renderer r in fairingHalfLeft.GetComponentsInChildren<Renderer>(true))
            {
                Color c = r.material.color;
                c.a = alpha;
                SetRendererColor(r, c);
            }
        }
        if (fairingHalfRight != null)
        {
            foreach (Renderer r in fairingHalfRight.GetComponentsInChildren<Renderer>(true))
            {
                Color c = r.material.color;
                c.a = alpha;
                SetRendererColor(r, c);
            }
        }
    }

    private void EnsureFairingHalvesCreated()
    {
        if (spacecraftHub == null) return;

        // Clean up previous fairing objects if any exist
        Transform oldAssembly = spacecraftHub.Find("Fairing_Assembly");
        if (oldAssembly != null) Destroy(oldAssembly.gameObject);
        Transform oldCutaway = spacecraftHub.Find("NASA_Rocket_Visuals/Fairing_Cutaway");
        if (oldCutaway != null) Destroy(oldCutaway.gameObject);

        GameObject fairingAssembly = new GameObject("Fairing_Assembly");
        fairingAssembly.transform.SetParent(spacecraftHub, false);
        fairingAssembly.transform.localPosition = Vector3.zero;
        fairingShell = fairingAssembly.transform;

        GameObject leftObj = new GameObject("Fairing_Half_Left");
        leftObj.transform.SetParent(fairingAssembly.transform, false);
        leftObj.transform.localPosition = Vector3.zero;
        fairingHalfLeft = leftObj.transform;

        GameObject rightObj = new GameObject("Fairing_Half_Right");
        rightObj.transform.SetParent(fairingAssembly.transform, false);
        rightObj.transform.localPosition = Vector3.zero;
        fairingHalfRight = rightObj.transform;

        // LEFT FAIRING HALF (Cylindrical barrel + aerodynamic ogive cone + seal + NASA insignia)
        CreateFairingPiece(leftObj.transform, "Fairing_Barrel_L", new Vector3(-0.14f, 0.14f, -0.16f), new Vector3(0.28f, 0.44f, 1f), 0f, new Color(0.95f, 0.96f, 0.98f));
        CreateFairingPiece(leftObj.transform, "Fairing_Ogive_L",  new Vector3(-0.09f, 0.48f, -0.16f), new Vector3(0.20f, 0.32f, 1f), -20f, new Color(0.95f, 0.96f, 0.98f));
        CreateFairingPiece(leftObj.transform, "Fairing_Tip_L",    new Vector3(-0.025f, 0.64f, -0.16f), new Vector3(0.06f, 0.08f, 1f), 0f, new Color(0.85f, 0.88f, 0.92f));
        CreateFairingPiece(leftObj.transform, "Fairing_Seal_L",   new Vector3(-0.14f, -0.08f, -0.165f), new Vector3(0.28f, 0.04f, 1f), 0f, new Color(0.18f, 0.22f, 0.28f));
        CreateFairingPiece(leftObj.transform, "Fairing_Stripe_L", new Vector3(-0.14f, 0.18f, -0.165f), new Vector3(0.26f, 0.04f, 1f), 0f, new Color(0.85f, 0.20f, 0.20f));
        CreateFairingPiece(leftObj.transform, "Fairing_Shadow_L", new Vector3(-0.25f, 0.14f, -0.165f), new Vector3(0.04f, 0.44f, 1f), 0f, new Color(0.78f, 0.82f, 0.88f));

        // RIGHT FAIRING HALF (Symmetric aerodynamic half)
        CreateFairingPiece(rightObj.transform, "Fairing_Barrel_R", new Vector3(0.14f, 0.14f, -0.16f), new Vector3(0.28f, 0.44f, 1f), 0f, new Color(0.95f, 0.96f, 0.98f));
        CreateFairingPiece(rightObj.transform, "Fairing_Ogive_R",  new Vector3(0.09f, 0.48f, -0.16f), new Vector3(0.20f, 0.32f, 1f), 20f, new Color(0.95f, 0.96f, 0.98f));
        CreateFairingPiece(rightObj.transform, "Fairing_Tip_R",    new Vector3(0.025f, 0.64f, -0.16f), new Vector3(0.06f, 0.08f, 1f), 0f, new Color(0.85f, 0.88f, 0.92f));
        CreateFairingPiece(rightObj.transform, "Fairing_Seal_R",   new Vector3(0.14f, -0.08f, -0.165f), new Vector3(0.28f, 0.04f, 1f), 0f, new Color(0.18f, 0.22f, 0.28f));
        CreateFairingPiece(rightObj.transform, "Fairing_Stripe_R", new Vector3(0.14f, 0.18f, -0.165f), new Vector3(0.26f, 0.04f, 1f), 0f, new Color(0.85f, 0.20f, 0.20f));
        CreateFairingPiece(rightObj.transform, "Fairing_Highlight_R", new Vector3(0.03f, 0.14f, -0.165f), new Vector3(0.04f, 0.44f, 1f), 0f, new Color(1f, 1f, 1f, 0.85f));

        SetFairingOpacity(0.25f); // Start in translucent cutaway blueprint mode for assembly
    }

    private void UpdateFairingAndStagingAnimation(float dt)
    {
        if (spacecraftHub == null) return;
        float currentY = spacecraftHub.localPosition.y;
        bool isAscending = (currentPhase == MissionPhase.Launching || currentPhase == MissionPhase.FlightCrisis);

        // 1. FAIRING JETTISON AT VACUUM THRESHOLD (Above Max-Q: Y >= 9.2f)
        if (isAscending && currentY >= 9.2f && !fairingJettisoned)
        {
            fairingJettisoned = true;
            fairingJettisonTimer = 0f;
            PlayStagingAudio();
            PlaySolarDeployAudio();
            SetStatusBanner("★ FAIRING JETTISON CONFIRMED // SOLAR ARRAYS DEPLOYED IN VACUUM ★", "Vehicle cleared supersonic atmosphere! Fairing halves separated & ROSA solar wings deploying!", DuoGreen);
        }

        if (fairingJettisoned)
        {
            fairingJettisonTimer += dt;
            if (fairingHalfLeft != null)
            {
                fairingHalfLeft.localPosition += new Vector3(-2.6f * dt, -1.8f * dt, 0f);
                fairingHalfLeft.localRotation *= Quaternion.Euler(0f, 0f, 50f * dt);
            }
            if (fairingHalfRight != null)
            {
                fairingHalfRight.localPosition += new Vector3(2.6f * dt, -1.8f * dt, 0f);
                fairingHalfRight.localRotation *= Quaternion.Euler(0f, 0f, -50f * dt);
            }

            float fade = Mathf.Clamp01(1.0f - (fairingJettisonTimer / 1.8f));
            SetFairingOpacity(fade);

            if (fairingJettisonTimer >= 1.8f)
            {
                if (fairingHalfLeft != null && fairingHalfLeft.gameObject.activeSelf)
                    fairingHalfLeft.gameObject.SetActive(false);
                if (fairingHalfRight != null && fairingHalfRight.gameObject.activeSelf)
                    fairingHalfRight.gameObject.SetActive(false);
            }

            // Smoothly roll out ROSA solar wings in space vacuum!
            solarDeployProgress = Mathf.MoveTowards(solarDeployProgress, 1.0f, dt * 0.75f);
        }

        // Apply solar array wing deployment scale
        if (parts != null && parts.Count > 0 && parts[0] != null && parts[0].partTransform != null)
        {
            float foldedX = parts[0].mountedScale.x * 0.18f;
            float targetX = Mathf.Lerp(foldedX, parts[0].mountedScale.x, Mathf.SmoothStep(0f, 1f, solarDeployProgress));
            parts[0].partTransform.localScale = new Vector3(targetX, parts[0].mountedScale.y, parts[0].mountedScale.z);
        }

        // 2. LOWER BOOSTER STAGING (MECO & Separation at Y >= 11.5f)
        if (isAscending && currentY >= 11.5f && !boosterSeparated)
        {
            boosterSeparated = true;
            boosterSepTimer = 0f;
            PlayStagingAudio();
            SetStatusBanner("★ MECO & FIRST STAGE SEPARATION CONFIRMED ★", "Main engine cutoff. Booster detached. Coasting to orbital insertion on upper stage!", DuoGreen);
        }

        if (boosterSeparated)
        {
            boosterSepTimer += dt;
            if (lowerBoosterStage != null)
            {
                lowerBoosterStage.localPosition = boosterBaseLocalPos + new Vector3(0f, -boosterSepTimer * 3.6f, 0f);
                float bFade = Mathf.Clamp01(1.0f - (boosterSepTimer / 2.0f));
                foreach (SpriteRenderer sr in lowerBoosterStage.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    Color c = sr.color;
                    c.a = bFade;
                    sr.color = c;
                }
                if (boosterSepTimer >= 2.0f && lowerBoosterStage.gameObject.activeSelf)
                {
                    lowerBoosterStage.gameObject.SetActive(false);
                }
            }
        }
    }

    private void UpdateAtmosphericSky(float rocketY)
    {
        if (Camera.main == null) return;
        if (Camera.main.clearFlags != CameraClearFlags.SolidColor) Camera.main.clearFlags = CameraClearFlags.SolidColor;

        Color spaceDark = new Color(0.012f, 0.018f, 0.032f);
        Color seaLevelDay = new Color(0.14f, 0.44f, 0.72f);   // Coastal daylight azure
        Color stratosphere = new Color(0.06f, 0.18f, 0.42f); // Deep navy blue
        Color mesosphere = new Color(0.025f, 0.055f, 0.18f); // Dark violet indigo

        Color targetSky;
        float starFactor;

        if (currentPhase == MissionPhase.TitleScreen)
        {
            targetSky = spaceDark;
            starFactor = 1.0f;
        }
        else if (currentPhase == MissionPhase.Assembly)
        {
            targetSky = new Color(0.12f, 0.36f, 0.62f); // Crisp launchpad morning sky
            starFactor = 0.08f; // Daylight washes out stars
        }
        else // Countdown, Launching, FlightCrisis, OrbitSimulation, Completed
        {
            float padY = LaunchpadY;
            if (rocketY <= padY + 1.0f)
            {
                targetSky = seaLevelDay;
                starFactor = 0.08f;
            }
            else if (rocketY < 4.0f)
            {
                float t = (rocketY - (padY + 1.0f)) / (4.0f - (padY + 1.0f));
                targetSky = Color.Lerp(seaLevelDay, stratosphere, t);
                starFactor = Mathf.Lerp(0.08f, 0.35f, t);
            }
            else if (rocketY < 8.0f)
            {
                float t = (rocketY - 4.0f) / 4.0f;
                targetSky = Color.Lerp(stratosphere, mesosphere, t);
                starFactor = Mathf.Lerp(0.35f, 0.75f, t);
            }
            else if (rocketY < 12.0f)
            {
                float t = (rocketY - 8.0f) / 4.0f;
                targetSky = Color.Lerp(mesosphere, spaceDark, t);
                starFactor = Mathf.Lerp(0.75f, 1.0f, t);
            }
            else
            {
                targetSky = spaceDark;
                starFactor = 1.0f;
            }
        }

        Camera.main.backgroundColor = Color.Lerp(Camera.main.backgroundColor, targetSky, Time.deltaTime * 4.0f);
        currentAtmosphericStarAlpha = Mathf.Lerp(currentAtmosphericStarAlpha, starFactor, Time.deltaTime * 4.0f);
    }

    private void UpdateLoxBoilOffVapor()
    {
        if (spacecraftHub == null) return;

        if (loxVaporPS == null)
        {
            GameObject loxObj = new GameObject("LOX_BoilOff_Vapor");
            loxObj.transform.SetParent(spacecraftHub, false);
            loxObj.transform.localPosition = new Vector3(0f, -0.18f, -0.10f);
            loxObj.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            loxVaporPS = loxObj.AddComponent<ParticleSystem>();
            var main = loxVaporPS.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;

            var emission = loxVaporPS.emission;
            emission.rateOverTime = 22f;

            var shape = loxVaporPS.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.34f, 0.05f, 0.05f);

            var colorOverLifetime = loxVaporPS.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.92f, 0.96f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.85f, 0.92f, 0.98f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.40f, 0.0f),
                    new GradientAlphaKey(0.25f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = loxVaporPS.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.6f);
            curve.AddKey(0.4f, 1.2f);
            curve.AddKey(1.0f, 2.0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var psRenderer = loxObj.GetComponent<ParticleSystemRenderer>();
            Shader particleShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            psRenderer.material = new Material(particleShader);
            psRenderer.sortingOrder = 16;
        }

        bool shouldVent = (currentPhase == MissionPhase.Assembly) || 
                          (currentPhase == MissionPhase.Countdown && countdownTimer > 1.0f);
        var em = loxVaporPS.emission;
        em.enabled = shouldVent;
    }

    private void UpdatePadLiftoffSmoke()
    {
        if (assemblyGantryGroup == null) return;

        if (padLiftoffSmokePS == null)
        {
            GameObject smokeObj = new GameObject("Pad_Liftoff_Smoke");
            smokeObj.transform.SetParent(assemblyGantryGroup.transform, false);
            smokeObj.transform.localPosition = new Vector3(0f, -2.08f, -0.15f);
            smokeObj.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            padLiftoffSmokePS = smokeObj.AddComponent<ParticleSystem>();
            var main = padLiftoffSmokePS.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 180;

            var emission = padLiftoffSmokePS.emission;
            emission.rateOverTime = 120f;

            var shape = padLiftoffSmokePS.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.8f, 0.2f, 0.1f);

            var colorOverLifetime = padLiftoffSmokePS.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.92f, 0.94f, 0.97f), 0.0f),
                    new GradientColorKey(new Color(0.70f, 0.74f, 0.80f), 0.6f),
                    new GradientColorKey(new Color(0.50f, 0.54f, 0.60f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.60f, 0.0f),
                    new GradientAlphaKey(0.40f, 0.5f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = padLiftoffSmokePS.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.8f);
            curve.AddKey(0.5f, 1.8f);
            curve.AddKey(1.0f, 3.2f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var psRenderer = smokeObj.GetComponent<ParticleSystemRenderer>();
            Shader particleShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            psRenderer.material = new Material(particleShader);
            psRenderer.sortingOrder = 6;
        }

        float altAbovePad = (spacecraftHub != null) ? (spacecraftHub.position.y - LaunchpadY) : 99f;
        bool shouldSmoke = (currentPhase == MissionPhase.Countdown && countdownTimer <= 1.0f) ||
                           (currentPhase == MissionPhase.Launching && altAbovePad >= 0f && altAbovePad < 3.2f);
        var em = padLiftoffSmokePS.emission;
        em.enabled = shouldSmoke;
    }

    private void InitializeAllSpaceParts()
    {
        // Real NASA Launch Services Program (LSP) Rockets
        rocketNames = new string[] {
            "Minotaur IV Lite ($40M | 1800kg)",
            "Falcon 9 NASA-LSP ($80M | 3100kg)",
            "NASA SLS Block 1B ($150M | 4500kg)"
        };
        rocketCostsM = new float[] { 40f, 80f, 150f };
        rocketMaxMassKg = new float[] { 1800f, 3100f, 4500f };

        if (parts == null) parts = new List<PartSpec>();

        while (parts.Count < 14)
        {
            parts.Add(new PartSpec());
        }

        // 0: ROSA Roll-Out Solar
        parts[0].partTitle = "ROSA Roll-Out Solar";
        parts[0].category = "Power";
        parts[0].massKg = 60f;
        parts[0].costMillion = 35f;
        parts[0].powerOutputW = 650f;
        parts[0].powerDrawW = 0f;
        parts[0].sciencePoints = 0f;
        parts[0].installedColor = new Color(0.20f, 0.55f, 0.95f);
        parts[0].catalogPosition = new Vector3(-6.35f, 1.40f, 0f);
        parts[0].busLocalOffset = new Vector3(0f, 0.18f, -0.06f);
        parts[0].catalogScale = new Vector3(0.16f, 0.16f, 1f);
        parts[0].mountedScale = new Vector3(1.45f, 0.16f, 0.1f);

        // 1: MMRTG Pu-238 Core
        parts[1].partTitle = "MMRTG Pu-238 Core";
        parts[1].category = "Power";
        parts[1].massKg = 185f;
        parts[1].costMillion = 135f;
        parts[1].powerOutputW = 380f;
        parts[1].powerDrawW = 0f;
        parts[1].sciencePoints = 15f;
        parts[1].installedColor = new Color(0.95f, 0.50f, 0.10f);
        parts[1].catalogPosition = new Vector3(-6.35f, 0.35f, 0f);
        parts[1].busLocalOffset = new Vector3(-0.24f, 0.18f, -0.09f);
        parts[1].catalogScale = new Vector3(0.065f, 0.065f, 1f);
        parts[1].mountedScale = new Vector3(0.18f, 0.22f, 0.1f);

        // 2: EaglePicher Li-Ion Battery
        parts[2].partTitle = "EaglePicher Li-Ion Bat";
        parts[2].category = "Battery";
        parts[2].massKg = 55f;
        parts[2].costMillion = 25f;
        parts[2].powerOutputW = 0f;
        parts[2].powerDrawW = 0f;
        parts[2].sciencePoints = 0f;
        parts[2].installedColor = new Color(0.25f, 0.85f, 0.40f);
        parts[2].catalogPosition = new Vector3(-6.35f, -0.70f, 0f);
        parts[2].busLocalOffset = new Vector3(0f, 0.14f, -0.11f);
        parts[2].catalogScale = new Vector3(0.06f, 0.06f, 1f);
        parts[2].mountedScale = new Vector3(0.28f, 0.18f, 0.1f);

        // 3: DSN Ka-Band Dish
        parts[3].partTitle = "DSN Ka-Band Dish";
        parts[3].category = "Comms";
        parts[3].massKg = 65f;
        parts[3].costMillion = 65f;
        parts[3].powerOutputW = 0f;
        parts[3].powerDrawW = 100f;
        parts[3].sciencePoints = 25f;
        parts[3].installedColor = new Color(0.85f, 0.90f, 0.98f);
        parts[3].catalogPosition = new Vector3(-6.35f, 2.45f, 0f);
        parts[3].busLocalOffset = new Vector3(0f, 0.38f, -0.09f);
        parts[3].catalogScale = new Vector3(0.085f, 0.085f, 1f);
        parts[3].mountedScale = new Vector3(0.38f, 0.12f, 0.1f);

        // 4: HiRISE Optical Camera
        parts[4].partTitle = "HiRISE Optical Camera";
        parts[4].category = "Science";
        parts[4].massKg = 175f;
        parts[4].costMillion = 145f;
        parts[4].powerOutputW = 0f;
        parts[4].powerDrawW = 150f;
        parts[4].sciencePoints = 60f;
        parts[4].installedColor = new Color(0.35f, 0.45f, 0.95f);
        parts[4].catalogPosition = new Vector3(-6.35f, 0.35f, 0f);
        parts[4].busLocalOffset = new Vector3(0f, 0.22f, -0.12f);
        parts[4].catalogScale = new Vector3(0.06f, 0.06f, 1f);
        parts[4].mountedScale = new Vector3(0.14f, 0.14f, 0.1f);

        // 5: NEXT-C Ion Thruster
        parts[5].partTitle = "NEXT-C Ion Thruster";
        parts[5].category = "Propulsion";
        parts[5].massKg = 245f;
        parts[5].costMillion = 160f;
        parts[5].powerOutputW = 0f;
        parts[5].powerDrawW = 240f;
        parts[5].sciencePoints = 0f;
        parts[5].installedColor = new Color(0.15f, 0.85f, 0.95f);
        parts[5].catalogPosition = new Vector3(-6.35f, 1.40f, 0f);
        parts[5].busLocalOffset = new Vector3(0f, 0.05f, -0.07f);
        parts[5].catalogScale = new Vector3(0.065f, 0.065f, 1f);
        parts[5].mountedScale = new Vector3(0.22f, 0.22f, 0.1f);

        // 6: THEMIS IR Spectrometer
        parts[6].partTitle = "THEMIS IR Spectrometer";
        parts[6].category = "Science";
        parts[6].massKg = 60f;
        parts[6].costMillion = 50f;
        parts[6].powerOutputW = 0f;
        parts[6].powerDrawW = 65f;
        parts[6].sciencePoints = 35f;
        parts[6].installedColor = new Color(0.95f, 0.35f, 0.35f);
        parts[6].catalogPosition = new Vector3(-6.35f, -0.70f, 0f);
        parts[6].busLocalOffset = new Vector3(0.24f, 0.18f, -0.12f);
        parts[6].catalogScale = new Vector3(0.06f, 0.06f, 1f);
        parts[6].mountedScale = new Vector3(0.14f, 0.14f, 0.1f);

        // 7: Kerolox Booster Stage
        parts[7].partTitle = "Kerolox Booster Stage";
        parts[7].category = "Propulsion";
        parts[7].massKg = 850f;
        parts[7].costMillion = 140f;
        parts[7].powerOutputW = 0f;
        parts[7].powerDrawW = 0f;
        parts[7].sciencePoints = 0f;
        parts[7].installedColor = Color.white;
        parts[7].catalogPosition = new Vector3(-6.35f, 2.45f, 0f);
        parts[7].busLocalOffset = new Vector3(0f, -0.48f, -0.05f);
        parts[7].catalogScale = new Vector3(0.10f, 0.10f, 1f);
        parts[7].mountedScale = new Vector3(0.115f, 0.115f, 1f);

        // 8: Twin Strap-On SRBs
        parts[8].partTitle = "Twin Strap-On SRBs";
        parts[8].category = "Booster";
        parts[8].massKg = 350f;
        parts[8].costMillion = 45f;
        parts[8].powerOutputW = 0f;
        parts[8].powerDrawW = 0f;
        parts[8].sciencePoints = 0f;
        parts[8].installedColor = new Color(0.92f, 0.94f, 0.98f);
        parts[8].catalogPosition = new Vector3(-6.35f, 1.40f, 0f);
        parts[8].busLocalOffset = new Vector3(0f, -0.48f, -0.04f);
        parts[8].catalogScale = new Vector3(0.10f, 0.10f, 1f);
        parts[8].mountedScale = Vector3.one;

        // 9: Vacuum Upper Stage (S-II)
        parts[9].partTitle = "Vacuum Upper Stage S-II";
        parts[9].category = "Propulsion";
        parts[9].massKg = 280f;
        parts[9].costMillion = 60f;
        parts[9].powerOutputW = 0f;
        parts[9].powerDrawW = 0f;
        parts[9].sciencePoints = 0f;
        parts[9].installedColor = new Color(0.70f, 0.75f, 0.85f);
        parts[9].catalogPosition = new Vector3(-6.35f, 0.35f, 0f);
        parts[9].busLocalOffset = new Vector3(0f, -0.15f, -0.06f);
        parts[9].catalogScale = new Vector3(0.10f, 0.10f, 1f);
        parts[9].mountedScale = Vector3.one;

        // 10: Titanium Grid Fins
        parts[10].partTitle = "Titanium Grid Fins";
        parts[10].category = "Aero";
        parts[10].massKg = 35f;
        parts[10].costMillion = 15f;
        parts[10].powerOutputW = 0f;
        parts[10].powerDrawW = 0f;
        parts[10].sciencePoints = 0f;
        parts[10].installedColor = new Color(0.40f, 0.45f, 0.55f);
        parts[10].catalogPosition = new Vector3(-6.35f, -0.70f, 0f);
        parts[10].busLocalOffset = new Vector3(0f, -0.22f, -0.07f);
        parts[10].catalogScale = new Vector3(0.10f, 0.10f, 1f);
        parts[10].mountedScale = Vector3.one;

        // 11: Payload Fairing Shroud
        parts[11].partTitle = "Payload Fairing Shroud";
        parts[11].category = "Aero";
        parts[11].massKg = 65f;
        parts[11].costMillion = 25f;
        parts[11].powerOutputW = 0f;
        parts[11].powerDrawW = 0f;
        parts[11].sciencePoints = 0f;
        parts[11].installedColor = Color.white;
        parts[11].catalogPosition = new Vector3(-6.35f, -1.75f, 0f);
        parts[11].busLocalOffset = Vector3.zero;
        parts[11].catalogScale = new Vector3(0.10f, 0.10f, 1f);
        parts[11].mountedScale = Vector3.one;

        // 12: Satellite Avionics Bus
        parts[12].partTitle = "Satellite Avionics Bus";
        parts[12].category = "Structure";
        parts[12].massKg = 120f;
        parts[12].costMillion = 40f;
        parts[12].powerOutputW = 0f;
        parts[12].powerDrawW = 15f;
        parts[12].sciencePoints = 0f;
        parts[12].installedColor = new Color(0.95f, 0.78f, 0.20f);
        parts[12].catalogPosition = new Vector3(-6.35f, 2.45f, 0f);
        parts[12].busLocalOffset = new Vector3(0f, 0.18f, -0.08f);
        parts[12].catalogScale = new Vector3(0.10f, 0.10f, 1f);
        parts[12].mountedScale = new Vector3(0.125f, 0.125f, 1f);

        // 13: Fluxgate Magnetometer
        parts[13].partTitle = "Fluxgate Magnetometer";
        parts[13].category = "Science";
        parts[13].massKg = 25f;
        parts[13].costMillion = 30f;
        parts[13].powerOutputW = 0f;
        parts[13].powerDrawW = 20f;
        parts[13].sciencePoints = 25f;
        parts[13].installedColor = new Color(0.80f, 0.40f, 0.90f);
        parts[13].catalogPosition = new Vector3(-6.35f, -1.75f, 0f);
        parts[13].busLocalOffset = new Vector3(-0.35f, 0.26f, -0.10f);
        parts[13].catalogScale = new Vector3(0.08f, 0.08f, 1f);
        parts[13].mountedScale = Vector3.one;

        // CRUCIAL: All parts start UNINSTALLED so the rocket starts from 0!
        for (int i = 0; i < parts.Count; i++)
        {
            parts[i].isInstalled = false;
        }
    }

    private void EnsureRocketHardwareObjects()
    {
        if (spacecraftHub == null) return;

        // 1. Lower Booster Stage
        if (lowerBoosterStage == null)
        {
            Transform b = spacecraftHub.Find("Part_Booster_Lower_Booster_Stage");
            if (b == null)
            {
                GameObject bg = new GameObject("Part_Booster_Lower_Booster_Stage");
                bg.transform.SetParent(spacecraftHub, false);
                bg.transform.localPosition = new Vector3(0f, -0.48f, -0.05f);
                CreateUIQuad(bg.transform, "Booster_Core_Body", Vector3.zero, new Vector3(0.38f, 0.65f, 1f), Color.white, false, 8);
                CreateUIQuad(bg.transform, "Booster_Interstage", new Vector3(0f, 0.28f, -0.01f), new Vector3(0.38f, 0.08f, 1f), new Color(0.18f, 0.20f, 0.24f), false, 9);
                CreateUIQuad(bg.transform, "Booster_Stripe", new Vector3(0f, 0.05f, -0.01f), new Vector3(0.38f, 0.04f, 1f), new Color(0.85f, 0.15f, 0.15f), false, 9);
                CreateUIQuad(bg.transform, "Booster_Nozzle", new Vector3(0f, -0.36f, -0.01f), new Vector3(0.24f, 0.14f, 1f), new Color(0.35f, 0.38f, 0.44f), false, 9);
                lowerBoosterStage = bg.transform;
            }
            else
            {
                lowerBoosterStage = b;
            }
        }

        // 2. Twin Solid Rocket Boosters (SRBs)
        if (srbGroup == null)
        {
            Transform s = spacecraftHub.Find("Twin_SRB_Group");
            if (s == null)
            {
                GameObject sg = new GameObject("Twin_SRB_Group");
                sg.transform.SetParent(spacecraftHub, false);
                sg.transform.localPosition = Vector3.zero;

                // Left SRB
                GameObject leftSrb = new GameObject("Left_SRB");
                leftSrb.transform.SetParent(sg.transform, false);
                leftSrb.transform.localPosition = new Vector3(-0.32f, -0.48f, -0.04f);
                CreateUIQuad(leftSrb.transform, "SRB_Body_L", Vector3.zero, new Vector3(0.13f, 0.64f, 1f), new Color(0.92f, 0.94f, 0.98f), false, 8);
                CreateUIQuad(leftSrb.transform, "SRB_Nose_L", new Vector3(0f, 0.34f, 0f), new Vector3(0.13f, 0.12f, 1f), new Color(0.25f, 0.28f, 0.35f), false, 9);
                CreateUIQuad(leftSrb.transform, "SRB_Nozzle_L", new Vector3(0f, -0.34f, 0f), new Vector3(0.11f, 0.08f, 1f), new Color(0.35f, 0.38f, 0.45f), false, 9);
                CreateUIQuad(leftSrb.transform, "SRB_Strut_L", new Vector3(0.08f, 0.15f, 0f), new Vector3(0.06f, 0.04f, 1f), new Color(0.40f, 0.45f, 0.50f), false, 7);

                // Right SRB
                GameObject rightSrb = new GameObject("Right_SRB");
                rightSrb.transform.SetParent(sg.transform, false);
                rightSrb.transform.localPosition = new Vector3(0.32f, -0.48f, -0.04f);
                CreateUIQuad(rightSrb.transform, "SRB_Body_R", Vector3.zero, new Vector3(0.13f, 0.64f, 1f), new Color(0.92f, 0.94f, 0.98f), false, 8);
                CreateUIQuad(rightSrb.transform, "SRB_Nose_R", new Vector3(0f, 0.34f, 0f), new Vector3(0.13f, 0.12f, 1f), new Color(0.25f, 0.28f, 0.35f), false, 9);
                CreateUIQuad(rightSrb.transform, "SRB_Nozzle_R", new Vector3(0f, -0.34f, 0f), new Vector3(0.11f, 0.08f, 1f), new Color(0.35f, 0.38f, 0.45f), false, 9);
                CreateUIQuad(rightSrb.transform, "SRB_Strut_R", new Vector3(-0.08f, 0.15f, 0f), new Vector3(0.06f, 0.04f, 1f), new Color(0.40f, 0.45f, 0.50f), false, 7);

                srbGroup = sg.transform;
            }
            else
            {
                srbGroup = s;
            }
        }

        // 3. Vacuum Upper Stage (S-II)
        if (upperStageGroup == null)
        {
            Transform u = spacecraftHub.Find("Vacuum_Upper_Stage_Group");
            if (u == null)
            {
                GameObject ug = new GameObject("Vacuum_Upper_Stage_Group");
                ug.transform.SetParent(spacecraftHub, false);
                ug.transform.localPosition = new Vector3(0f, -0.12f, -0.06f);
                CreateUIQuad(ug.transform, "Upper_Tank", Vector3.zero, new Vector3(0.34f, 0.24f, 1f), new Color(0.72f, 0.78f, 0.88f), false, 9);
                CreateUIQuad(ug.transform, "Upper_Band", new Vector3(0f, 0.06f, -0.01f), new Vector3(0.34f, 0.03f, 1f), new Color(0.20f, 0.45f, 0.75f), false, 10);
                CreateUIQuad(ug.transform, "Upper_Nozzle", new Vector3(0f, -0.15f, 0.01f), new Vector3(0.20f, 0.12f, 1f), new Color(0.40f, 0.44f, 0.50f), false, 8);
                upperStageGroup = ug.transform;
            }
            else
            {
                upperStageGroup = u;
            }
        }

        // 4. Titanium Grid Fins
        if (gridFinsGroup == null)
        {
            Transform g = spacecraftHub.Find("Titanium_Grid_Fins_Group");
            if (g == null)
            {
                GameObject gg = new GameObject("Titanium_Grid_Fins_Group");
                gg.transform.SetParent(spacecraftHub, false);
                gg.transform.localPosition = new Vector3(0f, -0.22f, -0.07f);
                CreateUIQuad(gg.transform, "Fin_Left", new Vector3(-0.21f, 0f, 0f), new Vector3(0.07f, 0.10f, 1f), new Color(0.38f, 0.42f, 0.50f), false, 11);
                CreateUIQuad(gg.transform, "Fin_Right", new Vector3(0.21f, 0f, 0f), new Vector3(0.07f, 0.10f, 1f), new Color(0.38f, 0.42f, 0.50f), false, 11);
                CreateUIQuad(gg.transform, "Fin_CenterL", new Vector3(-0.07f, 0f, -0.01f), new Vector3(0.06f, 0.08f, 1f), new Color(0.48f, 0.52f, 0.60f), false, 11);
                CreateUIQuad(gg.transform, "Fin_CenterR", new Vector3(0.07f, 0f, -0.01f), new Vector3(0.06f, 0.08f, 1f), new Color(0.48f, 0.52f, 0.60f), false, 11);
                gridFinsGroup = gg.transform;
            }
            else
            {
                gridFinsGroup = g;
            }
        }

        // 5. Satellite Avionics Bus Chassis
        Transform sceneBus = spacecraftHub.Find("Part_Bus_Sat_Gold_Bus");
        Transform procBus = spacecraftHub.Find("Satellite_Bus_Avionics");
        if (sceneBus != null)
        {
            satelliteBusTransform = sceneBus;
            if (procBus != null) Destroy(procBus.gameObject);
        }
        else if (procBus != null)
        {
            satelliteBusTransform = procBus;
        }
        else
        {
            GameObject bg = new GameObject("Satellite_Bus_Avionics");
            bg.transform.SetParent(spacecraftHub, false);
            bg.transform.localPosition = new Vector3(0f, 0.18f, -0.08f);
            CreateUIQuad(bg.transform, "Sat_Gold_Chassis", Vector3.zero, new Vector3(0.38f, 0.34f, 1f), new Color(0.95f, 0.78f, 0.20f), false, 10);
            CreateUIQuad(bg.transform, "Sat_Reaction_Wheel", new Vector3(0f, 0.08f, -0.01f), new Vector3(0.18f, 0.06f, 1f), new Color(0.25f, 0.28f, 0.35f), false, 11);
            CreateUIQuad(bg.transform, "Sat_Star_Tracker", new Vector3(0.12f, 0.12f, -0.01f), new Vector3(0.06f, 0.06f, 1f), new Color(0.15f, 0.65f, 0.95f), false, 11);
            CreateUIQuad(bg.transform, "Sat_Adapter_Ring", new Vector3(0f, -0.18f, 0.01f), new Vector3(0.28f, 0.04f, 1f), new Color(0.40f, 0.44f, 0.50f), false, 9);
            satelliteBusTransform = bg.transform;
        }

        // 6. Fluxgate Magnetometer Boom
        if (magnetometerTransform == null)
        {
            Transform m = spacecraftHub.Find("Magnetometer_Boom_Assembly");
            if (m == null)
            {
                GameObject mg = new GameObject("Magnetometer_Boom_Assembly");
                mg.transform.SetParent(spacecraftHub, false);
                mg.transform.localPosition = new Vector3(-0.32f, 0.26f, -0.10f);
                CreateUIQuad(mg.transform, "Mag_Mast", Vector3.zero, new Vector3(0.28f, 0.025f, 1f), new Color(0.70f, 0.75f, 0.85f), false, 12);
                CreateUIQuad(mg.transform, "Mag_Sensor_Cube", new Vector3(-0.15f, 0f, -0.01f), new Vector3(0.06f, 0.06f, 1f), new Color(0.85f, 0.40f, 0.95f), false, 13);
                magnetometerTransform = mg.transform;
            }
            else
            {
                magnetometerTransform = m;
            }
        }

        // 7. Clean up any empty pad cradle or floating text
        Transform c = spacecraftHub.Find("Empty_Pad_Assembly_Cradle");
        if (c != null) Destroy(c.gameObject);

        // 8. Clean up any legacy shield bubble
        Transform sh = spacecraftHub.Find("Flight_Magnetic_Shield_Bubble");
        if (sh != null) Destroy(sh.gameObject);

        // 9. Directional Cold-Gas Reaction Control System (RCS) Thruster Blocks & Nitrogen Plumes
        Transform oldRc = spacecraftHub.Find("Flight_RCS_Jets_Visual");
        if (oldRc != null) Destroy(oldRc.gameObject);
        flightRcsVisual = null;

        GameObject rObj = new GameObject("Flight_RCS_Jets_Visual");
        rObj.transform.SetParent(spacecraftHub, false);
        rObj.transform.localPosition = new Vector3(0f, 0.18f, -0.06f);

        // Small dark titanium RCS nozzle thruster blocks on fairing interstage
        CreateUIQuad(rObj.transform, "RCS_Block_L", new Vector3(-0.21f, 0f, 0.01f), new Vector3(0.06f, 0.04f, 1f), new Color(0.25f, 0.28f, 0.35f), false, 21);
        CreateUIQuad(rObj.transform, "RCS_Block_R", new Vector3(0.21f, 0f, 0.01f), new Vector3(0.06f, 0.04f, 1f), new Color(0.25f, 0.28f, 0.35f), false, 21);

        // Directional cold-gas nitrogen expansion vapor plumes (soft cyan/white mist)
        GameObject pL = CreateUIQuad(rObj.transform, "RCS_Plume_L", new Vector3(-0.32f, 0f, 0f), new Vector3(0.18f, 0.07f, 1f), new Color(0.85f, 0.95f, 1.0f, 0.80f), false, 22);
        GameObject pR = CreateUIQuad(rObj.transform, "RCS_Plume_R", new Vector3(0.32f, 0f, 0f), new Vector3(0.18f, 0.07f, 1f), new Color(0.85f, 0.95f, 1.0f, 0.80f), false, 22);

        rcsPlumeL = pL.transform;
        rcsPlumeR = pR.transform;
        rcsPlumeL.gameObject.SetActive(false);
        rcsPlumeR.gameObject.SetActive(false);

        rObj.SetActive(true);
        flightRcsVisual = rObj;
    }

    public void UpdateAllRocketHardwareVisuals()
    {
        if (spacecraftHub == null) return;

        bool hasBooster = (parts != null && parts.Count > 7 && parts[7] != null && parts[7].isInstalled);
        bool hasSrbs = (parts != null && parts.Count > 8 && parts[8] != null && parts[8].isInstalled);
        bool hasUpperStage = (parts != null && parts.Count > 9 && parts[9] != null && parts[9].isInstalled);
        bool hasGridFins = (parts != null && parts.Count > 10 && parts[10] != null && parts[10].isInstalled);
        bool hasFairing = (parts != null && parts.Count > 11 && parts[11] != null && parts[11].isInstalled);
        bool hasBus = (parts != null && parts.Count > 12 && parts[12] != null && parts[12].isInstalled);
        bool hasMag = (parts != null && parts.Count > 13 && parts[13] != null && parts[13].isInstalled);

        if (lowerBoosterStage != null) lowerBoosterStage.gameObject.SetActive(hasBooster && !stage1Separated);
        Transform sceneBooster = spacecraftHub.Find("Part_Booster_Lower_Booster_Stage");
        if (sceneBooster != null) sceneBooster.gameObject.SetActive(hasBooster && !stage1Separated);

        if (srbGroup != null) srbGroup.gameObject.SetActive(hasSrbs && !stage1Separated);
        if (upperStageGroup != null) upperStageGroup.gameObject.SetActive(hasUpperStage);
        if (gridFinsGroup != null) gridFinsGroup.gameObject.SetActive(hasGridFins && !stage1Separated);

        if (satelliteBusTransform != null) satelliteBusTransform.gameObject.SetActive(hasBus);
        Transform sBus = spacecraftHub.Find("Part_Bus_Sat_Gold_Bus");
        if (sBus != null) sBus.gameObject.SetActive(hasBus);

        if (magnetometerTransform != null) magnetometerTransform.gameObject.SetActive(hasMag);

        if (fairingHalfLeft != null && fairingHalfRight != null)
        {
            bool showFairing = hasFairing && !fairingJettisoned;
            fairingHalfLeft.gameObject.SetActive(showFairing);
            fairingHalfRight.gameObject.SetActive(showFairing);
            if (showFairing)
            {
                float alpha = (currentPhase == MissionPhase.Assembly) ? 0.25f : 1.0f;
                SetFairingOpacity(alpha);
            }
        }

        // Update parts 0..6
        if (parts != null)
        {
            for (int i = 0; i <= 6 && i < parts.Count; i++)
            {
                if (parts[i] == null || parts[i].partTransform == null) continue;
                bool showOnBus = parts[i].isInstalled && hasBus;
                parts[i].partTransform.gameObject.SetActive(showOnBus);
                if (showOnBus)
                {
                    parts[i].partTransform.SetParent(spacecraftHub, false);
                    parts[i].partTransform.localPosition = parts[i].busLocalOffset;
                    parts[i].partTransform.localScale = parts[i].mountedScale;
                }
            }
        }
    }

    private void EnsureCategorizedPartsPanel()
    {
        EnsurePartsPanelBounds();

        // 1. Permanently hide and destroy legacy scene slot objects and scrollbars inside Assembly_And_Catalog_Group
        Transform catalogGroup = transform.Find("Assembly_And_Catalog_Group");
        if (catalogGroup != null)
        {
            for (int i = catalogGroup.childCount - 1; i >= 0; i--)
            {
                Transform c = catalogGroup.GetChild(i);
                if (c == null) continue;
                string n = c.name;
                if (n.Contains("Scrollbar") || n.Contains("Selectable") || n == "Catalog_Header")
                {
                    c.gameObject.SetActive(false);
                    Destroy(c.gameObject);
                }
            }
        }

        if (partsCatalogContainer != null)
        {
            partsCatalogContainer.gameObject.SetActive(false);
            foreach (Transform c in partsCatalogContainer)
            {
                if (c != null) c.gameObject.SetActive(false);
            }
        }

        if (leftCategorizedPanelObj == null)
        {
            Transform existing = transform.Find("Left_Categorized_Parts_Panel");
            if (existing != null) Destroy(existing.gameObject);

            leftCategorizedPanelObj = new GameObject("Left_Categorized_Parts_Panel");
            leftCategorizedPanelObj.transform.SetParent(transform, false);
            leftCategorizedPanelObj.transform.localPosition = new Vector3(-6.35f, -0.05f, 0f);

            // Category Tab Buttons: [ STAGES ] [ POWER ] [ SCIENCE ]
            string[] tabNames = { "TAB_BTN_STAGES", "TAB_BTN_POWER", "TAB_BTN_SCIENCE" };
            string[] tabLabels = { "🚀 STAGES", "🛰️ POWER", "🔬 SCIENCE" };
            float[] tabXs = { -1.65f, 0.0f, 1.65f };

            for (int t = 0; t < 3; t++)
            {
                GameObject tBtn = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tBtn.name = tabNames[t];
                tBtn.transform.SetParent(leftCategorizedPanelObj.transform, false);
                tBtn.transform.localPosition = new Vector3(tabXs[t], 3.35f, 0f);
                tBtn.transform.localScale = new Vector3(1.58f, 0.46f, 0.1f);
                tabButtonRenderers[t] = tBtn.GetComponent<Renderer>();
                tabButtonRenderers[t].sortingOrder = 15;

                GameObject tBorder = GameObject.CreatePrimitive(PrimitiveType.Quad);
                tBorder.name = tabNames[t] + "_Border";
                tBorder.transform.SetParent(leftCategorizedPanelObj.transform, false);
                tBorder.transform.localPosition = new Vector3(tabXs[t], 3.33f, 0.05f);
                tBorder.transform.localScale = new Vector3(1.64f, 0.52f, 0.1f);
                Destroy(tBorder.GetComponent<Collider>());
                SetRendererColor(tBorder.GetComponent<Renderer>(), CardDarkEdge);
                tBorder.GetComponent<Renderer>().sortingOrder = 14;

                GameObject tLabel = new GameObject(tabNames[t] + "_Label");
                tLabel.transform.SetParent(leftCategorizedPanelObj.transform, false);
                tLabel.transform.localPosition = new Vector3(tabXs[t], 3.35f, -0.1f);
                TextMesh tm = tLabel.AddComponent<TextMesh>();
                tm.text = tabLabels[t];
                tm.characterSize = 0.046f;
                tm.fontSize = 32;
                tm.fontStyle = FontStyle.Bold;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = Color.white;
                tabButtonLabelMeshes[t] = tm;
            }

            // 5 Slot Display Cards
            float[] slotYs = { 2.45f, 1.40f, 0.35f, -0.70f, -1.75f };
            for (int k = 0; k < 5; k++)
            {
                GameObject sObj = new GameObject("PART_TAB_SLOT_" + k);
                sObj.transform.SetParent(leftCategorizedPanelObj.transform, false);
                sObj.transform.localPosition = new Vector3(0f, slotYs[k], 0f);
                tabSlotObjects[k] = sObj;

                // Card background with box collider for clicking
                GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bg.name = "PART_CARD_BG_" + k;
                bg.transform.SetParent(sObj.transform, false);
                bg.transform.localPosition = Vector3.zero;
                bg.transform.localScale = new Vector3(5.00f, 0.90f, 0.1f);
                tabSlotBgRenderers[k] = bg.GetComponent<Renderer>();
                tabSlotBgRenderers[k].sortingOrder = 15;

                // Card border
                GameObject border = GameObject.CreatePrimitive(PrimitiveType.Quad);
                border.name = "PART_CARD_BORDER_" + k;
                border.transform.SetParent(sObj.transform, false);
                border.transform.localPosition = new Vector3(0f, -0.02f, 0.05f);
                border.transform.localScale = new Vector3(5.08f, 0.98f, 0.1f);
                Destroy(border.GetComponent<Collider>());
                tabSlotBorderRenderers[k] = border.GetComponent<Renderer>();
                tabSlotBorderRenderers[k].sortingOrder = 14;

                // TextMesh
                GameObject labelObj = new GameObject("PART_CARD_LABEL_" + k);
                labelObj.transform.SetParent(sObj.transform, false);
                labelObj.transform.localPosition = new Vector3(-2.35f, 0.32f, -0.15f);
                TextMesh tm = labelObj.AddComponent<TextMesh>();
                tm.characterSize = 0.052f;
                tm.fontSize = 34;
                tm.fontStyle = FontStyle.Normal;
                tm.anchor = TextAnchor.UpperLeft;
                tm.alignment = TextAlignment.Left;
                tm.color = Color.white;
                tabSlotLabelMeshes[k] = tm;
            }

            // [ ✖ STRIP ALL HARDWARE TO ZERO ] - Full-width clean reset button inside panel
            GameObject stripBtn = GameObject.CreatePrimitive(PrimitiveType.Quad);
            stripBtn.name = "BTN_StripAll";
            stripBtn.transform.SetParent(leftCategorizedPanelObj.transform, false);
            stripBtn.transform.localPosition = new Vector3(0f, -2.75f, 0f);
            stripBtn.transform.localScale = new Vector3(4.80f, 0.56f, 0.1f);
            SetRendererColor(stripBtn.GetComponent<Renderer>(), CardDarkBg);
            stripBtn.GetComponent<Renderer>().sortingOrder = 16;

            GameObject stripBorder = GameObject.CreatePrimitive(PrimitiveType.Quad);
            stripBorder.name = "BTN_StripAll_Border";
            stripBorder.transform.SetParent(leftCategorizedPanelObj.transform, false);
            stripBorder.transform.localPosition = new Vector3(0f, -2.77f, 0.05f);
            stripBorder.transform.localScale = new Vector3(4.88f, 0.64f, 0.1f);
            Destroy(stripBorder.GetComponent<Collider>());
            SetRendererColor(stripBorder.GetComponent<Renderer>(), DuoRed);
            stripBorder.GetComponent<Renderer>().sortingOrder = 15;

            GameObject stripLabel = new GameObject("BTN_StripAll_Label");
            stripLabel.transform.SetParent(leftCategorizedPanelObj.transform, false);
            stripLabel.transform.localPosition = new Vector3(0f, -2.75f, -0.15f);
            TextMesh stm = stripLabel.AddComponent<TextMesh>();
            stm.text = "✖ STRIP HARDWARE TO ZERO";
            stm.characterSize = 0.048f;
            stm.fontSize = 34;
            stm.fontStyle = FontStyle.Bold;
            stm.anchor = TextAnchor.MiddleCenter;
            stm.alignment = TextAlignment.Center;
            stm.color = DuoRed;

            // Tip notice at bottom of panel
            GameObject tipObj = new GameObject("Category_Tip_Label");
            tipObj.transform.SetParent(leftCategorizedPanelObj.transform, false);
            tipObj.transform.localPosition = new Vector3(0f, -3.38f, -0.15f);
            TextMesh tipTm = tipObj.AddComponent<TextMesh>();
            tipTm.text = "★ Tip: Equip Booster + Avionics Bus + Power before launch";
            tipTm.characterSize = 0.044f;
            tipTm.fontSize = 32;
            tipTm.fontStyle = FontStyle.Normal;
            tipTm.anchor = TextAnchor.MiddleCenter;
            tipTm.alignment = TextAlignment.Center;
            tipTm.color = new Color(0.75f, 0.82f, 0.90f);
        }

        RefreshCategorizedPartsUI();
    }

    public void SwitchPartsTab(int tab)
    {
        activePartsTab = Mathf.Clamp(tab, 0, 2);
        PlayTelemetryBeep();
        RefreshCategorizedPartsUI();
    }

    public void ToggleCategorizedSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= tabPartIndices[activePartsTab].Length) return;
        int partIdx = tabPartIndices[activePartsTab][slotIndex];
        TogglePart(partIdx);
        RefreshCategorizedPartsUI();
        UpdateAllRocketHardwareVisuals();
    }

    public void DisassembleAllParts()
    {
        PlayTelemetryBeep();
        if (parts != null)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null) parts[i].isInstalled = false;
            }
        }
        fuelMassKg = 350f;
        repairKits = 2;
        stage1Separated = false;
        fairingJettisoned = false;
        RefreshCategorizedPartsUI();
        UpdateAllRocketHardwareVisuals();
        RecalculateTelemetry();
        SetStatusBanner("VEHICLE STRIPPED TO ZERO", "Launchpad empty. Select parts from the categories on the left to build your rocket from scratch!", DuoBlue);
    }

    public void RefreshCategorizedPartsUI()
    {
        if (leftCategorizedPanelObj == null) return;

        // 1. Highlight active category tab
        for (int t = 0; t < 3; t++)
        {
            bool isCurrent = (t == activePartsTab);
            Color bCol = isCurrent ? DuoBlue : CardDarkBg;
            Color tCol = isCurrent ? Color.white : new Color(0.65f, 0.70f, 0.78f);
            SetRendererColor(tabButtonRenderers[t], bCol);
            if (tabButtonLabelMeshes[t] != null) tabButtonLabelMeshes[t].color = tCol;
        }

        // 2. Populate up to 5 slot cards
        int[] partIndices = tabPartIndices[activePartsTab];
        for (int k = 0; k < 5; k++)
        {
            if (tabSlotObjects[k] == null) continue;

            if (k >= partIndices.Length)
            {
                tabSlotObjects[k].SetActive(false);
                continue;
            }

            tabSlotObjects[k].SetActive(true);
            int pIdx = partIndices[k];
            if (pIdx < 0 || pIdx >= parts.Count || parts[pIdx] == null) continue;
            PartSpec p = parts[pIdx];

            Color cardCol   = p.isInstalled ? CardEquipped : CardDarkBg;
            Color borderCol = p.isInstalled ? DuoGreen : CardDarkEdge;

            SetRendererColor(tabSlotBgRenderers[k], cardCol);
            SetRendererColor(tabSlotBorderRenderers[k], borderCol);

            if (tabSlotLabelMeshes[k] != null)
            {
                string tag = p.isInstalled ? "<color=#2ECC71><b>[EQUIPPED]</b></color>" : "<color=#BDC3C7>[ADD]</color>";
                float netP = p.powerOutputW - p.powerDrawW;
                string line1 = $"<b>{p.partTitle}</b>  {tag}";
                string line2 = $"<color=#BDC3C7>${p.costMillion}M | {p.massKg}kg | {netP:+0;-0;0}W | +{p.sciencePoints} Sci</color>";
                if (p.category == "Propulsion" || p.category == "Booster")
                {
                    string dvTag = pIdx == 7 ? "+1800 m/s dV" : (pIdx == 8 ? "+900 m/s dV" : (pIdx == 9 ? "+1200 m/s dV" : "+1400 m/s dV"));
                    line2 = $"<color=#BDC3C7>${p.costMillion}M | {p.massKg}kg | {dvTag}</color>";
                }
                else if (p.category == "Aero")
                {
                    string aeroTag = pIdx == 10 ? "+40% Max-Q Buffer" : "Supersonic Shroud";
                    line2 = $"<color=#BDC3C7>${p.costMillion}M | {p.massKg}kg | {aeroTag}</color>";
                }
                else if (p.category == "Structure")
                {
                    line2 = $"<color=#BDC3C7>${p.costMillion}M | {p.massKg}kg | Structural Hub & Avionics</color>";
                }
                else if (p.category == "Battery")
                {
                    line2 = $"<color=#BDC3C7>${p.costMillion}M | {p.massKg}kg | 410Wh Storage Buffer</color>";
                }

                tabSlotLabelMeshes[k].text = line1 + NL + line2;
            }
        }
    }

    private void EnsureFlightConsoleCreated()
    {
        if (flightConsoleGroup != null) Destroy(flightConsoleGroup);

        flightConsoleGroup = new GameObject("Flight_Pilot_Console_Group");
        flightConsoleGroup.transform.SetParent(transform, false);
        flightConsoleGroup.transform.localPosition = new Vector3(-0.3f, -2.85f, -2.5f);

        // Console background panel & glowing border (Docked sleekly at bottom of viewport at Z = -2.5f)
        CreateUIQuad(flightConsoleGroup.transform, "Console_Panel_Bg", Vector3.zero, new Vector3(10.8f, 1.85f, 0.1f), new Color(0.04f, 0.08f, 0.14f, 0.96f), false, 35);
        CreateUIQuad(flightConsoleGroup.transform, "Console_Panel_Border", new Vector3(0f, 0f, 0.05f), new Vector3(10.96f, 2.01f, 0.1f), new Color(0.12f, 0.65f, 0.95f, 0.85f), false, 34);

        // Console Header
        CreateUIText(flightConsoleGroup.transform, "Console_Header_Text", new Vector3(0f, 0.72f, -0.1f),
            "★ PILOT FLIGHT CONSOLE // REAL-TIME ATTITUDE, STAGING & TELEMETRY COMMAND ★", 0.044f, 30, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, DuoGold, 38);

        // 1. Throttle Cluster
        CreateUIText(flightConsoleGroup.transform, "Throttle_Header", new Vector3(-4.0f, 0.50f, -0.1f),
            "THROTTLE [W/S/X]", 0.040f, 28, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 38);

        // 100% Full
        GameObject t100 = CreateUIQuad(flightConsoleGroup.transform, "BTN_Throttle100", new Vector3(-4.0f, 0.22f, 0f), new Vector3(2.35f, 0.32f, 0.1f), CardDarkBg, true, 36);
        throttle100Renderer = t100.GetComponent<Renderer>();
        CreateUIText(flightConsoleGroup.transform, "BTN_Throttle100_Label", new Vector3(-4.0f, 0.22f, -0.1f), "100% FULL (W)", 0.042f, 28, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 38);

        // 65% Safe-Q
        GameObject t65 = CreateUIQuad(flightConsoleGroup.transform, "BTN_Throttle65", new Vector3(-4.0f, -0.14f, 0f), new Vector3(2.35f, 0.32f, 0.1f), CardDarkBg, true, 36);
        throttle65Renderer = t65.GetComponent<Renderer>();
        CreateUIText(flightConsoleGroup.transform, "BTN_Throttle65_Label", new Vector3(-4.0f, -0.14f, -0.1f), "65% SAFE-Q (S)", 0.042f, 28, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 38);

        // 0% Cutoff
        GameObject t0 = CreateUIQuad(flightConsoleGroup.transform, "BTN_Throttle0", new Vector3(-4.0f, -0.50f, 0f), new Vector3(2.35f, 0.32f, 0.1f), CardDarkBg, true, 36);
        throttle0Renderer = t0.GetComponent<Renderer>();
        CreateUIText(flightConsoleGroup.transform, "BTN_Throttle0_Label", new Vector3(-4.0f, -0.50f, -0.1f), "0% CUTOFF (X)", 0.042f, 28, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 38);

        // 2. Active Staging Control
        CreateUIText(flightConsoleGroup.transform, "Staging_Header", new Vector3(-1.35f, 0.50f, -0.1f),
            "STAGING [SPACE]", 0.040f, 28, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 38);

        GameObject stg = CreateUIQuad(flightConsoleGroup.transform, "BTN_FlightStage", new Vector3(-1.35f, -0.14f, 0f), new Vector3(2.55f, 0.96f, 0.1f), CardDarkBg, true, 36);
        flightStageRenderer = stg.GetComponent<Renderer>();
        flightStageLabelMesh = CreateUIText(flightConsoleGroup.transform, "BTN_FlightStage_Label", new Vector3(-1.35f, -0.14f, -0.1f),
            "⏏ STAGE (SPACE)", 0.046f, 30, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 38);

        // 3. Corridor Gimbal Steering
        CreateUIText(flightConsoleGroup.transform, "Gimbal_Header", new Vector3(1.35f, 0.50f, -0.1f),
            "GIMBAL STEER [A / D]", 0.040f, 28, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 38);

        GameObject gLeft = CreateUIQuad(flightConsoleGroup.transform, "BTN_GimbalLeft", new Vector3(0.75f, -0.05f, 0f), new Vector3(1.05f, 0.65f, 0.1f), CardDarkBg, true, 36);
        CreateUIText(flightConsoleGroup.transform, "BTN_GimbalLeft_Label", new Vector3(0.75f, -0.05f, -0.1f), "◄ A", 0.052f, 34, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, DuoBlue, 38);

        GameObject gRight = CreateUIQuad(flightConsoleGroup.transform, "BTN_GimbalRight", new Vector3(1.95f, -0.05f, 0f), new Vector3(1.05f, 0.65f, 0.1f), CardDarkBg, true, 36);
        CreateUIText(flightConsoleGroup.transform, "BTN_GimbalRight_Label", new Vector3(1.95f, -0.05f, -0.1f), "D ►", 0.052f, 34, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, DuoBlue, 38);

        flightGimbalLabelMesh = CreateUIText(flightConsoleGroup.transform, "Gimbal_Status_Label", new Vector3(1.35f, -0.52f, -0.1f),
            "CORRIDOR: 0.00°", 0.042f, 28, FontStyle.Normal, TextAnchor.MiddleCenter, TextAlignment.Center, DuoGreen, 38);

        // 4. Aerospace Telemetry & Live Flight Guidance
        CreateUIText(flightConsoleGroup.transform, "Advisory_Header", new Vector3(3.95f, 0.50f, -0.1f),
            "FLIGHT ADVISORY // LOAD", 0.040f, 28, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 38);

        GameObject advBox = CreateUIQuad(flightConsoleGroup.transform, "Advisory_Card_Bg", new Vector3(3.95f, -0.14f, 0f), new Vector3(2.45f, 0.98f, 0.1f), CardDarkBg, false, 36);
        flightAdvisoryBoxRenderer = advBox.GetComponent<Renderer>();
        CreateUIQuad(flightConsoleGroup.transform, "Advisory_Card_Border", new Vector3(3.95f, -0.14f, 0.02f), new Vector3(2.55f, 1.08f, 0.1f), new Color(0.20f, 0.40f, 0.60f, 0.8f), false, 35);

        flightAdvisoryTitleMesh = CreateUIText(flightConsoleGroup.transform, "Adv_Title_Text", new Vector3(3.95f, 0.18f, -0.1f),
            "AERODYNAMIC STATUS", 0.038f, 26, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, DuoGold, 38);

        flightAdvisoryQMesh = CreateUIText(flightConsoleGroup.transform, "Adv_Q_Text", new Vector3(3.95f, -0.08f, -0.1f),
            "DYNAMIC Q: 0.0 kPa", 0.042f, 28, FontStyle.Normal, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 38);

        flightAdvisoryCueMesh = CreateUIText(flightConsoleGroup.transform, "Adv_Cue_Text", new Vector3(3.95f, -0.36f, -0.1f),
            "FULL POWER AUTHORIZED", 0.038f, 26, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, DuoGreen, 38);

        flightConsoleGroup.SetActive(false);
    }

    private void UpdateFlightConsoleVisuals()
    {
        if (flightConsoleGroup == null || Camera.main == null) return;

        bool isFlight = (currentPhase == MissionPhase.Launching || currentPhase == MissionPhase.FlightCrisis);
        flightConsoleGroup.SetActive(isFlight);
        if (!isFlight) return;

        // Follow camera vertically docked cleanly at bottom regardless of camera zoom
        float camY = Camera.main.transform.position.y;
        float camOrtho = Camera.main.orthographicSize;
        float bottomY = camY - camOrtho;
        flightConsoleGroup.transform.position = new Vector3(-0.3f, bottomY + 0.95f, -2.5f);

        float currentY = spacecraftHub != null ? spacecraftHub.localPosition.y : LaunchpadY;

        // 1. Throttle Buttons Highlight & Max-Q Throttle Bucket Prompt
        bool inMaxQ = (currentY >= 5.8f && currentY <= 8.8f);
        if (inMaxQ && Mathf.Abs(flightThrottle - 0.65f) > 0.1f)
        {
            float pulse = Mathf.PingPong(Time.time * 6f, 1f);
            SetRendererColor(throttle65Renderer, Color.Lerp(DuoOrange, DuoGold, pulse));
        }
        else
        {
            SetRendererColor(throttle65Renderer, (Mathf.Abs(flightThrottle - 0.65f) < 0.1f) ? DuoGold : CardDarkBg);
        }
        SetRendererColor(throttle100Renderer, (Mathf.Abs(flightThrottle - 1.0f) < 0.1f) ? DuoBlue : CardDarkBg);
        SetRendererColor(throttle0Renderer,   (Mathf.Abs(flightThrottle - 0.0f) < 0.1f) ? DuoRed : CardDarkBg);

        // 2. Staging Button Visuals & Pulsing
        if (currentY >= 10.0f && !stage1Separated)
        {
            float pulse = Mathf.PingPong(Time.time * 5f, 1f);
            SetRendererColor(flightStageRenderer, Color.Lerp(DuoOrange, DuoGold, pulse));
            if (flightStageLabelMesh != null)
            {
                flightStageLabelMesh.text = "⏏ SEPARATE BOOSTER (SPACE)";
                flightStageLabelMesh.color = Color.black;
            }
        }
        else if (currentY >= 12.0f && !fairingJettisoned)
        {
            float pulse = Mathf.PingPong(Time.time * 5f, 1f);
            SetRendererColor(flightStageRenderer, Color.Lerp(DuoBlue, DuoGreen, pulse));
            if (flightStageLabelMesh != null)
            {
                flightStageLabelMesh.text = "⏏ JETTISON FAIRING (SPACE)";
                flightStageLabelMesh.color = Color.black;
            }
        }
        else
        {
            SetRendererColor(flightStageRenderer, CardDarkBg);
            if (flightStageLabelMesh != null)
            {
                flightStageLabelMesh.text = stage1Separated ? (fairingJettisoned ? "S-II NOMINAL" : "FAIRING ARMED") : "STAGE ARMED";
                flightStageLabelMesh.color = Color.white;
            }
        }

        // 3. Gimbal Status Readout
        if (flightGimbalLabelMesh != null)
        {
            float deg = flightGimbalOffset * 15f;
            string dir = deg > 0.5f ? "►" : (deg < -0.5f ? "◄" : "●");
            flightGimbalLabelMesh.text = (Mathf.Abs(deg) < 1.0f) ? "ATTITUDE: 0° ● NOMINAL" : $"ATTITUDE: {Mathf.Abs(deg):F0}° {dir} [A/D]";
            flightGimbalLabelMesh.color = (Mathf.Abs(deg) < 3.0f) ? DuoGreen : DuoOrange;
        }

        // 4. Live Dynamic Q & Telemetry Readout
        float altKm = Mathf.Max(0f, (currentY - LaunchpadY) * 6.5f);
        float velMs = 260f + (currentY - LaunchpadY) * 65f * (0.4f + 0.6f * flightThrottle);
        float airDensity = Mathf.Exp(-altKm / 12f);
        float qKpa = 0.5f * airDensity * Mathf.Pow(velMs * 0.055f, 2f);
        bool hasGridFins = (parts != null && parts.Count > 10 && parts[10] != null && parts[10].isInstalled);
        float maxQThreshold = hasGridFins ? 42.0f : 30.0f;

        if (flightAdvisoryQMesh != null)
        {
            flightAdvisoryQMesh.text = $"DYNAMIC Q: {qKpa:F1} kPa";
            flightAdvisoryQMesh.color = (qKpa > maxQThreshold) ? DuoOrange : DuoGreen;
        }

        if (flightAdvisoryCueMesh != null)
        {
            if (currentY < 5.8f)
            {
                flightAdvisoryCueMesh.text = "ATMOSPHERIC CLIMB";
                flightAdvisoryCueMesh.color = DuoGreen;
            }
            else if (inMaxQ)
            {
                if (qKpa > maxQThreshold)
                {
                    flightAdvisoryCueMesh.text = "⚠️ THROTTLE TO 65% (S)!";
                    flightAdvisoryCueMesh.color = DuoOrange;
                }
                else
                {
                    flightAdvisoryCueMesh.text = "SAFE-Q BUCKET NOMINAL";
                    flightAdvisoryCueMesh.color = DuoGreen;
                }
            }
            else if (!stage1Separated && currentY >= 10.0f)
            {
                flightAdvisoryCueMesh.text = "BOOSTER MECO READY";
                flightAdvisoryCueMesh.color = DuoGold;
            }
            else if (!fairingJettisoned && currentY >= 12.0f)
            {
                flightAdvisoryCueMesh.text = "VACUUM FAIRING READY";
                flightAdvisoryCueMesh.color = DuoBlue;
            }
            else if (currentY >= 18.5f && currentY <= 21.8f)
            {
                flightAdvisoryCueMesh.text = "DEBRIS: STEER [A / D]";
                flightAdvisoryCueMesh.color = DuoOrange;
            }
            else if (currentY >= 21.8f)
            {
                flightAdvisoryCueMesh.text = "ORBIT INSERTION BURN";
                flightAdvisoryCueMesh.color = DuoGreen;
            }
            else
            {
                flightAdvisoryCueMesh.text = "100% THRUST NOMINAL";
                flightAdvisoryCueMesh.color = DuoGreen;
            }
        }
    }

    public void SetFlightThrottle(float t)
    {
        flightThrottle = Mathf.Clamp01(t);
        PlayTelemetryBeep();

        if (rumbleAudioSource != null && rumbleAudioSource.isPlaying)
        {
            rumbleAudioSource.volume = Mathf.Clamp(0.12f + 0.48f * flightThrottle, 0.05f, 0.70f);
            rumbleAudioSource.pitch = Mathf.Clamp(0.80f + 0.35f * flightThrottle, 0.65f, 1.25f);
        }
    }

    public void AdjustGimbalSteering(float delta)
    {
        flightGimbalOffset = Mathf.Clamp(flightGimbalOffset + delta, -0.8f, 0.8f);
        rcsPlumeTimer = 0.20f;
        activeRcsSide = (delta < 0f) ? -1 : 1;
        if (flightRcsVisual != null) flightRcsVisual.SetActive(true);
    }

    public void TriggerFlightStageAction()
    {
        if (currentPhase != MissionPhase.Launching) return;
        float currentY = spacecraftHub != null ? spacecraftHub.localPosition.y : LaunchpadY;

        if (currentY >= 9.8f && !stage1Separated)
        {
            stage1Separated = true;
            boosterSeparated = true;
            boosterSepTimer = 0f;
            PlayStagingAudio();
            if (spacecraftHub != null)
            {
                Vector3 p = spacecraftHub.localPosition;
                p.y += 0.35f; // Pneumatic stage separation impulse!
                spacecraftHub.localPosition = p;
            }
            if (gridFinsGroup != null) gridFinsGroup.gameObject.SetActive(false);

            SetStatusBanner("STAGE 1 SEPARATION CONFIRMED!",
                "First stage booster released! S-II vacuum stage online. Accelerating to orbit!", DuoGreen);
        }
        else if (currentY >= 11.8f && !fairingJettisoned)
        {
            fairingJettisoned = true;
            fairingJettisonTimer = 0f;
            PlayStagingAudio();
            PlaySolarDeployAudio();
            solarDeployProgress = 1.0f;

            if (parts != null && parts.Count > 0 && parts[0] != null && parts[0].partTransform != null)
            {
                parts[0].partTransform.localScale = parts[0].mountedScale;
            }

            SetStatusBanner("PAYLOAD FAIRING JETTISONED!",
                "Fairing halves cleared payload. Scientific solar arrays deployed!", DuoGreen);
        }
        else
        {
            PlayTelemetryBeep();
        }
    }

    private void UpgradePartMeshesToDetailedVisuals()
    {
        if (parts == null || parts.Count < 7) return;

        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].partTransform == null) continue;
            Transform pt = parts[i].partTransform;

            // Hide the blocky cube mesh renderer on the root part, keep collider for clicking
            if (parts[i].partRenderer != null) parts[i].partRenderer.enabled = false;
            if (pt.Find("Detailed_Part_Art") != null) continue;

            GameObject art = new GameObject("Detailed_Part_Art");
            art.transform.SetParent(pt, false);

            if (i == 0) // ROSA Roll-Out Solar Array (Booms + Dark Blue Photovoltaic Cells)
            {
                MakeRocketPiece(art.transform, "Solar_Boom", new Vector3(0f, 0f, 0.02f), new Vector3(1.05f, 0.18f, 1f), 0f, new Color(0.75f, 0.80f, 0.85f));
                for (int c = -3; c <= 3; c++)
                {
                    if (c == 0) continue;
                    MakeRocketPiece(art.transform, "Cell_" + c, new Vector3(c * 0.13f, 0f, -0.01f), new Vector3(0.11f, 0.88f, 1f), 0f, new Color(0.08f, 0.28f, 0.58f));
                    MakeRocketPiece(art.transform, "Grid_" + c, new Vector3(c * 0.13f, 0f, -0.02f), new Vector3(0.09f, 0.72f, 1f), 0f, new Color(0.15f, 0.55f, 0.88f));
                }
            }
            else if (i == 1) // MMRTG Pu-238 Nuclear Core (White Finned Cylinder + Glowing Core)
            {
                MakeRocketPiece(art.transform, "RTG_Body", new Vector3(0f, 0f, 0f), new Vector3(0.55f, 0.95f, 1f), 0f, new Color(0.88f, 0.90f, 0.94f));
                MakeRocketPiece(art.transform, "RTG_Fins_1", new Vector3(0f, 0.22f, 0.02f), new Vector3(0.95f, 0.14f, 1f), 0f, new Color(0.70f, 0.75f, 0.80f));
                MakeRocketPiece(art.transform, "RTG_Fins_2", new Vector3(0f, -0.22f, 0.02f), new Vector3(0.95f, 0.14f, 1f), 0f, new Color(0.70f, 0.75f, 0.80f));
                MakeRocketPiece(art.transform, "RTG_Heat_Glow", new Vector3(0f, 0f, -0.02f), new Vector3(0.28f, 0.65f, 1f), 0f, new Color(1.0f, 0.45f, 0.05f));
            }
            else if (i == 2) // EaglePicher Li-Ion Battery Pack (Multi-Cell Aerospace Module)
            {
                MakeRocketPiece(art.transform, "Bat_Case", new Vector3(0f, 0f, 0.02f), new Vector3(0.95f, 0.90f, 1f), 0f, new Color(0.18f, 0.22f, 0.28f));
                MakeRocketPiece(art.transform, "Bat_Cell_L", new Vector3(-0.26f, 0f, -0.01f), new Vector3(0.24f, 0.72f, 1f), 0f, new Color(0.25f, 0.82f, 0.35f));
                MakeRocketPiece(art.transform, "Bat_Cell_M", new Vector3(0f, 0f, -0.01f), new Vector3(0.24f, 0.72f, 1f), 0f, new Color(0.25f, 0.82f, 0.35f));
                MakeRocketPiece(art.transform, "Bat_Cell_R", new Vector3(0.26f, 0f, -0.01f), new Vector3(0.24f, 0.72f, 1f), 0f, new Color(0.25f, 0.82f, 0.35f));
            }
            else if (i == 3) // DSN Ka-Band Parabolic Dish Antenna (Curved Dish + Feed Horn)
            {
                MakeRocketPiece(art.transform, "Dish_Reflector", new Vector3(0f, -0.10f, 0f), new Vector3(0.95f, 0.45f, 1f), 0f, new Color(0.92f, 0.95f, 0.98f));
                MakeRocketPiece(art.transform, "Dish_Rim", new Vector3(0f, 0.05f, -0.01f), new Vector3(1.0f, 0.14f, 1f), 0f, new Color(0.70f, 0.78f, 0.86f));
                MakeRocketPiece(art.transform, "Feed_Mast", new Vector3(0f, 0.45f, 0.01f), new Vector3(0.08f, 0.85f, 1f), 0f, new Color(0.55f, 0.62f, 0.70f));
                MakeRocketPiece(art.transform, "Feed_Tip", new Vector3(0f, 0.85f, -0.02f), new Vector3(0.22f, 0.22f, 1f), 45f, new Color(0.15f, 0.75f, 0.98f));
            }
            else if (i == 4) // HiRISE Optical Telescope Camera (Barrel + Glass Aperture Lens)
            {
                MakeRocketPiece(art.transform, "Cam_Housing", new Vector3(0f, 0f, 0.02f), new Vector3(0.85f, 0.85f, 1f), 0f, new Color(0.22f, 0.25f, 0.32f));
                MakeRocketPiece(art.transform, "Lens_Ring", new Vector3(0f, 0f, -0.01f), new Vector3(0.62f, 0.62f, 1f), 45f, new Color(0.75f, 0.80f, 0.88f));
                MakeRocketPiece(art.transform, "Optical_Glass", new Vector3(0f, 0f, -0.02f), new Vector3(0.42f, 0.42f, 1f), 45f, new Color(0.55f, 0.25f, 0.95f));
            }
            else if (i == 5) // NEXT-C Xenon Ion Thruster (Chamber + Blue Ion Grid Glow)
            {
                MakeRocketPiece(art.transform, "Ion_Chamber", new Vector3(0f, 0.20f, 0.01f), new Vector3(0.65f, 0.55f, 1f), 0f, new Color(0.35f, 0.40f, 0.48f));
                MakeRocketPiece(art.transform, "Nozzle_Bell", new Vector3(0f, -0.18f, 0f), new Vector3(0.92f, 0.45f, 1f), 0f, new Color(0.22f, 0.26f, 0.32f));
                MakeRocketPiece(art.transform, "Xenon_Ion_Glow", new Vector3(0f, -0.42f, -0.02f), new Vector3(0.75f, 0.22f, 1f), 0f, new Color(0.10f, 0.92f, 1.0f));
            }
            else if (i == 6) // THEMIS IR Spectrometer (Gold Foil Sensor Box + IR Window)
            {
                MakeRocketPiece(art.transform, "IR_Gold_Box", new Vector3(0f, 0f, 0.02f), new Vector3(0.88f, 0.88f, 1f), 0f, new Color(0.95f, 0.75f, 0.10f));
                MakeRocketPiece(art.transform, "IR_Sensor_Window", new Vector3(0f, 0.12f, -0.01f), new Vector3(0.55f, 0.38f, 1f), 0f, new Color(0.90f, 0.18f, 0.22f));
                MakeRocketPiece(art.transform, "IR_Louver", new Vector3(0f, -0.22f, -0.01f), new Vector3(0.65f, 0.16f, 1f), 0f, new Color(0.25f, 0.28f, 0.32f));
            }
        }
    }

    private void MakeRocketPiece(Transform parent, string pieceName, Vector3 pos, Vector3 scale, float rotZ, Color col)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = pieceName;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        q.transform.localScale = scale;
        Destroy(q.GetComponent<Collider>());
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        m.color = col;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        q.GetComponent<Renderer>().material = m;
    }

    private void SetAssemblyUIVisible(bool visible)
    {
        string[] uiToHide = {
            "Top_Budget_Fuel_Box", "Top_Budget_Fuel_Box_Border", "Top_Bar_Text",
            "Assembly_And_Catalog_Group",
            "Left_Categorized_Parts_Panel",
            "Right_Requirements_Box", "Right_Requirements_Box_Border", "Telemetry_Text_Object",
            "Center_Config_Board", "Center_Config_Board_Border", "Config_Selection_Text",
            "BTN_LessFuel", "BTN_LessFuel_Border", "BTN_LessFuel_Label",
            "BTN_MoreFuel", "BTN_MoreFuel_Border", "BTN_MoreFuel_Label",
            "BTN_LaunchSim", "BTN_LaunchSim_Border", "BTN_LaunchSim_Label",
            "BTN_Reset", "BTN_Reset_Border", "BTN_Reset_Label",
            "BTN_CycleTarget", "BTN_CycleTarget_Border", "BTN_CycleTarget_Label",
            "BTN_CycleRocket", "BTN_CycleRocket_Border", "BTN_CycleRocket_Label",
            "Status_Display_Board", "Status_Display_Board_Border", "Mission_Status_Text_Object",
            "BTN_AutoPreset", "BTN_AutoPreset_Border", "BTN_AutoPreset_Label",
            "BTN_StripAll", "BTN_StripAll_Border", "BTN_StripAll_Label",
            "BTN_Manual", "BTN_Manual_Border", "BTN_Manual_Label",
            "BTN_Home", "BTN_Home_Border", "BTN_Home_Label",
            "Custom_Launch_Gantry"
        };
        foreach (string objName in uiToHide)
        {
            Transform t = transform.Find(objName);
            if (t != null) t.gameObject.SetActive(visible);
        }
        if (leftCategorizedPanelObj != null) leftCategorizedPanelObj.SetActive(visible);

        if (visible)
        {
            Transform cat = transform.Find("Assembly_And_Catalog_Group");
            if (cat != null)
            {
                for (int i = cat.childCount - 1; i >= 0; i--)
                {
                    Transform c = cat.GetChild(i);
                    if (c != null && (c.name.Contains("Scrollbar") || c.name.Contains("Thumb") || c.name.Contains("Track") || c.name.Contains("Selectable") || c.name == "Catalog_Header"))
                    {
                        c.gameObject.SetActive(false);
                        Destroy(c.gameObject);
                    }
                }
            }
        }
    }

    private void UpdateCameraFollow(float dt)
    {
        if (Camera.main == null || spacecraftHub == null) return;

        bool inFlight = (currentPhase != MissionPhase.Assembly && currentPhase != MissionPhase.TitleScreen);
        float targetSize = inFlight ? 3.1f : 5.6f;
        Vector3 targetCamPos = inFlight
            ? new Vector3(-0.3f, spacecraftHub.position.y + 0.25f, -15f)
            : new Vector3(0f, 0f, -15f);

        Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, targetSize, dt * 5f);
        Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, targetCamPos, dt * 5f);

        float camY = Camera.main.transform.position.y;
        Transform stars = transform.Find("Starfield_Background");
        if (stars != null) stars.localPosition = new Vector3(0f, camY, 0f);

        if (currentPhase == MissionPhase.TitleScreen || (manualModalObj != null && manualModalObj.activeSelf))
        {
            SetAssemblyUIVisible(false);
            if (mapTargetLabel != null && mapTargetLabel.gameObject.activeSelf)
                mapTargetLabel.gameObject.SetActive(false);

            return;
        }

        if (inFlight)
        {
            Transform topBox = transform.Find("Top_Budget_Fuel_Box");
            Transform topBorder = transform.Find("Top_Budget_Fuel_Box_Border");
            Transform topText = transform.Find("Top_Bar_Text");

            float camOrtho = Camera.main.orthographicSize;
            float topY = camY + camOrtho;

            if (topBox != null)
            {
                if (!topBox.gameObject.activeSelf) topBox.gameObject.SetActive(true);
                topBox.localPosition = new Vector3(-0.3f, topY - 0.58f, -2.5f);
                topBox.localScale = new Vector3(11.2f, 1.25f, 0.1f);
            }
            if (topBorder != null)
            {
                if (!topBorder.gameObject.activeSelf) topBorder.gameObject.SetActive(true);
                topBorder.localPosition = new Vector3(-0.3f, topY - 0.58f, -2.45f);
                topBorder.localScale = new Vector3(11.36f, 1.35f, 0.1f);
            }
            if (topText != null)
            {
                if (!topText.gameObject.activeSelf) topText.gameObject.SetActive(true);
                topText.localPosition = new Vector3(-0.3f, topY - 0.58f, -2.6f);
                if (topBarDisplay != null)
                {
                    topBarDisplay.characterSize = 0.046f;
                    topBarDisplay.fontSize = 36;
                    topBarDisplay.lineSpacing = 1.16f;
                }
            }

            UpdateLiveFlightTelemetryHUD();

            bool showChoices = (currentPhase == MissionPhase.FlightCrisis);
            MoveGroupY("BTN_CycleTarget", camY - 1.95f, showChoices);
            MoveGroupY("BTN_CycleRocket", camY - 1.95f, showChoices);
            bool isCompleted = (currentPhase == MissionPhase.Completed);
            float boardY = isCompleted ? (camY + 0.35f) : (camY + 2.30f);
            float boardZ = isCompleted ? -2.0f : -0.5f;
            float resetY = isCompleted ? (camY - 1.55f) : (camY - 2.35f);
            float resetX = isCompleted ? -0.3f : 2.2f;
            float resetZ = isCompleted ? -2.0f : -0.5f;
            MoveGroupY("Status_Display_Board", boardY, isCompleted, "Mission_Status_Text_Object", -0.3f, boardZ);
            MoveGroupY("BTN_Reset", resetY, isCompleted, null, resetX, resetZ);

            // Hide map target label during all flight & orbit phases to prevent HUD overlap
            if (mapTargetLabel != null && mapTargetLabel.gameObject.activeSelf)
            {
                mapTargetLabel.gameObject.SetActive(false);
            }
        }
        else
        {
            Transform topBox = transform.Find("Top_Budget_Fuel_Box");
            Transform topBorder = transform.Find("Top_Budget_Fuel_Box_Border");
            Transform topText = transform.Find("Top_Bar_Text");

            if (topBox != null)
            {
                if (!topBox.gameObject.activeSelf) topBox.gameObject.SetActive(true);
                topBox.localPosition = new Vector3(0f, 4.8f, 0.5f);
                topBox.localScale = new Vector3(17.8f, 0.98f, 0.1f);
            }
            if (topBorder != null)
            {
                if (!topBorder.gameObject.activeSelf) topBorder.gameObject.SetActive(true);
                topBorder.localPosition = new Vector3(0f, 4.74f, 0.55f);
                topBorder.localScale = new Vector3(17.88f, 1.06f, 0.1f);
            }
            if (topText != null)
            {
                if (!topText.gameObject.activeSelf) topText.gameObject.SetActive(true);
                topText.localPosition = new Vector3(0f, 4.82f, 0.4f);
                if (topBarDisplay != null) topBarDisplay.characterSize = 0.055f;
            }

            MoveGroupY("BTN_CycleTarget", -3.10f, true);
            MoveGroupY("BTN_CycleRocket", -3.10f, true);
            MoveGroupY("Status_Display_Board", -2.45f, true, "Mission_Status_Text_Object", 6.1f, 0.5f);
            MoveGroupY("BTN_Reset", -3.65f, true, null, 7.85f, 0f);
            if (mapTargetLabel != null && !mapTargetLabel.gameObject.activeSelf)
            {
                mapTargetLabel.gameObject.SetActive(true);
            }
        }
    }

    private void MoveGroupY(string baseName, float y, bool active, string customLabel = null, float customX = -999f, float customZ = -999f)
    {
        Transform main = transform.Find(baseName);
        Transform border = transform.Find(baseName + "_Border");
        Transform label = transform.Find(customLabel ?? (baseName + "_Label"));
        if (main != null)
        {
            main.gameObject.SetActive(active);
            float x = (customX > -900f) ? customX : main.localPosition.x;
            float z = (customZ > -900f) ? customZ : main.localPosition.z;
            main.localPosition = new Vector3(x, y, z);
            if (border != null)
            {
                border.gameObject.SetActive(active);
                float bz = (customZ > -900f) ? (z + 0.05f) : border.localPosition.z;
                border.localPosition = new Vector3(x, y - 0.06f, bz);
            }
            if (label != null)
            {
                label.gameObject.SetActive(active);
                float lz = (customZ > -900f) ? (z - 0.1f) : label.localPosition.z;
                label.localPosition = new Vector3(x, y + 0.02f, lz);
            }
        }
    }

    private void InitCrises()
    {
        realisticCrises = new FlightCrisis[]
        {
            new FlightCrisis
            {
                title = "OBSTACLE 1: MAX-Q SUPERSONIC SHEAR!",
                realWorldWhy = "Aerodynamic pressure peaks at Max-Q. Throttle down or reinforce struts!",
                optionAText = "OPTION A:\nBurn 75kg Fuel",
                optionBText = "OPTION B:\nUse 1 Repair Kit"
            },
            new FlightCrisis
            {
                title = "OBSTACLE 2: VAN ALLEN RADIATION!",
                realWorldWhy = "High-energy protons flip avionics bits. Power shields or reboot systems!",
                optionAText = "OPTION A:\nDrain 150Wh Bat",
                optionBText = "OPTION B:\nUse 1 Repair Kit"
            },
            new FlightCrisis
            {
                title = "OBSTACLE 3: ORBITAL DEBRIS FIELD!",
                realWorldWhy = "Debris at 28,000 km/h can puncture fuel lines. Dodge or patch hull!",
                optionAText = "OPTION A:\nBurn 60kg Fuel",
                optionBText = "OPTION B:\nUse 1 Repair Kit"
            }
        };
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) && Camera.main != null)
        {
            HandleScreenClick();
        }

        AnimateEnvironmentAndObstacles(Time.deltaTime);

        if (currentPhase == MissionPhase.Countdown)
        {
            UpdateLaunchCountdown(Time.deltaTime);
        }
        else if (currentPhase == MissionPhase.Launching)
        {
            if (Input.GetKeyDown(KeyCode.W)) SetFlightThrottle(1.0f);
            if (Input.GetKeyDown(KeyCode.S)) SetFlightThrottle(0.65f);
            if (Input.GetKeyDown(KeyCode.X)) SetFlightThrottle(0.0f);
            if (Input.GetKeyDown(KeyCode.Space)) TriggerFlightStageAction();
            if (Input.GetKey(KeyCode.A))
            {
                AdjustGimbalSteering(-2.2f * Time.deltaTime);
            }
            else if (Input.GetKey(KeyCode.D))
            {
                AdjustGimbalSteering(2.2f * Time.deltaTime);
            }
            else
            {
                flightGimbalOffset = Mathf.MoveTowards(flightGimbalOffset, 0f, 1.0f * Time.deltaTime);
            }

            if (rcsPlumeTimer > 0f)
            {
                rcsPlumeTimer -= Time.deltaTime;
                float pAlpha = Mathf.Clamp01(rcsPlumeTimer / 0.20f);

                // Newton's 3rd Law: Steering Left (activeRcsSide == -1) fires the Right thruster outward!
                // Steering Right (activeRcsSide == 1) fires the Left thruster outward!
                if (rcsPlumeR != null)
                {
                    bool showR = (activeRcsSide == -1);
                    rcsPlumeR.gameObject.SetActive(showR);
                    if (showR)
                    {
                        rcsPlumeR.localScale = new Vector3(0.14f + 0.12f * pAlpha, 0.07f * (0.4f + 0.6f * pAlpha), 1f);
                        SetRendererColor(rcsPlumeR.GetComponent<Renderer>(), new Color(0.85f, 0.95f, 1.0f, 0.75f * pAlpha));
                    }
                }
                if (rcsPlumeL != null)
                {
                    bool showL = (activeRcsSide == 1);
                    rcsPlumeL.gameObject.SetActive(showL);
                    if (showL)
                    {
                        rcsPlumeL.localScale = new Vector3(0.14f + 0.12f * pAlpha, 0.07f * (0.4f + 0.6f * pAlpha), 1f);
                        SetRendererColor(rcsPlumeL.GetComponent<Renderer>(), new Color(0.85f, 0.95f, 1.0f, 0.75f * pAlpha));
                    }
                }
            }
            else
            {
                if (rcsPlumeL != null && rcsPlumeL.gameObject.activeSelf) rcsPlumeL.gameObject.SetActive(false);
                if (rcsPlumeR != null && rcsPlumeR.gameObject.activeSelf) rcsPlumeR.gameObject.SetActive(false);
            }

            UpdateAscentThroughObstacles(Time.deltaTime);
        }
        else if (currentPhase == MissionPhase.OrbitSimulation)
        {
            UpdateOrbitalSimulation(Time.deltaTime);
        }

        UpdateFlightConsoleVisuals();
        UpdateFairingAndStagingAnimation(Time.deltaTime);
        UpdateAtmosphericSky(spacecraftHub != null ? spacecraftHub.localPosition.y : LaunchpadY);
        UpdateCameraFollow(Time.deltaTime);
        UpdateThrusterRumbleAudio();
    }
    void LateUpdate()
    {
        StyleAndAnimateObstacles();
        StyleEnvironmentAndLaunchpad();
        UpdateRocketExhaustParticles();
        UpdateLoxBoilOffVapor();
        UpdatePadLiftoffSmoke();
    }

    // Animates the stars, drifting space debris, radiation belt pulses, and science reward nodes
    private void AnimateEnvironmentAndObstacles(float dt)
    {
        // 1. Subtle star twinkle
        if (backgroundStars != null)
        {
            for (int i = 0; i < backgroundStars.Length; i++)
            {
                if (backgroundStars[i] == null) continue;
                float s = 0.04f + 0.02f * Mathf.Sin(Time.time * (2.0f + (i % 5)) + i);
                backgroundStars[i].localScale = new Vector3(s, s, 1f);
            }
        }

        // 2. Drift space debris horizontally across Obstacle Zone 3 (Y = 1.25)
        if (debrisParticles != null)
        {
            for (int i = 0; i < debrisParticles.Length; i++)
            {
                if (debrisParticles[i] == null) continue;
                Vector3 p = debrisParticles[i].localPosition;
                p.x += (0.65f + (i % 3) * 0.25f) * (i % 2 == 0 ? 1f : -1f) * dt;
                if (p.x > 1.85f) p.x = -1.85f;
                if (p.x < -1.85f) p.x = 1.85f;
                debrisParticles[i].localPosition = p;
                debrisParticles[i].Rotate(0f, 0f, 65f * dt);
            }
        }

        // 3. Pulse radiation particles across Obstacle Zone 2 (Y = 0.40)
        if (radiationParticles != null)
        {
            for (int i = 0; i < radiationParticles.Length; i++)
            {
                if (radiationParticles[i] == null) continue;
                Vector3 p = radiationParticles[i].localPosition;
                float wave = Mathf.Sin(Time.time * 4f + i) * 0.08f;
                radiationParticles[i].localPosition = new Vector3(p.x, wave, p.z);
            }
        }

        // 4. Subtle Earth cloud drift & rotation
        if (earthCloudsGroup != null && earthCloudsGroup.gameObject.activeSelf)
        {
            earthCloudsGroup.Rotate(0f, 0f, -Time.deltaTime * 2.2f);
        }

        // 5. Twinkling Earth Nightside City Lights
        if (earthNightLightsGroup != null && earthNightLightsGroup.gameObject.activeSelf)
        {
            int lIdx = 0;
            foreach (Transform city in earthNightLightsGroup)
            {
                float shimmer = 0.65f + 0.35f * Mathf.Sin(Time.time * 4.5f + lIdx * 1.7f);
                Color c = new Color(1.0f, 0.85f, 0.35f, shimmer);
                SetRendererColor(city.GetComponent<Renderer>(), c);
                lIdx++;
            }
        }
    }

    // =========================================================================
    // 1. 2D MOUSE CLICK DETECTION
    // =========================================================================
    private void HandleScreenClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);
        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (ProcessClickedObjectName(h.collider.gameObject.name)) return;
            }
        }

        Vector3 wp = Camera.main.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, Mathf.Abs(Camera.main.transform.position.z)));
        float x = wp.x;
        float y = wp.y;

        if (manualModalObj != null && manualModalObj.activeSelf)
        {
            if (IsInsideBox(x, y, 0f, -2.85f, 5.4f, 0.90f))
            {
                CloseFlightManual();
                return;
            }
            return;
        }

        if (currentPhase == MissionPhase.TitleScreen)
        {
            if (IsInsideBox(x, y, -2.55f, -1.55f, 4.7f, 0.95f))
            {
                StartMissionFromTitle();
                return;
            }
            if (IsInsideBox(x, y, 2.55f, -1.55f, 4.7f, 0.95f))
            {
                OpenFlightManual();
                return;
            }
            return;
        }

        if (currentPhase == MissionPhase.Assembly)
        {
            // Category Tabs: Y in [3.10f, 3.60f]
            if (y >= 3.10f && y <= 3.60f)
            {
                if (x >= -8.85f && x <= -7.20f) { SwitchPartsTab(0); return; }
                if (x >= -7.15f && x <= -5.55f) { SwitchPartsTab(1); return; }
                if (x >= -5.50f && x <= -3.90f) { SwitchPartsTab(2); return; }
            }

            // 5 Slot Display Cards: X in [-8.90f, -3.80f]
            if (x >= -8.90f && x <= -3.80f)
            {
                float[] sYs = { 2.45f, 1.40f, 0.35f, -0.70f, -1.75f };
                for (int k = 0; k < 5; k++)
                {
                    if (Mathf.Abs(y - sYs[k]) <= 0.48f)
                    {
                        ToggleCategorizedSlot(k);
                        return;
                    }
                }
            }

            // Strip All Hardware button inside panel at Y in [-3.10f, -2.40f]
            if (y >= -3.10f && y <= -2.40f)
            {
                if (x >= -8.90f && x <= -3.80f) { DisassembleAllParts(); return; }
            }
        }

        if (currentPhase == MissionPhase.Launching && Camera.main != null)
        {
            float consoleCenterY = (Camera.main.transform.position.y - Camera.main.orthographicSize) + 0.95f;
            float relY = y - consoleCenterY;

            // Throttle buttons (x around -4.0f)
            if (x >= -5.2f && x <= -2.8f)
            {
                if (Mathf.Abs(relY - 0.22f) <= 0.18f) { SetFlightThrottle(1.0f); return; }
                if (Mathf.Abs(relY - (-0.14f)) <= 0.18f) { SetFlightThrottle(0.65f); return; }
                if (Mathf.Abs(relY - (-0.50f)) <= 0.18f) { SetFlightThrottle(0.0f); return; }
            }
            // Staging button (x around -1.35f)
            if (x >= -2.6f && x <= -0.1f && Mathf.Abs(relY - (-0.14f)) <= 0.50f)
            {
                TriggerFlightStageAction();
                return;
            }
            // Gimbal Steer (x around 0.75f and 1.95f)
            if (Mathf.Abs(relY - (-0.05f)) <= 0.35f)
            {
                if (x >= 0.2f && x <= 1.3f) { AdjustGimbalSteering(-0.35f); return; }
                if (x >= 1.4f && x <= 2.5f) { AdjustGimbalSteering(0.35f); return; }
            }
        }

        if (IsInsideBox(x, y, -1.65f, -3.10f, 2.4f, 0.58f)) { OnLeftBlueButtonClicked(); return; }
        if (IsInsideBox(x, y,  1.05f, -3.10f, 2.4f, 0.58f)) { OnRightBlueButtonClicked(); return; }
        if (IsInsideBox(x, y, -1.65f, -3.90f, 2.4f, 0.54f)) { AdjustFuelAndKits(-50f, -1); return; }
        if (IsInsideBox(x, y,  1.05f, -3.90f, 2.4f, 0.54f)) { AdjustFuelAndKits(50f, 1); return; }
        if (IsInsideBox(x, y,  5.05f, -3.65f, 3.3f, 0.78f)) { StartLaunchSequence(); return; }
        if (IsInsideBox(x, y,  7.85f, -3.65f, 1.8f, 0.78f)) { ResetToAssembly(); return; }
        if (currentPhase == MissionPhase.Assembly && IsInsideBox(x, y, -6.35f, -4.45f, 5.4f, 0.85f)) { AutoEquipRecommendedPreset(); return; }
        if (currentPhase == MissionPhase.Assembly && IsInsideBox(x, y,  5.05f, -4.45f, 3.4f, 0.75f)) { OpenFlightManual(); return; }
        if (currentPhase == MissionPhase.Assembly && IsInsideBox(x, y,  7.85f, -4.45f, 1.9f, 0.75f)) { ShowTitleScreen(); return; }

        if (currentPhase == MissionPhase.Completed && Camera.main != null)
        {
            float camY = Camera.main.transform.position.y;
            if (IsInsideBox(x, y, -0.3f, camY - 1.70f, 2.0f, 0.9f)) { ResetToAssembly(); return; }
        }
    }

    private bool IsInsideBox(float x, float y, float cx, float cy, float w, float h)
    {
        return Mathf.Abs(x - cx) <= (w * 0.52f) && Mathf.Abs(y - cy) <= (h * 0.52f);
    }

    private bool ProcessClickedObjectName(string objName)
    {
        if (objName == "BTN_LaunchSim")   { StartLaunchSequence(); return true; }
        if (objName == "BTN_Reset")       { ResetToAssembly(); return true; }
        if (objName == "BTN_CycleTarget") { OnLeftBlueButtonClicked(); return true; }
        if (objName == "BTN_CycleRocket") { OnRightBlueButtonClicked(); return true; }
        if (objName == "BTN_MoreFuel")    { AdjustFuelAndKits(50f, 1); return true; }
        if (objName == "BTN_LessFuel")    { AdjustFuelAndKits(-50f, -1); return true; }
        if (objName == "BTN_AutoPreset" || objName == "BTN_AutoPreset_Label" || objName == "BTN_CategorizedAutoPreset" || objName == "BTN_CategorizedAutoPreset_Label") { AutoEquipRecommendedPreset(); return true; }
        if (objName == "BTN_StripAll" || objName == "BTN_StripAll_Label") { DisassembleAllParts(); return true; }
        if (objName == "TAB_BTN_STAGES" || objName == "TAB_BTN_STAGES_Label") { SwitchPartsTab(0); return true; }
        if (objName == "TAB_BTN_POWER" || objName == "TAB_BTN_POWER_Label") { SwitchPartsTab(1); return true; }
        if (objName == "TAB_BTN_SCIENCE" || objName == "TAB_BTN_SCIENCE_Label") { SwitchPartsTab(2); return true; }
        if (objName == "BTN_StartMission" || objName == "BTN_StartMission_Label") { StartMissionFromTitle(); return true; }
        if (objName == "BTN_FlightManual" || objName == "BTN_FlightManual_Label" || objName == "BTN_Manual" || objName == "BTN_Manual_Label") { OpenFlightManual(); return true; }
        if (objName == "BTN_CloseManual" || objName == "BTN_CloseManual_Label") { CloseFlightManual(); return true; }
        if (objName == "BTN_Home" || objName == "BTN_Home_Label") { ShowTitleScreen(); return true; }

        // Flight Pilot Console
        if (objName == "BTN_Throttle100" || objName == "BTN_Throttle100_Label") { SetFlightThrottle(1.0f); return true; }
        if (objName == "BTN_Throttle65"  || objName == "BTN_Throttle65_Label")  { SetFlightThrottle(0.65f); return true; }
        if (objName == "BTN_Throttle0"   || objName == "BTN_Throttle0_Label")   { SetFlightThrottle(0.0f); return true; }
        if (objName == "BTN_FlightStage" || objName == "BTN_FlightStage_Label") { TriggerFlightStageAction(); return true; }
        if (objName == "BTN_GimbalLeft"  || objName == "BTN_GimbalLeft_Label")  { AdjustGimbalSteering(-0.35f); return true; }
        if (objName == "BTN_GimbalRight" || objName == "BTN_GimbalRight_Label") { AdjustGimbalSteering(0.35f); return true; }

        if (currentPhase == MissionPhase.Assembly)
        {
            if (objName.StartsWith("PART_CARD_BG_") || objName.StartsWith("PART_TAB_SLOT_") || objName.StartsWith("PART_CARD_LABEL_"))
            {
                string suffix = objName.Substring(objName.LastIndexOf('_') + 1);
                if (int.TryParse(suffix, out int slotIdx))
                {
                    ToggleCategorizedSlot(slotIdx);
                    return true;
                }
            }
        }
        return false;
    }

    private void EnsurePartsPanelBounds()
    {
        Transform catalogGroup = transform.Find("Assembly_And_Catalog_Group");
        if (catalogGroup == null) return;

        // Trim parts panel background so it ends neatly under part 7 (THEMIS at y = -3.45),
        // leaving the AUTO-RECOMMEND button sitting cleanly *under* the panel.
        Transform panelBox = catalogGroup.Find("Left_Parts_Panel_Box");
        if (panelBox != null)
        {
            panelBox.localPosition = new Vector3(-6.35f, -0.05f, 0.6f);
            panelBox.localScale = new Vector3(5.50f, 7.80f, 0.1f);
        }

        Transform panelBorder = catalogGroup.Find("Left_Parts_Panel_Box_Border");
        if (panelBorder != null)
        {
            panelBorder.localPosition = new Vector3(-6.35f, -0.09f, 0.65f);
            panelBorder.localScale = new Vector3(5.58f, 7.88f, 0.1f);
        }

        for (int i = catalogGroup.childCount - 1; i >= 0; i--)
        {
            Transform c = catalogGroup.GetChild(i);
            if (c != null && (c.name.Contains("Scrollbar") || c.name.Contains("Selectable") || c.name == "Catalog_Header"))
            {
                c.gameObject.SetActive(false);
                Destroy(c.gameObject);
            }
        }
    }

    private void EnsureAutoPresetButtonCreated()
    {
        string[] toClean = { "BTN_AutoPreset", "BTN_AutoPreset_Border", "BTN_AutoPreset_Label" };
        foreach (string n in toClean)
        {
            Transform t = transform.Find(n);
            if (t != null) Destroy(t.gameObject);
        }

        // Dedicated Auto-Recommend Button directly under the Parts Panel
        GameObject autoBtn = GameObject.CreatePrimitive(PrimitiveType.Quad);
        autoBtn.name = "BTN_AutoPreset";
        autoBtn.transform.SetParent(transform, false);
        autoBtn.transform.localPosition = new Vector3(-6.35f, -4.45f, 0f);
        autoBtn.transform.localScale = new Vector3(5.20f, 0.65f, 0.1f);
        SetRendererColor(autoBtn.GetComponent<Renderer>(), CardDarkBg);

        GameObject autoBorder = GameObject.CreatePrimitive(PrimitiveType.Quad);
        autoBorder.name = "BTN_AutoPreset_Border";
        autoBorder.transform.SetParent(transform, false);
        autoBorder.transform.localPosition = new Vector3(-6.35f, -4.49f, 0.05f);
        autoBorder.transform.localScale = new Vector3(5.28f, 0.72f, 0.1f);
        Destroy(autoBorder.GetComponent<Collider>());
        SetRendererColor(autoBorder.GetComponent<Renderer>(), DuoGreen);

        CreateUIText(transform, "BTN_AutoPreset_Label", new Vector3(-6.35f, -4.45f, -0.1f),
            "⚡ AUTO-RECOMMEND LOADOUT", 0.056f, 36, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, DuoGreen, 25);
    }

    private void EnsureHomeButtonCreated()
    {
        string[] toClean = { "BTN_Home", "BTN_Home_Border", "BTN_Home_Label", "BTN_Manual", "BTN_Manual_Border", "BTN_Manual_Label" };
        foreach (string n in toClean)
        {
            Transform t = transform.Find(n);
            if (t != null) Destroy(t.gameObject);
        }

        // Button 1: Flight Manual (x = 5.05f, width = 3.30f) - perfectly aligned under LAUNCH MISSION
        GameObject manualBtn = GameObject.CreatePrimitive(PrimitiveType.Quad);
        manualBtn.name = "BTN_Manual";
        manualBtn.transform.SetParent(transform, false);
        manualBtn.transform.localPosition = new Vector3(5.05f, -4.45f, 0f);
        manualBtn.transform.localScale = new Vector3(3.30f, 0.65f, 0.1f);
        SetRendererColor(manualBtn.GetComponent<Renderer>(), CardDarkBg);

        GameObject manualBorder = GameObject.CreatePrimitive(PrimitiveType.Quad);
        manualBorder.name = "BTN_Manual_Border";
        manualBorder.transform.SetParent(transform, false);
        manualBorder.transform.localPosition = new Vector3(5.05f, -4.49f, 0.05f);
        manualBorder.transform.localScale = new Vector3(3.38f, 0.72f, 0.1f);
        Destroy(manualBorder.GetComponent<Collider>());
        SetRendererColor(manualBorder.GetComponent<Renderer>(), DuoBlue);

        CreateUIText(transform, "BTN_Manual_Label", new Vector3(5.05f, -4.45f, -0.1f),
            "📖 FLIGHT MANUAL", 0.052f, 36, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, new Color(0.35f, 0.85f, 1.0f), 25);

        // Button 2: Title Screen (x = 7.85f, width = 1.80f) - perfectly aligned under RESET
        GameObject homeBtn = GameObject.CreatePrimitive(PrimitiveType.Quad);
        homeBtn.name = "BTN_Home";
        homeBtn.transform.SetParent(transform, false);
        homeBtn.transform.localPosition = new Vector3(7.85f, -4.45f, 0f);
        homeBtn.transform.localScale = new Vector3(1.80f, 0.65f, 0.1f);
        SetRendererColor(homeBtn.GetComponent<Renderer>(), CardDarkBg);

        GameObject homeBorder = GameObject.CreatePrimitive(PrimitiveType.Quad);
        homeBorder.name = "BTN_Home_Border";
        homeBorder.transform.SetParent(transform, false);
        homeBorder.transform.localPosition = new Vector3(7.85f, -4.49f, 0.05f);
        homeBorder.transform.localScale = new Vector3(1.88f, 0.72f, 0.1f);
        Destroy(homeBorder.GetComponent<Collider>());
        SetRendererColor(homeBorder.GetComponent<Renderer>(), DuoGold);

        CreateUIText(transform, "BTN_Home_Label", new Vector3(7.85f, -4.45f, -0.1f),
            "🏠 TITLE", 0.052f, 36, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, DuoGold, 25);
    }

    private GameObject CreateUIQuad(Transform parent, string name, Vector3 pos, Vector3 scale, Color col, bool hasCollider = false, int sortingOrder = 0)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localScale = scale;
        if (!hasCollider)
        {
            Collider c = q.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
        Material m = new Material(sh);
        m.color = col;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        Renderer r = q.GetComponent<Renderer>();
        r.material = m;
        if (sortingOrder != 0) r.sortingOrder = sortingOrder;
        return q;
    }

    private TextMesh CreateUIText(Transform parent, string name, Vector3 pos, string text, float charSize, int fontSize, FontStyle style, TextAnchor anchor, TextAlignment align, Color col, int sortingOrder = 0)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        TextMesh tm = obj.AddComponent<TextMesh>();
        tm.text = text;
        tm.characterSize = charSize;
        tm.fontSize = fontSize;
        tm.fontStyle = style;
        tm.anchor = anchor;
        tm.alignment = align;
        tm.color = col;
        tm.richText = true;

        Font defaultFont = null;
        try
        {
            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch { }

        if (defaultFont == null)
        {
            try
            {
                defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch { }
        }

        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
        if (defaultFont != null)
        {
            tm.font = defaultFont;
            if (mr != null && defaultFont.material != null)
            {
                mr.sharedMaterial = defaultFont.material;
            }
        }

        if (mr != null && sortingOrder != 0)
        {
            mr.sortingOrder = sortingOrder;
        }

        return tm;
    }

    private void EnsureTitleScreenCreated()
    {
        if (titleScreenGroup != null)
        {
            Destroy(titleScreenGroup);
            titleScreenGroup = null;
        }

        Transform existing = transform.Find("Title_Screen_Group");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        titleScreenGroup = new GameObject("Title_Screen_Group");
        titleScreenGroup.transform.SetParent(transform, false);
        titleScreenGroup.transform.localPosition = new Vector3(0f, 0f, -2.0f);
        titleScreenGroup.transform.localScale = Vector3.one;
        titleScreenGroup.transform.localRotation = Quaternion.identity;

        // Fullscreen Backdrop Overlay (dims the launchpad ground & stars behind the modal)
        CreateUIQuad(titleScreenGroup.transform, "Title_Backdrop_Overlay", new Vector3(0f, 0f, 0.25f), new Vector3(25.0f, 15.0f, 0.1f), new Color(0.01f, 0.03f, 0.06f, 0.78f), false, 20);

        // Main Background Dark Card (12.4f x 7.8f)
        CreateUIQuad(titleScreenGroup.transform, "Title_Card_Bg", new Vector3(0f, 0f, 0.05f), new Vector3(12.4f, 7.8f, 0.1f), new Color(0.04f, 0.07f, 0.12f, 0.98f), false, 22);

        // Border / Cyber-Cyan Frame
        CreateUIQuad(titleScreenGroup.transform, "Title_Card_Border", new Vector3(0f, 0f, 0.10f), new Vector3(12.56f, 7.96f, 0.1f), new Color(0.12f, 0.72f, 1.0f, 1.0f), false, 21);

        // Top Gold Accent Line
        CreateUIQuad(titleScreenGroup.transform, "Title_Gold_Accent", new Vector3(0f, 3.65f, -0.02f), new Vector3(11.6f, 0.05f, 0.1f), DuoGold, false, 23);

        // NASA Badge Header
        CreateUIText(titleScreenGroup.transform, "Title_Header_Badge", new Vector3(0f, 3.35f, -0.05f),
            "★ NASA SPACE APPS CHALLENGE // MISSION CONTROL COMMAND ★", 0.072f, 38, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, DuoGold, 25);

        // Main Game Title (Refined size, a little smaller and perfectly balanced)
        CreateUIText(titleScreenGroup.transform, "Title_Main_Heading", new Vector3(0f, 2.62f, -0.05f),
            "NASA ODYSSEY: DEEP SPACE ARCHITECT", 0.092f, 44, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 25);

        // Subtitle / Premise (Single sleek line, no wrapping awkwardness)
        string subtitle = "Design certified spacecraft, survive Max-Q aerodynamic shear & command deep-space orbit.";
        CreateUIText(titleScreenGroup.transform, "Title_Subtitle", new Vector3(0f, 2.02f, -0.05f),
            subtitle, 0.060f, 32, FontStyle.Normal, TextAnchor.MiddleCenter, TextAlignment.Center, new Color(0.85f, 0.94f, 1.0f), 25);

        // 3 Pillars Cards (Width: 3.7f, Height: 2.05f, centered at y = 0.35f)
        // Pillar 1: Architect
        CreatePillarCard(titleScreenGroup.transform, -3.95f, 0.35f, "1. ARCHITECT", DuoBlue,
            "• Equip Bus, Solar & MMRTG" + NL +
            "• Balance Mass vs Delta-V" + NL +
            "• Install Comms & Sensors" + NL + NL +
            "<color=#3498DB><b>Status: Pre-Flight Certified</b></color>");

        // Pillar 2: Ascend
        CreatePillarCard(titleScreenGroup.transform, 0f, 0.35f, "2. ASCEND", DuoOrange,
            "• Safe-Q Throttle Bucket (S)" + NL +
            "• Booster Staging & Fairing [SPACE]" + NL +
            "• Gimbal Vectoring [A / D]" + NL + NL +
            "<color=#E67E22><b>Live Pilot Command Deck</b></color>");

        // Pillar 3: Orbit
        CreatePillarCard(titleScreenGroup.transform, 3.95f, 0.35f, "3. ORBIT", DuoGreen,
            "• Battery Sizing for Umbra" + NL +
            "• Fire High-Isp Xenon Drive" + NL +
            "• Transmit Science to DSN" + NL + NL +
            "<color=#2ECC71><b>Target: 100% Science Yield</b></color>");

        // Button 1: Commence Mission (4.5f x 0.85f)
        CreateUIQuad(titleScreenGroup.transform, "BTN_StartMission", new Vector3(-2.55f, -1.55f, -0.08f), new Vector3(4.5f, 0.85f, 0.1f), DuoGreen, true, 23);
        CreateUIQuad(titleScreenGroup.transform, "BTN_StartMission_Border", new Vector3(-2.55f, -1.60f, -0.04f), new Vector3(4.58f, 0.93f, 0.1f), DuoGreenDark, false, 23);
        CreateUIText(titleScreenGroup.transform, "BTN_StartMission_Label", new Vector3(-2.55f, -1.55f, -0.15f),
            "▶ COMMENCE MISSION", 0.085f, 42, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 25);

        // Button 2: Flight Manual (4.5f x 0.85f)
        CreateUIQuad(titleScreenGroup.transform, "BTN_FlightManual", new Vector3(2.55f, -1.55f, -0.08f), new Vector3(4.5f, 0.85f, 0.1f), new Color(0.12f, 0.35f, 0.68f, 1.0f), true, 23);
        CreateUIQuad(titleScreenGroup.transform, "BTN_FlightManual_Border", new Vector3(2.55f, -1.60f, -0.04f), new Vector3(4.58f, 0.93f, 0.1f), new Color(0.20f, 0.80f, 1.0f, 1.0f), false, 23);
        CreateUIText(titleScreenGroup.transform, "BTN_FlightManual_Label", new Vector3(2.55f, -1.55f, -0.15f),
            "FLIGHT MANUAL", 0.085f, 42, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 25);

        // Footer attribution
        CreateUIText(titleScreenGroup.transform, "Title_Footer", new Vector3(0f, -2.55f, -0.05f),
            "PRODUCED FOR NASA SPACE APPS CHALLENGE // AEROSPACE SYSTEMS SIMULATOR", 0.052f, 28, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, new Color(0.60f, 0.75f, 0.90f), 25);
    }

    private void CreatePillarCard(Transform parent, float x, float y, string header, Color accentCol, string body)
    {
        Transform pillarGroup = new GameObject("Pillar_" + header.Replace(" ", "_")).transform;
        pillarGroup.SetParent(parent, false);
        pillarGroup.localPosition = new Vector3(x, y, 0f);

        CreateUIQuad(pillarGroup, "Bg", Vector3.zero, new Vector3(3.7f, 2.05f, 0.1f), new Color(0.06f, 0.12f, 0.20f, 0.95f), false, 23);
        CreateUIQuad(pillarGroup, "Border", new Vector3(0f, 0f, 0.03f), new Vector3(3.82f, 2.17f, 0.1f), accentCol, false, 23);

        CreateUIText(pillarGroup, "Header", new Vector3(0f, 0.72f, -0.05f),
            "[ " + header + " ]", 0.082f, 40, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, accentCol, 25);

        CreateUIText(pillarGroup, "Body", new Vector3(-1.65f, 0.38f, -0.05f),
            body, 0.060f, 32, FontStyle.Normal, TextAnchor.UpperLeft, TextAlignment.Left, new Color(0.90f, 0.94f, 1.0f), 25);
    }

    private void EnsureManualModalCreated()
    {
        if (manualModalObj != null)
        {
            Destroy(manualModalObj);
            manualModalObj = null;
        }

        Transform existing = transform.Find("Flight_Manual_Modal");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        manualModalObj = new GameObject("Flight_Manual_Modal");
        manualModalObj.transform.SetParent(transform, false);
        manualModalObj.transform.localPosition = new Vector3(0f, 0f, -3.0f);
        manualModalObj.transform.localScale = Vector3.one;
        manualModalObj.transform.localRotation = Quaternion.identity;

        // Backdrop Overlay for Modal
        CreateUIQuad(manualModalObj.transform, "Manual_Backdrop_Overlay", new Vector3(0f, 0f, 0.25f), new Vector3(25.0f, 15.0f, 0.1f), new Color(0.01f, 0.02f, 0.05f, 0.92f), false, 50);

        // Dark Background Card (12.4f x 7.8f)
        CreateUIQuad(manualModalObj.transform, "Manual_Card_Bg", new Vector3(0f, 0f, 0.05f), new Vector3(12.4f, 7.8f, 0.1f), new Color(0.03f, 0.05f, 0.10f, 0.98f), false, 52);

        // Gold Border Frame
        CreateUIQuad(manualModalObj.transform, "Manual_Card_Border", new Vector3(0f, 0f, 0.10f), new Vector3(12.56f, 7.96f, 0.1f), DuoGold, false, 51);

        // Header Title
        CreateUIText(manualModalObj.transform, "Manual_Header_Title", new Vector3(0f, 3.40f, -0.05f),
            "★ NASA FLIGHT DIRECTOR'S OPERATIONAL HANDBOOK ★", 0.082f, 42, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, DuoGold, 55);

        CreateUIQuad(manualModalObj.transform, "Manual_Header_Divider", new Vector3(0f, 3.05f, -0.02f), new Vector3(11.6f, 0.04f, 0.1f), DuoGold, false, 53);

        // Two Balanced Columns (Left: Flight Physics & Power, Right: Hazards & Automation)
        // Column 1: Left Card (x = -2.90f, y = 0.10f, size 5.6f x 4.8f)
        CreateUIQuad(manualModalObj.transform, "Manual_Col1_Bg", new Vector3(-2.90f, 0.10f, 0.02f), new Vector3(5.6f, 4.8f, 0.1f), new Color(0.05f, 0.08f, 0.15f, 0.92f), false, 53);
        CreateUIQuad(manualModalObj.transform, "Manual_Col1_Border", new Vector3(-2.90f, 0.10f, 0.04f), new Vector3(5.68f, 4.88f, 0.1f), new Color(0.12f, 0.50f, 0.85f, 0.70f), false, 53);

        string col1Text =
            "<color=#1CB0F6><b>1. TSIOLKOVSKY & DELTA-V:</b></color>" + NL +
            " • LEO Target: 1,600 m/s (Low Orbit)" + NL +
            " • Moon Target: 2,800 m/s (Lunar Polar)" + NL +
            " • Mars Target: 3,800 m/s (Deep Space)" + NL +
            " • Heavy wet payload cuts ΔV exponentially." + NL +
            "   Always maintain positive ΔV margin!" + NL + NL +
            "<color=#FFC800><b>2. POWER & PLANETARY ECLIPSE:</b></color>" + NL +
            " • Solar arrays yield 0W in eclipse umbra." + NL +
            " • Mars solar flux drops to 45% sunlight." + NL +
            " • MMRTG nuclear core provides continuous" + NL +
            "   24/7 power in shadows and deep range." + NL +
            " • Size battery watt-hours for blackout!";

        CreateUIText(manualModalObj.transform, "Manual_Col1_Text", new Vector3(-5.35f, 2.22f, -0.05f),
            col1Text, 0.056f, 32, FontStyle.Normal, TextAnchor.UpperLeft, TextAlignment.Left, Color.white, 55);

        // Column 2: Right Card (x = +2.90f, y = 0.10f, size 5.6f x 4.8f)
        CreateUIQuad(manualModalObj.transform, "Manual_Col2_Bg", new Vector3(2.90f, 0.10f, 0.02f), new Vector3(5.6f, 4.8f, 0.1f), new Color(0.05f, 0.08f, 0.15f, 0.92f), false, 53);
        CreateUIQuad(manualModalObj.transform, "Manual_Col2_Border", new Vector3(2.90f, 0.10f, 0.04f), new Vector3(5.68f, 4.88f, 0.1f), new Color(0.85f, 0.45f, 0.10f, 0.70f), false, 53);

        string col2Text =
            "<color=#FF4B4B><b>3. PILOTING & TRANSONIC HAZARDS:</b></color>" + NL +
            " • Max-Q (Y=5.8-8.8): Throttle to 65% [S]" + NL +
            "   or install Grid Fins to withstand shear." + NL +
            " • MECO (Y=10.2): Press [SPACE] to decouple" + NL +
            "   booster stage & ignite upper S-II engine." + NL +
            " • Vacuum (Y=12.2): Press [SPACE] to jettison" + NL +
            "   fairing shroud & deploy ROSA solar wings." + NL +
            " • Radiation (Y=16): Bus avionics shielding" + NL +
            "   absorbs high-energy particle radiation." + NL +
            " • Trajectory (Y=21): Use [A / D] gimbal to" + NL +
            "   hold flight corridor and evade debris." + NL + NL +
            "<color=#58CC02><b>4. ZERO-BUILD & NASA PRESETS:</b></color>" + NL +
            " • Build from Scratch: Equip custom parts!" + NL +
            " • 1-Click Loadout: Click [AUTO-RECOMMEND]" + NL +
            "   for NASA flight-certified stage configs!" + NL +
            " • Clean Reset: Click [STRIP TO ZERO].";

        CreateUIText(manualModalObj.transform, "Manual_Col2_Text", new Vector3(0.45f, 2.22f, -0.05f),
            col2Text, 0.056f, 32, FontStyle.Normal, TextAnchor.UpperLeft, TextAlignment.Left, Color.white, 55);

        // Close Button (5.2f x 0.78f)
        CreateUIQuad(manualModalObj.transform, "BTN_CloseManual", new Vector3(0f, -2.85f, -0.08f), new Vector3(5.2f, 0.78f, 0.1f), DuoOrange, true, 53);
        CreateUIQuad(manualModalObj.transform, "BTN_CloseManual_Border", new Vector3(0f, -2.90f, -0.04f), new Vector3(5.28f, 0.86f, 0.1f), DuoRed, false, 53);
        CreateUIText(manualModalObj.transform, "BTN_CloseManual_Label", new Vector3(0f, -2.85f, -0.15f),
            "✕ CLOSE MANUAL & RETURN", 0.078f, 40, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, Color.white, 55);

        manualModalObj.SetActive(false);
    }

    public void ShowTitleScreen()
    {
        currentPhase = MissionPhase.TitleScreen;
        EnsureTitleScreenCreated();
        if (titleScreenGroup != null) titleScreenGroup.SetActive(true);
        if (manualModalObj != null) manualModalObj.SetActive(false);
        SetAssemblyUIVisible(false);

        if (spacecraftHub != null) spacecraftHub.gameObject.SetActive(false);
        if (assemblyGantryGroup != null) assemblyGantryGroup.SetActive(false);
        if (mapTargetLabel != null) mapTargetLabel.gameObject.SetActive(false);
    }

    public void StartMissionFromTitle()
    {
        PlayTelemetryBeep();
        if (titleScreenGroup != null) titleScreenGroup.SetActive(false);
        if (manualModalObj != null) manualModalObj.SetActive(false);
        if (spacecraftHub != null) spacecraftHub.gameObject.SetActive(true);
        if (assemblyGantryGroup != null) assemblyGantryGroup.SetActive(true);
        ResetToAssembly();
    }

    public void OpenFlightManual()
    {
        PlayTelemetryBeep();
        if (titleScreenGroup != null) titleScreenGroup.SetActive(false);
        SetAssemblyUIVisible(false);
        if (spacecraftHub != null) spacecraftHub.gameObject.SetActive(false);
        if (assemblyGantryGroup != null) assemblyGantryGroup.SetActive(false);
        if (mapTargetLabel != null) mapTargetLabel.gameObject.SetActive(false);
        EnsureManualModalCreated();
        if (manualModalObj != null) manualModalObj.SetActive(true);
    }

    public void CloseFlightManual()
    {
        PlayTelemetryBeep();
        if (manualModalObj != null) manualModalObj.SetActive(false);
        if (currentPhase == MissionPhase.TitleScreen)
        {
            ShowTitleScreen();
        }
        else if (currentPhase == MissionPhase.Assembly)
        {
            ResetToAssembly();
        }
    }

    public void AutoEquipRecommendedPreset()
    {
        if (currentPhase != MissionPhase.Assembly) return;

        PlayTelemetryBeep();

        // 1. Wipe all 14 parts to clean slate
        if (parts != null)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null) parts[i].isInstalled = false;
            }
        }

        if (selectedTargetIndex == 0) // Low Earth Orbit (LEO) - $1000M budget, 1600 m/s dV, 80 Sci
        {
            selectedRocketIndex = 1; // Falcon 9 NASA-LSP ($80M, 3100kg)
            fuelMassKg = 450f;
            repairKits = 2;

            if (parts != null && parts.Count >= 14)
            {
                parts[7].isInstalled = true;  // Kerolox Booster Stage
                parts[8].isInstalled = false; // SRBs not needed for LEO
                parts[9].isInstalled = true;  // Vacuum Upper Stage S-II
                parts[10].isInstalled = true; // Titanium Grid Fins
                parts[11].isInstalled = true; // Payload Fairing Shroud
                parts[12].isInstalled = true; // Satellite Avionics Bus
                parts[0].isInstalled = true;  // ROSA Roll-Out Solar
                parts[1].isInstalled = false; // MMRTG Core
                parts[2].isInstalled = true;  // Battery
                parts[3].isInstalled = true;  // Ka-Band Dish
                parts[4].isInstalled = true;  // HiRISE Camera (+60 Sci)
                parts[5].isInstalled = false; // NEXT-C not needed for LEO
                parts[6].isInstalled = true;  // THEMIS IR (+35 Sci)
                parts[13].isInstalled = false; // Magnetometer
            }
        }
        else if (selectedTargetIndex == 1) // Lunar Polar Orbit - $1400M budget, 2800 m/s dV, 120 Sci
        {
            selectedRocketIndex = 1; // Falcon 9 NASA-LSP ($80M, 3100kg)
            fuelMassKg = 550f;
            repairKits = 2;

            if (parts != null && parts.Count >= 14)
            {
                parts[7].isInstalled = true;  // Kerolox Booster Stage
                parts[8].isInstalled = true;  // Twin Strap-On SRBs
                parts[9].isInstalled = true;  // Vacuum Upper Stage S-II
                parts[10].isInstalled = true; // Titanium Grid Fins
                parts[11].isInstalled = true; // Payload Fairing Shroud
                parts[12].isInstalled = true; // Satellite Avionics Bus
                parts[0].isInstalled = true;  // ROSA Roll-Out Solar
                parts[1].isInstalled = false; // MMRTG Core
                parts[2].isInstalled = true;  // Battery
                parts[3].isInstalled = true;  // Ka-Band Dish
                parts[4].isInstalled = true;  // HiRISE Camera (+60 Sci)
                parts[5].isInstalled = true;  // NEXT-C Ion Thruster
                parts[6].isInstalled = true;  // THEMIS IR (+35 Sci)
                parts[13].isInstalled = true; // Magnetometer (+25 Sci)
            }
        }
        else // Mars Deep Space Orbit - $1850M budget, 3800 m/s dV, 180 Sci
        {
            selectedRocketIndex = 2; // NASA SLS Block 1B ($150M, 4500kg)
            fuelMassKg = 750f;
            repairKits = 2;

            if (parts != null && parts.Count >= 14)
            {
                parts[7].isInstalled = true;  // Kerolox Booster Stage
                parts[8].isInstalled = true;  // Twin Strap-On SRBs
                parts[9].isInstalled = true;  // Vacuum Upper Stage S-II
                parts[10].isInstalled = true; // Titanium Grid Fins
                parts[11].isInstalled = true; // Payload Fairing Shroud
                parts[12].isInstalled = true; // Satellite Avionics Bus
                parts[0].isInstalled = true;  // ROSA Roll-Out Solar
                parts[1].isInstalled = true;  // MMRTG Nuclear Core (vital for 45% solar distance!)
                parts[2].isInstalled = true;  // Battery
                parts[3].isInstalled = true;  // Ka-Band Dish
                parts[4].isInstalled = true;  // HiRISE Camera (+60 Sci)
                parts[5].isInstalled = true;  // NEXT-C Ion Thruster
                parts[6].isInstalled = true;  // THEMIS IR (+35 Sci)
                parts[13].isInstalled = true; // Magnetometer (+25 Sci)
            }
        }

        if (parts != null)
        {
            foreach (var p in parts)
            {
                if (p != null) UpdatePartVisuals(p);
            }
        }

        RefreshCategorizedPartsUI();
        UpdateAllRocketHardwareVisuals();
        RecalculateTelemetry();
        SetStatusBanner(
            "OPTIMAL NASA LOADOUT CONFIGURED!",
            $"Equipped flight-certified hardware, fuel & margins for {targetNames[selectedTargetIndex]}! All requirements PASS [GO FOR LAUNCH].",
            DuoGreen
        );
    }

    // =========================================================================
    // 2. SPACECRAFT SUBSYSTEM MOUNTING & VISUALS
    // =========================================================================
    public void TogglePart(int index)
    {
        if (index < 0 || index >= parts.Count) return;
        PlayTelemetryBeep();
        parts[index].isInstalled = !parts[index].isInstalled;
        UpdatePartVisuals(parts[index]);
        UpdateAllRocketHardwareVisuals();
        RefreshCategorizedPartsUI();
        RecalculateTelemetry();

        bool hasBus = (parts != null && parts.Count > 12 && parts[12] != null && parts[12].isInstalled);

        if (remainingBudgetMillion < 0f)
            SetStatusBanner("OVER BUDGET RISK!", "You exceeded $" + maxBudgetMillion.ToString("F0") + "M! Remove a subsystem or choose a smaller launcher.", DuoRed);
        else if (parts[index].isInstalled && index < 7 && !hasBus)
            SetStatusBanner("BUS CHASSIS REQUIRED", "Equipped " + parts[index].partTitle + ", but it requires the Satellite Avionics Bus (under [ 🛰️ POWER ]) to physically mount!", DuoOrange);
        else
            SetStatusBanner(parts[index].isInstalled ? "SUBSYSTEM INSTALLED: " + parts[index].partTitle : "SUBSYSTEM REMOVED: " + parts[index].partTitle,
                "Check the Right Panel to see how this trade-off impacts Mass, Power, Risk, and Science Reward!", DuoGreen);
    }

    public void UpdatePartVisuals(PartSpec p)
    {
        if (p == null) return;

        bool hasBus = (parts != null && parts.Count > 12 && parts[12] != null && parts[12].isInstalled);

        if (p.partTransform != null && spacecraftHub != null)
        {
            bool showOnBus = p.isInstalled && hasBus;
            p.partTransform.gameObject.SetActive(showOnBus);
            if (showOnBus)
            {
                p.partTransform.SetParent(spacecraftHub, false);
                p.partTransform.localPosition = p.busLocalOffset;
                p.partTransform.localRotation = Quaternion.identity;
                p.partTransform.localScale = p.mountedScale;
            }
        }

        // Permanently deactivate legacy scene slot objects so they never overlap
        if (p.slotCardRenderer != null) p.slotCardRenderer.gameObject.SetActive(false);
        if (p.slotShadowRenderer != null) p.slotShadowRenderer.gameObject.SetActive(false);
        if (p.labelMesh != null) p.labelMesh.gameObject.SetActive(false);
    }

    private void SetRendererColor(Renderer r, Color col)
    {
        if (r == null) return;
        if (Application.isPlaying) r.material.color = col;
        else if (r.sharedMaterial != null) r.sharedMaterial.color = col;
    }

    // =========================================================================
    // 3. RISK VS. REWARD, BUDGET & MISSION REQUIREMENTS
    // =========================================================================
    public void OnLeftBlueButtonClicked()
    {
        PlayTelemetryBeep();
        if (currentPhase == MissionPhase.FlightCrisis) { ResolveFlightCrisis(true); return; }
        if (currentPhase != MissionPhase.Assembly) ResetToAssembly();
        selectedTargetIndex = (selectedTargetIndex + 1) % targetNames.Length;
        UpdateTargetPlanetVisuals();
        RecalculateTelemetry();
        SetStatusBanner("DESTINATION CHANGED: " + targetNames[selectedTargetIndex],
            targetRiskRating[selectedTargetIndex] + " | Science Reward Multiplier: " + targetRewardMultiplier[selectedTargetIndex].ToString("F1") + "x!", DuoBlue);
    }

    public void OnRightBlueButtonClicked()
    {
        PlayTelemetryBeep();
        if (currentPhase == MissionPhase.FlightCrisis) { ResolveFlightCrisis(false); return; }
        if (currentPhase != MissionPhase.Assembly) ResetToAssembly();
        selectedRocketIndex = (selectedRocketIndex + 1) % rocketNames.Length;
        RecalculateTelemetry();
        SetStatusBanner("LAUNCH VEHICLE CHANGED", "Switched to " + rocketNames[selectedRocketIndex], DuoBlue);
    }

    public void AdjustFuelAndKits(float deltaFuelKg, int deltaKits)
    {
        if (currentPhase != MissionPhase.Assembly) return;
        PlayTelemetryBeep();
        fuelMassKg = Mathf.Clamp(fuelMassKg + deltaFuelKg, 50f, 1200f);
        repairKits = Mathf.Clamp(repairKits + deltaKits, 0, 6);
        RecalculateTelemetry();
        SetStatusBanner("RESOURCES ADJUSTED", "Fuel Reserve: " + fuelMassKg.ToString("F0") + "kg  |  Emergency Repair Kits: " + repairKits, DuoBlue);
    }

    private Transform atmosphereHalo;

    private void UpdateTargetPlanetVisuals()
    {
        if (targetPlanetVisual != null)
        {
            Color planetCol = selectedTargetIndex == 0 ? DuoBlue :
                              (selectedTargetIndex == 1 ? new Color(0.75f, 0.78f, 0.84f) : new Color(0.92f, 0.38f, 0.20f));
            SetRendererColor(targetPlanetVisual.GetComponent<Renderer>(), planetCol);

            Transform pRoot = (orbitRingRoot != null) ? orbitRingRoot : targetPlanetVisual;

            if (atmosphereHalo == null)
            {
                Transform existing = pRoot.Find("Atmosphere_Halo");
                if (existing == null && targetPlanetVisual != null) existing = targetPlanetVisual.Find("Atmosphere_Halo");
                if (existing != null)
                {
                    existing.SetParent(pRoot, false);
                    existing.localPosition = new Vector3(0f, 0f, 0.08f);
                    existing.localRotation = Quaternion.identity;
                    existing.localScale = new Vector3(1.28f, 1.28f, 1f);
                    atmosphereHalo = existing;
                }
                else
                {
                    GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    halo.name = "Atmosphere_Halo";
                    halo.transform.SetParent(pRoot, false);
                    halo.transform.localPosition = new Vector3(0f, 0f, 0.08f);
                    halo.transform.localRotation = Quaternion.identity;
                    halo.transform.localScale = new Vector3(1.28f, 1.28f, 1f);
                    Destroy(halo.GetComponent<Collider>());
                    atmosphereHalo = halo.transform;
                }
            }

            if (atmosphereHalo != null)
            {
                Color haloCol;
                if (selectedTargetIndex == 0) // LEO: Cyan-Blue Earth atmosphere
                    haloCol = new Color(0.18f, 0.70f, 1.0f, 0.55f);
                else if (selectedTargetIndex == 1) // Moon: Soft lunar exosphere silver
                    haloCol = new Color(0.80f, 0.85f, 0.95f, 0.35f);
                else // Mars: Warm Martian dust & CO2 atmospheric glow
                    haloCol = new Color(1.0f, 0.42f, 0.15f, 0.50f);

                SetRendererColor(atmosphereHalo.GetComponent<Renderer>(), haloCol);
            }

            // 1. Earth Continents (LEO Target)
            if (earthContinentsGroup == null)
            {
                Transform existingContinents = pRoot.Find("Earth_Continents_Group");
                if (existingContinents != null) earthContinentsGroup = existingContinents;
                else
                {
                    earthContinentsGroup = new GameObject("Earth_Continents_Group").transform;
                    earthContinentsGroup.SetParent(pRoot, false);
                    earthContinentsGroup.localPosition = new Vector3(0f, 0f, -0.02f);
                    earthContinentsGroup.localRotation = Quaternion.identity;
                    earthContinentsGroup.localScale = new Vector3(0.92f, 0.92f, 1f);

                    CreateUIQuad(earthContinentsGroup, "Cont_Americas", new Vector3(-0.16f, 0.08f, 0f), new Vector3(0.26f, 0.40f, 1f), new Color(0.18f, 0.55f, 0.28f, 0.92f));
                    CreateUIQuad(earthContinentsGroup, "Cont_Eurasia", new Vector3(0.14f, 0.18f, 0f), new Vector3(0.36f, 0.26f, 1f), new Color(0.22f, 0.52f, 0.25f, 0.92f));
                    CreateUIQuad(earthContinentsGroup, "Cont_Africa", new Vector3(0.10f, -0.12f, 0f), new Vector3(0.24f, 0.28f, 1f), new Color(0.48f, 0.45f, 0.22f, 0.92f));
                    CreateUIQuad(earthContinentsGroup, "Cont_Oceania", new Vector3(0.25f, -0.24f, 0f), new Vector3(0.16f, 0.14f, 1f), new Color(0.50f, 0.40f, 0.22f, 0.92f));
                }
            }

            // 2. Earth Cloud Bands (LEO Target)
            if (earthCloudsGroup == null)
            {
                Transform existingClouds = pRoot.Find("Earth_Clouds_Group");
                if (existingClouds == null && targetPlanetVisual != null) existingClouds = targetPlanetVisual.Find("Earth_Clouds_Group");
                if (existingClouds != null)
                {
                    existingClouds.SetParent(pRoot, false);
                    existingClouds.localPosition = new Vector3(0f, 0f, -0.04f);
                    existingClouds.localRotation = Quaternion.identity;
                    existingClouds.localScale = new Vector3(0.92f, 0.92f, 1f);
                    earthCloudsGroup = existingClouds;
                }
                else
                {
                    earthCloudsGroup = new GameObject("Earth_Clouds_Group").transform;
                    earthCloudsGroup.SetParent(pRoot, false);
                    earthCloudsGroup.localPosition = new Vector3(0f, 0f, -0.04f);
                    earthCloudsGroup.localRotation = Quaternion.identity;
                    earthCloudsGroup.localScale = new Vector3(0.92f, 0.92f, 1f);

                    CreateUIQuad(earthCloudsGroup, "Cloud_1", new Vector3(-0.04f, 0.16f, 0f), new Vector3(0.60f, 0.14f, 1f), new Color(1f, 1f, 1f, 0.65f));
                    CreateUIQuad(earthCloudsGroup, "Cloud_2", new Vector3(0.06f, -0.04f, 0f), new Vector3(0.65f, 0.16f, 1f), new Color(1f, 1f, 1f, 0.60f));
                    CreateUIQuad(earthCloudsGroup, "Cloud_3", new Vector3(-0.10f, -0.22f, 0f), new Vector3(0.48f, 0.12f, 1f), new Color(1f, 1f, 1f, 0.55f));
                }
            }

            // 3. Earth Nightside City Lights (LEO Target - Twinkling on Eclipse Shadow side X > 0.10f)
            if (earthNightLightsGroup == null)
            {
                Transform existingLights = pRoot.Find("Earth_NightLights_Group");
                if (existingLights != null) earthNightLightsGroup = existingLights;
                else
                {
                    earthNightLightsGroup = new GameObject("Earth_NightLights_Group").transform;
                    earthNightLightsGroup.SetParent(pRoot, false);
                    earthNightLightsGroup.localPosition = new Vector3(0f, 0f, -0.03f);
                    earthNightLightsGroup.localRotation = Quaternion.identity;
                    earthNightLightsGroup.localScale = new Vector3(0.92f, 0.92f, 1f);

                    CreateUIQuad(earthNightLightsGroup, "City_1", new Vector3(0.16f, 0.14f, 0f), new Vector3(0.06f, 0.06f, 1f), new Color(1.0f, 0.85f, 0.35f, 0.95f));
                    CreateUIQuad(earthNightLightsGroup, "City_2", new Vector3(0.24f, 0.22f, 0f), new Vector3(0.05f, 0.05f, 1f), new Color(1.0f, 0.90f, 0.45f, 0.95f));
                    CreateUIQuad(earthNightLightsGroup, "City_3", new Vector3(0.20f, -0.06f, 0f), new Vector3(0.07f, 0.05f, 1f), new Color(1.0f, 0.80f, 0.30f, 0.95f));
                    CreateUIQuad(earthNightLightsGroup, "City_4", new Vector3(0.30f, -0.16f, 0f), new Vector3(0.05f, 0.04f, 1f), new Color(1.0f, 0.88f, 0.40f, 0.95f));
                }
            }

            // 4. Moon Basaltic Maria / Craters (Lunar Target)
            if (moonCratersGroup == null)
            {
                Transform existingCraters = pRoot.Find("Moon_Craters_Group");
                if (existingCraters == null && targetPlanetVisual != null) existingCraters = targetPlanetVisual.Find("Moon_Craters_Group");
                if (existingCraters != null)
                {
                    existingCraters.SetParent(pRoot, false);
                    existingCraters.localPosition = new Vector3(0f, 0f, -0.03f);
                    existingCraters.localRotation = Quaternion.identity;
                    existingCraters.localScale = new Vector3(0.92f, 0.92f, 1f);
                    moonCratersGroup = existingCraters;
                }
                else
                {
                    moonCratersGroup = new GameObject("Moon_Craters_Group").transform;
                    moonCratersGroup.SetParent(pRoot, false);
                    moonCratersGroup.localPosition = new Vector3(0f, 0f, -0.03f);
                    moonCratersGroup.localRotation = Quaternion.identity;
                    moonCratersGroup.localScale = new Vector3(0.92f, 0.92f, 1f);

                    CreateUIQuad(moonCratersGroup, "Mare_1", new Vector3(-0.16f, 0.14f, 0f), new Vector3(0.28f, 0.25f, 1f), new Color(0.35f, 0.38f, 0.44f, 0.90f));
                    CreateUIQuad(moonCratersGroup, "Mare_2", new Vector3(0.18f, -0.08f, 0f), new Vector3(0.34f, 0.28f, 1f), new Color(0.38f, 0.41f, 0.47f, 0.90f));
                    CreateUIQuad(moonCratersGroup, "Mare_3", new Vector3(-0.04f, -0.22f, 0f), new Vector3(0.20f, 0.20f, 1f), new Color(0.32f, 0.35f, 0.40f, 0.90f));
                    CreateUIQuad(moonCratersGroup, "Tycho_Ray", new Vector3(0.08f, -0.28f, 0f), new Vector3(0.10f, 0.10f, 1f), new Color(0.92f, 0.94f, 0.98f, 0.90f));
                }
            }

            // 5. Mars North Polar Ice Cap & Valles Marineris (Deep Space Target)
            if (marsFeaturesGroup == null)
            {
                Transform existingMars = pRoot.Find("Mars_Features_Group");
                if (existingMars == null && targetPlanetVisual != null) existingMars = targetPlanetVisual.Find("Mars_Features_Group");
                if (existingMars != null)
                {
                    existingMars.SetParent(pRoot, false);
                    existingMars.localPosition = new Vector3(0f, 0f, -0.03f);
                    existingMars.localRotation = Quaternion.identity;
                    existingMars.localScale = new Vector3(0.92f, 0.92f, 1f);
                    marsFeaturesGroup = existingMars;
                }
                else
                {
                    marsFeaturesGroup = new GameObject("Mars_Features_Group").transform;
                    marsFeaturesGroup.SetParent(pRoot, false);
                    marsFeaturesGroup.localPosition = new Vector3(0f, 0f, -0.03f);
                    marsFeaturesGroup.localRotation = Quaternion.identity;
                    marsFeaturesGroup.localScale = new Vector3(0.92f, 0.92f, 1f);

                    CreateUIQuad(marsFeaturesGroup, "IceCap", new Vector3(0f, 0.36f, 0f), new Vector3(0.44f, 0.14f, 1f), new Color(0.96f, 0.98f, 1.0f, 0.95f));
                    GameObject basin = CreateUIQuad(marsFeaturesGroup, "Basin", new Vector3(0.05f, -0.04f, 0f), new Vector3(0.56f, 0.12f, 1f), new Color(0.40f, 0.12f, 0.06f, 0.95f));
                    basin.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);
                    CreateUIQuad(marsFeaturesGroup, "Olympus", new Vector3(-0.20f, 0.10f, 0f), new Vector3(0.12f, 0.12f, 1f), new Color(0.45f, 0.15f, 0.08f, 0.95f));
                }
            }

            if (earthContinentsGroup != null) earthContinentsGroup.gameObject.SetActive(selectedTargetIndex == 0);
            if (earthCloudsGroup != null) earthCloudsGroup.gameObject.SetActive(selectedTargetIndex == 0);
            if (earthNightLightsGroup != null) earthNightLightsGroup.gameObject.SetActive(selectedTargetIndex == 0);
            if (moonCratersGroup != null) moonCratersGroup.gameObject.SetActive(selectedTargetIndex == 1);
            if (marsFeaturesGroup != null) marsFeaturesGroup.gameObject.SetActive(selectedTargetIndex == 2);
        }
        if (mapTargetLabel != null)
        {
            mapTargetLabel.text = "TARGET: " + targetNames[selectedTargetIndex].ToUpper() +
                                  " (REWARD: " + targetRewardMultiplier[selectedTargetIndex].ToString("F1") + "x SCIENCE)";
        }
    }

    public void RecalculateTelemetry()
    {
        dryMassKg = (repairKits * 15f);
        float partsCostM = 0f;
        powerGenW = 0f;
        powerDrawW = 0f;
        batteryCapacityWh = 0f;
        float baseSciencePts = 0f;
        bool hasBattery = false;

        bool hasBooster = (parts != null && parts.Count > 7 && parts[7] != null && parts[7].isInstalled);
        bool hasSrbs = (parts != null && parts.Count > 8 && parts[8] != null && parts[8].isInstalled);
        bool hasUpperStage = (parts != null && parts.Count > 9 && parts[9] != null && parts[9].isInstalled);
        bool hasIon = (parts != null && parts.Count > 5 && parts[5] != null && parts[5].isInstalled);
        bool hasPropulsion = (hasBooster || hasUpperStage || hasIon);

        if (parts != null)
        {
            foreach (var p in parts)
            {
                if (p == null) continue;
                UpdatePartVisuals(p);
                if (p.isInstalled)
                {
                    dryMassKg += p.massKg;
                    partsCostM += p.costMillion;
                    if (p.partTitle.Contains("Solar"))
                        powerGenW += p.powerOutputW * targetSolarMultiplier[selectedTargetIndex];
                    else
                        powerGenW += p.powerOutputW;

                    powerDrawW += p.powerDrawW;
                    baseSciencePts += p.sciencePoints;

                    if (p.category == "Battery") { batteryCapacityWh += 410f; hasBattery = true; }
                }
            }
        }

        maxBudgetMillion = (targetBudgetMillion != null && selectedTargetIndex < targetBudgetMillion.Length)
            ? targetBudgetMillion[selectedTargetIndex] : 1000f;
        targetSciencePts = (targetScienceGoals != null && selectedTargetIndex < targetScienceGoals.Length)
            ? targetScienceGoals[selectedTargetIndex] : 80f;

        // Reward scales with Destination Multiplier (LEO = 1x, Moon = 1.8x, Mars = 3.0x)!
        potentialSciencePts = Mathf.Round(baseSciencePts * targetRewardMultiplier[selectedTargetIndex]);

        totalMassKg = dryMassKg + fuelMassKg;
        totalCostMillion = partsCostM + rocketCostsM[selectedRocketIndex] + (fuelMassKg * 0.05f) + (repairKits * 15f);
        remainingBudgetMillion = maxBudgetMillion - totalCostMillion;
        netPowerW = powerGenW - powerDrawW;

        // Realistic Staged Rocket Delta-V
        float effectiveVe = 0f;
        if (hasPropulsion)
        {
            if (hasBooster) effectiveVe += 4200f;
            if (hasUpperStage) effectiveVe += 4800f;
            if (hasSrbs) effectiveVe += 1800f;
            if (hasIon) effectiveVe += 6500f;
            if (selectedRocketIndex == 2) effectiveVe += 2000f; // SLS Heavy Boost
        }
        calculatedDeltaV = (effectiveVe > 0f && dryMassKg > 0f && totalMassKg > dryMassKg) ?
            (effectiveVe * Mathf.Log(totalMassKg / dryMassKg)) : 0f;

        // Calculate Real-World Mission Failure Risk % based on destination hazards vs. player preparation
        float baseHazard = selectedTargetIndex == 0 ? 25f : (selectedTargetIndex == 1 ? 50f : 75f);
        float kitMitigation = repairKits * 15f;
        float batteryMitigation = hasBattery ? 15f : 0f;
        float fuelMarginMitigation = (calculatedDeltaV >= targetDeltaVReq[selectedTargetIndex] + 300f) ? 10f : 0f;
        missionFailureRiskPct = Mathf.Clamp(baseHazard - kitMitigation - batteryMitigation - fuelMarginMitigation, 5f, 95f);

        if (currentPhase == MissionPhase.Assembly)
        {
            if (btnTargetLabel != null) btnTargetLabel.text = "CYCLE TARGET";
            if (btnRocketLabel != null) btnRocketLabel.text = "CYCLE ROCKET";
        }

        UpdateTargetPlanetVisuals();

        if (topBarDisplay != null && currentPhase == MissionPhase.Assembly)
        {
            topBarDisplay.characterSize = 0.055f;
            topBarDisplay.color = Color.white;
            topBarDisplay.text = 
                $"<b>BUDGET:</b> <color=#3498DB>${remainingBudgetMillion:F0}M / ${maxBudgetMillion}M</color>  |  " +
                $"<b>FUEL:</b> <color=#E67E22>{fuelMassKg:F0}kg</color>  |  " +
                $"<b>REPAIRS:</b> <color=#2ECC71>{repairKits}</color>  |  " +
                $"<b>RISK:</b> <color=#E74C3C>{missionFailureRiskPct:F0}%</color>  |  " +
                $"<b>SCIENCE:</b> <color=#F1C40F>{potentialSciencePts} Sci ({targetRewardMultiplier[selectedTargetIndex]:F1}x)</color>";
        }

        if (configDisplay != null && currentPhase != MissionPhase.FlightCrisis)
        {
            configDisplay.fontSize = 36;
            configDisplay.characterSize = 0.050f;
            configDisplay.color = Color.white;
            configDisplay.text = $"Launcher: {rocketNames[selectedRocketIndex]} | Integrity: {hullIntegrity:F0}%";
        }   

        if (parts != null)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].partRenderer != null) parts[i].partRenderer.enabled = false;
            }
        }
        SetupAttachedSprites();

        RefreshMissionRequirementsBoard();
    }

    private void RefreshMissionRequirementsBoard()
    {
        if (telemetryDisplay == null) return;

        float maxRocketMass = rocketMaxMassKg[selectedRocketIndex];
        float reqDv = targetDeltaVReq[selectedTargetIndex];

        bool hasComms = false;
        bool hasBooster = (parts != null && parts.Count > 7 && parts[7] != null && parts[7].isInstalled);
        bool hasUpperStage = (parts != null && parts.Count > 9 && parts[9] != null && parts[9].isInstalled);
        bool hasIon = (parts != null && parts.Count > 5 && parts[5] != null && parts[5].isInstalled);
        bool hasPropulsion = (hasBooster || hasUpperStage || hasIon);
        bool hasBus = (parts != null && parts.Count > 12 && parts[12] != null && parts[12].isInstalled);

        if (parts != null)
        {
            foreach (var p in parts)
            {
                if (p != null && p.isInstalled)
                {
                    if (p.category == "Comms") hasComms = true;
                }
            }
        }

        string budgetPass = totalCostMillion <= maxBudgetMillion ? "[PASS]" : "[OVER!]";
        string massPass   = (totalMassKg <= maxRocketMass && totalMassKg > 0f) ? "[PASS]" : (totalMassKg > maxRocketMass ? "[HEAVY!]" : "[NONE]");
        string dvPass     = (calculatedDeltaV >= reqDv && calculatedDeltaV > 0f) ? "[PASS]" : "[LOW dV!]";
        string pwrPass    = (netPowerW >= 0f && powerGenW > 0f) ? "[PASS]" : (powerGenW == 0f ? "[NO GEN!]" : "[DEFICIT!]");
        string busPass    = hasBus ? "[PASS]" : "[MISSING!]";
        string commsPass  = hasComms ? "[PASS]" : "[MISSING!]";
        string propPass   = hasPropulsion ? "[PASS]" : "[MISSING!]";
        string sciPass    = potentialSciencePts >= targetSciencePts ? "[PASS]" : "[LOW SCI]";

        float shownBat = (currentPhase == MissionPhase.OrbitSimulation) ? currentBatteryWh : batteryCapacityWh;
        float shownSci = (currentPhase == MissionPhase.OrbitSimulation || currentPhase == MissionPhase.Completed) ? transmittedSciencePts : potentialSciencePts;

        string bCol = totalCostMillion <= maxBudgetMillion ? "#2ECC71" : "#E74C3C";
        string mCol = (totalMassKg <= maxRocketMass && totalMassKg > 0f) ? "#2ECC71" : "#E74C3C";
        string dCol = (calculatedDeltaV >= reqDv && calculatedDeltaV > 0f) ? "#2ECC71" : "#E74C3C";
        string pCol = (netPowerW >= 0f && powerGenW > 0f) ? "#2ECC71" : "#E74C3C";
        string busCol = hasBus ? "#2ECC71" : "#E74C3C";
        string comCol = hasComms ? "#2ECC71" : "#E74C3C";
        string propCol = hasPropulsion ? "#2ECC71" : "#E74C3C";

        if (telemetryDisplay != null)
        {
            telemetryDisplay.fontSize = 44;
            telemetryDisplay.characterSize = 0.048f;
            telemetryDisplay.lineSpacing = 1.04f;
            telemetryDisplay.text = 
                "<color=#3498DB><b>MISSION REQUIREMENTS</b></color>\n" +
                $"• Target: {targetNames[selectedTargetIndex]}\n" +
                $"• Cost: ${totalCostMillion:F0}M / ${maxBudgetMillion}M  <color={bCol}><b>{budgetPass}</b></color>\n" +
                $"• Mass: {totalMassKg:F0} / {maxRocketMass:F0}kg  <color={mCol}><b>{massPass}</b></color>\n" +
                $"• Thrust: {calculatedDeltaV:F0} / {reqDv:F0} m/s  <color={dCol}><b>{dvPass}</b></color>\n" +
                $"• Power: {netPowerW:+0;-0;0}W (Bat: {shownBat:F0}Wh)  <color={pCol}><b>{pwrPass}</b></color>\n" +
                $"• Avionics Bus: {(hasBus ? "Installed" : "None")}  <color={busCol}><b>{busPass}</b></color>\n" +
                $"• Comms Dish: {(hasComms ? "Ready" : "None")}  <color={comCol}><b>{commsPass}</b></color>\n" +
                $"• Engine Stage: {(hasPropulsion ? "Ready" : "None")}  <color={propCol}><b>{propPass}</b></color>\n\n" +
                "<color=#E67E22><b>RISK & REWARD OUTCOME</b></color>\n" +
                $"• Hazard Risk: <color=#E74C3C>{missionFailureRiskPct:F0}%</color> ({repairKits} Kits)\n" +
                $"• Solar Yield: <color=#2ECC71>{Mathf.Clamp((targetSolarMultiplier[selectedTargetIndex] * 100f), 0, 100):F0}%</color> Sunlight\n" +
                $"• Est. Science: {shownSci:F0} pts <color=#F1C40F>({targetRewardMultiplier[selectedTargetIndex]:F1}x)</color>";
        }
    }

    private void UpdateLiveFlightTelemetryHUD()
    {
        if (topBarDisplay == null) return;

        topBarDisplay.color = Color.white;
        string calloutHex = ColorUtility.ToHtmlStringRGB(activeFlightDirectorColor);
        
        // Dynamic length guard to ensure HUD text never spills past the box edges
        string headerPart = string.IsNullOrEmpty(activeFlightDirectorHeader) ? "" : $"• [{activeFlightDirectorHeader}] ";
        string bodyPart = activeFlightDirectorCallout ?? "";
        int maxTotalChars = 62;
        int maxBodyLen = Mathf.Max(16, maxTotalChars - headerPart.Length);
        if (bodyPart.Length > maxBodyLen)
        {
            bodyPart = bodyPart.Substring(0, maxBodyLen - 2) + "..";
        }
        string calloutLine = $"<color=#{calloutHex}><b>{headerPart}</b></color><color=#F0F3F4>{bodyPart}</color>";

        if (currentPhase == MissionPhase.Countdown)
        {
            string tLine =
                $"<b>ALT:</b> <color=#3498DB>0.0 km</color>  |  " +
                $"<b>VEL:</b> <color=#2ECC71>0 m/s</color>  |  " +
                $"<b>Q:</b> <color=#2ECC71>0.0 kPa</color>  |  " +
                $"<b>STAGE:</b> PRE-LAUNCH  |  " +
                $"<b>FUEL:</b> <color=#2ECC71>{fuelMassKg:F0}kg</color>  |  " +
                $"<b>HULL:</b> <color=#2ECC71>{hullIntegrity:F0}%</color>";

            topBarDisplay.text = tLine + NL + calloutLine;
        }
        else if (currentPhase == MissionPhase.Launching || currentPhase == MissionPhase.FlightCrisis)
        {
            float rocketY = spacecraftHub != null ? spacecraftHub.localPosition.y : LaunchpadY;
            float flightProg = Mathf.Clamp01((rocketY - LaunchpadY) / (OrbitCenterY - LaunchpadY));

            float altKm = Mathf.Lerp(0f, 420f, Mathf.Pow(flightProg, 1.4f));
            float velMs = Mathf.Lerp(35f, 7840f, Mathf.Pow(flightProg, 1.15f));

            // Realistic Dynamic Pressure curve: peaks at Max-Q (Y ~ 7.0f) around 38 kPa, drops in vacuum
            float maxQCenter = ObstacleYPositions[0]; // 7.0f
            float qFactor = Mathf.Exp(-Mathf.Pow((rocketY - maxQCenter) / 2.2f, 2f));
            float dynPKPa = (rocketY > LaunchpadY + 0.1f) ? (qFactor * (25f + 14f * flightThrottle) + Mathf.Max(0f, (1f - flightProg) * 3.5f)) : 0f;

            string stageName;
            if (rocketY < 7.0f) stageName = "BOOSTER (S-IC)";
            else if (rocketY < 9.2f) stageName = "MAX-Q BUCKET";
            else if (rocketY < 11.5f) stageName = !stage1Separated ? "BOOSTER MECO" : "STAGE 2";
            else if (rocketY < 14.5f) stageName = !fairingJettisoned ? "SHROUD ARMED" : "VACUUM";
            else if (rocketY < 23.5f) stageName = "S-II UPPER";
            else stageName = "ORBIT INSERTION";

            string hullCol = (hullIntegrity > 60f) ? "#2ECC71" : ((hullIntegrity > 25f) ? "#F1C40F" : "#E74C3C");
            string qCol = (dynPKPa > 32.0f) ? "#E74C3C" : ((dynPKPa > 22.0f) ? "#E67E22" : "#2ECC71");
            string fuelCol = (fuelMassKg > 150f) ? "#2ECC71" : ((fuelMassKg > 50f) ? "#F1C40F" : "#E74C3C");

            // Tsiolkovsky dynamic G-Force: climbs as mass burns off, zero in orbit freefall
            float fuelBurnFraction = Mathf.Clamp01(1.0f - (fuelMassKg / 450f));
            float baseG = 1.35f + (fuelBurnFraction * 2.35f);
            float currentG = (flightThrottle > 0.05f) ? (baseG * flightThrottle) : 0.0f;
            if (!stage1Separated && rocketY >= 10.0f) currentG *= 0.55f;
            string gCol = (currentG > 3.0f) ? "#E74C3C" : ((currentG > 2.0f) ? "#F1C40F" : "#2ECC71");

            string tLine =
                $"<b>ALT:</b> <color=#3498DB>{altKm:F0} km</color>  |  " +
                $"<b>VEL:</b> <color=#2ECC71>{velMs:F0} m/s</color>  |  " +
                $"<b>G-LOAD:</b> <color={gCol}>{currentG:F1} G</color>  |  " +
                $"<b>Q:</b> <color={qCol}>{dynPKPa:F1} kPa</color>  |  " +
                $"<b>STAGE:</b> <b>{stageName}</b>  |  " +
                $"<b>FUEL:</b> <color={fuelCol}>{fuelMassKg:F0}kg</color>  |  " +
                $"<b>HULL:</b> <color={hullCol}>{hullIntegrity:F0}%</color>";

            topBarDisplay.text = tLine + NL + calloutLine;
        }
        else if (currentPhase == MissionPhase.OrbitSimulation)
        {
            string orbitTag = selectedTargetIndex == 0 ? "LEO 420x415km" : (selectedTargetIndex == 1 ? "LUNAR 100x100km" : "MARS 350x340km");
            string sunState = inEclipse ? "<color=#E74C3C>ECLIPSE (0W)</color>" : "<color=#F1C40F>SUNLIGHT (+100%)</color>";
            string battCol = (currentBatteryWh > 120f) ? "#2ECC71" : ((currentBatteryWh > 40f) ? "#F1C40F" : "#E74C3C");
            bool hasComms = parts != null && parts.Exists(p => p != null && p.isInstalled && p.category == "Comms");
            string txState = hasComms ? $"<color=#2ECC71>{transmittedSciencePts:F0}/{targetSciencePts:F0} pts</color>" : "<color=#E74C3C>NO COMM DISH</color>";

            string tLine =
                $"<b>ORBIT:</b> <color=#3498DB>{orbitTag}</color>  |  " +
                $"<b>G-LOAD:</b> <color=#2ECC71>0.0 G</color>  |  " +
                $"<b>PWR:</b> {sunState}  |  " +
                $"<b>BATT:</b> <color={battCol}>{currentBatteryWh:F0}/{batteryCapacityWh:F0}Wh</color>  |  " +
                $"<b>DOWNLINK:</b> {txState}";

            topBarDisplay.text = tLine + NL + calloutLine;
        }
        else if (currentPhase == MissionPhase.Completed)
        {
            topBarDisplay.text = "<b>★ NASA MISSION LOG // FLIGHT DIRECTOR SUMMARY RECORDED ★</b>" + NL +
                "<color=#85C1E9>Debrief card displayed below. Review flight analytics or return to Hangar.</color>";
        }
    }

    // =========================================================================
    // 4. VISUAL ASCENT THROUGH OBSTACLE ZONES & ORBIT REWARD COLLECTION
    // =========================================================================
    public void StartLaunchSequence()
    {
        if (currentPhase != MissionPhase.Assembly) ResetToAssembly();
        RecalculateTelemetry();

        float maxRocketMass = rocketMaxMassKg[selectedRocketIndex];
        float reqDv = targetDeltaVReq[selectedTargetIndex];

        bool hasBooster = (parts != null && parts.Count > 7 && parts[7] != null && parts[7].isInstalled);
        bool hasUpperStage = (parts != null && parts.Count > 9 && parts[9] != null && parts[9].isInstalled);
        bool hasIon = (parts != null && parts.Count > 5 && parts[5] != null && parts[5].isInstalled);
        bool hasPropulsion = (hasBooster || hasUpperStage || hasIon);

        bool hasBus = (parts != null && parts.Count > 12 && parts[12] != null && parts[12].isInstalled);

        bool hasPower = (parts != null && parts.Count > 1 &&
            ((parts[0] != null && parts[0].isInstalled) || (parts[1] != null && parts[1].isInstalled)));

        if (!hasPropulsion)
        {
            SetStatusBanner("LAUNCH SCRUBBED: NO PROPULSION STAGE!", "Equip a Kerolox Booster Stage or Upper Stage in the [STAGES] tab to lift off!", DuoRed);
            return;
        }
        if (!hasBus)
        {
            SetStatusBanner("LAUNCH SCRUBBED: NO SATELLITE BUS!", "Equip the Satellite Avionics Bus in the [POWER & BUS] tab to house your instruments!", DuoRed);
            return;
        }
        if (!hasPower)
        {
            SetStatusBanner("LAUNCH SCRUBBED: NO POWER GENERATION!", "Equip ROSA Solar Wings or an MMRTG Nuclear Core in [POWER & BUS]!", DuoRed);
            return;
        }
        if (netPowerW < 0f)
        {
            SetStatusBanner("LAUNCH SCRUBBED: POWER DEFICIT!", "Instruments draw more power than generated! Add Solar Wings or MMRTG Core.", DuoOrange);
            return;
        }
        if (totalCostMillion > maxBudgetMillion)
        {
            SetStatusBanner("LAUNCH SCRUBBED: OVER BUDGET!", "Cost ($" + totalCostMillion.ToString("F0") + "M) exceeds $" + maxBudgetMillion.ToString("F0") + "M limit!", DuoRed);
            return;
        }
        if (totalMassKg > maxRocketMass)
        {
            SetStatusBanner("LAUNCH SCRUBBED: OVERWEIGHT!", "Wet mass (" + totalMassKg.ToString("F0") + "kg) exceeds launcher limit (" + maxRocketMass.ToString("F0") + "kg)!", DuoRed);
            return;
        }
        if (calculatedDeltaV < reqDv)
        {
            SetStatusBanner("LAUNCH SCRUBBED: INSUFFICIENT DELTA-V!", "Need " + reqDv.ToString("F0") + " m/s dV to reach " + targetNames[selectedTargetIndex] + "! Add Fuel, Booster, or Ion Thruster.", DuoOrange);
            return;
        }

        SetAssemblyUIVisible(false);
        if (missionCorridorMap != null) missionCorridorMap.gameObject.SetActive(true);
        if (mapTargetLabel != null) mapTargetLabel.gameObject.SetActive(false);
        if (spacecraftHub != null)
        {
            spacecraftHub.gameObject.SetActive(true);
            spacecraftHub.localPosition = new Vector3(-0.3f, LaunchpadY, 0f);
            spacecraftHub.localScale = new Vector3(0.85f, 0.85f, 1f);
        }

        currentPhase = MissionPhase.Countdown;
        countdownTimer = 3.0f;
        lastCountdownBeepSec = 3;
        simTimer = 0f;
        hullIntegrity = 100f;
        currentBatteryWh = batteryCapacityWh;

        flightThrottle = 1.0f;
        flightGimbalOffset = 0f;
        stage1Separated = false;
        rcsPlumeTimer = 0f;
        debrisHitCooldown = 0f;
        lastStatusBannerHeader = "";

        fairingJettisoned = false;
        fairingJettisonTimer = 0f;
        solarDeployProgress = 0f;
        boosterSeparated = false;
        boosterSepTimer = 0f;

        UpdateAllRocketHardwareVisuals();
        EnsureFlightConsoleCreated();
        if (flightConsoleGroup != null) flightConsoleGroup.SetActive(false);

        if (fairingHalfLeft != null)
        {
            fairingHalfLeft.gameObject.SetActive(true);
            fairingHalfLeft.localPosition = Vector3.zero;
            fairingHalfLeft.localRotation = Quaternion.identity;
        }
        if (fairingHalfRight != null)
        {
            fairingHalfRight.gameObject.SetActive(true);
            fairingHalfRight.localPosition = Vector3.zero;
            fairingHalfRight.localRotation = Quaternion.identity;
        }
        SetFairingOpacity(1.0f);

        if (parts != null && parts.Count > 0 && parts[0] != null && parts[0].partTransform != null)
        {
            parts[0].partTransform.localScale = new Vector3(parts[0].mountedScale.x * 0.18f, parts[0].mountedScale.y, parts[0].mountedScale.z);
        }

        // Reset all 3 Golden Science Reward Nodes in orbit
        if (scienceRewardNodes != null)
        {
            foreach (var n in scienceRewardNodes)
                if (n != null) n.gameObject.SetActive(true);
        }

        SetStatusBanner("LAUNCH COUNTDOWN: T - 3", "T-3: Ground telemetry verified... Guidance internal...", DuoOrange);
        PlayCountdownPip(3);
    }

    private float countdownTimer = 0f;
    private int lastCountdownBeepSec = -1;

    private void UpdateLaunchCountdown(float dt)
    {
        countdownTimer -= dt;

        int currentSec = Mathf.CeilToInt(countdownTimer);
        if (currentSec != lastCountdownBeepSec && currentSec >= 1 && currentSec <= 2)
        {
            lastCountdownBeepSec = currentSec;
            PlayCountdownPip(currentSec);

            if (currentSec == 2)
            {
                SetStatusBanner("LAUNCH COUNTDOWN: T - 2", "T-2: Hold-down clamps released... Gantry umbilical retracting...", DuoOrange);
            }
            else if (currentSec == 1)
            {
                SetStatusBanner("LAUNCH COUNTDOWN: T - 1 (IGNITION)", "T-1: Main engine ignition sequence start... Turbopumps at 100%!", DuoGreen);
                PlayIgnitionBlastAudio();
            }
        }

        if (countdownTimer <= 0f)
        {
            currentPhase = MissionPhase.Launching;
            if (flightConsoleGroup != null) flightConsoleGroup.SetActive(true);
            SetStatusBanner("LIFTOFF! WE HAVE LIFTOFF!", "Spacecraft clears the launch gantry! Pilot: Monitor Throttle & Max-Q Dynamic Pressure!", DuoGreen);
            PlayCountdownPip(0);
            PlayLiftoffAudio();
        }
    }

    private void UpdateAscentThroughObstacles(float dt)
    {
        if (spacecraftHub == null) return;

        // Dynamic speed based on pilot throttle - tuned for an energetic, thrilling ~21s ascent
        float throttleSpeed = (0.32f + 0.96f * flightThrottle) * 1.02f;
        float currentY = spacecraftHub.localPosition.y;

        // If booster hasn't been decoupled after MECO (currentY >= 10.0f), deadweight creates heavy drag!
        if (currentY >= 10.0f && !stage1Separated)
        {
            throttleSpeed *= 0.50f; // 50% ascent speed penalty for carrying dead booster!
        }

        // Responsive pilot steering across the entire flight corridor
        float targetX = -0.3f + flightGimbalOffset * 2.4f;
        Vector3 curPos = spacecraftHub.localPosition;
        curPos.x = Mathf.MoveTowards(curPos.x, targetX, 4.2f * dt);

        // Realistic aerospace attitude banking (pitches into the turn, stabilized by cold-gas RCS)
        float targetRoll = -flightGimbalOffset * 15.0f;
        spacecraftHub.localRotation = Quaternion.Euler(0f, 0f, Mathf.MoveTowardsAngle(spacecraftHub.localRotation.eulerAngles.z, targetRoll, 90f * dt));

        // REAL RISK: FUEL DEPLETION & FLAMEOUT CHECK
        if (fuelMassKg <= 0.01f)
        {
            flightThrottle = 0f;
            throttleSpeed = 0f;
            // Gravity takes over: vehicle decelerates and falls back into suborbital decay
            curPos.y -= 4.2f * dt;
            spacecraftHub.localPosition = curPos;
            currentY = curPos.y;

            SetStatusBannerIfChanged("⚠️ ENGINE FLAMEOUT: PROPELLANT DEPLETED!",
                "Thrust failure! Delta-V exhausted before orbital velocity. Suborbital re-entry decay!", DuoRed);

            if (Time.time % 0.7f < dt) PlayWarningAlarm();

            if (currentY <= LaunchpadY + 0.3f || curPos.y < 3.0f)
            {
                currentPhase = MissionPhase.Completed;
                if (rumbleAudioSource != null && rumbleAudioSource.isPlaying) rumbleAudioSource.Stop();
                ShowFlightDirectorDebrief(false, "SUBORBITAL FLAMEOUT: PROPELLANT EXHAUSTION");
                PlayMissionAbortAudio();
                return;
            }
            return;
        }

        curPos.y += throttleSpeed * dt;
        spacecraftHub.localPosition = curPos;
        currentY = curPos.y;

        // Acoustic realism: atmospheric wind & exhaust roar drops to vacuum silence upon reaching space
        if (rumbleAudioSource != null && rumbleAudioSource.isPlaying)
        {
            if (currentY >= 12.0f) // Space vacuum reached
            {
                rumbleAudioSource.volume = Mathf.Clamp(0.06f + 0.12f * flightThrottle, 0.03f, 0.18f);
                rumbleAudioSource.pitch = 0.65f; // Muffled vacuum hull vibration
            }
            else
            {
                rumbleAudioSource.volume = Mathf.Clamp(0.18f + 0.50f * flightThrottle, 0.08f, 0.72f);
                rumbleAudioSource.pitch = Mathf.Clamp(0.85f + 0.35f * flightThrottle, 0.70f, 1.25f);
            }
        }

        // REAL RISK: BURN PROPELLANT WITH STAGING & CORRIDOR DRAG PENALTIES
        float burnRate = (flightThrottle > 0.05f) ? (15.5f * flightThrottle) : 0f;
        if (currentY >= 10.0f && !stage1Separated)
        {
            burnRate += 16.0f; // Deadweight booster drag burns heavy extra fuel!
        }
        if (Mathf.Abs(flightGimbalOffset) > 0.28f)
        {
            burnRate += 6.5f; // Angle-of-attack corridor drift induces aerodynamic drag!
        }
        fuelMassKg = Mathf.Max(0f, fuelMassKg - (burnRate * dt));

        // Calculate Realistic Dynamic Pressure Q (kPa)
        float altKm = Mathf.Max(0f, (currentY - LaunchpadY) * 6.5f);
        float velMs = 260f + (currentY - LaunchpadY) * 65f * (0.4f + 0.6f * flightThrottle);
        float airDensity = Mathf.Exp(-altKm / 12f);
        float qKpa = 0.5f * airDensity * Mathf.Pow(velMs * 0.055f, 2f);
        bool hasGridFins = (parts != null && parts.Count > 10 && parts[10] != null && parts[10].isInstalled);
        float maxQThreshold = hasGridFins ? 42.0f : 30.0f;

        // 1. LIFTOFF & LOWER ATMOSPHERIC CLIMB (currentY < 5.8f)
        if (currentY < 5.8f)
        {
            if (currentY >= 3.2f)
            {
                // Upper atmospheric jet stream crosswind pushing vehicle East!
                curPos.x += 0.50f * dt;
                spacecraftHub.localPosition = curPos;

                if (Mathf.Abs(curPos.x - (-0.3f)) > 0.85f)
                {
                    fuelMassKg = Mathf.Max(0f, fuelMassKg - 5.5f * dt);
                    SetStatusBannerIfChanged("JETSTREAM CROSSWIND SHEAR",
                        "Upper atmospheric jetstream pushing East! Counter-steer with [A] to maintain ascent vector!", DuoOrange);
                }
                else
                {
                    SetStatusBannerIfChanged("ASCENT PHASE: FULL THROTTLE",
                        "Atmospheric climb nominal! Steer [A / D] to stay locked on blue guidance vector.", DuoGreen);
                }
            }
            else
            {
                SetStatusBannerIfChanged("ASCENT PHASE: FULL THROTTLE",
                    "Cleared launch gantry! Accelerating through lower atmosphere. Pilot: Maintain 100% thrust (W)!", DuoGreen);
            }
        }
        // 2. MAX-Q SUPERSONIC ZONE (currentY = 5.8f to 8.8f) - REAL RISK OF CATASTROPHIC STRUCTURAL BREAKUP
        else if (currentY <= 8.8f)
        {
            if (qKpa > maxQThreshold)
            {
                // Dynamic pressure exceeds structural limits: rapid structural breakup damage!
                hullIntegrity -= 24.0f * dt;
                if (Camera.main != null)
                    Camera.main.transform.position += UnityEngine.Random.insideUnitSphere * 0.14f;

                if (Time.time % 0.5f < dt) PlayWarningAlarm();

                SetStatusBannerIfChanged("⚠️ CRITICAL MAX-Q: STRUCTURAL YIELD EXCEEDED!",
                    $"Aerodynamic stress at {qKpa:F1} kPa exceeds structural limit ({maxQThreshold:F0} kPa)! Throttle down to 65% Safe-Q [S] immediately!", DuoRed);
            }
            else
            {
                SetStatusBannerIfChanged("MAX-Q THROTTLE BUCKET NOMINAL",
                    $"Dynamic pressure at {qKpa:F1} kPa safely contained. Supersonic vapor cone stable. Hull nominal.", DuoGreen);
            }
        }
        // 3. POST-MAX-Q ACCELERATION (currentY = 8.8f to 10.0f)
        else if (currentY < 10.0f)
        {
            SetStatusBannerIfChanged("MAX-Q CLEARED: GO FOR FULL THROTTLE",
                "Atmospheric dynamic pressure falling. Pilot: Push throttle to 100% [W] for stage 1 burn completion!", DuoGreen);
        }
        // 4. MECO & BOOSTER STAGING (currentY = 10.0f to 12.0f)
        else if (currentY <= 12.0f)
        {
            if (!stage1Separated)
            {
                SetStatusBannerIfChanged("⏏ BOOSTER MECO // SEPARATION ARMED",
                    "First stage propellant depleted! Press [SPACE] to decouple booster stage & ignite S-II!", DuoOrange);

                if (currentY >= 11.8f)
                {
                    TriggerFlightStageAction();
                }
            }
            else
            {
                SetStatusBannerIfChanged("STAGE 1 SEPARATION COMPLETE",
                    "Booster released! S-II vacuum stage online. Accelerating toward orbital velocity!", DuoGreen);
            }
        }
        // 5. VACUUM FAIRING JETTISON (currentY = 12.0f to 14.0f)
        else if (currentY <= 14.0f)
        {
            if (!fairingJettisoned)
            {
                SetStatusBannerIfChanged("⏏ VACUUM REACHED: JETTISON FAIRING",
                    "Exosphere reached! Aerodynamic shroud is dead weight. Press [SPACE] to jettison fairing!", DuoBlue);

                if (currentY >= 13.8f)
                {
                    TriggerFlightStageAction();
                }
            }
            else
            {
                SetStatusBannerIfChanged("PAYLOAD FAIRING JETTISONED",
                    "Fairing halves cleared! Satellite science package and solar arrays exposed to space.", DuoGreen);
            }
        }
        // 6. VAN ALLEN BELT PASSAGE (currentY = 14.0f to 17.0f)
        else if (currentY <= 17.0f)
        {
            SetStatusBannerIfChanged("VAN ALLEN RADIATION BELT",
                "Traversing trapped magnetospheric belt. Passive avionics shielding absorbing radiation nominal.", DuoGreen);
        }
        // 7. LEO HYPERVELOCITY DEBRIS FIELD EVASION (currentY = 17.0f to 22.8f) - EXTENDED 3-WAVE CORRIDOR
        else if (currentY <= 22.8f)
        {
            if (debrisHitCooldown > 0f) debrisHitCooldown -= dt;

            bool hadImpactThisFrame = false;
            GameObject debrisGroup = GameObject.Find("Obstacle3_Space_Debris");
            if (debrisGroup != null)
            {
                for (int i = 0; i <= 8; i++)
                {
                    Transform rock = debrisGroup.transform.Find("DebrisRock_" + i);
                    if (rock != null)
                    {
                        float dist = Vector2.Distance(new Vector2(spacecraftHub.position.x, spacecraftHub.position.y), new Vector2(rock.position.x, rock.position.y));
                        // Precise, fair hitbox matching visible rock sprite radius (0.20f)
                        if (dist < 0.20f && debrisHitCooldown <= 0f)
                        {
                            debrisHitCooldown = 0.90f;
                            hullIntegrity = Mathf.Max(0f, hullIntegrity - 25.0f);
                            fuelMassKg = Mathf.Max(0f, fuelMassKg - 20.0f); // Punctured tank leak!

                            // Attitude perturbation kick from hypervelocity strike
                            flightGimbalOffset = Mathf.Clamp(flightGimbalOffset + ((spacecraftHub.position.x > rock.position.x) ? 0.35f : -0.35f), -1f, 1f);

                            if (Camera.main != null) Camera.main.transform.position += UnityEngine.Random.insideUnitSphere * 0.18f;
                            PlayWarningAlarm();
                            SetStatusBannerIfChanged("💥 CRITICAL DEBRIS IMPACT!",
                                "Hypervelocity fragment struck hull (-25% Hull, -20kg Propellant)! Pilot: Counter-steer [A / D] & hold corridor!", DuoRed);
                            hadImpactThisFrame = true;
                            break;
                        }
                    }
                }
            }

            if (!hadImpactThisFrame && debrisHitCooldown <= 0f)
            {
                if (currentY < 19.0f)
                {
                    SetStatusBannerIfChanged("LEO DEBRIS WAVE 1: LOWER CLUSTER",
                        "Derelict fragments on orbital flanks. Center flight corridor is OPEN — hold vector!", DuoGreen);
                }
                else if (currentY < 20.8f)
                {
                    SetStatusBannerIfChanged("⚠️ CONJUNCTION ALERT: DERELICT CORE DEAD AHEAD!",
                        "Defunct satellite cluster blocking center! Steer LEFT [A] or RIGHT [D] to evade collision!", DuoOrange);
                }
                else
                {
                    SetStatusBannerIfChanged("LEO DEBRIS WAVE 3: CLEARING FIELD",
                        "Debris field clearing on starboard. Steer back toward center orbital injection corridor!", DuoBlue);
                }
            }
        }
        // 8. ORBITAL INSERTION TRAJECTORY (currentY > 22.8f)
        else
        {
            SetStatusBannerIfChanged("ORBITAL INSERTION TRAJECTORY",
                "Space debris cleared! Maintaining orbital injection vector for circularization burn.", DuoGreen);
        }

        // REAL RISK: Check Hull Destruction / Breakup
        if (hullIntegrity <= 5.0f)
        {
            currentPhase = MissionPhase.Completed;
            if (rumbleAudioSource != null && rumbleAudioSource.isPlaying) rumbleAudioSource.Stop();
            ShowFlightDirectorDebrief(false, (currentY <= 9.0f) ? "CATASTROPHIC VEHICLE BREAKUP AT MAX-Q" : "VEHICLE DESTROYED BY DYNAMIC FLIGHT FORCES");
            PlayMissionAbortAudio();
            return;
        }

        // Reached Orbit Center!
        if (currentY >= OrbitCenterY - 0.1f)
        {
            BeginOrbitalPhase();
        }
    }

    private void TriggerObstacleCrisis(int obstacleIdx)
    {
        // Legacy fallback - active flight uses real-time obstacle zones
    }

    private void ResolveFlightCrisis(bool choseOptionA)
    {
        // Legacy fallback - active flight uses real-time console actions
    }

    private void BeginOrbitalPhase()
    {
        currentPhase = MissionPhase.OrbitSimulation;
        simTimer = 0f;
        orbitAngle = -Mathf.PI * 0.5f;
        transmittedSciencePts = 0f;

        // REAL REWARD: High-yield bonus for surplus propellant and pristine hull integrity!
        float surplusFuelBonus = Mathf.Round(fuelMassKg * 0.75f);
        float pristineHullBonus = (hullIntegrity >= 90f) ? 45f : 0f;
        float rewardMultiplier = targetRewardMultiplier[selectedTargetIndex];
        float flightBonus = Mathf.Round((surplusFuelBonus + pristineHullBonus) * rewardMultiplier);
        potentialSciencePts += flightBonus;

        if (flightConsoleGroup != null) flightConsoleGroup.SetActive(false);
        if (lowerBoosterStage != null) lowerBoosterStage.gameObject.SetActive(false);
        if (srbGroup != null) srbGroup.gameObject.SetActive(false);
        if (gridFinsGroup != null) gridFinsGroup.gameObject.SetActive(false);
        if (fairingShell != null) fairingShell.gameObject.SetActive(false);
        if (fairingHalfLeft != null) fairingHalfLeft.gameObject.SetActive(false);
        if (fairingHalfRight != null) fairingHalfRight.gameObject.SetActive(false);
        solarDeployProgress = 1.0f;
        if (parts != null && parts.Count > 0 && parts[0] != null && parts[0].partTransform != null)
        {
            parts[0].partTransform.localScale = parts[0].mountedScale;
        }

        if (mapTargetLabel != null) mapTargetLabel.gameObject.SetActive(false);

        SetStatusBanner("ORBIT ACHIEVED",
            $"{targetNames[selectedTargetIndex]} Nominal! Fuel: +{surplusFuelBonus * rewardMultiplier:F0} Sci | Hull: +{pristineHullBonus * rewardMultiplier:F0} Sci", DuoGreen);
    }

    private void UpdateOrbitalSimulation(float dt)
    {
        simTimer += dt;
        orbitAngle += dt * 1.65f;

        // Move the player's actual spacecraft around the Target Planet's orbit ring!
        Vector3 orbitCenter = new Vector3(-0.3f, OrbitCenterY, 0f);
        Vector3 probePos = orbitCenter + new Vector3(Mathf.Cos(orbitAngle) * OrbitRadius, Mathf.Sin(orbitAngle) * OrbitRadius, -0.1f);
        if (spacecraftHub != null)
        {
            spacecraftHub.localPosition = probePos;
            spacecraftHub.localScale = new Vector3(0.55f, 0.55f, 1f);
        }

        // Check if spacecraft is passing through the dark Eclipse Shadow Zone on the right side of the planet
        inEclipse = (Mathf.Cos(orbitAngle) > 0.35f);

        // Visually collect the Golden Science Reward Nodes as the spacecraft flies past them!
        if (scienceRewardNodes != null && spacecraftHub != null)
        {
            foreach (var node in scienceRewardNodes)
            {
                if (node != null && node.gameObject.activeSelf)
                {
                    if (Vector2.Distance(spacecraftHub.position, node.position) < 0.45f)
                    {
                        node.gameObject.SetActive(false);
                    }
                }
            }
        }

        float activeGenW = 0f;
        bool hasComms = false;
        if (parts != null)
        {
            foreach (var p in parts)
            {
                if (p != null && p.isInstalled)
                {
                    if (p.category == "Comms") hasComms = true;
                    if (p.partTitle.Contains("Solar"))
                        activeGenW += inEclipse ? 0f : (p.powerOutputW * targetSolarMultiplier[selectedTargetIndex]);
                    else
                        activeGenW += p.powerOutputW; // RTG nuclear power works even in Eclipse!
                }
            }
        }

        float instantaneousNetW = activeGenW - powerDrawW;
        currentBatteryWh = Mathf.Clamp(currentBatteryWh + (instantaneousNetW * dt * 0.45f), 0f, batteryCapacityWh);

        if (currentBatteryWh <= 0.01f)
        {
            currentPhase = MissionPhase.Completed;
            ShowFlightDirectorDebrief(false, "POWER LOSS IN PLANETARY ECLIPSE SHADOW");
            RefreshMissionRequirementsBoard();
            return;
        }

        if (hasComms)
        {
            transmittedSciencePts = Mathf.MoveTowards(transmittedSciencePts, potentialSciencePts, (potentialSciencePts / 6.5f) * dt);

            // Visual Downlink Telemetry Radio Pulses emitting from probe towards Earth ground station!
            downlinkTimer += dt;
            Transform pRoot = (orbitRingRoot != null) ? orbitRingRoot : targetPlanetVisual;
            if (downlinkPulse0 == null && pRoot != null)
            {
                GameObject dp0 = CreateUIQuad(pRoot, "Downlink_Pulse_0", Vector3.zero, new Vector3(0.08f, 0.08f, 1f), new Color(1f, 0.88f, 0.25f, 0.8f));
                downlinkPulse0 = dp0.transform;
            }
            if (downlinkPulse1 == null && pRoot != null)
            {
                GameObject dp1 = CreateUIQuad(pRoot, "Downlink_Pulse_1", Vector3.zero, new Vector3(0.08f, 0.08f, 1f), new Color(1f, 0.88f, 0.25f, 0.8f));
                downlinkPulse1 = dp1.transform;
            }

            if (downlinkPulse0 != null && downlinkPulse1 != null && spacecraftHub != null && pRoot != null)
            {
                if (!downlinkPulse0.gameObject.activeSelf) downlinkPulse0.gameObject.SetActive(true);
                if (!downlinkPulse1.gameObject.activeSelf) downlinkPulse1.gameObject.SetActive(true);

                float p0Frac = (downlinkTimer * 1.35f) % 1.0f;
                float p1Frac = (downlinkTimer * 1.35f + 0.5f) % 1.0f;

                Vector3 targetP = Vector3.zero;
                Vector3 satLocal = pRoot.InverseTransformPoint(spacecraftHub.position);
                satLocal.z = -0.05f;

                downlinkPulse0.localPosition = Vector3.Lerp(satLocal, targetP, p0Frac);
                downlinkPulse0.localScale = Vector3.one * (0.06f + p0Frac * 0.14f);
                SetRendererColor(downlinkPulse0.GetComponent<Renderer>(), new Color(1f, 0.85f, 0.20f, (1f - p0Frac) * 0.85f));

                downlinkPulse1.localPosition = Vector3.Lerp(satLocal, targetP, p1Frac);
                downlinkPulse1.localScale = Vector3.one * (0.06f + p1Frac * 0.14f);
                SetRendererColor(downlinkPulse1.GetComponent<Renderer>(), new Color(1f, 0.85f, 0.20f, (1f - p1Frac) * 0.85f));
            }
        }
        else
        {
            if (downlinkPulse0 != null && downlinkPulse0.gameObject.activeSelf) downlinkPulse0.gameObject.SetActive(false);
            if (downlinkPulse1 != null && downlinkPulse1.gameObject.activeSelf) downlinkPulse1.gameObject.SetActive(false);
        }

        RefreshMissionRequirementsBoard();

        if (simTimer >= 8.0f)
        {
            currentPhase = MissionPhase.Completed;
            if (!hasComms)
            {
                ShowFlightDirectorDebrief(true, "ORBIT SURVIVED WITHOUT ANTENNA TRANSMISSION");
            }
            else if (transmittedSciencePts < targetSciencePts)
            {
                ShowFlightDirectorDebrief(true, "ORBIT SURVIVED WITH PARTIAL SCIENCE RETURN");
            }
            else
            {
                ShowFlightDirectorDebrief(true, "MISSION ACCOMPLISHED & ORBIT SECURED");
            }
        }
    }

    public void ResetToAssembly()
    {
        currentPhase = MissionPhase.Assembly;
        countdownTimer = 0f;
        lastCountdownBeepSec = -1;
        flightThrottle = 1.0f;
        flightGimbalOffset = 0f;
        stage1Separated = false;
        rcsPlumeTimer = 0f;
        debrisHitCooldown = 0f;
        lastStatusBannerHeader = "";

        if (flightConsoleGroup != null) flightConsoleGroup.SetActive(false);
        if (manualModalObj != null) manualModalObj.SetActive(false);
        if (rumbleAudioSource != null && rumbleAudioSource.isPlaying)
            rumbleAudioSource.Stop();
        if (sfxAudioSource != null && sfxAudioSource.isPlaying)
            sfxAudioSource.Stop();
        inEclipse = false;
        hullIntegrity = 100f;
        if (crisisHighlightRing != null) crisisHighlightRing.gameObject.SetActive(false);
        if (downlinkPulse0 != null) downlinkPulse0.gameObject.SetActive(false);
        if (downlinkPulse1 != null) downlinkPulse1.gameObject.SetActive(false);

        if (scienceRewardNodes != null)
        {
            foreach (var n in scienceRewardNodes)
                if (n != null) n.gameObject.SetActive(true);
        }
        if (mapTargetLabel != null) mapTargetLabel.gameObject.SetActive(true);
        EnsurePartsPanelBounds();
        EnsureAutoPresetButtonCreated();
        EnsureHomeButtonCreated();
        SetAssemblyUIVisible(true);
        if (missionCorridorMap != null) missionCorridorMap.gameObject.SetActive(false);
        if (spacecraftHub != null)
        {
            spacecraftHub.gameObject.SetActive(true);
            spacecraftHub.localPosition = new Vector3(-0.3f, 1.55f, 0f);
            spacecraftHub.localRotation = Quaternion.identity;
            spacecraftHub.localScale = new Vector3(2.45f, 2.45f, 1f);
        }

        fairingJettisoned = false;
        fairingJettisonTimer = 0f;
        solarDeployProgress = 1.0f;
        boosterSeparated = false;
        boosterSepTimer = 0f;

        UpdateAllRocketHardwareVisuals();
        RefreshCategorizedPartsUI();
        RecalculateTelemetry();
        SetStatusBanner("NASA ODYSSEY: DEEP SPACE ARCHITECT", "Equip flight hardware on the left, balance power & delta-V for your target, and click LAUNCH!", DuoGreen);
    }

    private void SetStatusBannerIfChanged(string header, string body, Color bannerColor, bool isDebrief = false)
    {
        if (header != lastStatusBannerHeader || isDebrief)
        {
            SetStatusBanner(header, body, bannerColor, isDebrief);
        }
    }

    private void SetStatusBanner(string header, string body, Color bannerColor, bool isDebrief = false)
    {
        lastStatusBannerHeader = header;
        activeFlightDirectorHeader = header;
        activeFlightDirectorCallout = body;
        activeFlightDirectorColor = bannerColor;

        bool showBoard = (currentPhase == MissionPhase.Assembly || isDebrief);

        Color mainFill = new Color(0.06f, 0.10f, 0.16f);
        Color borderFill = bannerColor;
        borderFill.a = 1f;

        SetRendererColor(statusCardRenderer, mainFill);

        Transform boardT = transform.Find("Status_Display_Board");
        Transform borderT = transform.Find("Status_Display_Board_Border");

        if (boardT != null)
        {
            boardT.gameObject.SetActive(showBoard);
            Vector3 targetScale = isDebrief ? new Vector3(7.5f, 2.75f, 0.1f) : new Vector3(5.65f, 1.34f, 0.1f);
            boardT.localScale = targetScale;
            Vector3 pos = boardT.localPosition;
            boardT.localPosition = new Vector3(pos.x, pos.y, isDebrief ? -2.0f : -0.5f);
        }

        if (borderT != null)
        {
            borderT.gameObject.SetActive(showBoard);
            SetRendererColor(borderT.GetComponent<Renderer>(), borderFill);
            Vector3 targetBorderScale = isDebrief ? new Vector3(7.65f, 2.90f, 0.1f) : new Vector3(5.80f, 1.46f, 0.1f);
            borderT.localScale = targetBorderScale;
            Vector3 bPos = borderT.localPosition;
            borderT.localPosition = new Vector3(bPos.x, bPos.y, isDebrief ? -1.95f : -0.45f);
        }

        if (statusDisplay != null)
        {
            statusDisplay.gameObject.SetActive(showBoard);
            statusDisplay.color = Color.white;
            statusDisplay.fontSize = 36;
            statusDisplay.characterSize = isDebrief ? 0.042f : 0.052f;
            statusDisplay.lineSpacing = 1.10f;
            Vector3 tPos = statusDisplay.transform.localPosition;
            statusDisplay.transform.localPosition = new Vector3(tPos.x, tPos.y, isDebrief ? -2.1f : -0.6f);

            string hex = ColorUtility.ToHtmlStringRGB(bannerColor);
            if (isDebrief)
            {
                statusDisplay.text = $"<color=#{hex}><b>{header}</b></color>" + NL + body;
            }
            else
            {
                statusDisplay.text = $"<color=#{hex}><b>{header}</b></color>" + NL + WrapText(body, 38);
            }
        }
    }

    private void ShowFlightDirectorDebrief(bool isOrbitSuccess, string debriefNote)
    {
        if (mapTargetLabel != null) mapTargetLabel.gameObject.SetActive(false);

        // 1. Final Orbit Status (SUCCESS / ABORT)
        string orbitStatus = isOrbitSuccess ? "SUCCESS" : "ABORT";
        string statusColor = isOrbitSuccess ? "#2ECC71" : "#E74C3C";

        // 2. Science Yield vs. Target
        bool hasComms = parts != null && parts.Exists(p => p != null && p.isInstalled && p.category == "Comms");
        string scienceYield = $"{transmittedSciencePts:F0} / {targetSciencePts:F0} pts";
        string scienceEvaluation;
        if (!hasComms)
        {
            scienceEvaluation = "<color=#E67E22>0% (NO DISH ANTENNA)</color>";
        }
        else if (transmittedSciencePts >= targetSciencePts)
        {
            scienceEvaluation = "<color=#2ECC71>TARGET EXCEEDED</color>";
        }
        else if (transmittedSciencePts > 0f)
        {
            scienceEvaluation = "<color=#E67E22>PARTIAL RETURN</color>";
        }
        else
        {
            scienceEvaluation = "<color=#E74C3C>ZERO DATA</color>";
        }

        // 3. Remaining Budget & Fuel reserves
        string budgetStatus = $"${remainingBudgetMillion:F0}M";
        string fuelStatus = $"{fuelMassKg:F0} kg";
        string hullStatus = $"{hullIntegrity:F0}%";

        // 4. Efficiency Rating (Gold/Silver/Bronze) based on remaining resources
        string efficiencyRating;
        string ratingColor;
        if (isOrbitSuccess && hullIntegrity >= 90f && fuelMassKg >= 60f)
        {
            efficiencyRating = "GRADE: S+ ★ NASA MASTER FLIGHT ARCHITECT";
            ratingColor = "#F1C40F"; // Gold
        }
        else if (isOrbitSuccess && hullIntegrity >= 70f)
        {
            efficiencyRating = "GRADE: A  ★ CERTIFIED FLIGHT SPECIALIST";
            ratingColor = "#2ECC71"; // Green
        }
        else if (isOrbitSuccess && hullIntegrity >= 40f)
        {
            efficiencyRating = "GRADE: B  ● MISSION SUCCESS (MODERATE WEAR)";
            ratingColor = "#3498DB"; // Blue
        }
        else if (isOrbitSuccess)
        {
            efficiencyRating = "GRADE: C  ▲ MARGINAL ORBIT (CRITICAL DAMAGE)";
            ratingColor = "#E67E22"; // Orange
        }
        else
        {
            efficiencyRating = "GRADE: F  ✖ MISSION ABORTED (VEHICLE LOST)";
            ratingColor = "#E74C3C"; // Red
        }

        string bonusSummary = "";
        if (isOrbitSuccess)
        {
            float surplusFuelBonus = Mathf.Round(fuelMassKg * 0.75f * targetRewardMultiplier[selectedTargetIndex]);
            float pristineBonus = (hullIntegrity >= 90f) ? Mathf.Round(45f * targetRewardMultiplier[selectedTargetIndex]) : 0f;
            bonusSummary = $"FLIGHT BONUSES: Propellant: <color=#2ECC71>+{surplusFuelBonus:F0} Sci</color>  |  Pristine Hull: <color=#2ECC71>+{pristineBonus:F0} Sci</color>" + NL;
        }

        string debriefBody =
            $"MISSION TARGET: <b>{targetNames[selectedTargetIndex]}</b> ({targetRewardMultiplier[selectedTargetIndex]:F1}x Science Multiplier)" + NL +
            $"FINAL ORBIT STATUS: <color={statusColor}><b>{orbitStatus}</b></color> ({debriefNote})" + NL +
            $"TOTAL SCIENCE SECURED: <b>{scienceYield}</b>  |  {scienceEvaluation}" + NL +
            bonusSummary +
            $"REMAINING RESERVES: Budget: <b>{budgetStatus}</b>  |  Fuel: <b>{fuelStatus}</b>  |  Hull: <b>{hullStatus}</b>" + NL +
            $"FLIGHT CERTIFICATION: <color={ratingColor}><b>★ {efficiencyRating} ★</b></color>";

        Color bannerColor = isOrbitSuccess ? DuoGreen : DuoRed;
        SetStatusBanner("NASA FLIGHT DIRECTOR DEBRIEF", debriefBody, bannerColor, true);
        if (isOrbitSuccess)
            PlayVictoryFanfare();
        else
            PlayMissionAbortAudio();
    }
    private void StyleAndAnimateObstacles()
    {
        float t = Time.time;
        bool inFlight = (currentPhase != MissionPhase.Assembly && currentPhase != MissionPhase.TitleScreen);

        // 1. MAX-Q TURBULENCE
        GameObject maxQ = GameObject.Find("Obstacle1_MaxQ_Turbulence");
        if (maxQ != null)
        {
            Transform band = maxQ.transform.Find("MaxQ_Band");
            if (band != null)
            {
                Renderer r = band.GetComponent<Renderer>();
                if (r != null) r.enabled = !inFlight;
                SetRendererColor(r, new Color(0.55f, 0.22f, 0.02f));
                band.localScale = new Vector3(6.5f, 1.15f, 1f);
            }

            for (int i = 1; i <= 2; i++)
            {
                Transform streak = maxQ.transform.Find("MaxQ_Streak" + i);
                if (streak != null)
                {
                    SetRendererColor(streak.GetComponent<Renderer>(), new Color(1f, 0.85f, 0.3f, 0.75f));
                    float dir = (i == 1) ? 1f : -1f;
                    float slideX = Mathf.Sin(t * 3.5f + i) * 1.4f * dir;
                    float yOff = (i == 1) ? 0.28f : -0.28f;
                    streak.localPosition = new Vector3(slideX, yOff, -0.05f);
                    streak.localScale = new Vector3(2.8f + Mathf.Sin(t * 5f + i) * 0.6f, 0.09f, 1f);
                }
            }
        }

        // 2. RADIATION BELT (Fixes vertical oval stretching by Rotating into Energy Diamonds & Scaling X wider)
        GameObject rad = GameObject.Find("Obstacle2_Radiation_Belt");
        if (rad != null)
        {
            Transform band = rad.transform.Find("Rad_Band");
            if (band != null)
            {
                Renderer r = band.GetComponent<Renderer>();
                if (r != null) r.enabled = !inFlight;
                SetRendererColor(r, new Color(0.04f, 0.20f, 0.32f));
                band.localScale = new Vector3(6.5f, 1.15f, 1f);
            }

            for (int i = 0; i < 8; i++)
            {
                Transform p = rad.transform.Find("RadParticle_" + i);
                if (p != null)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6f + i * 1.3f);
                    SetRendererColor(p.GetComponent<Renderer>(), Color.Lerp(new Color(0f, 0.95f, 1f), new Color(0.3f, 1f, 0.55f), pulse));
                    
                    float xSpread = (i - 3.5f) * 0.72f;
                    float bobY = Mathf.Sin(t * 3.2f + i * 0.9f) * 0.25f;
                    p.localPosition = new Vector3(xSpread, bobY, -0.05f);
                    
                    // Counteract parent/mesh vertical stretch so they render as crisp compact orbs
                    float s = 0.20f + pulse * 0.06f;
                    p.localScale = new Vector3(s, s * 0.22f, s);
                }
            }
        }

        // 2b. HOLOGRAPHIC 100 KM KÁRMÁN LINE BEACON (EDGE OF SPACE BOUNDARY)
        GameObject karmanObj = GameObject.Find("Holographic_Karman_Line");
        if (karmanObj == null)
        {
            karmanObj = new GameObject("Holographic_Karman_Line");
            if (missionCorridorMap != null) karmanObj.transform.SetParent(missionCorridorMap, false);
            karmanObj.transform.position = new Vector3(-0.3f, 12.0f, 0.35f);

            CreateUIQuad(karmanObj.transform, "Karman_Line_Bar", Vector3.zero, new Vector3(8.5f, 0.04f, 1f), new Color(0f, 0.85f, 1f, 0.75f));
            CreateUIText(karmanObj.transform, "Karman_Label", new Vector3(0f, 0.16f, -0.05f),
                "─── 100 KM // KÁRMÁN LINE // ENTRY TO SPACE ───", 0.034f, 32, FontStyle.Bold, TextAnchor.MiddleCenter, TextAlignment.Center, new Color(0.2f, 0.95f, 1.0f, 0.9f));
        }
        else
        {
            Transform bar = karmanObj.transform.Find("Karman_Line_Bar");
            if (bar != null)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(t * 4.0f);
                SetRendererColor(bar.GetComponent<Renderer>(), new Color(0f, 0.85f, 1f, pulse * 0.8f));
            }
        }

        // 3. SPACE DEBRIS FIELD
        GameObject debris = GameObject.Find("Obstacle3_Space_Debris");
        if (debris != null)
        {
            Transform band = debris.transform.Find("Debris_Band");
            if (band != null)
            {
                Renderer r = band.GetComponent<Renderer>();
                if (r != null) r.enabled = !inFlight;
                SetRendererColor(r, new Color(0.32f, 0.08f, 0.12f));
                band.localScale = new Vector3(6.5f, 1.15f, 1f);
            }

            // Distribute debris across 3 distinct altitude waves with clearly telegraphed, navigable corridors
            // Wave 1 (Lower, local Y ~ -1.8f): Flanks blocked (X = -2.1, -1.3, +1.4). Center lane (X ~ -0.3) is OPEN!
            // Wave 2 (Mid, local Y ~ 0.0f): Center blocked (X = -0.5, -0.1, +0.25). Left (X ~ -1.5) & Right (X ~ +1.0) lanes OPEN!
            // Wave 3 (Upper, local Y ~ +1.8f): Right blocked (X = +0.7, +1.5, +2.2). Left/Center lane (X ~ -0.8 to -0.2) OPEN!
            float[] tierBaseY = { -1.8f, -1.9f, -1.8f, 0.0f, 0.1f, -0.1f, 1.8f, 1.9f, 1.7f };
            float[] tierBaseX = { -2.1f, -1.3f, 1.4f, -0.50f, 0.25f, -0.10f, 0.7f, 1.5f, 2.2f };

            for (int i = 0; i <= 8; i++)
            {
                Transform rock = debris.transform.Find("DebrisRock_" + i);
                if (rock != null)
                {
                    Color rockCol = (i == 3 || i == 4 || i == 5)
                        ? new Color(0.95f, 0.75f, 0.25f) // Defunct satellite core
                        : ((i % 2 == 0) ? new Color(0.85f, 0.88f, 0.94f) : new Color(0.92f, 0.42f, 0.28f)); // Orbital shrapnel
                    SetRendererColor(rock.GetComponent<Renderer>(), rockCol);

                    float bX = (i < tierBaseX.Length) ? tierBaseX[i] : (i - 4f) * 0.6f;
                    float bY = (i < tierBaseY.Length) ? tierBaseY[i] : 0f;

                    // Physical orbital drift (calibrated so safe corridors always remain open)
                    float driftX = bX + Mathf.Sin(t * 0.85f + i * 1.8f) * 0.08f;
                    float driftY = bY + Mathf.Cos(t * 0.6f + i * 1.2f) * 0.08f;

                    rock.localPosition = new Vector3(driftX, driftY, -0.05f);
                    float rSize = (i == 3) ? 0.26f : ((i % 2 == 0) ? 0.22f : 0.18f);
                    rock.localScale = new Vector3(rSize, rSize, 1f);
                    rock.localRotation = Quaternion.Euler(0f, 0f, (t * (35f + i * 15f) * (i % 2 == 0 ? 1f : -1f)) % 360f);
                }
            }
        }

        // 4. TARGET ORBIT & REWARD END ZONE UPGRADE (Global lookup so it never misses!)
        GameObject mapRoot = GameObject.Find("Mission_Corridor_Map");
        Transform searchRoot = (mapRoot != null) ? mapRoot.transform : transform.root;

        // 1. Style and reposition the Orbit TextMeshes anywhere under Mission_Corridor_Map
        foreach (TextMesh tm in searchRoot.GetComponentsInChildren<TextMesh>(true))
        {
            if (tm.text == null) continue;
            string txt = tm.text.ToUpper();

            if (txt.Contains("TARGET:") || txt.Contains("REWARD:"))
            {
                tm.color = new Color(0.2f, 0.95f, 1f); // Crisp cyan telemetry header
            }
            else if (txt.Contains("ECLIPSE") || txt.Contains("0W SOLAR"))
            {
                tm.color = new Color(1f, 0.6f, 0.2f); // Warm solar-warning amber
                // Shift right in world space relative to the orbit center (X = 0) so it clears the ring
                Vector3 wp = tm.transform.position;
                tm.transform.position = new Vector3(2.35f, wp.y, -0.25f);
            }
        }

        // 2. Style the Orbit Ring, Planet, and Science Nodes under Target_Orbit_And_Reward
        foreach (Transform tChild in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            if (tChild.name.Contains("Target_Orbit") || (tChild.parent != null && tChild.parent.name.Contains("Target_Orbit")))
            {
                LineRenderer lr = tChild.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    Color ringCyan = new Color(0f, 0.88f, 1f, 1f);
                    lr.startColor = ringCyan;
                    lr.endColor = ringCyan;
                    SetRendererColor(lr, ringCyan);
                }

                Renderer r = tChild.GetComponent<Renderer>();
                if (r != null && tChild.GetComponent<TextMesh>() == null && lr == null)
                {
                    string n = tChild.name.ToLower();
                    if (n.Contains("node") || n.Contains("sci") || n.Contains("reward") || n.Contains("point") || n.Contains("gold"))
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f + tChild.GetSiblingIndex() * 1.5f);
                        SetRendererColor(r, Color.Lerp(new Color(1f, 0.75f, 0.05f), new Color(1f, 1f, 0.7f), pulse));
                    }
                    else if (n.Contains("ring") || n.Contains("orbit"))
                    {
                        SetRendererColor(r, new Color(0f, 0.88f, 1f)); // Cyan orbit ring if it's a mesh
                    }
                    else if (n.Contains("planet") || n.Contains("earth") || n.Contains("body") || n.Contains("core"))
                    {
                        if (!n.Contains("halo"))
                        {
                            Color pCol = selectedTargetIndex == 0 ? new Color(0.08f, 0.52f, 0.92f) :
                                         (selectedTargetIndex == 1 ? new Color(0.75f, 0.78f, 0.84f) : new Color(0.92f, 0.38f, 0.20f));
                            SetRendererColor(r, pCol);
                        }
                    }
                }
            }
        }

        if (atmosphereHalo != null)
        {
            float pulse = 1.38f + Mathf.Sin(t * 2.2f) * 0.06f;
            atmosphereHalo.localScale = new Vector3(pulse, pulse, 1f);
        }
    }

    private GameObject assemblyGantryGroup;
    private Transform gantryBeacon;
    private Transform scannerLine;
    private Vector3 launchOriginPos;
    private bool originCaptured = false;

    private void StyleEnvironmentAndLaunchpad()
    {
        float t = Time.time;
        Shader unlit = Shader.Find("Unlit/Color");
        bool isAssemblyOrTitle = (currentPhase == MissionPhase.Assembly || currentPhase == MissionPhase.TitleScreen);
        Transform earthHorizon = GameObject.Find("Earth_Launch_Horizon")?.transform;

        if (assemblyGantryGroup != null)
        {
            Transform existingHaze = assemblyGantryGroup.transform.Find("Atmosphere_Launch_Sky");
            if (existingHaze != null) Destroy(existingHaze.gameObject);
            Transform existingGlow = assemblyGantryGroup.transform.Find("Atmosphere_Horizon_Glow");
            if (existingGlow != null) Destroy(existingGlow.gameObject);
        }

        // 1. DETAILED TRUSS LAUNCH COMPLEX & DEEP-Z EARTH GROUND (PROPERLY SCALED & LOWERED)
        if (assemblyGantryGroup == null && spacecraftHub != null)
        {
            launchOriginPos = spacecraftHub.position;
            originCaptured = true;

            // Ensure Starfield_Background sits at Z = 6.0f so our Earth Ground at Z = 4.5f is behind all UI panels!
            GameObject starBg = GameObject.Find("Starfield_Background");
            if (starBg != null)
            {
                starBg.transform.position = new Vector3(starBg.transform.position.x, starBg.transform.position.y, 6.0f);
            }

            // Lowered from -1.62f to -2.08f so the pad deck sits right beneath the orange engine nozzle!
            float nozzleBottomY = -2.08f;
            // Taller tower (3.25f) so it spans from the lowered pad all the way up to the nosecone!
            float towerHeight = 3.25f;

            assemblyGantryGroup = new GameObject("Custom_Launch_Gantry");
            assemblyGantryGroup.transform.position = new Vector3(launchOriginPos.x, launchOriginPos.y, -0.05f);

            // 1. EARTH GROUND: Placed at local Z = 4.5f so it renders cleanly BEHIND all dark slate UI panels!
            float groundHeight = 8.0f;
            GameObject earthSurface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            earthSurface.name = "Earth_Surface_Ground";
            Destroy(earthSurface.GetComponent<Collider>());
            earthSurface.transform.SetParent(assemblyGantryGroup.transform, false);
            earthSurface.transform.localPosition = new Vector3(0f, nozzleBottomY - (groundHeight * 0.5f) + 0.05f, 4.5f);
            earthSurface.transform.localScale = new Vector3(70f, groundHeight, 1f);
            earthSurface.GetComponent<Renderer>().material = new Material(unlit);
            SetRendererColor(earthSurface.GetComponent<Renderer>(), new Color(0.05f, 0.20f, 0.12f));

            // Subtle top turf strip along the horizon
            GameObject turfEdge = GameObject.CreatePrimitive(PrimitiveType.Quad);
            turfEdge.name = "Earth_Turf_Edge";
            Destroy(turfEdge.GetComponent<Collider>());
            turfEdge.transform.SetParent(assemblyGantryGroup.transform, false);
            turfEdge.transform.localPosition = new Vector3(0f, nozzleBottomY + 0.01f, 4.45f);
            turfEdge.transform.localScale = new Vector3(70f, 0.14f, 1f);
            turfEdge.GetComponent<Renderer>().material = new Material(unlit);
            SetRendererColor(turfEdge.GetComponent<Renderer>(), new Color(0.08f, 0.28f, 0.16f));

            // 2. SCALED-UP MULTI-TIERED LAUNCH PEDESTAL & FLAME TRENCH
            // Wide concrete foundation slab (Scaled up to 4.8f wide x 0.28f tall)
            GameObject foundation = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(foundation.GetComponent<Collider>());
            foundation.transform.SetParent(assemblyGantryGroup.transform, false);
            foundation.transform.localPosition = new Vector3(-0.15f, nozzleBottomY - 0.36f, -0.08f);
            foundation.transform.localScale = new Vector3(4.8f, 0.28f, 1f);
            foundation.GetComponent<Renderer>().material = new Material(unlit);
            SetRendererColor(foundation.GetComponent<Renderer>(), new Color(0.16f, 0.20f, 0.26f));

            // Upper steel blast deck (Scaled up to 3.8f wide x 0.24f tall)
            GameObject basePad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            basePad.name = "Pad_Deck";
            Destroy(basePad.GetComponent<Collider>());
            basePad.transform.SetParent(assemblyGantryGroup.transform, false);
            basePad.transform.localPosition = new Vector3(-0.10f, nozzleBottomY - 0.14f, -0.10f);
            basePad.transform.localScale = new Vector3(3.8f, 0.24f, 1f);
            basePad.GetComponent<Renderer>().material = new Material(unlit);
            SetRendererColor(basePad.GetComponent<Renderer>(), new Color(0.24f, 0.30f, 0.38f));

            // Dark recessed Flame Deflector Trench right under the orange flame
            GameObject trench = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(trench.GetComponent<Collider>());
            trench.transform.SetParent(assemblyGantryGroup.transform, false);
            trench.transform.localPosition = new Vector3(0f, nozzleBottomY - 0.16f, -0.12f);
            trench.transform.localScale = new Vector3(1.35f, 0.22f, 1f);
            trench.GetComponent<Renderer>().material = new Material(unlit);
            SetRendererColor(trench.GetComponent<Renderer>(), new Color(0.07f, 0.09f, 0.12f));

            // Two Heavy Hold-Down Clamps flanking the engine nozzle
            for (int side = -1; side <= 1; side += 2)
            {
                // Clamp main chassis (heavy dark industrial steel)
                GameObject clamp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                clamp.name = (side == -1) ? "HoldClamp_L" : "HoldClamp_R";
                Destroy(clamp.GetComponent<Collider>());
                clamp.transform.SetParent(assemblyGantryGroup.transform, false);
                clamp.transform.localPosition = new Vector3(side * 0.55f, nozzleBottomY + 0.05f, -0.13f);
                clamp.transform.localScale = new Vector3(0.24f, 0.22f, 1f);
                clamp.GetComponent<Renderer>().material = new Material(unlit);
                SetRendererColor(clamp.GetComponent<Renderer>(), new Color(0.25f, 0.30f, 0.38f));

                // Hydraulic piston cylinder shaft
                GameObject piston = GameObject.CreatePrimitive(PrimitiveType.Quad);
                piston.name = "Clamp_Piston";
                Destroy(piston.GetComponent<Collider>());
                piston.transform.SetParent(clamp.transform, false);
                piston.transform.localPosition = new Vector3(side * 0.15f, -0.05f, -0.01f);
                piston.transform.localScale = new Vector3(0.35f, 0.65f, 1f);
                piston.GetComponent<Renderer>().material = new Material(unlit);
                SetRendererColor(piston.GetComponent<Renderer>(), new Color(0.55f, 0.62f, 0.70f));

                // Safety-orange grip claw pad (facing inward toward the rocket)
                GameObject claw = GameObject.CreatePrimitive(PrimitiveType.Quad);
                claw.name = "Clamp_Jaw";
                Destroy(claw.GetComponent<Collider>());
                claw.transform.SetParent(clamp.transform, false);
                claw.transform.localPosition = new Vector3(-side * 0.35f, 0.15f, -0.02f);
                claw.transform.localScale = new Vector3(0.35f, 0.55f, 1f);
                claw.GetComponent<Renderer>().material = new Material(unlit);
                SetRendererColor(claw.GetComponent<Renderer>(), new Color(0.92f, 0.52f, 0.10f));
            }

            // 3. WIDER, TALLER LATTICE TRUSS UMBILICAL TOWER
            float towerCenterX = -1.45f;
            float towerBottomY = nozzleBottomY - 0.04f;

            // Left & Right vertical steel columns of the truss tower (Wider spacing: ±0.26f)
            for (int col = -1; col <= 1; col += 2)
            {
                GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(pillar.GetComponent<Collider>());
                pillar.transform.SetParent(assemblyGantryGroup.transform, false);
                pillar.transform.localPosition = new Vector3(towerCenterX + (col * 0.26f), towerBottomY + (towerHeight * 0.5f), -0.09f);
                pillar.transform.localScale = new Vector3(0.10f, towerHeight, 1f);
                pillar.GetComponent<Renderer>().material = new Material(unlit);
                SetRendererColor(pillar.GetComponent<Renderer>(), new Color(0.28f, 0.35f, 0.44f));
            }

            // Horizontal & Diagonal Truss Cross-Braces up the tower
            int bays = 8;
            float bayH = towerHeight / bays;
            for (int b = 0; b <= bays; b++)
            {
                float by = towerBottomY + b * bayH;
                // Horizontal strut
                GameObject hStrut = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(hStrut.GetComponent<Collider>());
                hStrut.transform.SetParent(assemblyGantryGroup.transform, false);
                hStrut.transform.localPosition = new Vector3(towerCenterX, by, -0.08f);
                hStrut.transform.localScale = new Vector3(0.60f, 0.05f, 1f);
                hStrut.GetComponent<Renderer>().material = new Material(unlit);
                SetRendererColor(hStrut.GetComponent<Renderer>(), new Color(0.22f, 0.28f, 0.36f));

                // Diagonal X-brace inside each bay
                if (b < bays)
                {
                    GameObject diag = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Destroy(diag.GetComponent<Collider>());
                    diag.transform.SetParent(assemblyGantryGroup.transform, false);
                    diag.transform.localPosition = new Vector3(towerCenterX, by + bayH * 0.5f, -0.07f);
                    diag.transform.localRotation = Quaternion.Euler(0f, 0f, (b % 2 == 0) ? 40f : -40f);
                    diag.transform.localScale = new Vector3(0.68f, 0.04f, 1f);
                    diag.GetComponent<Renderer>().material = new Material(unlit);
                    SetRendererColor(diag.GetComponent<Renderer>(), new Color(0.20f, 0.26f, 0.34f));
                }
            }

            // 3 Heavy Umbilical Swing Arms + Connector Heads reaching the rocket
            float[] armFractions = { 0.26f, 0.54f, 0.82f };
            foreach (float frac in armFractions)
            {
                float ay = towerBottomY + towerHeight * frac;

                // Unscaled (1,1,1) hinge pivot right on the right pillar of the truss tower
                GameObject pivot = new GameObject("Swing_Arm");
                pivot.transform.SetParent(assemblyGantryGroup.transform, false);
                pivot.transform.localPosition = new Vector3(-1.19f, ay, -0.06f);
                pivot.transform.localRotation = Quaternion.identity;
                pivot.transform.localScale = Vector3.one;

                // Orange arm beam offset to the right of the hinge
                GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(arm.GetComponent<Collider>());
                arm.transform.SetParent(pivot.transform, false);
                arm.transform.localPosition = new Vector3(0.42f, 0f, 0f);
                arm.transform.localScale = new Vector3(0.85f, 0.075f, 1f);
                arm.GetComponent<Renderer>().material = new Material(unlit);
                SetRendererColor(arm.GetComponent<Renderer>(), new Color(0.92f, 0.48f, 0.10f));

                // Clamp head parented to the unscaled pivot so it never shears when rotating!
                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Quad);
                head.name = "Arm_Head";
                Destroy(head.GetComponent<Collider>());
                head.transform.SetParent(pivot.transform, false);
                head.transform.localPosition = new Vector3(0.88f, 0f, -0.01f);
                head.transform.localScale = new Vector3(0.14f, 0.16f, 1f);
                head.GetComponent<Renderer>().material = new Material(unlit);
                SetRendererColor(head.GetComponent<Renderer>(), new Color(0.35f, 0.42f, 0.50f));
            }

            // Lightning Arrestor Mast & Blinking Red Beacon on top of the Truss Tower
            float towerTopY = towerBottomY + towerHeight;
            GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(rod.GetComponent<Collider>());
            rod.transform.SetParent(assemblyGantryGroup.transform, false);
            rod.transform.localPosition = new Vector3(towerCenterX, towerTopY + 0.18f, -0.09f);
            rod.transform.localScale = new Vector3(0.045f, 0.36f, 1f);
            rod.GetComponent<Renderer>().material = new Material(unlit);
            SetRendererColor(rod.GetComponent<Renderer>(), new Color(0.5f, 0.58f, 0.65f));

            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            beacon.name = "Tower_Beacon";
            Destroy(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(assemblyGantryGroup.transform, false);
            beacon.transform.localPosition = new Vector3(towerCenterX, towerTopY + 0.38f, -0.12f);
            beacon.transform.localScale = new Vector3(0.16f, 0.16f, 1f);
            beacon.GetComponent<Renderer>().material = new Material(unlit);
            gantryBeacon = beacon.transform;

            // Blueprint Laser Scanner
            GameObject scan = GameObject.CreatePrimitive(PrimitiveType.Quad);
            scan.name = "Blueprint_Scanner";
            Destroy(scan.GetComponent<Collider>());
            scan.transform.SetParent(assemblyGantryGroup.transform, false);
            scan.transform.localPosition = new Vector3(0f, 0f, -0.25f);
            scan.transform.localScale = new Vector3(3.4f, 0.04f, 1f);
            scan.GetComponent<Renderer>().material = new Material(unlit);
            SetRendererColor(scan.GetComponent<Renderer>(), new Color(0f, 0.95f, 1f));
            scannerLine = scan.transform;


        }

        if (assemblyGantryGroup != null && originCaptured)
        {
            Transform flightPad = GameObject.Find("Launch_Pad_Platform")?.transform;

            if (!isAssemblyOrTitle)
            {
                assemblyGantryGroup.SetActive(true);

                if (earthHorizon != null && earthHorizon.GetComponent<Renderer>() != null)
                    earthHorizon.GetComponent<Renderer>().enabled = false;
                if (flightPad != null && flightPad.GetComponent<Renderer>() != null)
                    flightPad.GetComponent<Renderer>().enabled = false;

                float scaleX = 0.35f;
                float scaleY = 0.35f;
                assemblyGantryGroup.transform.localScale = new Vector3(scaleX, scaleY, 1f);

                // Anchor to fixed world space so the rocket leaves the ground behind
                float fixedWorldY = -1.48f;
                if (flightPad != null)
                    fixedWorldY = flightPad.position.y;
                else if (earthHorizon != null)
                    fixedWorldY = earthHorizon.position.y + 0.27f;

                float groupY = fixedWorldY - (-2.08f * scaleY);
                assemblyGantryGroup.transform.position = new Vector3(-0.3f, groupY, -0.05f);

                // Stretch Earth ground vertically in Flight so shrinking assemblyGantryGroup never exposes stars underneath!
                Transform earthSurf = assemblyGantryGroup.transform.Find("Earth_Surface_Ground");
                Transform turf = assemblyGantryGroup.transform.Find("Earth_Turf_Edge");
                if (earthSurf != null)
                {
                    float flightGroundH = 40f;
                    earthSurf.localScale = new Vector3(80f, flightGroundH, 1f);
                    earthSurf.localPosition = new Vector3(0f, -2.08f - (flightGroundH * 0.5f) + 0.05f, -0.02f);
                }
                if (turf != null)
                {
                    turf.localScale = new Vector3(80f, 0.14f, 1f);
                    turf.localPosition = new Vector3(0f, -2.08f + 0.01f, -0.03f);
                }
            }
            else if (isAssemblyOrTitle)
            {
                assemblyGantryGroup.SetActive(true);
                assemblyGantryGroup.transform.localScale = Vector3.one;
                assemblyGantryGroup.transform.position = new Vector3(launchOriginPos.x, launchOriginPos.y, -0.05f);

                Transform earthSurf = assemblyGantryGroup.transform.Find("Earth_Surface_Ground");
                Transform turf = assemblyGantryGroup.transform.Find("Earth_Turf_Edge");
                if (earthSurf != null)
                {
                    float assemblyGroundH = 8.0f;
                    earthSurf.localScale = new Vector3(70f, assemblyGroundH, 1f);
                    earthSurf.localPosition = new Vector3(0f, -2.08f - (assemblyGroundH * 0.5f) + 0.05f, 4.5f);
                }
                if (turf != null)
                {
                    turf.localScale = new Vector3(70f, 0.14f, 1f);
                    turf.localPosition = new Vector3(0f, -2.08f + 0.01f, 4.45f);
                }
            }
            else
            {
                assemblyGantryGroup.SetActive(false);
            }

            // DYNAMIC UMBILICAL RETRACTION & CLAMP RELEASE
            foreach (Transform child in assemblyGantryGroup.transform)
            {
                if (child.name == "Swing_Arm")
                {
                    bool armOpen = !isAssemblyOrTitle && !(currentPhase == MissionPhase.Countdown && countdownTimer > 1.0f);
                    Quaternion targetRot = armOpen ? Quaternion.Euler(0f, 0f, 75f) : Quaternion.identity;
                    child.localRotation = Quaternion.Lerp(child.localRotation, targetRot, Time.deltaTime * 6f);
                }
                else if (child.name == "HoldClamp_L" || child.name == "HoldClamp_R")
                {
                    float side = (child.name == "HoldClamp_L") ? -1f : 1f;
                    bool clampsReleased = !isAssemblyOrTitle && !(currentPhase == MissionPhase.Countdown && countdownTimer > 2.0f);
                    float targetClampX = clampsReleased ? (side * 0.95f) : (side * 0.55f);
                    Quaternion targetClampRot = clampsReleased ? Quaternion.Euler(0f, 0f, -side * 35f) : Quaternion.identity;

                    child.localPosition = Vector3.Lerp(child.localPosition, new Vector3(targetClampX, child.localPosition.y, child.localPosition.z), Time.deltaTime * 8f);
                    child.localRotation = Quaternion.Lerp(child.localRotation, targetClampRot, Time.deltaTime * 8f);
                }
            }

            if (gantryBeacon != null)
            {
                float blink = Mathf.Sin(t * 6f) > 0f ? 1f : 0.15f;
                SetRendererColor(gantryBeacon.GetComponent<Renderer>(), new Color(1f, 0.15f, 0.15f) * blink);
            }

            if (scannerLine != null)
            {
                bool showScanner = (currentPhase == MissionPhase.Assembly);
                scannerLine.gameObject.SetActive(showScanner);
                if (showScanner)
                {
                    float scanY = Mathf.Sin(t * 2.2f) * 1.55f - 0.15f;
                    scannerLine.localPosition = new Vector3(0f, scanY, -0.25f);
                }
            }
        }

        

        // 2. TWINKLING STARFIELD BACKGROUND
        GameObject starfield = GameObject.Find("Starfield_Background");
        if (starfield != null)
        {
            int idx = 0;
            foreach (Transform star in starfield.transform)
            {
                Renderer sr = star.GetComponent<Renderer>();
                if (sr != null && star.localScale.x < 1.5f)
                {
                    float twinkle = 0.45f + 0.55f * Mathf.Sin(t * (2.2f + (idx % 5) * 0.7f) + idx * 1.9f);
                    Color baseStar = (idx % 3 == 0) ? new Color(0.6f, 0.85f, 1f) :
                                     (idx % 4 == 0) ? new Color(1f, 0.88f, 0.65f) : Color.white;
                    Color finalStar = baseStar * twinkle * currentAtmosphericStarAlpha;
                    finalStar.a = currentAtmosphericStarAlpha * (0.6f + twinkle * 0.4f);
                    SetRendererColor(sr, finalStar);
                    idx++;
                }
            }
        }

        // 3. HOLOGRAPHIC OBSTACLE RAILS IN FLIGHT
        AddBandBorders("Obstacle1_MaxQ_Turbulence", "MaxQ_Band", new Color(1f, 0.6f, 0.1f));
        AddBandBorders("Obstacle2_Radiation_Belt", "Rad_Band", new Color(0f, 0.9f, 1f));
        AddBandBorders("Obstacle3_Space_Debris", "Debris_Band", new Color(1f, 0.25f, 0.3f));

        // 4. FLIGHT VISUAL JUICE EFFECTS (FLAME PULSE & OBSTACLE PARTICLE WAKE)
        UpdateFlightVisualJuice();
    }

    private readonly Dictionary<Transform, Vector3> flameBaseScales = new Dictionary<Transform, Vector3>();
    private Transform cachedExhaustFlame;

    private void UpdateFlightVisualJuice()
    {
        if (spacecraftHub == null) return;

        bool isFlight = (currentPhase != MissionPhase.Assembly && currentPhase != MissionPhase.TitleScreen);

        // 1. FLICKER & PULSE ENGINE EXHAUST FLAME SCALE USING PERLIN NOISE
        if (cachedExhaustFlame == null)
        {
            cachedExhaustFlame = spacecraftHub.Find("Rocket_Exhaust_Flame");
            if (cachedExhaustFlame == null)
            {
                GameObject flameObj = CreateUIQuad(spacecraftHub, "Rocket_Exhaust_Flame", new Vector3(0f, -0.84f, -0.04f), new Vector3(0.25f, 0.62f, 1f), new Color(1.0f, 0.55f, 0.12f, 0.95f), false, 12);
                cachedExhaustFlame = flameObj.transform;
            }
        }

        if (cachedExhaustFlame != null)
        {
            if (cachedExhaustFlame.parent != spacecraftHub)
            {
                cachedExhaustFlame.SetParent(spacecraftHub, false);
            }

            bool isFlameActive = (currentPhase == MissionPhase.Launching && flightThrottle > 0.05f && fuelMassKg > 0.01f) ||
                                 (currentPhase == MissionPhase.Countdown && countdownTimer <= 1.0f);
            if (!isFlameActive)
            {
                if (cachedExhaustFlame.gameObject.activeSelf)
                    cachedExhaustFlame.gameObject.SetActive(false);
            }
            else
            {
                if (!cachedExhaustFlame.gameObject.activeSelf)
                    cachedExhaustFlame.gameObject.SetActive(true);

                // Dynamically position, size, and tint the flame for Stage 1 vs Stage 2 Upper Stage
                Vector3 targetPos;
                Vector3 targetBaseScale;
                Color targetColor;

                if (!stage1Separated)
                {
                    // Stage 1 (Booster attached) - Lower nozzle at Y = -0.84f, Atmospheric kerolox orange plume
                    targetPos = new Vector3(0f, -0.84f, -0.04f);
                    targetBaseScale = new Vector3(0.25f, 0.62f, 1f);
                    targetColor = new Color(1.0f, 0.55f, 0.12f, 0.95f);
                }
                else
                {
                    // Stage 2 (Upper Stage S-II online) - Upper nozzle at Y = -0.28f, Electric cyan/blue vacuum plume
                    targetPos = new Vector3(0f, -0.28f, -0.04f);
                    targetBaseScale = new Vector3(0.19f, 0.40f, 1f);
                    targetColor = new Color(0.18f, 0.85f, 1.0f, 0.92f);
                }

                float throttleFactor = (currentPhase == MissionPhase.Countdown) ? 1.0f : (0.45f + 0.55f * flightThrottle);
                float noise = Mathf.PerlinNoise(Time.time * 35f, 0f);
                float pulseY = targetBaseScale.y * throttleFactor * (0.82f + noise * 0.40f);
                float pulseX = targetBaseScale.x * throttleFactor * (0.88f + (1f - noise) * 0.24f);

                cachedExhaustFlame.localPosition = targetPos;
                cachedExhaustFlame.localScale = new Vector3(pulseX, pulseY, 1f);
                SetRendererColor(cachedExhaustFlame.GetComponent<Renderer>(), targetColor);

                // Physical Thrust Vector Control (TVC) Engine Bell Gimbaling
                float tvcAngle = -flightGimbalOffset * 9.0f;
                cachedExhaustFlame.localRotation = Quaternion.Euler(0f, 0f, tvcAngle);

                if (lowerBoosterStage != null && lowerBoosterStage.gameObject.activeInHierarchy)
                {
                    Transform bNozzle = lowerBoosterStage.Find("Booster_Nozzle");
                    if (bNozzle != null) bNozzle.localRotation = Quaternion.Euler(0f, 0f, tvcAngle);
                }
                if (upperStageGroup != null && upperStageGroup.gameObject.activeInHierarchy)
                {
                    Transform uNozzle = upperStageGroup.Find("Upper_Nozzle");
                    if (uNozzle != null) uNozzle.localRotation = Quaternion.Euler(0f, 0f, tvcAngle);
                }
            }
        }

        // 1b. MAX-Q TRANSONIC CONDENSATION VAPOR CONE
        if (maxQVaporCone == null)
        {
            Transform existing = spacecraftHub.Find("MaxQ_Vapor_Cone");
            if (existing != null)
            {
                maxQVaporCone = existing;
            }
            else
            {
                GameObject cone = CreateUIQuad(spacecraftHub, "MaxQ_Vapor_Cone", new Vector3(0f, 0.22f, -0.07f), new Vector3(0.58f, 0.32f, 1f), new Color(0.85f, 0.95f, 1.0f, 0.65f));
                maxQVaporCone = cone.transform;
            }
        }

        if (maxQVaporCone != null)
        {
            float distToMaxQ = Mathf.Abs(spacecraftHub.position.y - ObstacleYPositions[0]);
            bool inMaxQZone = (currentPhase == MissionPhase.Launching || currentPhase == MissionPhase.FlightCrisis) && (distToMaxQ <= 1.4f);
            if (!inMaxQZone)
            {
                if (maxQVaporCone.gameObject.activeSelf) maxQVaporCone.gameObject.SetActive(false);
            }
            else
            {
                if (!maxQVaporCone.gameObject.activeSelf) maxQVaporCone.gameObject.SetActive(true);
                float intensity = 1.0f - (distToMaxQ / 1.4f);
                float flutter = Mathf.PerlinNoise(Time.time * 48f, 0f);
                float scaleX = (0.50f + intensity * 0.22f) * (0.92f + flutter * 0.16f);
                float scaleY = (0.28f + intensity * 0.14f) * (0.90f + (1f - flutter) * 0.20f);
                maxQVaporCone.localScale = new Vector3(scaleX, scaleY, 1f);
                Color vaporCol = new Color(0.88f, 0.96f, 1.0f, 0.45f + intensity * 0.45f);
                SetRendererColor(maxQVaporCone.GetComponent<Renderer>(), vaporCol);
            }
        }

        // 1c. ION THRUSTER XENON BLUE PLASMA PLUME (ORBIT OPERATIONS)
        if (ionPlasmaPlume == null)
        {
            Transform existing = spacecraftHub.Find("Ion_Plasma_Glow");
            if (existing != null)
            {
                ionPlasmaPlume = existing;
            }
            else
            {
                GameObject plume = CreateUIQuad(spacecraftHub, "Ion_Plasma_Glow", new Vector3(0f, -0.22f, -0.08f), new Vector3(0.12f, 0.28f, 1f), new Color(0.12f, 0.88f, 1.0f, 0.90f));
                ionPlasmaPlume = plume.transform;
            }
        }

        if (ionPlasmaPlume != null)
        {
            bool isOrbiting = (currentPhase == MissionPhase.OrbitSimulation);
            if (!isOrbiting)
            {
                if (ionPlasmaPlume.gameObject.activeSelf) ionPlasmaPlume.gameObject.SetActive(false);
            }
            else
            {
                if (!ionPlasmaPlume.gameObject.activeSelf) ionPlasmaPlume.gameObject.SetActive(true);
                float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 22f);
                ionPlasmaPlume.localScale = new Vector3(0.12f * (0.95f + 0.10f * Mathf.Cos(Time.time * 30f)), 0.28f * pulse, 1f);
                Color ionCol = new Color(0.10f, 0.88f, 1.0f, 0.75f + 0.25f * pulse);
                SetRendererColor(ionPlasmaPlume.GetComponent<Renderer>(), ionCol);
            }
        }

        if (isFlight)
        {
            // Pulse any other flame/plume child under spacecraftHub if present
            foreach (Transform c in spacecraftHub)
            {
                if (c != cachedExhaustFlame && (c.name.Contains("Flame") || c.name.Contains("Plume")) && !c.name.Contains("Particles"))
                {
                    if (!c.gameObject.activeSelf) c.gameObject.SetActive(true);
                    if (!flameBaseScales.ContainsKey(c)) flameBaseScales[c] = c.localScale;
                    Vector3 bScale = flameBaseScales[c];
                    float noise = Mathf.PerlinNoise(Time.time * 35f, 0f);
                    c.localScale = new Vector3(bScale.x * (0.88f + (1f - noise) * 0.24f), bScale.y * (0.80f + noise * 0.45f), bScale.z);
                }
            }

            // 2. OBSTACLE PARTICLE WAKE / SCATTER DURING FLIGHT
            // Obstacle 2: Radiation Belt
            GameObject radObj = GameObject.Find("Obstacle2_Radiation_Belt");
            if (radObj == null && missionCorridorMap != null)
            {
                Transform t = missionCorridorMap.Find("Obstacle2_Radiation_Belt");
                if (t != null) radObj = t.gameObject;
            }

            if (radObj != null && Mathf.Abs(spacecraftHub.position.y - radObj.transform.position.y) <= 1.5f)
            {
                foreach (Transform p in radObj.GetComponentsInChildren<Transform>())
                {
                    if (p.name.StartsWith("RadParticle_"))
                    {
                        float dx = p.position.x - spacecraftHub.position.x;
                        float distX = Mathf.Abs(dx);
                        if (distX < 1.4f)
                        {
                            float dy = Mathf.Abs(spacecraftHub.position.y - p.position.y);
                            float yFalloff = Mathf.Clamp01(1f - (dy / 1.5f));
                            float xFalloff = Mathf.Clamp01(1f - (distX / 1.4f));
                            float pushStrength = yFalloff * xFalloff * 0.45f;
                            float pushSign = (dx >= 0f) ? 1f : -1f;
                            p.position += new Vector3(pushSign * pushStrength, 0f, 0f);
                        }
                    }
                }
            }

            // Obstacle 3: Space Debris
            GameObject debrisObj = GameObject.Find("Obstacle3_Space_Debris");
            if (debrisObj == null && missionCorridorMap != null)
            {
                Transform t = missionCorridorMap.Find("Obstacle3_Space_Debris");
                if (t != null) debrisObj = t.gameObject;
            }

            if (debrisObj != null && Mathf.Abs(spacecraftHub.position.y - debrisObj.transform.position.y) <= 1.5f)
            {
                foreach (Transform rock in debrisObj.GetComponentsInChildren<Transform>())
                {
                    if (rock.name.StartsWith("DebrisRock_"))
                    {
                        float dx = rock.position.x - spacecraftHub.position.x;
                        float distX = Mathf.Abs(dx);
                        if (distX < 1.5f)
                        {
                            float dy = Mathf.Abs(spacecraftHub.position.y - rock.position.y);
                            float yFalloff = Mathf.Clamp01(1f - (dy / 1.5f));
                            float xFalloff = Mathf.Clamp01(1f - (distX / 1.5f));
                            float pushStrength = yFalloff * xFalloff * 0.50f;
                            float pushSign = (dx >= 0f) ? 1f : -1f;
                            rock.position += new Vector3(pushSign * pushStrength, 0f, 0f);
                        }
                    }
                }
            }
        }
    }

    private void AddBandBorders(string parentName, string bandName, Color railColor)
    {
        GameObject parent = GameObject.Find(parentName);
        if (parent == null) return;
        Transform band = parent.transform.Find(bandName);
        if (band == null) return;

        bool inFlight = (currentPhase != MissionPhase.Assembly && currentPhase != MissionPhase.TitleScreen);

        Transform topR = band.Find("Top_Rail");
        Transform btmR = band.Find("Bottom_Rail");
        if (topR != null) topR.gameObject.SetActive(!inFlight);
        if (btmR != null) btmR.gameObject.SetActive(!inFlight);

        if (inFlight) return;

        // Widen the obstacle band horizontally across the flight corridor
        float targetWidth = 14.5f;
        if (Mathf.Abs(band.localScale.x - targetWidth) > 0.05f)
        {
            band.localScale = new Vector3(targetWidth, band.localScale.y, band.localScale.z);
        }

        if (band.Find("Top_Rail") != null) return;

        Shader unlit = Shader.Find("Unlit/Color");
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Quad);
            rail.name = (side == 1) ? "Top_Rail" : "Bottom_Rail";
            Destroy(rail.GetComponent<Collider>());
            rail.transform.SetParent(band, false);
            rail.transform.localPosition = new Vector3(0f, side * 0.48f, -0.02f);
            rail.transform.localScale = new Vector3(1f, 0.06f, 1f);
            rail.GetComponent<Renderer>().material = new Material(unlit);
            SetRendererColor(rail.GetComponent<Renderer>(), railColor);
        }

        // CAMERA SHAKE FOR LIFTOFF & MAX-Q TURBULENCE
            if (currentPhase != MissionPhase.Assembly && Camera.main != null && spacecraftHub != null)
            {
                float shakeIntensity = 0f;

                // Pre-liftoff engine ignition rumble on launchpad during T-1
                if (currentPhase == MissionPhase.Countdown && countdownTimer <= 1.0f)
                {
                    shakeIntensity = Mathf.Lerp(0.015f, 0.055f, 1.0f - countdownTimer);
                }

                // 1. Liftoff rumble near the pad
                float altAbovePad = spacecraftHub.position.y - LaunchpadY;
                if (currentPhase == MissionPhase.Launching && altAbovePad >= 0f && altAbovePad < 3.5f)
                {
                    shakeIntensity = Mathf.Lerp(0.065f, 0.015f, altAbovePad / 3.5f);
                }

                // 2. Heavy aerodynamic buffet inside Obstacle 1 (Max-Q)
                GameObject maxQ = GameObject.Find("Obstacle1_MaxQ_Turbulence");
                if (maxQ != null && Mathf.Abs(spacecraftHub.position.y - maxQ.transform.position.y) < 1.6f)
                {
                    shakeIntensity = Mathf.Max(shakeIntensity, 0.075f);
                }

                if (shakeIntensity > 0.001f)
                {
                    float sx = (Mathf.PerlinNoise(Time.time * 45f, 0f) - 0.5f) * 2f * shakeIntensity;
                    float sy = (Mathf.PerlinNoise(0f, Time.time * 45f) - 0.5f) * 2f * shakeIntensity;
                    Camera.main.transform.position += new Vector3(sx, sy, 0f);
                }
            }
    }

    private ParticleSystem rocketExhaustPS;
    private ParticleSystem.MinMaxGradient orangeFireGrad;
    private ParticleSystem.MinMaxGradient cyanVacuumGrad;

    private void UpdateRocketExhaustParticles()
    {
        if (spacecraftHub == null) return;

        if (rocketExhaustPS == null)
        {
            GameObject psObj = new GameObject("Rocket_Exhaust_Particles");
            psObj.transform.SetParent(spacecraftHub, false);
            // Initial position at booster nozzle
            psObj.transform.localPosition = new Vector3(0f, -0.84f, 0.05f);
            psObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            rocketExhaustPS = psObj.AddComponent<ParticleSystem>();
            var main = rocketExhaustPS.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.8f, 4.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 250;

            var emission = rocketExhaustPS.emission;
            emission.rateOverTime = 110f;

            var shape = rocketExhaustPS.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.03f;

            var colorOverLifetime = rocketExhaustPS.colorOverLifetime;
            colorOverLifetime.enabled = true;

            // 1. Stage 1 Atmospheric Fire Gradient (Yellow/Orange flame + smoke)
            Gradient gradOrange = new Gradient();
            gradOrange.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.98f, 0.85f), 0.0f),
                    new GradientColorKey(new Color(1f, 0.52f, 0.08f), 0.35f),
                    new GradientColorKey(new Color(0.85f, 0.18f, 0.05f), 0.75f),
                    new GradientColorKey(new Color(0.35f, 0.35f, 0.35f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            orangeFireGrad = new ParticleSystem.MinMaxGradient(gradOrange);

            // 2. Stage 2 Supersonic Vacuum Cyan Gradient (High-energy ion/vacuum expansion)
            Gradient gradCyan = new Gradient();
            gradCyan.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.90f, 0.98f, 1.0f), 0.0f),
                    new GradientColorKey(new Color(0.15f, 0.88f, 1.0f), 0.35f),
                    new GradientColorKey(new Color(0.10f, 0.40f, 0.95f), 0.75f),
                    new GradientColorKey(new Color(0.05f, 0.15f, 0.50f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            cyanVacuumGrad = new ParticleSystem.MinMaxGradient(gradCyan);

            colorOverLifetime.color = orangeFireGrad;

            var sizeOverLifetime = rocketExhaustPS.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 0.8f);
            curve.AddKey(0.25f, 1.25f);
            curve.AddKey(1.0f, 0.15f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var psRenderer = psObj.GetComponent<ParticleSystemRenderer>();
            Shader particleShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            psRenderer.material = new Material(particleShader);
            psRenderer.sortingOrder = 11;
        }

        // Fire exhaust during active flight phases when throttle is positive and fuel remains
        bool isThrusting = (currentPhase == MissionPhase.Launching && flightThrottle > 0.05f && fuelMassKg > 0.01f) ||
                           (currentPhase == MissionPhase.Countdown && countdownTimer <= 1.0f);

        var em = rocketExhaustPS.emission;
        em.enabled = isThrusting;

        if (isThrusting)
        {
            float tvcAngle = -flightGimbalOffset * 9.0f;
            rocketExhaustPS.transform.localRotation = Quaternion.Euler(90f, tvcAngle, 0f);

            var col = rocketExhaustPS.colorOverLifetime;
            var shape = rocketExhaustPS.shape;
            if (!stage1Separated)
            {
                // Stage 1 booster nozzle at Y = -0.84f
                rocketExhaustPS.transform.localPosition = new Vector3(0f, -0.84f, 0.05f);
                shape.angle = 8f;
                col.color = orangeFireGrad;
                em.rateOverTime = 120f * Mathf.Clamp01(flightThrottle);
            }
            else
            {
                // Stage 2 upper stage nozzle at Y = -0.28f
                rocketExhaustPS.transform.localPosition = new Vector3(0f, -0.28f, 0.05f);
                shape.angle = 16f; // Vacuum plume expansion in space!
                col.color = cyanVacuumGrad;
                em.rateOverTime = 85f * Mathf.Clamp01(flightThrottle);
            }
        }
    }

    private string WrapText(string input, int maxCharsPerLine)
    {
        if (string.IsNullOrEmpty(input)) return "";
        string[] words = input.Split(' ');
        string result = "";
        int lineLen = 0;
        foreach (string w in words)
        {
            if (lineLen + w.Length > maxCharsPerLine)
            {
                result += NL + w + " ";
                lineLen = w.Length + 1;
            }
            else
            {
                result += w + " ";
                lineLen += w.Length + 1;
            }
        }
        return result.TrimEnd();
    }

    // =========================================================================
    // PROCEDURAL AUDIO SYSTEM (AudioSource + AudioClip.Create)
    // =========================================================================
    private AudioSource sfxAudioSource;
    private AudioSource rumbleAudioSource;
    private AudioClip telemetryBeepClip;
    private AudioClip warningAlarmClip;
    private AudioClip thrusterRumbleClip;
    private AudioClip countdownPipClip_Low;
    private AudioClip countdownPipClip_High;
    private AudioClip countdownPipClip_Go;
    private AudioClip ignitionBlastClip;
    private AudioClip liftoffRoarClip;
    private AudioClip victoryFanfareClip;
    private AudioClip missionAbortClip;

    private void InitProceduralAudio()
    {
        AudioSource[] existing = GetComponents<AudioSource>();
        sfxAudioSource = (existing.Length > 0) ? existing[0] : gameObject.AddComponent<AudioSource>();
        rumbleAudioSource = (existing.Length > 1) ? existing[1] : gameObject.AddComponent<AudioSource>();

        sfxAudioSource.playOnAwake = false;
        rumbleAudioSource.playOnAwake = false;
        rumbleAudioSource.loop = true;
        rumbleAudioSource.volume = 0.55f;

        telemetryBeepClip = CreateTelemetryBeepClip();
        warningAlarmClip = CreateWarningAlarmClip();
        thrusterRumbleClip = CreateThrusterRumbleClip();

        countdownPipClip_Low = CreateCountdownPipClip(700f, 0.12f, true, 0.12f);
        countdownPipClip_High = CreateCountdownPipClip(920f, 0.14f, true, 0.15f);
        countdownPipClip_Go = CreateCountdownPipClip(1200f, 0.35f, false, 0.25f);
        ignitionBlastClip = CreateIgnitionBlastClip();
        liftoffRoarClip = CreateLiftoffRoarClip();
        victoryFanfareClip = CreateVictoryFanfareClip();
        missionAbortClip = CreateMissionAbortClip();
        stagingPopClip = CreateStagingPopClip();
        solarDeployClip = CreateSolarDeployClip();

        rumbleAudioSource.clip = thrusterRumbleClip;
    }

    private AudioClip CreateCountdownPipClip(float freq, float duration, bool withQuindar, float harmonicMix)
    {
        int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        float quindarDuration = withQuindar ? 0.015f : 0f;
        int quindarSamples = (int)(sampleRate * quindarDuration);

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float val = 0f;

            if (i < quindarSamples)
            {
                // NASA Apollo Quindar chirp (2525 Hz)
                float qEnv = Mathf.Sin(((float)i / quindarSamples) * Mathf.PI);
                val = Mathf.Sin(2f * Mathf.PI * 2525f * t) * qEnv * 0.35f;
            }
            else
            {
                float toneT = t - quindarDuration;
                float env = (toneT < 0.005f) ? (toneT / 0.005f) : Mathf.Exp(-(toneT - 0.005f) * 18f);

                float fundamental = Mathf.Sin(2f * Mathf.PI * freq * toneT);
                float harmonic = Mathf.Sin(2f * Mathf.PI * (freq * 2f) * toneT) * harmonicMix;
                val = (fundamental + harmonic) * env * 0.5f;
            }

            samples[i] = val / (1f + Mathf.Abs(val));
        }

        AudioClip clip = AudioClip.Create($"CountdownPip_{freq:F0}Hz", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateIgnitionBlastClip()
    {
        int sampleRate = 44100;
        float duration = 1.6f;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        float brownVal = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float s = 0f;

            if (t < 0.38f)
            {
                // Turbopump spin-up whine sweeping 160Hz -> 680Hz
                float pumpFreq = Mathf.Lerp(160f, 680f, t / 0.38f);
                float pumpTone = Mathf.Sin(2f * Mathf.PI * pumpFreq * t) * (t / 0.38f) * 0.32f;

                // Valve hiss & igniter pops
                float white = UnityEngine.Random.Range(-1f, 1f);
                float hiss = white * (t / 0.38f) * 0.12f;

                // Pyrotechnic igniter pop clicks at 0.12s, 0.24s, 0.34s
                float pop1 = Mathf.Exp(-Mathf.Pow((t - 0.12f) / 0.004f, 2f));
                float pop2 = Mathf.Exp(-Mathf.Pow((t - 0.24f) / 0.004f, 2f));
                float pop3 = Mathf.Exp(-Mathf.Pow((t - 0.34f) / 0.004f, 2f));
                float pops = (pop1 + pop2 * 1.2f + pop3 * 1.5f) * UnityEngine.Random.Range(-1f, 1f) * 0.35f;

                s = pumpTone + hiss + pops;
            }
            else
            {
                float blastT = t - 0.38f;
                // Sub-bass detonation boom (starts at 55Hz and descends to 38Hz)
                float subFreq = Mathf.Lerp(55f, 38f, Mathf.Clamp01(blastT / 0.6f));
                float subBass = Mathf.Sin(2f * Mathf.PI * subFreq * blastT) * Mathf.Exp(-blastT * 3.2f) * 0.70f;

                // Combustion flame turbulence
                float white = UnityEngine.Random.Range(-1f, 1f);
                brownVal = (brownVal + 0.08f * white) / 1.08f;
                float fireNoise = brownVal * 2.4f * Mathf.Exp(-blastT * 1.4f);

                // Supersonic flame crackle
                float crackle = (Mathf.Abs(white) > 0.82f) ? white * 0.35f * Mathf.Exp(-blastT * 1.8f) : 0f;

                s = subBass + fireNoise + crackle;
            }

            samples[i] = s / (1f + Mathf.Abs(s));
        }

        AudioClip clip = AudioClip.Create("RocketIgnition_Blast", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateLiftoffRoarClip()
    {
        int sampleRate = 44100;
        float duration = 2.8f;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        float brownVal = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;

            // 1. Initial acoustic expansion thump at T-0 (first 0.6s)
            float subThump = 0f;
            if (t < 0.6f)
            {
                float thumpFreq = Mathf.Lerp(48f, 32f, t / 0.6f);
                subThump = Mathf.Sin(2f * Mathf.PI * thumpFreq * t) * Mathf.Exp(-t * 2.8f) * 0.85f;
            }

            // 2. Continuous roaring combustion noise (heavily textured)
            float white = UnityEngine.Random.Range(-1f, 1f);
            brownVal = (brownVal + 0.06f * white) / 1.06f;

            // Rocket acoustic plume resonance: low rumble around 58Hz
            float resonance = Mathf.Sin(2f * Mathf.PI * 58f * t) * 0.22f;

            // Turbulent aerodynamic crackle
            float crackle = (Mathf.Abs(white) > 0.78f) ? white * 0.4f : 0f;

            // Overall envelope: rapid attack (20ms), swells in first 0.4s, then sustains and gently fades
            float env = (t < 0.02f) ? (t / 0.02f) : (1.0f - (t / duration) * 0.45f);

            float total = (subThump + (brownVal * 3.0f) + resonance + crackle) * env * 0.75f;
            samples[i] = total / (1f + Mathf.Abs(total));
        }

        AudioClip clip = AudioClip.Create("RocketLiftoff_AcousticRoar", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateTelemetryBeepClip()
    {
        int sampleRate = 44100;
        float duration = 0.075f; // 75ms
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float tone = Mathf.Sin(2f * Mathf.PI * 880f * t);
            float env = (t < 0.003f) ? (t / 0.003f) : Mathf.Exp(-(t - 0.003f) * 45f);
            samples[i] = tone * env * 0.45f;
        }

        AudioClip clip = AudioClip.Create("TelemetryBeep_880Hz", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateWarningAlarmClip()
    {
        int sampleRate = 44100;
        float duration = 0.28f; // 280ms two-tone alarm
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        float halfDuration = duration * 0.5f;

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            bool isFirstHalf = (t < halfDuration);
            float freq = isFirstHalf ? 620f : 880f;
            float toneT = isFirstHalf ? t : (t - halfDuration);

            float env = Mathf.Sin(Mathf.Clamp01(toneT / halfDuration) * Mathf.PI);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
            samples[i] = tone * env * 0.5f;
        }

        AudioClip clip = AudioClip.Create("WarningAlarm_TwoTone", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateThrusterRumbleClip()
    {
        int sampleRate = 22050;
        float duration = 2.0f; // 2.0s seamless loop
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        float brownVal = 0f;
        for (int i = 0; i < totalSamples; i++)
        {
            float white = UnityEngine.Random.Range(-1f, 1f);
            brownVal = (brownVal + (0.035f * white)) / 1.035f;

            float t = (float)i / sampleRate;
            float subRumble = Mathf.Sin(2f * Mathf.PI * 52f * t) * 0.15f;
            samples[i] = Mathf.Clamp((brownVal * 3.4f) + subRumble, -1f, 1f) * 0.65f;
        }

        // Circular cross-fade over the last 150ms
        int blendSamples = (int)(sampleRate * 0.15f);
        for (int i = 0; i < blendSamples; i++)
        {
            float factor = (float)i / blendSamples;
            int tailIdx = totalSamples - blendSamples + i;
            samples[i] = Mathf.Lerp(samples[tailIdx], samples[i], factor);
        }

        AudioClip clip = AudioClip.Create("RocketThruster_BrownNoise", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public void PlayTelemetryBeep()
    {
        if (sfxAudioSource != null && telemetryBeepClip != null)
        {
            sfxAudioSource.PlayOneShot(telemetryBeepClip, 0.7f);
        }
    }

    public void PlayWarningAlarm()
    {
        if (sfxAudioSource != null && warningAlarmClip != null)
        {
            sfxAudioSource.PlayOneShot(warningAlarmClip, 0.85f);
        }
    }

    public void PlayCountdownPip(int sec)
    {
        if (sfxAudioSource == null) return;
        if (sec == 3 || sec == 2)
        {
            if (countdownPipClip_Low != null) sfxAudioSource.PlayOneShot(countdownPipClip_Low, 0.85f);
        }
        else if (sec == 1)
        {
            if (countdownPipClip_High != null) sfxAudioSource.PlayOneShot(countdownPipClip_High, 0.95f);
        }
        else if (sec == 0) // T-0 GO
        {
            if (countdownPipClip_Go != null) sfxAudioSource.PlayOneShot(countdownPipClip_Go, 0.90f);
        }
    }

    public void PlayIgnitionBlastAudio()
    {
        if (sfxAudioSource != null && ignitionBlastClip != null)
        {
            sfxAudioSource.PlayOneShot(ignitionBlastClip, 0.95f);
        }
    }

    public void PlayLiftoffAudio()
    {
        if (sfxAudioSource != null && liftoffRoarClip != null)
        {
            sfxAudioSource.PlayOneShot(liftoffRoarClip, 1.0f);
        }
        if (rumbleAudioSource != null && !rumbleAudioSource.isPlaying)
        {
            rumbleAudioSource.Play();
        }
    }

    private void UpdateThrusterRumbleAudio()
    {
        if (rumbleAudioSource == null) return;

        bool shouldRumble = (currentPhase == MissionPhase.Launching);
        if (shouldRumble)
        {
            if (!rumbleAudioSource.isPlaying)
            {
                rumbleAudioSource.Play();
            }
        }
        else
        {
            if (rumbleAudioSource.isPlaying)
            {
                rumbleAudioSource.Stop();
            }
        }
    }

    private AudioClip CreateVictoryFanfareClip()
    {
        int sampleRate = 44100;
        float duration = 1.6f;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        float[] noteFreqs = { 523.25f, 659.25f, 783.99f, 1046.50f };
        float[] noteStarts = { 0.00f, 0.26f, 0.52f, 0.80f };
        float[] noteEnds = { 0.25f, 0.51f, 0.79f, 1.58f };

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float s = 0f;

            for (int n = 0; n < noteFreqs.Length; n++)
            {
                if (t >= noteStarts[n] && t <= noteEnds[n])
                {
                    float tNote = t - noteStarts[n];
                    float env = (tNote < 0.015f) ? (tNote / 0.015f) : Mathf.Exp(-(tNote - 0.015f) * (n == 3 ? 2.5f : 4.5f));

                    float f = noteFreqs[n];
                    float fundamental = Mathf.Sin(2f * Mathf.PI * f * tNote);
                    float h2 = Mathf.Sin(2f * Mathf.PI * (f * 2f) * tNote) * 0.45f;
                    float h3 = Mathf.Sin(2f * Mathf.PI * (f * 3f) * tNote) * 0.22f;
                    float h4 = Mathf.Sin(2f * Mathf.PI * (f * 4f) * tNote) * 0.12f;

                    s += (fundamental + h2 + h3 + h4) * env * 0.45f;
                }
            }

            int delaySamples = (int)(sampleRate * 0.18f);
            if (i > delaySamples)
            {
                s += samples[i - delaySamples] * 0.26f;
            }

            samples[i] = s / (1f + Mathf.Abs(s));
        }

        AudioClip clip = AudioClip.Create("VictoryFanfare_NASA", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateMissionAbortClip()
    {
        int sampleRate = 44100;
        float duration = 1.4f;
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        float[] noteFreqs = { 311.13f, 293.66f, 261.63f };
        float[] noteStarts = { 0.00f, 0.32f, 0.65f };
        float[] noteEnds = { 0.30f, 0.63f, 1.38f };

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float s = 0f;

            for (int n = 0; n < noteFreqs.Length; n++)
            {
                if (t >= noteStarts[n] && t <= noteEnds[n])
                {
                    float tNote = t - noteStarts[n];
                    float env = (tNote < 0.02f) ? (tNote / 0.02f) : Mathf.Exp(-(tNote - 0.02f) * (n == 2 ? 2.2f : 4.0f));

                    float f = noteFreqs[n];
                    float fundamental = Mathf.Sin(2f * Mathf.PI * f * tNote);
                    float sub = Mathf.Sin(2f * Mathf.PI * (f * 0.5f) * tNote) * 0.45f;
                    float minor3rd = Mathf.Sin(2f * Mathf.PI * (f * 1.1892f) * tNote) * 0.25f;

                    s += (fundamental + sub + minor3rd) * env * 0.50f;
                }
            }

            float white = UnityEngine.Random.Range(-1f, 1f) * 0.04f * Mathf.Exp(-t * 2.5f);
            s += white;

            samples[i] = s / (1f + Mathf.Abs(s));
        }

        AudioClip clip = AudioClip.Create("MissionAbort_Stinger", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public void PlayVictoryFanfare()
    {
        if (sfxAudioSource != null && victoryFanfareClip != null)
        {
            sfxAudioSource.PlayOneShot(victoryFanfareClip, 0.85f);
        }
    }

    public void PlayMissionAbortAudio()
    {
        if (sfxAudioSource != null && missionAbortClip != null)
        {
            sfxAudioSource.PlayOneShot(missionAbortClip, 0.85f);
        }
    }

    private AudioClip CreateStagingPopClip()
    {
        int sampleRate = 44100;
        float duration = 0.42f; // 420ms
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            // 1. Heavy low-frequency pneumatic impulse thud (85Hz decaying fast)
            float thudEnv = Mathf.Exp(-t * 28f);
            float thud = Mathf.Sin(2f * Mathf.PI * 85f * t) * thudEnv;

            // 2. High-pressure cold nitrogen gas expulsion hiss (white noise decaying over 0.35s)
            float hissEnv = (t < 0.015f) ? (t / 0.015f) : Mathf.Exp(-(t - 0.015f) * 9.5f);
            float noise = (UnityEngine.Random.value * 2f - 1f) * hissEnv;

            // 3. Metallic clamp ping resonance (1280Hz)
            float pingEnv = Mathf.Exp(-t * 40f);
            float ping = Mathf.Sin(2f * Mathf.PI * 1280f * t) * pingEnv * 0.25f;

            samples[i] = Mathf.Clamp(thud * 0.7f + noise * 0.4f + ping, -1f, 1f) * 0.85f;
        }

        AudioClip clip = AudioClip.Create("StagingPop_Pneumatic", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateSolarDeployClip()
    {
        int sampleRate = 44100;
        float duration = 0.65f; // 650ms servo unroll
        int totalSamples = (int)(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(380f, 620f, t / duration);
            float motorPulse = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 35f * t);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * motorPulse;
            float env = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            samples[i] = tone * env * 0.35f;
        }

        AudioClip clip = AudioClip.Create("SolarDeploy_Servo", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    public void PlayStagingAudio()
    {
        if (sfxAudioSource != null && stagingPopClip != null)
        {
            sfxAudioSource.PlayOneShot(stagingPopClip, 0.95f);
        }
    }

    public void PlaySolarDeployAudio()
    {
        if (sfxAudioSource != null && solarDeployClip != null)
        {
            sfxAudioSource.PlayOneShot(solarDeployClip, 0.75f);
        }
    }
}