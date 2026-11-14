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
    public enum MissionPhase { Assembly, Launching, FlightCrisis, OrbitSimulation, Completed }

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
    public MissionPhase currentPhase = MissionPhase.Assembly;
    public int selectedTargetIndex = 0;
    public string[] targetNames = { "Low Earth Orbit (LEO)", "Lunar Polar Orbit", "Mars Deep Space Orbit" };
    public float[] targetDeltaVReq = { 1600f, 2800f, 3800f };
    public float[] targetSolarMultiplier = { 1.0f, 0.85f, 0.45f };
    public float[] targetRewardMultiplier = { 1.0f, 1.8f, 3.0f };
    public string[] targetRiskRating = { "LOW RISK (Protected by Earth Magnetosphere)", "MEDIUM RISK (Deep Space Radiation & Debris)", "EXTREME RISK (45% Sunlight, High Debris & Delay)" };

    public int selectedRocketIndex = 1;
    public string[] rocketNames = { "Light Launcher ($90M | 650kg)", "Medium Falcon ($160M | 1200kg)", "Heavy Titan ($260M | 2200kg)" };
    public float[] rocketCostsM = { 90f, 160f, 260f };
    public float[] rocketMaxMassKg = { 650f, 1200f, 2200f };

    [Header("Budget & Mission Requirements")]
    public float maxBudgetMillion = 850f;
    public float remainingBudgetMillion = 212f;
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
    private int currentCrisisIndex = -1;
    private int nextObstacleToTrigger = 0;
    private FlightCrisis[] realisticCrises;
    private string NL { get { return Environment.NewLine; } }

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
private readonly float LaunchpadY = -1.35f;
    private readonly float[] ObstacleYPositions = { 2.5f, 7.0f, 11.5f };
    private readonly float OrbitCenterY = 16.5f;
    private readonly float OrbitRadius = 1.35f;

    void Start()
    {
        // Automatically finds Mission_Corridor_Map even when it is unchecked/hidden!
        missionCorridorMap = transform.Find("Mission_Corridor_Map");
        //BuildDetailedNasaRocket();
        ApplyRealNasaHardwareSpecs();
        //UpgradePartMeshesToDetailedVisuals();
        SetupAttachedSprites();

        lowerBoosterStage = spacecraftHub.Find("Part_Booster_Lower_Booster_Stage");

        if (missionCorridorMap != null) missionCorridorMap.gameObject.SetActive(false);

        // Arrange equipped parts into a clean vertical rocket stack
        if (parts != null && parts.Count >= 7)
        {
            parts[0].busLocalOffset = new Vector3(0f, 0.05f, 0.02f);   // Solar Wings (Wide Center)
            parts[0].mountedScale   = new Vector3(1.45f, 0.16f, 0.1f);
            parts[1].busLocalOffset = new Vector3(0.28f, -0.15f, -0.02f); // RTG Core (Right Side)
            parts[1].mountedScale   = new Vector3(0.18f, 0.22f, 0.1f);
            parts[2].busLocalOffset = new Vector3(0f, -0.12f, -0.04f); // Battery Bank (Core Center)
            parts[2].mountedScale   = new Vector3(0.28f, 0.18f, 0.1f);
            parts[3].busLocalOffset = new Vector3(0f, 0.34f, -0.04f);  // Dish Antenna (Nose Tip)
            parts[3].mountedScale   = new Vector3(0.38f, 0.12f, 0.1f);
            parts[4].busLocalOffset = new Vector3(0.14f, 0.16f, -0.05f); // Camera (Upper Right)
            parts[4].mountedScale   = new Vector3(0.14f, 0.14f, 0.1f);
            parts[5].busLocalOffset = new Vector3(0f, -0.34f, -0.02f); // Ion Thruster (Bottom Engine)
            parts[5].mountedScale   = new Vector3(0.22f, 0.22f, 0.1f);
            parts[6].busLocalOffset = new Vector3(-0.14f, 0.16f, -0.05f); // Scanner (Upper Left)
            parts[6].mountedScale   = new Vector3(0.14f, 0.14f, 0.1f);
        }

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
            traj.GetComponent<LineRenderer>().SetPosition(1, new Vector3(-0.3f, OrbitCenterY - 1.2f, 0.4f));
        
        InitCrises();
        ResetToAssembly();
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

        // Clean 2D front-orthographic hardpoints centered around the Satellite Bus (Y = 0.12)
        Vector3[] cleanBusPositions = {
            new Vector3(0f, 0.12f, -0.06f),      // 0: ROSA Solar Wings
            new Vector3(-0.22f, 0.12f, -0.09f),  // 1: MMRTG Nuclear Core (Left mount)
            new Vector3(0f, 0.10f, -0.11f),      // 2: EaglePicher Battery (Core bay)
            new Vector3(0f, 0.36f, -0.09f),      // 3: DSN Parabolic Dish (Top deck)
            new Vector3(0f, 0.18f, -0.12f),      // 4: HiRISE Optical Camera (Center optics)
            new Vector3(0f, -0.06f, -0.07f),     // 5: NEXT-C Ion Thruster (Bus base adapter)
            new Vector3(0.22f, 0.12f, -0.12f)    // 6: THEMIS IR Spectrometer (Right mount)
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

    private void BuildDetailedNasaRocket()
    {
        if (spacecraftHub == null) return;

        // 1. Hide the old blocky placeholder cubes
        string[] oldBoxes = { "Spacecraft_Core_Outline", "Spacecraft_Core_Body", "Spacecraft_Gold_Foil", "NASA_Rocket_Visuals" };
        foreach (string s in oldBoxes)
        {
            Transform old = spacecraftHub.Find(s);
            if (old != null) old.gameObject.SetActive(false);
        }

        GameObject viz = new GameObject("NASA_Rocket_Visuals");
        viz.transform.SetParent(spacecraftHub, false);

        // 2. SATELLITE BUS (Upper Payload sitting at the top of the rocket)
        MakeRocketPiece(viz.transform, "Sat_Gold_Bus",    new Vector3(0f, 0.05f, 0.01f), new Vector3(0.24f, 0.28f, 1f), 0f, new Color(0.85f, 0.68f, 0.15f));
        MakeRocketPiece(viz.transform, "Sat_Silver_Trim", new Vector3(0f, 0.05f, 0.02f), new Vector3(0.26f, 0.30f, 1f), 0f, new Color(0.75f, 0.80f, 0.88f));

        // 3. LOWER ROCKET BOOSTER (Visible during Assembly & Ascent, separates in Orbit!)
        GameObject booster = new GameObject("Lower_Booster_Stage");
        booster.transform.SetParent(viz.transform, false);
        lowerBoosterStage = booster.transform;

        MakeRocketPiece(booster.transform, "Interstage_Adapter", new Vector3(0f, -0.18f, 0.03f), new Vector3(0.32f, 0.14f, 1f), 0f, new Color(0.16f, 0.20f, 0.26f));
        MakeRocketPiece(booster.transform, "Booster_Main_Tank",  new Vector3(0f, -0.62f, 0.04f), new Vector3(0.32f, 0.76f, 1f), 0f, new Color(0.92f, 0.94f, 0.97f));
        MakeRocketPiece(booster.transform, "Booster_Shadow_Side",new Vector3(0.11f, -0.62f, 0.035f), new Vector3(0.10f, 0.76f, 1f), 0f, new Color(0.78f, 0.82f, 0.88f));
        MakeRocketPiece(booster.transform, "NASA_Stripe",        new Vector3(0f, -0.40f, 0.03f), new Vector3(0.325f, 0.05f, 1f), 0f, new Color(0.85f, 0.20f, 0.20f));
        MakeRocketPiece(booster.transform, "Grid_Fin_L",         new Vector3(-0.20f, -0.22f, 0.05f), new Vector3(0.08f, 0.06f, 1f), 0f, new Color(0.25f, 0.30f, 0.36f));
        MakeRocketPiece(booster.transform, "Grid_Fin_R",         new Vector3( 0.20f, -0.22f, 0.05f), new Vector3(0.08f, 0.06f, 1f), 0f, new Color(0.25f, 0.30f, 0.36f));
        MakeRocketPiece(booster.transform, "Landing_Leg_L",      new Vector3(-0.21f, -0.92f, 0.05f), new Vector3(0.10f, 0.24f, 1f), 18f, new Color(0.20f, 0.24f, 0.30f));
        MakeRocketPiece(booster.transform, "Landing_Leg_R",      new Vector3( 0.21f, -0.92f, 0.05f), new Vector3(0.10f, 0.24f, 1f), -18f, new Color(0.20f, 0.24f, 0.30f));
        MakeRocketPiece(booster.transform, "Engine_Bell_Center", new Vector3(0f, -1.04f, 0.05f), new Vector3(0.12f, 0.10f, 1f), 0f, new Color(0.30f, 0.35f, 0.42f));
        MakeRocketPiece(booster.transform, "Exhaust_Plume_Outer",new Vector3(0f, -1.28f, 0.06f), new Vector3(0.18f, 0.38f, 1f), 0f, new Color(1.0f, 0.45f, 0.05f));
        MakeRocketPiece(booster.transform, "Exhaust_Plume_Core", new Vector3(0f, -1.20f, 0.055f),new Vector3(0.10f, 0.22f, 1f), 0f, new Color(1.0f, 0.92f, 0.50f));

        // 4. CUTAWAY PAYLOAD FAIRING OUTLINE (Shows where the satellite sits on top of the rocket)
        GameObject fairing = new GameObject("Fairing_Cutaway");
        fairing.transform.SetParent(viz.transform, false);
        fairingShell = fairing.transform;
        MakeRocketPiece(fairing.transform, "Fairing_Wall_L", new Vector3(-0.24f, 0.08f, 0.05f), new Vector3(0.03f, 0.42f, 1f), 0f, new Color(0.45f, 0.65f, 0.85f));
        MakeRocketPiece(fairing.transform, "Fairing_Wall_R", new Vector3( 0.24f, 0.08f, 0.05f), new Vector3(0.03f, 0.42f, 1f), 0f, new Color(0.45f, 0.65f, 0.85f));
        MakeRocketPiece(fairing.transform, "Fairing_Nose_L", new Vector3(-0.12f, 0.38f, 0.05f), new Vector3(0.03f, 0.28f, 1f), -38f, new Color(0.45f, 0.65f, 0.85f));
        MakeRocketPiece(fairing.transform, "Fairing_Nose_R", new Vector3( 0.12f, 0.38f, 0.05f), new Vector3(0.03f, 0.28f, 1f),  38f, new Color(0.45f, 0.65f, 0.85f));
    }

    private void ApplyRealNasaHardwareSpecs()
    {
        // Real NASA Launch Services Program (LSP) Rockets
        rocketNames = new string[] {
            "Minotaur IV Lite ($90M | 650kg)",
            "Falcon 9 NASA-LSP ($160M | 1250kg)",
            "NASA SLS Block 1B ($260M | 2200kg)"
        };
        rocketMaxMassKg = new float[] { 650f, 1250f, 2200f };

        // Real NASA Spacecraft Subsystems & Instruments (from NSSDCA / NASA Glenn)
        if (parts != null && parts.Count >= 7)
        {
            parts[0].partTitle = "ROSA Roll-Out Solar";     // Flown on NASA DART & ISS
            parts[1].partTitle = "MMRTG Pu-238 Core";       // Flown on Perseverance & Curiosity
            parts[2].partTitle = "EaglePicher Li-Ion Bat";  // NASA Standard Aerospace Battery
            parts[3].partTitle = "DSN Ka-Band Dish";        // NASA Deep Space Network Antenna
            parts[4].partTitle = "HiRISE Optical Camera";   // Mars Reconnaissance Orbiter Imager
            parts[5].partTitle = "NEXT-C Ion Thruster";     // NASA Glenn Xenon Gridded Ion Engine
            parts[6].partTitle = "THEMIS IR Spectrometer";  // NASA Thermal Emission Spectrometer
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
            "Assembly_And_Catalog_Group", "Top_Budget_Fuel_Box", "Top_Budget_Fuel_Box_Border", "Top_Bar_Text",
            "Right_Requirements_Box", "Right_Requirements_Box_Border", "Telemetry_Text_Object",
            "Center_Config_Board", "Center_Config_Board_Border", "Config_Selection_Text",
            "BTN_LessFuel", "BTN_LessFuel_Border", "BTN_LessFuel_Label",
            "BTN_MoreFuel", "BTN_MoreFuel_Border", "BTN_MoreFuel_Label",
            "BTN_LaunchSim", "BTN_LaunchSim_Border", "BTN_LaunchSim_Label"
        };
        foreach (string objName in uiToHide)
        {
            Transform t = transform.Find(objName);
            if (t != null) t.gameObject.SetActive(visible);
        }
    }

    private void UpdateCameraFollow(float dt)
    {
        if (Camera.main == null || spacecraftHub == null) return;

        bool inFlight = (currentPhase != MissionPhase.Assembly);
        float targetSize = inFlight ? 3.1f : 5.6f;
        Vector3 targetCamPos = inFlight
            ? new Vector3(-0.3f, spacecraftHub.position.y + 0.25f, -15f)
            : new Vector3(0f, 0f, -15f);

        Camera.main.orthographicSize = Mathf.Lerp(Camera.main.orthographicSize, targetSize, dt * 5f);
        Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, targetCamPos, dt * 5f);

        float camY = Camera.main.transform.position.y;
        Transform stars = transform.Find("Starfield_Background");
        if (stars != null) stars.localPosition = new Vector3(0f, camY, 0f);

        if (inFlight)
        {
            bool showChoices = (currentPhase == MissionPhase.FlightCrisis);
            MoveGroupY("BTN_CycleTarget", camY - 1.95f, showChoices);
            MoveGroupY("BTN_CycleRocket", camY - 1.95f, showChoices);
            MoveGroupY("Status_Display_Board", camY + 2.15f, true, "Mission_Status_Text_Object", -0.3f);
            MoveGroupY("BTN_Reset", camY - 2.35f, currentPhase == MissionPhase.Completed, null, 2.2f);
        }
        else
        {
            MoveGroupY("BTN_CycleTarget", -3.10f, true);
            MoveGroupY("BTN_CycleRocket", -3.10f, true);
            MoveGroupY("Status_Display_Board", -2.45f, true, "Mission_Status_Text_Object", 6.1f);
            MoveGroupY("BTN_Reset", -3.65f, true, null, 7.85f);
        }
    }

    private void MoveGroupY(string baseName, float y, bool active, string customLabel = null, float customX = -999f)
    {
        Transform main = transform.Find(baseName);
        Transform border = transform.Find(baseName + "_Border");
        Transform label = transform.Find(customLabel ?? (baseName + "_Label"));
        if (main != null)
        {
            main.gameObject.SetActive(active);
            float x = (customX > -900f) ? customX : main.localPosition.x;
            main.localPosition = new Vector3(x, y, main.localPosition.z);
            if (border != null) { border.gameObject.SetActive(active); border.localPosition = new Vector3(x, y - 0.06f, border.localPosition.z); }
            if (label != null)  { label.gameObject.SetActive(active);  label.localPosition  = new Vector3(x, y + 0.02f, label.localPosition.z); }
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

        if (currentPhase == MissionPhase.Launching)
        {
            UpdateAscentThroughObstacles(Time.deltaTime);
        }
        else if (currentPhase == MissionPhase.OrbitSimulation)
        {
            UpdateOrbitalSimulation(Time.deltaTime);
        }

        UpdateCameraFollow(Time.deltaTime);
    }
    void LateUpdate()
    {
        StyleAndAnimateObstacles();
        StyleEnvironmentAndLaunchpad();
        UpdateRocketExhaustParticles();
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

        if (currentPhase == MissionPhase.Assembly && x >= -9.1f && x <= -3.6f)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (Mathf.Abs(y - parts[i].catalogPosition.y) <= 0.44f)
                {
                    TogglePart(i);
                    return;
                }
            }
        }

        if (IsInsideBox(x, y, -1.65f, -3.10f, 2.4f, 0.58f)) { OnLeftBlueButtonClicked(); return; }
        if (IsInsideBox(x, y,  1.05f, -3.10f, 2.4f, 0.58f)) { OnRightBlueButtonClicked(); return; }
        if (IsInsideBox(x, y, -1.65f, -3.90f, 2.4f, 0.54f)) { AdjustFuelAndKits(-50f, -1); return; }
        if (IsInsideBox(x, y,  1.05f, -3.90f, 2.4f, 0.54f)) { AdjustFuelAndKits(50f, 1); return; }
        if (IsInsideBox(x, y,  5.05f, -3.65f, 3.3f, 0.78f)) { StartLaunchSequence(); return; }
        if (IsInsideBox(x, y,  7.85f, -3.65f, 1.8f, 0.78f)) { ResetToAssembly(); return; }
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

        if (currentPhase == MissionPhase.Assembly)
        {
            if (objName.StartsWith("PART_SLOT_") || objName.StartsWith("PART_MESH_"))
            {
                if (int.TryParse(objName.Substring(10), out int idx))
                {
                    TogglePart(idx);
                    return true;
                }
            }
        }
        return false;
    }

    // =========================================================================
    // 2. SPACECRAFT SUBSYSTEM MOUNTING & VISUALS
    // =========================================================================
    public void TogglePart(int index)
    {
        if (index < 0 || index >= parts.Count) return;
        parts[index].isInstalled = !parts[index].isInstalled;
        UpdatePartVisuals(parts[index]);
        RecalculateTelemetry();

        if (remainingBudgetMillion < 0f)
            SetStatusBanner("OVER BUDGET RISK!", "You exceeded $" + maxBudgetMillion.ToString("F0") + "M! Remove a subsystem or choose a smaller launcher.", DuoRed);
        else
            SetStatusBanner(parts[index].isInstalled ? "SUBSYSTEM INSTALLED: " + parts[index].partTitle : "SUBSYSTEM REMOVED: " + parts[index].partTitle,
                "Check the Right Panel to see how this trade-off impacts Mass, Power, Risk, and Science Reward!", DuoGreen);
    }

    public void UpdatePartVisuals(PartSpec p)
    {
        if (p == null) return;

        if (p.partTransform != null && spacecraftHub != null && partsCatalogContainer != null)
        {
            if (p.isInstalled)
            {
                p.partTransform.SetParent(spacecraftHub, false);
                p.partTransform.localPosition = p.busLocalOffset;
                p.partTransform.localRotation = Quaternion.identity;
                p.partTransform.localScale = p.mountedScale;
            }
            else
            {
                p.partTransform.SetParent(partsCatalogContainer, false);
                p.partTransform.localPosition = p.catalogPosition;
                p.partTransform.localRotation = Quaternion.identity;
                p.partTransform.localScale = p.catalogScale;
            }
        }

        Color partCol   = p.isInstalled ? p.installedColor : new Color(0.38f, 0.46f, 0.56f);
        Color cardCol   = p.isInstalled ? CardEquipped : CardDarkBg;
        Color shadowCol = p.isInstalled ? DuoGreenDark : CardDarkEdge;
        Color textCol   = p.isInstalled ? DuoGreen : Color.white;

        SetRendererColor(p.partRenderer, partCol);
        SetRendererColor(p.slotCardRenderer, cardCol);
        SetRendererColor(p.slotShadowRenderer, shadowCol);

        if (p.labelMesh != null)
        {
            p.labelMesh.color = textCol;
            string stateTag = p.isInstalled ? "[EQUIPPED]" : "[ADD]";
            float netP = p.powerOutputW - p.powerDrawW;
            p.labelMesh.text = p.partTitle + " " + stateTag + NL + "$" + p.costMillion + "M | " + p.massKg + "kg | " + netP.ToString("+0;-0;0") + "W | +" + p.sciencePoints + " Sci";
        }
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
        if (currentPhase == MissionPhase.FlightCrisis) { ResolveFlightCrisis(false); return; }
        if (currentPhase != MissionPhase.Assembly) ResetToAssembly();
        selectedRocketIndex = (selectedRocketIndex + 1) % rocketNames.Length;
        RecalculateTelemetry();
        SetStatusBanner("LAUNCH VEHICLE CHANGED", "Switched to " + rocketNames[selectedRocketIndex], DuoBlue);
    }

    public void AdjustFuelAndKits(float deltaFuelKg, int deltaKits)
    {
        if (currentPhase != MissionPhase.Assembly) return;
        fuelMassKg = Mathf.Clamp(fuelMassKg + deltaFuelKg, 50f, 1200f);
        repairKits = Mathf.Clamp(repairKits + deltaKits, 0, 6);
        RecalculateTelemetry();
        SetStatusBanner("RESOURCES ADJUSTED", "Fuel Reserve: " + fuelMassKg.ToString("F0") + "kg  |  Emergency Repair Kits: " + repairKits, DuoBlue);
    }

    private void UpdateTargetPlanetVisuals()
    {
        if (targetPlanetVisual != null)
        {
            Color planetCol = selectedTargetIndex == 0 ? DuoBlue :
                              (selectedTargetIndex == 1 ? new Color(0.75f, 0.78f, 0.84f) : new Color(0.92f, 0.38f, 0.20f));
            SetRendererColor(targetPlanetVisual.GetComponent<Renderer>(), planetCol);
        }
        if (mapTargetLabel != null)
        {
            mapTargetLabel.text = "TARGET: " + targetNames[selectedTargetIndex].ToUpper() +
                                  " (REWARD: " + targetRewardMultiplier[selectedTargetIndex].ToString("F1") + "x SCIENCE)";
        }
    }

    public void RecalculateTelemetry()
    {
        dryMassKg = 120f + (repairKits * 15f);
        float partsCostM = 0f;
        powerGenW = 0f;
        powerDrawW = 25f;
        batteryCapacityWh = 60f;
        float baseSciencePts = 0f;
        bool hasPropulsion = false;
        bool hasBattery = false;

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

                    if (p.category == "Battery") { batteryCapacityWh += 350f; hasBattery = true; }
                    if (p.category == "Propulsion") hasPropulsion = true;
                }
            }
        }

        // Reward scales with Destination Multiplier (LEO = 1x, Moon = 1.8x, Mars = 3.0x)!
        potentialSciencePts = Mathf.Round(baseSciencePts * targetRewardMultiplier[selectedTargetIndex]);

        totalMassKg = dryMassKg + fuelMassKg;
        totalCostMillion = partsCostM + rocketCostsM[selectedRocketIndex] + (fuelMassKg * 0.05f) + (repairKits * 15f);
        remainingBudgetMillion = maxBudgetMillion - totalCostMillion;
        netPowerW = powerGenW - powerDrawW;
        calculatedDeltaV = hasPropulsion ? (680f * 9.81f * Mathf.Log(totalMassKg / dryMassKg)) : 0f;

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

        if (topBarDisplay != null)
        {
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
        bool hasPropulsion = false;
        if (parts != null)
        {
            foreach (var p in parts)
            {
                if (p != null && p.isInstalled)
                {
                    if (p.category == "Comms") hasComms = true;
                    if (p.category == "Propulsion") hasPropulsion = true;
                }
            }
        }

        string budgetPass = totalCostMillion <= maxBudgetMillion ? "[PASS]" : "[OVER!]";
        string massPass   = totalMassKg <= maxRocketMass ? "[PASS]" : "[HEAVY!]";
        string dvPass     = calculatedDeltaV >= reqDv ? "[PASS]" : "[LOW dV!]";
        string pwrPass    = netPowerW >= 0f ? "[PASS]" : "[DEFICIT!]";
        string commPass   = hasComms ? "[PASS]" : "[MISSING!]";
        string propPass   = hasPropulsion ? "[PASS]" : "[MISSING!]";
        string sciPass    = potentialSciencePts >= targetSciencePts ? "[PASS]" : "[LOW SCI]";

        float shownBat = (currentPhase == MissionPhase.OrbitSimulation) ? currentBatteryWh : batteryCapacityWh;
        float shownSci = (currentPhase == MissionPhase.OrbitSimulation || currentPhase == MissionPhase.Completed) ? transmittedSciencePts : potentialSciencePts;

        if (telemetryDisplay != null)
        {
            telemetryDisplay.text = 
                "<color=#3498DB><b>MISSION REQUIREMENTS</b></color>\n" +
                $"• Target: {targetNames[selectedTargetIndex]}\n" +
                $"• Cost: ${totalCostMillion:F0}M / ${maxBudgetMillion}M  <color=#2ECC71><b>{budgetPass}</b></color>\n" +
                $"• Mass: {totalMassKg:F0} / {maxRocketMass:F0}kg  <color=#2ECC71><b>{massPass}</b></color>\n" +
                $"• Thrust: {calculatedDeltaV:F0} / {reqDv:F0} m/s  <color=#2ECC71><b>{dvPass}</b></color>\n" +
                $"• Power: {netPowerW:+0;-0;0}W (Bat: {shownBat:F0}Wh)  <color=#2ECC71><b>{pwrPass}</b></color>\n" +
                $"• Engine: {(hasPropulsion ? "Ready" : "None")}  <color=#2ECC71><b>{propPass}</b></color>\n\n" +
                "<color=#E67E22><b>RISK & REWARD OUTCOME</b></color>\n" +
                $"• Hazard Risk: <color=#E74C3C>{missionFailureRiskPct:F0}%</color> ({repairKits} Kits)\n" +
                $"• Solar Yield: <color=#2ECC71>{Mathf.Clamp((targetSolarMultiplier[selectedTargetIndex] * 100f), 0, 100):F0}%</color> Sunlight\n" +
                $"• Est. Science: {shownSci:F0} pts <color=#F1C40F>({targetRewardMultiplier[selectedTargetIndex]:F1}x)</color>";
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
            SetStatusBanner("LAUNCH SCRUBBED: INSUFFICIENT DELTA-V!", "Need " + reqDv.ToString("F0") + " m/s dV to reach " + targetNames[selectedTargetIndex] + "! Add Fuel or Ion Thruster.", DuoOrange);
            return;
        }
        if (netPowerW < 0f)
        {
            SetStatusBanner("LAUNCH SCRUBBED: POWER DEFICIT!", "Instruments draw more power than generated! Add Solar Wings or RTG Nuclear Core.", DuoOrange);
            return;
        }

        SetAssemblyUIVisible(false);
        if (missionCorridorMap != null) missionCorridorMap.gameObject.SetActive(true);
        if (spacecraftHub != null)
        {
            spacecraftHub.localPosition = new Vector3(-0.3f, LaunchpadY, 0f);
            spacecraftHub.localScale = new Vector3(0.85f, 0.85f, 1f);
        }

        currentPhase = MissionPhase.Launching;
        simTimer = 0f;
        nextObstacleToTrigger = 0;
        hullIntegrity = 100f;
        currentBatteryWh = batteryCapacityWh;

        // Reset all 3 Golden Science Reward Nodes in orbit
        if (scienceRewardNodes != null)
        {
            foreach (var n in scienceRewardNodes)
                if (n != null) n.gameObject.SetActive(true);
        }

        SetStatusBanner("LIFTOFF FROM EARTH!", "Ascending through the trajectory corridor... Approaching Obstacle Zone 1 (Max-Q)!", DuoGreen);
    }

    private void UpdateAscentThroughObstacles(float dt)
    {
        if (spacecraftHub == null) return;

        spacecraftHub.localPosition += Vector3.up * (2.8f * dt);
        float currentY = spacecraftHub.localPosition.y;

        // Check if spacecraft reached the next visible obstacle band on the map!
        if (nextObstacleToTrigger < ObstacleYPositions.Length && currentY >= ObstacleYPositions[nextObstacleToTrigger])
        {
            TriggerObstacleCrisis(nextObstacleToTrigger);
            nextObstacleToTrigger++;
            return;
        }

        // Reached the Target Planet Orbit Ring at the top of the map!
        if (currentY >= OrbitCenterY - 0.1f)
        {
            BeginOrbitalPhase();
        }
    }

    private void TriggerObstacleCrisis(int obstacleIdx)
    {
        currentPhase = MissionPhase.FlightCrisis;
        currentCrisisIndex = obstacleIdx;
        FlightCrisis c = realisticCrises[obstacleIdx];

        if (crisisHighlightRing != null && spacecraftHub != null)
        {
            crisisHighlightRing.gameObject.SetActive(false);
            crisisHighlightRing.transform.position = spacecraftHub.position + new Vector3(0f, 0f, 0.05f);
        }

        if (btnTargetLabel != null) btnTargetLabel.text = c.optionAText;
        if (btnRocketLabel != null) btnRocketLabel.text = c.optionBText;

        if (configDisplay != null)
        {
            configDisplay.color = DuoOrange;
            configDisplay.text = "OBSTACLE ENCOUNTERED!" + NL + "Click a BLUE BUTTON below to survive:";
        }

        SetStatusBanner(c.title, c.realWorldWhy, DuoOrange);
    }

    private void ResolveFlightCrisis(bool choseOptionA)
    {
        if (crisisHighlightRing != null)
            crisisHighlightRing.gameObject.SetActive(false);

        if (choseOptionA)
        {
            if (currentCrisisIndex == 0 && fuelMassKg >= 75f)
            {
                fuelMassKg -= 75f;
                SetStatusBanner("MAX-Q SURVIVED!", "Burned 75kg reserve fuel to throttle down safely through supersonic shear!", DuoGreen);
            }
            else if (currentCrisisIndex == 1 && currentBatteryWh >= 150f)
            {
                currentBatteryWh -= 150f;
                SetStatusBanner("RADIATION BELT SURVIVED!", "Discharged 150Wh battery power into electromagnetic shielding!", DuoGreen);
            }
            else if (currentCrisisIndex == 2 && fuelMassKg >= 60f)
            {
                fuelMassKg -= 60f;
                SetStatusBanner("DEBRIS FIELD DODGED!", "Burned 60kg thruster fuel for an evasive orbital debris maneuver!", DuoGreen);
            }
            else
            {
                hullIntegrity -= 45f;
                SetStatusBanner("INSUFFICIENT RESERVES! (-45% HULL)", "You didn't have enough Fuel/Battery for Option A! Spacecraft took heavy damage!", DuoRed);
            }
        }
        else
        {
            if (repairKits > 0)
            {
                repairKits--;
                SetStatusBanner("OBSTACLE OVERCOME! (1 KIT USED)", "Used 1 Repair Kit to reinforce/patch the spacecraft systems in flight!", DuoGreen);
            }
            else
            {
                hullIntegrity -= 45f;
                SetStatusBanner("0 REPAIR KITS LEFT! (-45% HULL)", "You ran out of Repair Kits! The obstacle severely damaged the spacecraft!", DuoRed);
            }
        }

        if (btnTargetLabel != null) btnTargetLabel.text = "CYCLE TARGET";
        if (btnRocketLabel != null) btnRocketLabel.text = "CYCLE ROCKET";
        RecalculateTelemetry();

        if (hullIntegrity <= 15f)
        {
            currentPhase = MissionPhase.Completed;
            SetStatusBanner("MISSION LOST IN FLIGHT!", "Cumulative obstacle damage destroyed the spacecraft! Bring more Repair Kits or Fuel next time.", DuoRed);
            return;
        }

        currentPhase = MissionPhase.Launching;
    }

    private void BeginOrbitalPhase()
    {
        currentPhase = MissionPhase.OrbitSimulation;
        simTimer = 0f;
        orbitAngle = -Mathf.PI * 0.5f;
        transmittedSciencePts = 0f;

        if (lowerBoosterStage != null) lowerBoosterStage.gameObject.SetActive(false);
        if (fairingShell != null) fairingShell.gameObject.SetActive(false);

        SetStatusBanner("ORBIT INSERTION COMPLETE!",
            "Orbiting " + targetNames[selectedTargetIndex] + "! Collecting Golden Science Nodes & testing Eclipse battery survival...", DuoGreen);
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
            SetStatusBanner("MISSION LOST IN ECLIPSE SHADOW!",
                "REAL RISK: Solar panels generate 0W behind a planet! Without enough Batteries or an RTG, the spacecraft froze.", DuoRed);
            RefreshMissionRequirementsBoard();
            return;
        }

        if (hasComms)
        {
            transmittedSciencePts = Mathf.MoveTowards(transmittedSciencePts, potentialSciencePts, (potentialSciencePts / 5.5f) * dt);
        }

        RefreshMissionRequirementsBoard();

        if (simTimer >= 6.5f)
        {
            currentPhase = MissionPhase.Completed;
            if (!hasComms)
            {
                SetStatusBanner("ORBIT SURVIVED, 0 REWARD TRANSMITTED!",
                    "REAL RISK: You survived every obstacle, but without a High-Gain Dish Antenna, 0 bytes of science reached Earth!", DuoOrange);
            }
            else if (transmittedSciencePts < targetSciencePts)
            {
                SetStatusBanner("PARTIAL MISSION SUCCESS!",
                    "Transmitted " + transmittedSciencePts.ToString("F0") + "/" + targetSciencePts.ToString("F0") + " Science pts. Equip more cameras/scanners or try a higher-reward orbit!", DuoOrange);
            }
            else
            {
                float fundingBonus = Mathf.Round(transmittedSciencePts * 2.5f);
                SetStatusBanner("MISSION ACCOMPLISHED! (+" + transmittedSciencePts.ToString("F0") + " SCI | +$" + fundingBonus.ToString("F0") + "M GRANT)",
                    "You navigated all 3 space hazards, survived the eclipse, and unlocked a $" + fundingBonus.ToString("F0") + "M NASA Discovery Grant!", DuoGreen);
            }
        }
    }

    public void ResetToAssembly()
    {
        currentPhase = MissionPhase.Assembly;
        inEclipse = false;
        hullIntegrity = 100f;
        if (crisisHighlightRing != null) crisisHighlightRing.gameObject.SetActive(false);

        if (scienceRewardNodes != null)
        {
            foreach (var n in scienceRewardNodes)
                if (n != null) n.gameObject.SetActive(true);
        }
        SetAssemblyUIVisible(true);
        if (missionCorridorMap != null) missionCorridorMap.gameObject.SetActive(false);
        if (spacecraftHub != null)
        {
            spacecraftHub.gameObject.SetActive(true);
            spacecraftHub.localPosition = new Vector3(-0.3f, 1.55f, 0f);
            spacecraftHub.localRotation = Quaternion.identity;
            spacecraftHub.localScale = new Vector3(2.45f, 2.45f, 1f);
        }

        if (lowerBoosterStage != null) lowerBoosterStage.gameObject.SetActive(true);
        if (fairingShell != null) fairingShell.gameObject.SetActive(true);

        RecalculateTelemetry();
        SetStatusBanner("ASSEMBLY & RISK PLANNING", "Equip subsystems on the left, review the 3 Obstacle Zones in the center map, and click LAUNCH!", DuoGreen);
    }

    private void SetStatusBanner(string header, string body, Color bannerColor)
    {
        Color mainFill = new Color(0.06f, 0.10f, 0.16f);
        Color borderFill = bannerColor;
        borderFill.a = 1f;

        SetRendererColor(statusCardRenderer, mainFill);

        if (statusCardRenderer != null)
        {
            Vector3 pos = statusCardRenderer.transform.localPosition;
            statusCardRenderer.transform.localPosition = new Vector3(pos.x, pos.y, -0.5f);
        }

        GameObject borderObj = GameObject.Find("Status_Display_Board_Border");
        if (borderObj != null)
        {
            SetRendererColor(borderObj.GetComponent<Renderer>(), borderFill);
            Vector3 bPos = borderObj.transform.localPosition;
            borderObj.transform.localPosition = new Vector3(bPos.x, bPos.y, -0.45f);
        }

        if (statusDisplay != null)
        {
            statusDisplay.color = Color.white;
            Vector3 tPos = statusDisplay.transform.localPosition;
            statusDisplay.transform.localPosition = new Vector3(tPos.x, tPos.y, -0.6f);

            string hex = ColorUtility.ToHtmlStringRGB(bannerColor);
            statusDisplay.text = $"<color=#{hex}><b>{header}</b></color>" + NL + WrapText(body, 44);
        }
    }
    private void StyleAndAnimateObstacles()
    {
        float t = Time.time;

        // 1. MAX-Q TURBULENCE
        GameObject maxQ = GameObject.Find("Obstacle1_MaxQ_Turbulence");
        if (maxQ != null)
        {
            Transform band = maxQ.transform.Find("MaxQ_Band");
            if (band != null)
            {
                SetRendererColor(band.GetComponent<Renderer>(), new Color(0.55f, 0.22f, 0.02f));
                band.localScale = new Vector3(6.5f, 1.15f, 1f);
            }

            for (int i = 1; i <= 2; i++)
            {
                Transform streak = maxQ.transform.Find("MaxQ_Streak" + i);
                if (streak != null)
                {
                    SetRendererColor(streak.GetComponent<Renderer>(), new Color(1f, 0.85f, 0.3f));
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
                SetRendererColor(band.GetComponent<Renderer>(), new Color(0.04f, 0.20f, 0.32f));
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

        // 3. SPACE DEBRIS
        GameObject debris = GameObject.Find("Obstacle3_Space_Debris");
        if (debris != null)
        {
            Transform band = debris.transform.Find("Debris_Band");
            if (band != null)
            {
                SetRendererColor(band.GetComponent<Renderer>(), new Color(0.32f, 0.08f, 0.12f));
                band.localScale = new Vector3(6.5f, 1.15f, 1f);
            }

            for (int i = 0; i <= 8; i++)
            {
                Transform rock = debris.transform.Find("DebrisRock_" + i);
                if (rock != null)
                {
                    SetRendererColor(rock.GetComponent<Renderer>(), (i % 2 == 0) ? new Color(0.85f, 0.88f, 0.92f) : new Color(1f, 0.45f, 0.2f));
                    float xSpread = (i - 4f) * 0.62f;
                    float ySpread = Mathf.Sin(i * 2.1f) * 0.32f;
                    rock.localPosition = new Vector3(xSpread, ySpread, -0.05f);
                    float rSize = (i % 2 == 0) ? 0.24f : 0.18f;
                    rock.localScale = new Vector3(rSize, rSize, 1f);
                    rock.localRotation = Quaternion.Euler(0f, 0f, (t * (45f + i * 15f) * (i % 2 == 0 ? 1f : -1f)) % 360f);
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
                        SetRendererColor(r, new Color(0.08f, 0.52f, 0.92f)); // Deep ocean blue planet
                    }
                }
            }
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
        bool isAssemblyPhase = (currentPhase == MissionPhase.Assembly);
        Transform earthHorizon = GameObject.Find("Earth_Launch_Horizon")?.transform;

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
                GameObject clamp = GameObject.CreatePrimitive(PrimitiveType.Quad);
                clamp.name = (side == -1) ? "HoldClamp_L" : "HoldClamp_R"; // <-- ADD THIS LINE
                Destroy(clamp.GetComponent<Collider>());
                clamp.transform.SetParent(assemblyGantryGroup.transform, false);
                clamp.transform.localPosition = new Vector3(side * 0.55f, nozzleBottomY + 0.05f, -0.13f);
                clamp.transform.localScale = new Vector3(0.26f, 0.22f, 1f);
                clamp.GetComponent<Renderer>().material = new Material(unlit);
                SetRendererColor(clamp.GetComponent<Renderer>(), new Color(0.90f, 0.55f, 0.10f));
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
                GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Quad);
                arm.name = "Swing_Arm"; // <-- ADD THIS LINE
                Destroy(arm.GetComponent<Collider>());
                arm.transform.SetParent(assemblyGantryGroup.transform, false);
                arm.transform.localPosition = new Vector3(-0.78f, ay, -0.06f);
                arm.transform.localScale = new Vector3(1.15f, 0.075f, 1f);
                arm.GetComponent<Renderer>().material = new Material(unlit);
                SetRendererColor(arm.GetComponent<Renderer>(), new Color(0.92f, 0.48f, 0.10f));

                // Clamp head where the arm meets the rocket hull
                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Quad);
                head.name = "Arm_Head"; // <-- ADD THIS LINE
                Destroy(head.GetComponent<Collider>());
                head.transform.SetParent(arm.transform, false); // <-- Parented to 'arm' so it swings with the arm!
                head.transform.localPosition = new Vector3(0.47f, 0f, -0.01f);
                head.transform.localScale = new Vector3(0.12f, 2.1f, 1f);
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

            if (!isAssemblyPhase)
            {
                assemblyGantryGroup.SetActive(true);

                if (earthHorizon != null && earthHorizon.GetComponent<Renderer>() != null)
                    earthHorizon.GetComponent<Renderer>().enabled = false;
                if (flightPad != null && flightPad.GetComponent<Renderer>() != null)
                    flightPad.GetComponent<Renderer>().enabled = false;

                float scaleX = 0.38f;
                float scaleY = 0.28f;
                assemblyGantryGroup.transform.localScale = new Vector3(scaleX, scaleY, 1f);

                // Anchor to fixed world space so the rocket leaves the ground behind
                float fixedWorldY = -4.45f;
                if (flightPad != null)
                    fixedWorldY = flightPad.position.y;
                else if (earthHorizon != null)
                    fixedWorldY = earthHorizon.position.y + 0.35f;

                float groupY = fixedWorldY - (-2.08f * scaleY);
                assemblyGantryGroup.transform.position = new Vector3(0f, groupY, -0.05f);

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
            else if (isAssemblyPhase)
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
                    // In Flight, swing the arms 72 degrees upward and pull them back against the truss tower!
                    Quaternion targetRot = isAssemblyPhase ? Quaternion.identity : Quaternion.Euler(0f, 0f, 72f);
                    float targetX = isAssemblyPhase ? -0.78f : -1.18f;

                    child.localRotation = Quaternion.Lerp(child.localRotation, targetRot, Time.deltaTime * 6f);
                    child.localPosition = Vector3.Lerp(child.localPosition, new Vector3(targetX, child.localPosition.y, child.localPosition.z), Time.deltaTime * 6f);
                }
                else if (child.name == "HoldClamp_L" || child.name == "HoldClamp_R")
                {
                    float side = (child.name == "HoldClamp_L") ? -1f : 1f;
                    float targetClampX = isAssemblyPhase ? (side * 0.55f) : (side * 0.95f);
                    Quaternion targetClampRot = isAssemblyPhase ? Quaternion.identity : Quaternion.Euler(0f, 0f, -side * 35f);

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
                scannerLine.gameObject.SetActive(isAssemblyPhase);
                if (isAssemblyPhase)
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
                    SetRendererColor(sr, baseStar * twinkle);
                    idx++;
                }
            }
        }

        // 3. HOLOGRAPHIC OBSTACLE RAILS IN FLIGHT
        AddBandBorders("Obstacle1_MaxQ_Turbulence", "MaxQ_Band", new Color(1f, 0.6f, 0.1f));
        AddBandBorders("Obstacle2_Radiation_Belt", "Rad_Band", new Color(0f, 0.9f, 1f));
        AddBandBorders("Obstacle3_Space_Debris", "Debris_Band", new Color(1f, 0.25f, 0.3f));
    }

    private void AddBandBorders(string parentName, string bandName, Color railColor)
    {
        GameObject parent = GameObject.Find(parentName);
        if (parent == null) return;
        Transform band = parent.transform.Find(bandName);
        if (band == null) return;

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
    }

    private ParticleSystem rocketExhaustPS;

    private void UpdateRocketExhaustParticles()
    {
        if (spacecraftHub == null) return;

        if (rocketExhaustPS == null)
        {
            GameObject psObj = new GameObject("Rocket_Exhaust_Particles");
            psObj.transform.SetParent(spacecraftHub, false);
            // Position right at engine nozzle and behind the rocket body (Z = 0.05f)
            psObj.transform.localPosition = new Vector3(0f, -0.38f, 0.05f);
            psObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            rocketExhaustPS = psObj.AddComponent<ParticleSystem>();
            var main = rocketExhaustPS.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var emission = rocketExhaustPS.emission;
            emission.rateOverTime = 90f;

            var shape = rocketExhaustPS.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.02f;

            var colorOverLifetime = rocketExhaustPS.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.98f, 0.8f), 0.0f),
                    new GradientColorKey(new Color(1f, 0.5f, 0.05f), 0.4f),
                    new GradientColorKey(new Color(0.8f, 0.15f, 0.05f), 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = rocketExhaustPS.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0.0f, 1.0f);
            curve.AddKey(0.3f, 1.2f);
            curve.AddKey(1.0f, 0.1f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var psRenderer = psObj.GetComponent<ParticleSystemRenderer>();
            Shader particleShader = Shader.Find("Sprites/Default");
            if (particleShader == null) particleShader = Shader.Find("Unlit/Color");
            psRenderer.material = new Material(particleShader);
            psRenderer.sortingOrder = -1;
        }

        // Only fire exhaust during active ascent or orbit insertion, turn off when frozen/completed
        // Only fire main rocket exhaust while the lower booster stage is attached and ascending!
        bool boosterAttached = (lowerBoosterStage == null || lowerBoosterStage.gameObject.activeInHierarchy);
        bool shouldThrust = boosterAttached && 
                            currentPhase != MissionPhase.Assembly && 
                            currentPhase != MissionPhase.OrbitSimulation && 
                            currentPhase != MissionPhase.Completed;
        var em = rocketExhaustPS.emission;
        em.enabled = shouldThrust;
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
}