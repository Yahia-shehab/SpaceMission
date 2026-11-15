using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InterplanetaryCruiseManager : MonoBehaviour
{
    public static InterplanetaryCruiseManager Instance { get; private set; }

    // Visual Palette (Cyber-Cyan Aerospace theme)
    private static readonly Color CyberCyan      = new Color(0.00f, 0.898f, 1.00f);
    private static readonly Color CyberCyanDark  = new Color(0.00f, 0.350f, 0.55f);
    private static readonly Color TelemetryGold  = new Color(1.00f, 0.820f, 0.20f);
    private static readonly Color NominalGreen   = new Color(0.18f, 0.850f, 0.45f);
    private static readonly Color HazardRed      = new Color(1.00f, 0.280f, 0.28f);
    private static readonly Color WarningAmber   = new Color(1.00f, 0.620f, 0.15f);
    private static readonly Color DarkNavyBg     = new Color(0.02f, 0.05f, 0.10f, 0.98f);

    public enum CruiseAnimationType
    {
        None,
        FlameSurge,       // Giant engine plume inferno, camera rumble, forward velocity jolt
        RcsPuffs,         // 4 prominent white jet streams shooting from attitude thrusters
        ThermalRoll,      // 2.5D Barbecue roll with dynamic glints on solar panels
        AvionicsReboot,   // Fullscreen cyan digital scanline grid & reboot chime
        LifeSupportHiss,  // Expanding neon-green holographic seal ring & gas hiss
        PowerStandby      // Blue forcefield protective dome & amber emergency lighting
    }

    [Serializable]
    public class CruiseEventChoice
    {
        public string choiceTitle;
        public string prosText;
        public string consText;
        public float fuelDelta;
        public float hullDelta;
        public float powerDelta;
        public float scienceDelta;
        public CruiseAnimationType animType;
        public string resolutionMessage;
    }

    [Serializable]
    public class CruiseEventData
    {
        public string eventId;
        public string missionSource;
        public string eventTitle;
        public string description;
        public CruiseEventChoice[] choices;
        public bool hasTriggered;
    }

    // State Variables
    public bool isCruiseActive = false;
    public bool IsCruiseActive => isCruiseActive || isLoadingScreenActive;
    private bool isLoadingScreenActive = false;
    public bool IsLoadingActive => isLoadingScreenActive;
    private bool isEventModalOpen = false;
    private bool isDebriefOpen = false;

    private float totalFlightDuration = 120.0f; // 2 minutes snappy transit
    private float currentFlightTime = 0.0f;
    private float timeMultiplier = 1.0f; // 1x, 2x, 4x, 8x warp

    // Spacecraft live stats during cruise
    private float currentFuelKg = 350f;
    private float maxFuelKg = 350f;
    private float currentHullIntegrity = 100f;
    private float currentPowerPct = 100f;
    private float currentSciencePts = 0f;
    private int selectedTargetIndex = 1; // 0=LEO, 1=Moon, 2=Mars
    private int eventsSurvivedCount = 0;

    // Events pool & scheduling (Fast, engaging pacing)
    private List<CruiseEventData> allEventsPool = new List<CruiseEventData>();
    private List<float> scheduledEventTimes = new List<float>();
    private CruiseEventData currentActiveEvent = null;

    // Hierarchy Roots
    private GameObject cruiseMasterRoot;
    private GameObject loadingScreenRoot;
    private GameObject cruiseWorldRoot;
    private GameObject cruiseUIRoot;
    private GameObject eventModalRoot;
    private GameObject debriefModalRoot;

    // Player's REAL Spacecraft Reference
    private Transform realRocketTransform;
    private Transform realRocketOriginalParent;
    private Vector3 realRocketOriginalPos;
    private Quaternion realRocketOriginalRot;
    private Vector3 realRocketOriginalScale;

    // Dynamic Rocket Animation Components
    private GameObject flameSurgeGroup;
    private Transform mainEngineFlame;
    private Transform flameCore;
    private Transform flameOuter;
    private Transform flameSparks;
    private Transform rcsN;
    private Transform rcsS;
    private Transform rcsE;
    private Transform rcsW;
    private Transform bluePowerShield;
    private Transform greenSealRing;
    private GameObject scanlineOverlay;
    private Transform floatingCalloutRoot;
    private TextMesh floatingCalloutText;
    private float floatingCalloutTimer = 0f;

    // Moving Background, Speed Streaks & Moving Objects
    private List<Transform> parallaxStarsFar = new List<Transform>();
    private List<Transform> parallaxStarsNear = new List<Transform>();
    private List<Transform> speedStreaks = new List<Transform>();
    private List<GameObject> activeSpaceObjects = new List<GameObject>();
    private float spaceObjectSpawnTimer = 0f;

    // Bottom Progress Bar Components
    private Transform miniRocketIcon;
    private Transform progressBarFill;
    private TextMesh hudTimeText;
    private TextMesh hudDistanceText;
    private TextMesh hudFuelText;
    private TextMesh hudHullText;
    private TextMesh hudPowerText;
    private TextMesh hudScienceText;
    private TextMesh hudSpeedButtonText;
    private Transform destPlanetProgressIcon;
    private Transform bigDestinationPlanet;

    // Animation & Camera Feedback
    private float activeAnimTimer = 0f;
    private CruiseAnimationType currentPlayingAnim = CruiseAnimationType.None;
    private float cameraShakeDuration = 0f;
    private float cameraShakeMagnitude = 0f;
    private Vector3 originalCameraPos;
    private GameObject statusToastBanner;
    private TextMesh statusToastText;
    private float toastTimer = 0f;

    // Audio
    private AudioSource audioSourceSFX;
    private AudioSource audioSourceRumble;
    private AudioClip chimeClip;
    private AudioClip alertKlaxonClip;
    private AudioClip thrusterRoarClip;
    private AudioClip rcsPuffClip;
    private AudioClip rebootBeepClip;
    private AudioClip gasHissClip;
    private AudioClip victoryClip;

    void Awake()
    {
        if (Instance == null) Instance = this;
        InitProceduralAudio();
        Initialize14NASAEvents();
    }

    void Update()
    {
        if (Camera.main != null && originalCameraPos == Vector3.zero)
        {
            originalCameraPos = Camera.main.transform.position;
        }

        if (isLoadingScreenActive)
        {
            UpdateLoadingScreen(Time.deltaTime);
            return;
        }

        if (!isCruiseActive) return;

        // Shortcut: T toggles Time Warp (1X -> 2X -> 4X -> 8X -> 1X)
        if (Input.GetKeyDown(KeyCode.T))
        {
            CycleTimeWarp();
        }

        // Mouse clicks for Event choices, warp button, and return button
        HandleMouseInteraction();

        // While an event modal is open, transit pauses so player can inspect the technical scenario
        float dt = isEventModalOpen ? 0f : (Time.deltaTime * timeMultiplier);

        currentFlightTime += dt;
        float progressFrac = Mathf.Clamp01(currentFlightTime / totalFlightDuration);

        // 1. Update Player's Real Rocket Motion & Triggered Visual Effects
        UpdateRealRocketFlight(dt, progressFrac);

        // 2. High-Speed Parallax Space Background & Drifting Space Objects
        UpdateParallaxSpaceBackground(dt);
        UpdateMovingSpaceObjects(dt);

        // 3. Update Bottom Progress Bar & Large Mission Telemetry HUD
        UpdateProgressBarAndHUD(progressFrac);

        // 4. Check for Scheduled NASA In-Flight Events
        CheckScheduledEvents();

        // 5. Update Floating Notification Toast & Floating Stat Callouts
        UpdateToastBanner(Time.deltaTime);
        UpdateFloatingCallout(Time.deltaTime);

        // 6. Camera Screen Shake
        UpdateCameraShake();

        // 7. Arrival at Destination (Progress >= 1.0)
        if (progressFrac >= 1.0f && !isDebriefOpen)
        {
            TriggerDestinationArrival();
        }
    }

    // =========================================================================
    // 1. PUBLIC ENTRY POINTS & STARTUP
    // =========================================================================
    public void StartInterplanetaryCruise(int targetIndex, float fuelKg, float hullIntegrity, float powerWh, float potentialSci)
    {
        Transform hub = null;
        SpaceMissionPrototype proto = FindAnyObjectByType<SpaceMissionPrototype>();
        if (proto != null) hub = proto.spacecraftHub;
        StartInterplanetaryCruise(hub, targetIndex, fuelKg, hullIntegrity, powerWh, potentialSci);
    }

    public void StartInterplanetaryCruise(Transform playerRocketHub, int targetIndex, float fuelKg, float hullIntegrity, float powerWh, float potentialSci)
    {
        selectedTargetIndex = Mathf.Clamp(targetIndex, 0, 2);
        currentFuelKg = Mathf.Max(50f, fuelKg);
        maxFuelKg = currentFuelKg;
        currentHullIntegrity = Mathf.Clamp(hullIntegrity, 20f, 100f);
        currentPowerPct = 100f;
        currentSciencePts = potentialSci;
        currentFlightTime = 0f;
        eventsSurvivedCount = 0;
        timeMultiplier = 1.0f;
        isEventModalOpen = false;
        isDebriefOpen = false;

        // Save reference to the player's real assembled spacecraft
        realRocketTransform = playerRocketHub;
        if (realRocketTransform == null)
        {
            SpaceMissionPrototype proto = FindAnyObjectByType<SpaceMissionPrototype>();
            if (proto != null && proto.spacecraftHub != null) realRocketTransform = proto.spacecraftHub;
            else
            {
                GameObject hubObj = GameObject.Find("Spacecraft_Bus_Hub");
                if (hubObj != null) realRocketTransform = hubObj.transform;
            }
        }

        if (realRocketTransform != null)
        {
            realRocketOriginalParent = realRocketTransform.parent;
            realRocketOriginalPos = realRocketTransform.localPosition;
            realRocketOriginalRot = realRocketTransform.localRotation;
            realRocketOriginalScale = realRocketTransform.localScale;
        }

        // Explicitly hide any vertical launchpad, service tower, and corridor map
        GameObject corridor = GameObject.Find("Mission_Corridor_Map");
        if (corridor != null) corridor.SetActive(false);
        GameObject earthPad = GameObject.Find("Earth_Launch_Label");
        if (earthPad != null) earthPad.SetActive(false);
        GameObject pad = GameObject.Find("Launchpad_Platform");
        if (pad != null) pad.SetActive(false);
        GameObject tower = GameObject.Find("Launchpad_Service_Tower");
        if (tower != null) tower.SetActive(false);
        GameObject gantry = GameObject.Find("Custom_Launch_Gantry");
        if (gantry != null) gantry.SetActive(false);
        GameObject earthSurf = GameObject.Find("Earth_Surface_Ground");
        if (earthSurf != null) earthSurf.SetActive(false);

        BuildScheduledTimeline();
        BuildCruiseMasterHierarchy();
        StartCoroutine(RunLoadingScreenSequence());
    }

    public void StopCruise()
    {
        isCruiseActive = false;
        isLoadingScreenActive = false;
        isEventModalOpen = false;
        isDebriefOpen = false;

        // Restore player's real rocket back to its assembly state
        if (realRocketTransform != null)
        {
            realRocketTransform.SetParent(realRocketOriginalParent, false);
            realRocketTransform.localPosition = realRocketOriginalPos;
            realRocketTransform.localRotation = realRocketOriginalRot;
            realRocketTransform.localScale = realRocketOriginalScale;
            realRocketTransform.gameObject.SetActive(true);
        }

        if (cruiseMasterRoot != null)
        {
            Destroy(cruiseMasterRoot);
            cruiseMasterRoot = null;
        }

        GameObject gantry = GameObject.Find("Custom_Launch_Gantry");
        if (gantry != null) gantry.SetActive(true);
        GameObject earthSurf = GameObject.Find("Earth_Surface_Ground");
        if (earthSurf != null) earthSurf.SetActive(true);
    }

    private void BuildScheduledTimeline()
    {
        scheduledEventTimes.Clear();
        // Fast, exciting pacing: Events start at 6s, then fire every 14-16 seconds!
        float[] baseTimes = { 6f, 18f, 32f, 48f, 64f, 82f, 100f, 114f };
        for (int i = 0; i < baseTimes.Length; i++)
        {
            scheduledEventTimes.Add(baseTimes[i]);
        }

        foreach (var ev in allEventsPool)
        {
            ev.hasTriggered = false;
        }
    }

    // =========================================================================
    // 2. LOADING SCREEN (NASA TLI & TRAJECTORY COMPUTATION)
    // =========================================================================
    private Transform loadingRadarRing;
    private TextMesh loadingStatusText;
    private Transform loadingBarFill;
    private float loadingTimer = 0f;
    private readonly float loadingTotalDuration = 2.2f;

    private IEnumerator RunLoadingScreenSequence()
    {
        isCruiseActive = false;
        isLoadingScreenActive = true;
        loadingTimer = 0f;

        if (loadingScreenRoot != null) loadingScreenRoot.SetActive(true);
        if (cruiseWorldRoot != null) cruiseWorldRoot.SetActive(false);
        if (cruiseUIRoot != null) cruiseUIRoot.SetActive(false);

        PlaySfx(chimeClip, 0.85f);

        while (loadingTimer < loadingTotalDuration)
        {
            loadingTimer += Time.deltaTime;
            yield return null;
        }

        // Loading complete -> Reveal the real rocket horizontal cruise!
        isLoadingScreenActive = false;
        if (loadingScreenRoot != null) loadingScreenRoot.SetActive(false);
        if (cruiseWorldRoot != null) cruiseWorldRoot.SetActive(true);
        if (cruiseUIRoot != null) cruiseUIRoot.SetActive(true);

        isCruiseActive = true;
        PlaySfx(thrusterRoarClip, 0.65f);
        ShowToast("★ TRANS-PLANETARY INJECTION CONFIRMED // HORIZONTAL CRUISE ACTIVE ★", NominalGreen);
    }

    private void UpdateLoadingScreen(float dt)
    {
        if (loadingRadarRing != null)
        {
            loadingRadarRing.Rotate(Vector3.forward, -160f * dt);
        }

        float p = Mathf.Clamp01(loadingTimer / loadingTotalDuration);
        if (loadingBarFill != null)
        {
            loadingBarFill.localScale = new Vector3(9.2f * p, 0.35f, 1f);
            loadingBarFill.localPosition = new Vector3(-4.6f + (9.2f * p * 0.5f), -2.2f, -0.05f);
        }

        if (loadingStatusText != null)
        {
            string targetName = (selectedTargetIndex == 0) ? "LOW EARTH ORBIT (LEO)" : (selectedTargetIndex == 1 ? "LUNAR POLAR ORBIT" : "MARS DEEP SPACE");
            if (p < 0.28f)
                loadingStatusText.text = $"[1/4] ASCENT BOOST COMPLETE // CLEARING UPPER EXOSPHERE...\nEN ROUTE TO: <color=#00E5FF>{targetName}</color>";
            else if (p < 0.60f)
                loadingStatusText.text = $"[2/4] JETTISONING SPENT PROPULSION & DEPLOYING ROSA SOLAR WINGS...\nCOMPUTING TLI INJECTION VECTORS (dV: NOMINAL)";
            else if (p < 0.88f)
                loadingStatusText.text = $"[3/4] ORIENTING HIGH-GAIN DSN DISH & CRUISE AVIONICS...\nCOMM LINK SYNCHRONIZED WITH GOLDSTONE TRACKING";
            else
                loadingStatusText.text = $"[4/4] HORIZONTAL TRANSIT CORRIDOR LOCKED // 120 SEC CRUISE\nIGNITING MAIN INTERPLANETARY CRUISE ENGINES!";
        }
    }

    // =========================================================================
    // 3. THE REAL ASSEMBLED ROCKET & DRAMATIC IN-FLIGHT ANIMATIONS
    // =========================================================================
    private void UpdateRealRocketFlight(float dt, float progress)
    {
        if (realRocketTransform == null) return;

        // 1. Natural horizontal cruise floating & bobbing
        float bobY = 0.25f + Mathf.Sin(Time.time * 3.0f) * 0.07f;
        float bobRotZ = -90f + Mathf.Sin(Time.time * 2.0f) * 0.8f;
        realRocketTransform.localPosition = new Vector3(-0.6f, bobY, -1.0f);
        realRocketTransform.localRotation = Quaternion.Euler(0f, 0f, bobRotZ);

        // Position the horizontal exhaust flame directly at the rocket's engine nozzle on the left
        if (flameSurgeGroup != null)
        {
            flameSurgeGroup.transform.localPosition = new Vector3(-0.6f - 1.85f, bobY, -0.95f);
        }

        // 2. Dynamic Main Engine Flame Animation
        if (mainEngineFlame != null)
        {
            bool isFlameSurge = (currentPlayingAnim == CruiseAnimationType.FlameSurge && activeAnimTimer > 0f);
            float flicker = UnityEngine.Random.Range(0.92f, 1.18f);

            // In FlameSurge, flame shoots out 4x longer across the screen!
            float flameScaleX = (isFlameSurge ? 5.2f : 1.8f) * flicker;
            float flameScaleY = (isFlameSurge ? 1.35f : 0.55f) * flicker;

            mainEngineFlame.localScale = new Vector3(flameScaleX, flameScaleY, 1f);

            Color coreCol = isFlameSurge ? new Color(1f, 1f, 1f, 1f) : new Color(0.4f, 0.95f, 1.0f, 0.95f);
            Color outerCol = isFlameSurge ? new Color(1.0f, 0.40f, 0.05f, 0.95f) : new Color(0.1f, 0.55f, 1.0f, 0.75f);

            if (flameCore != null) SetRendererColor(flameCore.GetComponent<Renderer>(), coreCol);
            if (flameOuter != null) SetRendererColor(flameOuter.GetComponent<Renderer>(), outerCol);
            if (flameSparks != null) flameSparks.gameObject.SetActive(isFlameSurge);
        }

        // 3. Active Animation Execution on the Real Rocket
        if (activeAnimTimer > 0f)
        {
            activeAnimTimer -= Time.deltaTime; // Real-time countdown

            switch (currentPlayingAnim)
            {
                case CruiseAnimationType.FlameSurge:
                    TriggerCameraShake(0.08f, 0.06f);
                    // Spacecraft jolts forward with acceleration thrust!
                    realRocketTransform.localPosition += new Vector3(0.20f * Mathf.Sin(activeAnimTimer * 8f), 0f, 0f);
                    break;

                case CruiseAnimationType.RcsPuffs:
                    if (rcsN != null) rcsN.gameObject.SetActive(Mathf.Sin(Time.time * 30f) > 0f);
                    if (rcsS != null) rcsS.gameObject.SetActive(Mathf.Sin(Time.time * 30f + 1.5f) > 0f);
                    if (rcsE != null) rcsE.gameObject.SetActive(Mathf.Sin(Time.time * 30f + 3f) > 0f);
                    if (rcsW != null) rcsW.gameObject.SetActive(Mathf.Sin(Time.time * 30f + 4.5f) > 0f);
                    break;

                case CruiseAnimationType.ThermalRoll:
                    // 2.5D Barbecue Roll: smooth perspective sine-scale flip so 2D sprites don't vanish!
                    float flip = Mathf.Cos(Time.time * 4.5f);
                    realRocketTransform.localScale = new Vector3(1.15f, 1.15f * flip, 1f);
                    break;

                case CruiseAnimationType.AvionicsReboot:
                    if (scanlineOverlay != null)
                    {
                        scanlineOverlay.SetActive(true);
                        Renderer sr = scanlineOverlay.GetComponent<Renderer>();
                        float alpha = Mathf.PingPong(Time.time * 10f, 0.45f);
                        SetRendererColor(sr, new Color(0.0f, 0.95f, 1.0f, alpha));
                    }
                    break;

                case CruiseAnimationType.LifeSupportHiss:
                    if (greenSealRing != null)
                    {
                        greenSealRing.gameObject.SetActive(true);
                        float expand = (1f - (activeAnimTimer / 3.5f)) * 3.5f;
                        greenSealRing.localScale = new Vector3(expand, expand, 1f);
                        greenSealRing.position = realRocketTransform.position;
                    }
                    break;

                case CruiseAnimationType.PowerStandby:
                    if (bluePowerShield != null)
                    {
                        bluePowerShield.gameObject.SetActive(true);
                        float pulse = 0.90f + 0.12f * Mathf.Sin(Time.time * 10f);
                        bluePowerShield.localScale = new Vector3(4.5f * pulse, 2.4f * pulse, 1f);
                        bluePowerShield.position = realRocketTransform.position;
                    }
                    break;
            }
        }
        else
        {
            // Reset temporary animation states
            currentPlayingAnim = CruiseAnimationType.None;
            if (rcsN != null) rcsN.gameObject.SetActive(false);
            if (rcsS != null) rcsS.gameObject.SetActive(false);
            if (rcsE != null) rcsE.gameObject.SetActive(false);
            if (rcsW != null) rcsW.gameObject.SetActive(false);
            if (bluePowerShield != null) bluePowerShield.gameObject.SetActive(false);
            if (greenSealRing != null) greenSealRing.gameObject.SetActive(false);
            if (scanlineOverlay != null) scanlineOverlay.SetActive(false);
            if (realRocketTransform != null) realRocketTransform.localScale = new Vector3(1.15f, 1.15f, 1f);
        }

        // Approaching destination planet: grow big destination planet on the right horizon!
        if (progress > 0.78f && bigDestinationPlanet != null)
        {
            bigDestinationPlanet.gameObject.SetActive(true);
            float arriveFrac = (progress - 0.78f) / 0.22f;
            float px = Mathf.Lerp(13.0f, 6.2f, arriveFrac);
            float pSize = Mathf.Lerp(3.2f, 7.2f, arriveFrac);
            bigDestinationPlanet.localPosition = new Vector3(px, 0.2f, 2.0f);
            bigDestinationPlanet.localScale = new Vector3(pSize, pSize, 1f);
        }
        else if (bigDestinationPlanet != null)
        {
            bigDestinationPlanet.gameObject.SetActive(false);
        }
    }

    // =========================================================================
    // 4. FAST PARALLAX SPACE BACKGROUND, SPEED STREAKS & MOVING OBJECTS
    // =========================================================================
    private void UpdateParallaxSpaceBackground(float dt)
    {
        // High-velocity space transit speeds
        float speedFar = 2.2f * timeMultiplier;
        float speedNear = 6.5f * timeMultiplier;
        float speedStreaksRate = 18.0f * timeMultiplier;

        // Far stars
        foreach (var t in parallaxStarsFar)
        {
            if (t == null) continue;
            t.Translate(Vector3.left * speedFar * dt, Space.World);
            if (t.position.x < -14.0f)
            {
                t.position = new Vector3(14.0f, t.position.y, t.position.z);
            }
        }

        // Near stardust
        foreach (var t in parallaxStarsNear)
        {
            if (t == null) continue;
            t.Translate(Vector3.left * speedNear * dt, Space.World);
            if (t.position.x < -14.0f)
            {
                t.position = new Vector3(14.0f, t.position.y, t.position.z);
            }
        }

        // Cosmic speed streaks (giving unmistakable feeling of extreme velocity)
        foreach (var st in speedStreaks)
        {
            if (st == null) continue;
            st.Translate(Vector3.left * speedStreaksRate * dt, Space.World);
            if (st.position.x < -16.0f)
            {
                st.position = new Vector3(16.0f, UnityEngine.Random.Range(-4.5f, 4.5f), st.position.z);
            }
        }
    }

    private void UpdateMovingSpaceObjects(float dt)
    {
        spaceObjectSpawnTimer += dt;
        if (spaceObjectSpawnTimer >= UnityEngine.Random.Range(2.5f, 4.2f) / timeMultiplier)
        {
            spaceObjectSpawnTimer = 0f;
            SpawnRandomSpaceObject();
        }

        for (int i = activeSpaceObjects.Count - 1; i >= 0; i--)
        {
            GameObject obj = activeSpaceObjects[i];
            if (obj == null)
            {
                activeSpaceObjects.RemoveAt(i);
                continue;
            }

            float objSpeed = 4.8f * timeMultiplier;
            obj.transform.Translate(Vector3.left * objSpeed * dt, Space.World);
            obj.transform.Rotate(Vector3.forward, 45f * dt);

            if (obj.transform.position.x < -15.0f)
            {
                activeSpaceObjects.RemoveAt(i);
                Destroy(obj);
            }
        }
    }

    private void SpawnRandomSpaceObject()
    {
        if (cruiseWorldRoot == null) return;
        GameObject obj = new GameObject("Drifting_Space_Object");
        obj.transform.SetParent(cruiseWorldRoot.transform, false);

        float spawnY = UnityEngine.Random.Range(-3.0f, 3.2f);
        if (Mathf.Abs(spawnY - 0.2f) < 0.7f) spawnY += (spawnY > 0.2f ? 0.9f : -0.9f);

        obj.transform.position = new Vector3(14.5f, spawnY, 1.5f);

        int type = UnityEngine.Random.Range(0, 3);
        if (type == 0) // Jagged rocky asteroid
        {
            float sz = UnityEngine.Random.Range(0.6f, 1.2f);
            CreateFlatQuad(obj.transform, "Asteroid_Body", Vector3.zero, new Vector3(sz, sz * 0.85f, 1f), new Color(0.45f, 0.42f, 0.38f), false, 10);
            CreateFlatQuad(obj.transform, "Crater", new Vector3(sz * 0.15f, sz * 0.1f, -0.02f), new Vector3(sz * 0.35f, sz * 0.35f, 1f), new Color(0.25f, 0.23f, 0.21f), false, 11);
        }
        else if (type == 1) // Deep space relay satellite / probe
        {
            float sz = UnityEngine.Random.Range(0.7f, 1.1f);
            CreateFlatQuad(obj.transform, "Probe_Bus", Vector3.zero, new Vector3(sz * 0.55f, sz * 0.55f, 1f), new Color(0.8f, 0.85f, 0.9f), false, 10);
            CreateFlatQuad(obj.transform, "Wing_L", new Vector3(-sz * 0.75f, 0f, -0.02f), new Vector3(sz * 0.75f, sz * 0.30f, 1f), CyberCyanDark, false, 11);
            CreateFlatQuad(obj.transform, "Wing_R", new Vector3( sz * 0.75f, 0f, -0.02f), new Vector3(sz * 0.75f, sz * 0.30f, 1f), CyberCyanDark, false, 11);
        }
        else // Cometary ice fragment
        {
            float sz = UnityEngine.Random.Range(0.45f, 0.85f);
            CreateFlatQuad(obj.transform, "Ice_Fragment", Vector3.zero, new Vector3(sz, sz, 1f), new Color(0.65f, 0.92f, 1.0f, 0.90f), false, 10);
        }

        activeSpaceObjects.Add(obj);
    }

    // =========================================================================
    // 5. LARGE, BOLD BOTTOM PROGRESS BAR & LIVE MISSION TELEMETRY HUD
    // =========================================================================
    private void UpdateProgressBarAndHUD(float progress)
    {
        // 1. Move mini rocket icon along trajectory path from X = -5.8f to X = 5.8f
        float pathStart = -5.8f;
        float pathEnd = 5.8f;
        float curX = Mathf.Lerp(pathStart, pathEnd, progress);

        if (miniRocketIcon != null)
        {
            miniRocketIcon.localPosition = new Vector3(curX, 0f, -0.08f);
        }

        if (progressBarFill != null)
        {
            float fillW = Mathf.Max(0.01f, curX - pathStart);
            progressBarFill.localScale = new Vector3(fillW, 0.14f, 1f);
            progressBarFill.localPosition = new Vector3(pathStart + fillW * 0.5f, 0f, -0.02f);
        }

        // 2. Large, Bold HUD Text Labels
        int elapsedSec = Mathf.FloorToInt(currentFlightTime);
        int remSec = Mathf.Max(0, Mathf.FloorToInt(totalFlightDuration - currentFlightTime));
        if (hudTimeText != null)
        {
            hudTimeText.text = $"TIME: {elapsedSec / 60:D2}:{elapsedSec % 60:D2} / 02:00  (ETA: -{remSec / 60:D2}:{remSec % 60:D2})";
        }

        float totalDistKm = (selectedTargetIndex == 0) ? 420f : (selectedTargetIndex == 1 ? 384400f : 225000000f);
        float currentDistKm = totalDistKm * progress;
        if (hudDistanceText != null)
        {
            if (totalDistKm > 1000000f)
                hudDistanceText.text = $"DISTANCE: {currentDistKm / 1000000f:F1}M / {totalDistKm / 1000000f:F0}M km ({progress * 100f:F0}%)";
            else
                hudDistanceText.text = $"DISTANCE: {currentDistKm:N0} / {totalDistKm:N0} km ({progress * 100f:F0}%)";
        }

        if (hudFuelText != null)
        {
            string fCol = (currentFuelKg > 80f) ? "#2ED973" : ((currentFuelKg > 30f) ? "#FFD133" : "#FF4B4B");
            hudFuelText.text = $"FUEL: <color={fCol}>{currentFuelKg:F0} kg</color>";
        }

        if (hudHullText != null)
        {
            string hCol = (currentHullIntegrity > 60f) ? "#2ED973" : ((currentHullIntegrity > 30f) ? "#FFD133" : "#FF4B4B");
            hudHullText.text = $"HULL: <color={hCol}>{currentHullIntegrity:F0}%</color>";
        }

        if (hudPowerText != null)
        {
            string pCol = (currentPowerPct > 50f) ? "#2ED973" : ((currentPowerPct > 20f) ? "#FFD133" : "#FF4B4B");
            hudPowerText.text = $"POWER: <color={pCol}>{currentPowerPct:F0}%</color>";
        }

        if (hudScienceText != null)
        {
            hudScienceText.text = $"SCI YIELD: <color=#00E5FF>{currentSciencePts:F0} pts</color>";
        }
    }

    public void SetTimeWarp(float speed)
    {
        timeMultiplier = speed;
        if (hudSpeedButtonText != null)
        {
            hudSpeedButtonText.text = $"WARP: {timeMultiplier:F0}X";
        }
        PlaySfx(chimeClip, 0.55f);
        ShowToast($"SIMULATION SPEED: {timeMultiplier:F0}X", TelemetryGold);
    }

    public void CycleTimeWarp()
    {
        if (timeMultiplier == 1.0f) timeMultiplier = 2.0f;
        else if (timeMultiplier == 2.0f) timeMultiplier = 4.0f;
        else if (timeMultiplier == 4.0f) timeMultiplier = 8.0f;
        else timeMultiplier = 1.0f;

        if (hudSpeedButtonText != null)
        {
            hudSpeedButtonText.text = $"WARP: {timeMultiplier:F0}X";
        }
        PlaySfx(chimeClip, 0.55f);
        ShowToast($"SIMULATION SPEED: {timeMultiplier:F0}X", TelemetryGold);
    }

    // =========================================================================
    // 6. SCHEDULING & EVENT HANDLING (14 NASA RESEARCH EVENTS - LARGE FONTS)
    // =========================================================================
    private void CheckScheduledEvents()
    {
        if (isEventModalOpen || isDebriefOpen || scheduledEventTimes.Count == 0) return;

        float nextTime = scheduledEventTimes[0];
        if (currentFlightTime >= nextTime)
        {
            scheduledEventTimes.RemoveAt(0);
            TriggerRandomNASAEvent();
        }
    }

    private void TriggerRandomNASAEvent()
    {
        List<CruiseEventData> available = allEventsPool.FindAll(e => !e.hasTriggered);
        if (available.Count == 0)
        {
            allEventsPool.ForEach(e => e.hasTriggered = false);
            available = allEventsPool;
        }

        int idx = UnityEngine.Random.Range(0, available.Count);
        CruiseEventData ev = available[idx];
        ev.hasTriggered = true;
        currentActiveEvent = ev;

        OpenEventModal(ev);
    }

    private string WordWrap(string text, int maxLineLength = 32)
    {
        if (string.IsNullOrEmpty(text)) return "";
        string[] words = text.Split(' ');
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int currentLineLen = 0;

        foreach (string word in words)
        {
            if (string.IsNullOrEmpty(word)) continue;
            int visibleLen = System.Text.RegularExpressions.Regex.Replace(word, "<.*?>", "").Length;

            if (currentLineLen + visibleLen > maxLineLength)
            {
                if (sb.Length > 0) sb.Append("\n");
                sb.Append(word);
                currentLineLen = visibleLen;
            }
            else
            {
                if (currentLineLen > 0)
                {
                    sb.Append(" ");
                    currentLineLen += 1;
                }
                sb.Append(word);
                currentLineLen += visibleLen;
            }
        }
        return sb.ToString();
    }

    private void OpenEventModal(CruiseEventData ev)
    {
        isEventModalOpen = true;
        PlaySfx(alertKlaxonClip, 0.95f);
        TriggerCameraShake(0.08f, 0.40f);

        if (eventModalRoot != null)
        {
            Destroy(eventModalRoot);
        }

        eventModalRoot = new GameObject("NASA_Incident_Event_Modal");
        eventModalRoot.transform.SetParent(cruiseUIRoot.transform, false);
        eventModalRoot.transform.localPosition = new Vector3(0f, 0.35f, -3.0f);

        // 1. Dark modal dim background
        CreateFlatQuad(eventModalRoot.transform, "Dark_Dim", Vector3.zero, new Vector3(26f, 15f, 1f), new Color(0.01f, 0.02f, 0.05f, 0.90f), true, 50);

        // 2. High-tech main card body
        float cardW = 16.8f;
        float cardH = 10.4f;
        CreateFlatQuad(eventModalRoot.transform, "Card_Bg", Vector3.zero, new Vector3(cardW, cardH, 1f), DarkNavyBg, false, 52);

        // 3. Glowing Hazard border
        CreateFlatQuad(eventModalRoot.transform, "Border_T", new Vector3(0f,  cardH * 0.5f, -0.05f), new Vector3(cardW + 0.08f, 0.07f, 1f), HazardRed, false, 53);
        CreateFlatQuad(eventModalRoot.transform, "Border_B", new Vector3(0f, -cardH * 0.5f, -0.05f), new Vector3(cardW + 0.08f, 0.07f, 1f), HazardRed, false, 53);
        CreateFlatQuad(eventModalRoot.transform, "Border_L", new Vector3(-cardW * 0.5f, 0f, -0.05f), new Vector3(0.07f, cardH + 0.08f, 1f), HazardRed, false, 53);
        CreateFlatQuad(eventModalRoot.transform, "Border_R", new Vector3( cardW * 0.5f, 0f, -0.05f), new Vector3(0.07f, cardH + 0.08f, 1f), HazardRed, false, 53);

        // 4. Alert Header & Citation (Large & Bold)
        CreateText(eventModalRoot.transform, "Alert_Header", new Vector3(0f, 4.45f, -0.1f), "⚠ NASA MISSION CONTROL INCIDENT ALERT ⚠", 0.088f, 52, FontStyle.Bold, TextAnchor.MiddleCenter, HazardRed, 55);
        CreateText(eventModalRoot.transform, "Source_Badge", new Vector3(0f, 3.88f, -0.1f), $"[{ev.missionSource}]", 0.060f, 36, FontStyle.Bold, TextAnchor.MiddleCenter, TelemetryGold, 55);

        // 5. Event Title & Technical Scenario (Large & Bold with WordWrap)
        CreateText(eventModalRoot.transform, "Event_Title", new Vector3(0f, 3.25f, -0.1f), ev.eventTitle, 0.072f, 44, FontStyle.Bold, TextAnchor.MiddleCenter, CyberCyan, 55);
        string wrappedDesc = WordWrap(ev.description, 72);
        CreateText(eventModalRoot.transform, "Event_Desc", new Vector3(0f, 2.30f, -0.1f), wrappedDesc, 0.058f, 36, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white, 55);

        // 6. Exactly 3 Solution Cards arranged horizontally with large readable fonts
        float colSpacing = 5.35f;
        for (int i = 0; i < 3; i++)
        {
            CruiseEventChoice choice = ev.choices[i];
            float colX = -colSpacing + i * colSpacing;
            GameObject choiceBtn = new GameObject("BTN_Choice_" + i);
            choiceBtn.transform.SetParent(eventModalRoot.transform, false);
            choiceBtn.transform.localPosition = new Vector3(colX, -1.05f, -0.1f);

            BoxCollider bc = choiceBtn.AddComponent<BoxCollider>();
            bc.size = new Vector3(5.10f, 5.20f, 2.0f);

            // Card Body & Accent Border
            CreateFlatQuad(choiceBtn.transform, "Choice_Bg", Vector3.zero, new Vector3(5.10f, 5.20f, 1f), new Color(0.04f, 0.08f, 0.16f, 0.98f), false, 54);
            CreateFlatQuad(choiceBtn.transform, "Choice_Border", new Vector3(0f, 0f, 0.02f), new Vector3(5.18f, 5.28f, 1f), CyberCyanDark, false, 53);

            // Choice Header (Large & Crisp)
            CreateText(choiceBtn.transform, "Choice_Num", new Vector3(0f, 2.20f, -0.05f), $"[SOLUTION {i + 1}]", 0.062f, 38, FontStyle.Bold, TextAnchor.MiddleCenter, TelemetryGold, 56);
            string wrappedTitle = WordWrap(choice.choiceTitle, 26);
            CreateText(choiceBtn.transform, "Choice_Title", new Vector3(0f, 1.62f, -0.05f), wrappedTitle, 0.056f, 32, FontStyle.Bold, TextAnchor.MiddleCenter, CyberCyan, 56);

            // Pros & Cons (Large, Bold & High-Contrast with WordWrap)
            string wrappedPros = WordWrap(choice.prosText, 30);
            string wrappedCons = WordWrap(choice.consText, 30);

            string bodyText =
                $"<color=#2ED973><b>PROS:</b></color> {wrappedPros}\n\n" +
                $"<color=#FF4B4B><b>CONS:</b></color> {wrappedCons}\n\n" +
                $"<color=#FFD133><b>IMPACT:</b></color> " +
                (choice.fuelDelta != 0 ? $"Fuel {choice.fuelDelta:+0;-#;0}kg  " : "") +
                (choice.hullDelta != 0 ? $"Hull {choice.hullDelta:+0;-#;0}%  " : "") +
                (choice.powerDelta != 0 ? $"Power {choice.powerDelta:+0;-#;0}%  " : "") +
                (choice.scienceDelta != 0 ? $"Sci {choice.scienceDelta:+0;-#;0}pts" : "");

            CreateText(choiceBtn.transform, "Choice_Body", new Vector3(-2.30f, 0.95f, -0.05f), bodyText, 0.052f, 30, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, 56);

            // Large Execute Button Pill
            GameObject pill = new GameObject("Execute_Pill");
            pill.transform.SetParent(choiceBtn.transform, false);
            pill.transform.localPosition = new Vector3(0f, -2.15f, -0.05f);
            CreateFlatQuad(pill.transform, "Pill_Bg", Vector3.zero, new Vector3(4.5f, 0.75f, 1f), NominalGreen, false, 56);
            CreateText(pill.transform, "Pill_Text", new Vector3(0f, 0f, -0.02f), $"▶ EXECUTE [{i + 1}]", 0.060f, 34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.black, 57);
        }
    }

    private void HandleEventChoiceSelected(int choiceIdx)
    {
        if (currentActiveEvent == null || choiceIdx < 0 || choiceIdx >= currentActiveEvent.choices.Length) return;

        CruiseEventChoice chosen = currentActiveEvent.choices[choiceIdx];

        // 1. Apply Stat Changes
        currentFuelKg = Mathf.Max(0f, currentFuelKg + chosen.fuelDelta);
        currentHullIntegrity = Mathf.Clamp(currentHullIntegrity + chosen.hullDelta, 0f, 100f);
        currentPowerPct = Mathf.Clamp(currentPowerPct + chosen.powerDelta, 0f, 100f);
        currentSciencePts = Mathf.Max(0f, currentSciencePts + chosen.scienceDelta);
        eventsSurvivedCount++;

        // 2. Trigger Matching Rocket Animation on Player's Real Rocket!
        TriggerAnimation(chosen.animType);

        // 3. Floating Callout Text Rising from Rocket (+30 Sci, -50kg Fuel, etc.)
        string calloutStr = "";
        if (chosen.scienceDelta > 0) calloutStr += $"<color=#00E5FF>+{chosen.scienceDelta:F0} SCI! </color>";
        if (chosen.fuelDelta < 0)    calloutStr += $"<color=#FF9600>{chosen.fuelDelta:F0}kg FUEL </color>";
        if (chosen.powerDelta != 0)  calloutStr += $"<color=#2ED973>{chosen.powerDelta:+0;-#}% POWER </color>";
        if (chosen.hullDelta < 0)    calloutStr += $"<color=#FF4B4B>{chosen.hullDelta:F0}% HULL </color>";
        ShowFloatingCallout(calloutStr);

        // 4. Audio & Banner Feedback
        PlaySfx(chimeClip, 0.95f);
        ShowToast($"ACTION RESOLVED: {chosen.resolutionMessage}", NominalGreen);

        // 5. Close Event Modal & Resume Cruise
        if (eventModalRoot != null) Destroy(eventModalRoot);
        isEventModalOpen = false;

        if (currentHullIntegrity <= 5.0f)
        {
            TriggerMissionFailure("CATASTROPHIC VEHICLE BREAKUP IN DEEP SPACE");
        }
    }

    private void TriggerAnimation(CruiseAnimationType anim)
    {
        currentPlayingAnim = anim;
        activeAnimTimer = 3.5f; // Animation runs prominently for 3.5 seconds

        switch (anim)
        {
            case CruiseAnimationType.FlameSurge:
                TriggerCameraShake(0.09f, 2.2f);
                PlaySfx(thrusterRoarClip, 1.0f);
                break;

            case CruiseAnimationType.RcsPuffs:
                PlaySfx(rcsPuffClip, 0.95f);
                break;

            case CruiseAnimationType.AvionicsReboot:
                PlaySfx(rebootBeepClip, 0.95f);
                break;

            case CruiseAnimationType.LifeSupportHiss:
                PlaySfx(gasHissClip, 0.95f);
                break;

            case CruiseAnimationType.PowerStandby:
                PlaySfx(rebootBeepClip, 0.7f);
                break;

            case CruiseAnimationType.ThermalRoll:
                PlaySfx(chimeClip, 0.6f);
                break;
        }
    }

    private void ShowFloatingCallout(string text)
    {
        if (floatingCalloutText != null && floatingCalloutRoot != null)
        {
            floatingCalloutText.text = text;
            floatingCalloutRoot.localPosition = new Vector3(-0.6f, 1.4f, -2.5f);
            floatingCalloutRoot.gameObject.SetActive(true);
            floatingCalloutTimer = 3.0f;
        }
    }

    private void UpdateFloatingCallout(float dt)
    {
        if (floatingCalloutTimer > 0f && floatingCalloutRoot != null)
        {
            floatingCalloutTimer -= dt;
            floatingCalloutRoot.Translate(Vector3.up * 0.45f * dt, Space.World);
            if (floatingCalloutTimer <= 0f)
            {
                floatingCalloutRoot.gameObject.SetActive(false);
            }
        }
    }

    // =========================================================================
    // 7. ARRIVAL & DEBRIEF MODAL (LARGE FONTS)
    // =========================================================================
    private void TriggerDestinationArrival()
    {
        isCruiseActive = false;
        isDebriefOpen = true;

        TriggerAnimation(CruiseAnimationType.FlameSurge);
        PlaySfx(victoryClip, 1.0f);

        StartCoroutine(ShowDebriefAfterDelay(2.5f));
    }

    private IEnumerator ShowDebriefAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (debriefModalRoot != null) Destroy(debriefModalRoot);
        debriefModalRoot = new GameObject("Mission_Debrief_Modal");
        debriefModalRoot.transform.SetParent(cruiseUIRoot.transform, false);
        debriefModalRoot.transform.localPosition = new Vector3(0f, 0.2f, -3.5f);

        CreateFlatQuad(debriefModalRoot.transform, "Dark_Dim", Vector3.zero, new Vector3(26f, 15f, 1f), new Color(0.01f, 0.02f, 0.05f, 0.92f), true, 60);

        float cardW = 15.5f;
        float cardH = 9.8f;
        CreateFlatQuad(debriefModalRoot.transform, "Card_Bg", Vector3.zero, new Vector3(cardW, cardH, 1f), DarkNavyBg, false, 62);

        CreateFlatQuad(debriefModalRoot.transform, "Border_T", new Vector3(0f,  cardH * 0.5f, -0.05f), new Vector3(cardW + 0.08f, 0.07f, 1f), NominalGreen, false, 63);
        CreateFlatQuad(debriefModalRoot.transform, "Border_B", new Vector3(0f, -cardH * 0.5f, -0.05f), new Vector3(cardW + 0.08f, 0.07f, 1f), NominalGreen, false, 63);
        CreateFlatQuad(debriefModalRoot.transform, "Border_L", new Vector3(-cardW * 0.5f, 0f, -0.05f), new Vector3(0.07f, cardH + 0.08f, 1f), NominalGreen, false, 63);
        CreateFlatQuad(debriefModalRoot.transform, "Border_R", new Vector3( cardW * 0.5f, 0f, -0.05f), new Vector3(0.07f, cardH + 0.08f, 1f), NominalGreen, false, 63);

        string targetName = (selectedTargetIndex == 0) ? "LOW EARTH ORBIT (LEO)" : (selectedTargetIndex == 1 ? "LUNAR POLAR ORBIT" : "MARS DEEP SPACE");

        CreateText(debriefModalRoot.transform, "Title", new Vector3(0f, 4.00f, -0.1f), "★ MISSION ACCOMPLISHED: ORBIT SECURED ★", 0.088f, 52, FontStyle.Bold, TextAnchor.MiddleCenter, NominalGreen, 65);
        CreateText(debriefModalRoot.transform, "Subtitle", new Vector3(0f, 3.25f, -0.1f), $"SAFE ARRIVAL AT {targetName} // 2M 30S TRANSIT COMPLETE", 0.062f, 36, FontStyle.Bold, TextAnchor.MiddleCenter, TelemetryGold, 65);

        string statsSummary =
            $"<color=#00E5FF><b>FLIGHT DURATION:</b></color>      <b>2 Minutes 30 Seconds</b>\n\n" +
            $"<color=#00E5FF><b>EVENTS SURVIVED:</b></color>      <b>{eventsSurvivedCount} NASA Critical Anomalies Resolved</b>\n\n" +
            $"<color=#00E5FF><b>REMAINING FUEL:</b></color>       <b>{currentFuelKg:F0} kg Propellant Margin</b>\n\n" +
            $"<color=#00E5FF><b>HULL INTEGRITY:</b></color>       <b>{currentHullIntegrity:F0}% Nominal Structural State</b>\n\n" +
            $"<color=#00E5FF><b>POWER RESERVES:</b></color>       <b>{currentPowerPct:F0}% Battery & Bus Health</b>\n\n" +
            $"<color=#FFD133><b>FINAL SCIENCE YIELD:</b></color>   <b>+{currentSciencePts:F0} Points Transmitted to Earth!</b>";

        CreateText(debriefModalRoot.transform, "Stats", new Vector3(-6.2f, 2.30f, -0.1f), statsSummary, 0.058f, 34, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, 65);

        // Return Button
        GameObject retBtn = new GameObject("BTN_Return_To_Assembly");
        retBtn.transform.SetParent(debriefModalRoot.transform, false);
        retBtn.transform.localPosition = new Vector3(0f, -3.50f, -0.1f);
        BoxCollider bc = retBtn.AddComponent<BoxCollider>();
        bc.size = new Vector3(8.0f, 1.0f, 2.0f);
        CreateFlatQuad(retBtn.transform, "Bg", Vector3.zero, new Vector3(8.0f, 1.0f, 1f), CyberCyan, false, 65);
        CreateText(retBtn.transform, "Label", new Vector3(0f, 0f, -0.05f), "✔ RETURN TO VEHICLE ASSEMBLY", 0.062f, 36, FontStyle.Bold, TextAnchor.MiddleCenter, Color.black, 66);
    }

    private void TriggerMissionFailure(string cause)
    {
        isCruiseActive = false;
        isDebriefOpen = true;

        if (debriefModalRoot != null) Destroy(debriefModalRoot);
        debriefModalRoot = new GameObject("Mission_Failure_Modal");
        debriefModalRoot.transform.SetParent(cruiseUIRoot.transform, false);
        debriefModalRoot.transform.localPosition = new Vector3(0f, 0.2f, -3.5f);

        CreateFlatQuad(debriefModalRoot.transform, "Dark_Dim", Vector3.zero, new Vector3(26f, 15f, 1f), new Color(0.05f, 0.01f, 0.01f, 0.94f), true, 60);

        float cardW = 15.5f;
        float cardH = 9.2f;
        CreateFlatQuad(debriefModalRoot.transform, "Card_Bg", Vector3.zero, new Vector3(cardW, cardH, 1f), DarkNavyBg, false, 62);
        CreateFlatQuad(debriefModalRoot.transform, "Border", Vector3.zero, new Vector3(cardW + 0.08f, cardH + 0.08f, 1f), HazardRed, false, 61);

        CreateText(debriefModalRoot.transform, "Title", new Vector3(0f, 3.50f, -0.1f), "✖ MISSION ABORT: VEHICLE LOST ✖", 0.088f, 52, FontStyle.Bold, TextAnchor.MiddleCenter, HazardRed, 65);
        CreateText(debriefModalRoot.transform, "Subtitle", new Vector3(0f, 2.65f, -0.1f), cause, 0.062f, 36, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, 65);

        string statsSummary =
            $"<color=#FF4B4B><b>FAILURE CAUSE:</b></color>        <b>Structural Integrity Critical Collapse</b>\n\n" +
            $"<color=#00E5FF><b>EVENTS SURVIVED:</b></color>      <b>{eventsSurvivedCount} Challenges Overcome</b>\n\n" +
            $"<color=#00E5FF><b>SCIENCE PRESERVED:</b></color>    <b>{currentSciencePts:F0} Points Transmitted to DSN</b>\n\n" +
            "Analyze engineering telemetry, re-balance rocket subsystem trade-offs, and attempt re-flight.";

        CreateText(debriefModalRoot.transform, "Stats", new Vector3(-6.2f, 1.50f, -0.1f), statsSummary, 0.058f, 34, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, 65);

        GameObject retBtn = new GameObject("BTN_Return_To_Assembly");
        retBtn.transform.SetParent(debriefModalRoot.transform, false);
        retBtn.transform.localPosition = new Vector3(0f, -3.10f, -0.1f);
        BoxCollider bc = retBtn.AddComponent<BoxCollider>();
        bc.size = new Vector3(8.0f, 1.0f, 2.0f);
        CreateFlatQuad(retBtn.transform, "Bg", Vector3.zero, new Vector3(8.0f, 1.0f, 1f), HazardRed, false, 65);
        CreateText(retBtn.transform, "Label", new Vector3(0f, 0f, -0.05f), "↺ RE-ENTER ASSEMBLY & RE-FLIGHT", 0.062f, 36, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, 66);
    }

    private void ReturnToAssembly()
    {
        StopCruise();
        SpaceMissionPrototype smp = FindAnyObjectByType<SpaceMissionPrototype>();
        if (smp != null)
        {
            smp.ResetToAssembly();
        }
    }

    // =========================================================================
    // 8. MOUSE & HUD INTERACTION
    // =========================================================================
    private void HandleMouseInteraction()
    {
        // Keyboard shortcuts for solutions: 1, 2, 3
        if (isEventModalOpen)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { HandleEventChoiceSelected(0); return; }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { HandleEventChoiceSelected(1); return; }
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { HandleEventChoiceSelected(2); return; }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) { SetTimeWarp(1.0f); return; }
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) { SetTimeWarp(2.0f); return; }
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) { SetTimeWarp(4.0f); return; }
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) { SetTimeWarp(8.0f); return; }
        }

        if (Camera.main == null) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                Transform cur = h.collider.transform;
                while (cur != null)
                {
                    string n = cur.gameObject.name;

                    // 1. Solution Choice Buttons
                    if (n.StartsWith("BTN_Choice_"))
                    {
                        if (int.TryParse(n.Substring("BTN_Choice_".Length), out int choiceIdx))
                        {
                            if (Input.GetMouseButtonDown(0))
                            {
                                HandleEventChoiceSelected(choiceIdx);
                                return;
                            }
                        }
                        break;
                    }

                    // 2. Return to Assembly Button
                    if (n == "BTN_Return_To_Assembly")
                    {
                        if (Input.GetMouseButtonDown(0))
                        {
                            PlaySfx(chimeClip, 0.8f);
                            ReturnToAssembly();
                            return;
                        }
                        break;
                    }

                    // 3. Time Warp Button
                    if (n == "BTN_WarpSpeed")
                    {
                        if (Input.GetMouseButtonDown(0))
                        {
                            CycleTimeWarp();
                            return;
                        }
                        break;
                    }

                    cur = cur.parent;
                }
            }
        }
    }

    // =========================================================================
    // 9. VISUAL HIERARCHY BUILDERS (WORLD & UI)
    // =========================================================================
    private void BuildCruiseMasterHierarchy()
    {
        if (cruiseMasterRoot != null) Destroy(cruiseMasterRoot);

        cruiseMasterRoot = new GameObject("Interplanetary_Cruise_Master");
        cruiseMasterRoot.transform.position = Vector3.zero;

        BuildLoadingScreenHierarchy();
        BuildWorldHierarchy();
        BuildUIHierarchy();
    }

    private void BuildLoadingScreenHierarchy()
    {
        loadingScreenRoot = new GameObject("Loading_Screen_Root");
        loadingScreenRoot.transform.SetParent(cruiseMasterRoot.transform, false);

        CreateFlatQuad(loadingScreenRoot.transform, "Bg", Vector3.zero, new Vector3(26f, 15f, 1f), new Color(0.015f, 0.030f, 0.065f, 1f), false, 90);

        GameObject radarObj = new GameObject("Radar_Ring");
        radarObj.transform.SetParent(loadingScreenRoot.transform, false);
        radarObj.transform.localPosition = new Vector3(0f, 1.4f, -0.1f);
        CreateFlatQuad(radarObj.transform, "Radar_Circle", Vector3.zero, new Vector3(2.5f, 2.5f, 1f), CyberCyan, false, 92);
        CreateFlatQuad(radarObj.transform, "Radar_H", Vector3.zero, new Vector3(3.2f, 0.05f, 1f), TelemetryGold, false, 93);
        CreateFlatQuad(radarObj.transform, "Radar_V", Vector3.zero, new Vector3(0.05f, 3.2f, 1f), TelemetryGold, false, 93);
        loadingRadarRing = radarObj.transform;

        CreateText(loadingScreenRoot.transform, "Title", new Vector3(0f, 3.7f, -0.1f), "★ NASA TRANS-PLANETARY INJECTION // CRUISE PHASE LOADING ★", 0.082f, 48, FontStyle.Bold, TextAnchor.MiddleCenter, CyberCyan, 95);
        CreateText(loadingScreenRoot.transform, "Sub", new Vector3(0f, 3.0f, -0.1f), "COMPUTING INTERPLANETARY TRANSFER TRAJECTORY VECTORS", 0.056f, 32, FontStyle.Bold, TextAnchor.MiddleCenter, TelemetryGold, 95);

        GameObject statObj = CreateText(loadingScreenRoot.transform, "Status", new Vector3(0f, -1.0f, -0.1f), "INITIALIZING SYSTEMS...", 0.058f, 34, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white, 95);
        loadingStatusText = statObj.GetComponent<TextMesh>();

        CreateFlatQuad(loadingScreenRoot.transform, "Bar_Bg", new Vector3(0f, -2.2f, 0f), new Vector3(9.3f, 0.42f, 1f), new Color(0.05f, 0.10f, 0.18f), false, 92);
        CreateFlatQuad(loadingScreenRoot.transform, "Bar_Border", new Vector3(0f, -2.2f, 0.02f), new Vector3(9.38f, 0.50f, 1f), CyberCyanDark, false, 91);
        GameObject fillObj = CreateFlatQuad(loadingScreenRoot.transform, "Bar_Fill", new Vector3(-4.6f, -2.2f, -0.05f), new Vector3(0.1f, 0.35f, 1f), NominalGreen, false, 94);
        loadingBarFill = fillObj.transform;
    }

    private void BuildWorldHierarchy()
    {
        cruiseWorldRoot = new GameObject("Cruise_World_Root");
        cruiseWorldRoot.transform.SetParent(cruiseMasterRoot.transform, false);

        // 1. Deep Space Panoramic Backdrop
        CreateFlatQuad(cruiseWorldRoot.transform, "Cruise_Space_Backdrop", Vector3.zero, new Vector3(26f, 15f, 1f), new Color(0.008f, 0.015f, 0.035f), false, 0);

        // 2. High-Speed Parallax Starfields & Cosmic Speed Streaks
        parallaxStarsFar.Clear();
        parallaxStarsNear.Clear();
        speedStreaks.Clear();
        activeSpaceObjects.Clear();

        System.Random rnd = new System.Random(777);
        for (int i = 0; i < 90; i++)
        {
            float rx = (float)(rnd.NextDouble() * 26.0 - 13.0);
            float ry = (float)(rnd.NextDouble() * 12.0 - 6.0);
            float sz = (float)(rnd.NextDouble() * 0.045 + 0.020);
            GameObject star = CreateFlatQuad(cruiseWorldRoot.transform, "StarFar_" + i, new Vector3(rx, ry, 3.5f), new Vector3(sz, sz, 1f), new Color(0.85f, 0.95f, 1.0f, 0.50f), false, 1);
            parallaxStarsFar.Add(star.transform);
        }

        for (int i = 0; i < 45; i++)
        {
            float rx = (float)(rnd.NextDouble() * 26.0 - 13.0);
            float ry = (float)(rnd.NextDouble() * 11.0 - 5.5);
            float sz = (float)(rnd.NextDouble() * 0.075 + 0.035);
            GameObject dust = CreateFlatQuad(cruiseWorldRoot.transform, "DustNear_" + i, new Vector3(rx, ry, 2.5f), new Vector3(sz, sz, 1f), CyberCyan, false, 2);
            parallaxStarsNear.Add(dust.transform);
        }

        // Horizontal Speed Streaks for feeling of interplanetary velocity
        for (int i = 0; i < 24; i++)
        {
            float rx = (float)(rnd.NextDouble() * 30.0 - 15.0);
            float ry = (float)(rnd.NextDouble() * 9.0 - 4.5);
            float streakLen = (float)(rnd.NextDouble() * 1.8 + 0.8);
            GameObject streak = CreateFlatQuad(cruiseWorldRoot.transform, "SpeedStreak_" + i, new Vector3(rx, ry, 2.0f), new Vector3(streakLen, 0.035f, 1f), new Color(0.40f, 0.85f, 1.0f, 0.35f), false, 2);
            speedStreaks.Add(streak.transform);
        }

        // 3. MOUNT THE PLAYER'S REAL ROCKET!
        MountPlayerRealRocket();

        // 4. Big Destination Planet (scales in on approach)
        BuildBigDestinationPlanet();
    }

    private void MountPlayerRealRocket()
    {
        if (realRocketTransform != null)
        {
            // Take the real rocket that the player built and mount it horizontally in the cruise view!
            realRocketTransform.SetParent(cruiseWorldRoot.transform, false);
            realRocketTransform.localPosition = new Vector3(-0.6f, 0.25f, -1.0f);
            realRocketTransform.localRotation = Quaternion.Euler(0f, 0f, -90f); // Point nose to the right!
            realRocketTransform.localScale = new Vector3(1.15f, 1.15f, 1f);
            realRocketTransform.gameObject.SetActive(true);

            // Ensure all rocket renderers are active and sorted above space background
            foreach (Renderer r in realRocketTransform.GetComponentsInChildren<Renderer>(true))
            {
                if (r != null)
                {
                    r.enabled = true;
                    r.sortingOrder = Mathf.Max(20, r.sortingOrder);
                }
            }
        }

        // Horizontal Engine Exhaust Plume attached directly to rear of real rocket
        flameSurgeGroup = new GameObject("RealRocket_Horizontal_Plume");
        flameSurgeGroup.transform.SetParent(cruiseWorldRoot.transform, false);
        flameSurgeGroup.transform.localPosition = new Vector3(-2.45f, 0.25f, -0.95f);

        GameObject flameObj = new GameObject("Flame_Pivot");
        flameObj.transform.SetParent(flameSurgeGroup.transform, false);

        flameOuter = CreateFlatQuad(flameObj.transform, "Flame_Outer", new Vector3(-0.90f, 0f, 0.02f), new Vector3(1.8f, 0.55f, 1f), new Color(0.1f, 0.55f, 1.0f, 0.75f), false, 18).transform;
        flameCore  = CreateFlatQuad(flameObj.transform, "Flame_Core",  new Vector3(-0.60f, 0f, -0.02f), new Vector3(1.2f, 0.30f, 1f), Color.white, false, 19).transform;
        flameSparks = CreateFlatQuad(flameObj.transform, "Flame_Sparks", new Vector3(-1.8f, 0f, 0.05f), new Vector3(2.4f, 0.85f, 1f), new Color(1f, 0.6f, 0.1f, 0.85f), false, 17).transform;
        flameSparks.gameObject.SetActive(false);
        mainEngineFlame = flameObj.transform;

        // 4 Lateral Cold Gas RCS Jet Puffs
        rcsN = CreateFlatQuad(cruiseWorldRoot.transform, "RCS_N", new Vector3(0.5f,  1.10f, -0.9f), new Vector3(0.30f, 0.75f, 1f), new Color(1f, 1f, 1f, 0.90f), false, 25).transform;
        rcsS = CreateFlatQuad(cruiseWorldRoot.transform, "RCS_S", new Vector3(0.5f, -0.60f, -0.9f), new Vector3(0.30f, 0.75f, 1f), new Color(1f, 1f, 1f, 0.90f), false, 25).transform;
        rcsE = CreateFlatQuad(cruiseWorldRoot.transform, "RCS_E", new Vector3(1.8f,  0.25f, -0.9f), new Vector3(0.75f, 0.25f, 1f), new Color(1f, 1f, 1f, 0.90f), false, 25).transform;
        rcsW = CreateFlatQuad(cruiseWorldRoot.transform, "RCS_W", new Vector3(-1.8f, 0.25f, -0.9f), new Vector3(0.75f, 0.25f, 1f), new Color(1f, 1f, 1f, 0.90f), false, 25).transform;
        rcsN.gameObject.SetActive(false);
        rcsS.gameObject.SetActive(false);
        rcsE.gameObject.SetActive(false);
        rcsW.gameObject.SetActive(false);

        // Power Standby Blue Energy Shield
        bluePowerShield = CreateFlatQuad(cruiseWorldRoot.transform, "Power_Standby_Shield", new Vector3(-0.6f, 0.25f, -0.95f), new Vector3(4.5f, 2.4f, 1f), new Color(0.0f, 0.70f, 1.0f, 0.40f), false, 28).transform;
        bluePowerShield.gameObject.SetActive(false);

        // Green Life Support Expanding Seal Ring
        greenSealRing = CreateFlatQuad(cruiseWorldRoot.transform, "LifeSupport_Seal_Ring", new Vector3(-0.6f, 0.25f, -0.95f), new Vector3(1f, 1f, 1f), new Color(0.18f, 0.850f, 0.45f, 0.50f), false, 28).transform;
        greenSealRing.gameObject.SetActive(false);

        // Fullscreen scanline overlay for computer reboot effect
        scanlineOverlay = CreateFlatQuad(cruiseWorldRoot.transform, "Avionics_Scanline_Overlay", new Vector3(0f, 0f, -2.5f), new Vector3(26f, 15f, 1f), new Color(0f, 0.95f, 1f, 0.20f), false, 40);
        scanlineOverlay.SetActive(false);

        // Floating Callout Numbers (+35 Sci, -30kg Fuel, etc.)
        GameObject fObj = new GameObject("Floating_Callout_Root");
        fObj.transform.SetParent(cruiseWorldRoot.transform, false);
        fObj.transform.localPosition = new Vector3(-0.6f, 1.4f, -2.5f);
        GameObject fText = CreateText(fObj.transform, "Callout_Text", Vector3.zero, "", 0.080f, 48, FontStyle.Bold, TextAnchor.MiddleCenter, TelemetryGold, 45);
        floatingCalloutText = fText.GetComponent<TextMesh>();
        floatingCalloutRoot = fObj.transform;
        fObj.SetActive(false);
    }

    private void BuildBigDestinationPlanet()
    {
        GameObject planetObj = new GameObject("Big_Destination_Planet");
        planetObj.transform.SetParent(cruiseWorldRoot.transform, false);
        planetObj.transform.localPosition = new Vector3(13.0f, 0.2f, 2.0f);
        planetObj.transform.localScale = new Vector3(3.5f, 3.5f, 1f);

        Color pCol = (selectedTargetIndex == 0) ? new Color(0.15f, 0.45f, 0.85f) : (selectedTargetIndex == 1 ? new Color(0.75f, 0.78f, 0.82f) : new Color(0.88f, 0.38f, 0.18f));
        CreateFlatQuad(planetObj.transform, "Sphere", Vector3.zero, Vector3.one, pCol, false, 5);
        CreateFlatQuad(planetObj.transform, "Halo", new Vector3(0f, 0f, 0.05f), new Vector3(1.18f, 1.18f, 1f), new Color(pCol.r, pCol.g, pCol.b, 0.45f), false, 4);

        bigDestinationPlanet = planetObj.transform;
        bigDestinationPlanet.gameObject.SetActive(false);
    }

    private void BuildUIHierarchy()
    {
        cruiseUIRoot = new GameObject("Cruise_UI_Root");
        cruiseUIRoot.transform.SetParent(cruiseMasterRoot.transform, false);

        // 1. Top Mission Telemetry Bar (Large fonts)
        GameObject topBar = new GameObject("Top_Telemetry_Bar");
        topBar.transform.SetParent(cruiseUIRoot.transform, false);
        topBar.transform.localPosition = new Vector3(0f, 4.45f, -2.0f);

        CreateFlatQuad(topBar.transform, "Top_Bar_Bg", Vector3.zero, new Vector3(16.0f, 0.95f, 1f), DarkNavyBg, false, 30);
        CreateFlatQuad(topBar.transform, "Top_Bar_Border", new Vector3(0f, -0.47f, -0.02f), new Vector3(16.0f, 0.04f, 1f), CyberCyanDark, false, 31);

        string targetName = (selectedTargetIndex == 0) ? "LOW EARTH ORBIT (LEO)" : (selectedTargetIndex == 1 ? "LUNAR POLAR ORBIT" : "MARS DEEP SPACE");
        CreateText(topBar.transform, "Mission_Header", new Vector3(-7.60f, 0.12f, -0.05f), $"MISSION: NASA ODYSSEY // TARGET: <color=#00E5FF>{targetName}</color>", 0.058f, 34, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, 32);

        GameObject hudFuel = CreateText(topBar.transform, "HUD_Fuel", new Vector3(1.2f, 0.12f, -0.05f), "FUEL: 350 kg", 0.056f, 32, FontStyle.Bold, TextAnchor.MiddleLeft, NominalGreen, 32);
        hudFuelText = hudFuel.GetComponent<TextMesh>();

        GameObject hudHull = CreateText(topBar.transform, "HUD_Hull", new Vector3(3.8f, 0.12f, -0.05f), "HULL: 100%", 0.056f, 32, FontStyle.Bold, TextAnchor.MiddleLeft, NominalGreen, 32);
        hudHullText = hudHull.GetComponent<TextMesh>();

        GameObject hudPower = CreateText(topBar.transform, "HUD_Power", new Vector3(6.0f, 0.12f, -0.05f), "POWER: 100%", 0.056f, 32, FontStyle.Bold, TextAnchor.MiddleLeft, NominalGreen, 32);
        hudPowerText = hudPower.GetComponent<TextMesh>();

        // 2. Large Bottom Progress Bar Tray
        GameObject tray = new GameObject("Bottom_Progress_Tray");
        tray.transform.SetParent(cruiseUIRoot.transform, false);
        tray.transform.localPosition = new Vector3(0f, -4.15f, -2.0f);

        CreateFlatQuad(tray.transform, "Tray_Bg", Vector3.zero, new Vector3(16.0f, 1.45f, 1f), DarkNavyBg, false, 30);
        CreateFlatQuad(tray.transform, "Tray_Border", new Vector3(0f, 0.72f, -0.02f), new Vector3(16.0f, 0.04f, 1f), CyberCyanDark, false, 31);

        // Origin Planet (Earth on Left)
        CreateFlatQuad(tray.transform, "Earth_Icon", new Vector3(-7.0f, 0f, -0.05f), new Vector3(0.95f, 0.95f, 1f), new Color(0.18f, 0.55f, 0.95f), false, 33);
        CreateText(tray.transform, "Earth_Label", new Vector3(-7.0f, -0.56f, -0.05f), "EARTH\n(T+0:00)", 0.042f, 24, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, 34);

        // Destination Planet (Right)
        Color pCol = (selectedTargetIndex == 0) ? new Color(0.15f, 0.45f, 0.85f) : (selectedTargetIndex == 1 ? new Color(0.75f, 0.78f, 0.82f) : new Color(0.88f, 0.38f, 0.18f));
        destPlanetProgressIcon = CreateFlatQuad(tray.transform, "Dest_Icon", new Vector3(7.0f, 0f, -0.05f), new Vector3(0.95f, 0.95f, 1f), pCol, false, 33).transform;
        string destName = (selectedTargetIndex == 0) ? "LEO" : (selectedTargetIndex == 1 ? "MOON" : "MARS");
        CreateText(tray.transform, "Dest_Label", new Vector3(7.0f, -0.56f, -0.05f), $"{destName}\n(100%)", 0.042f, 24, FontStyle.Bold, TextAnchor.MiddleCenter, TelemetryGold, 34);

        // Trajectory Guide Line
        CreateFlatQuad(tray.transform, "Track_Base", new Vector3(0f, 0f, 0.02f), new Vector3(12.0f, 0.08f, 1f), new Color(0.15f, 0.25f, 0.38f), false, 31);
        GameObject fillBar = CreateFlatQuad(tray.transform, "Track_Fill", new Vector3(-5.8f, 0f, -0.02f), new Vector3(0.1f, 0.14f, 1f), CyberCyan, false, 32);
        progressBarFill = fillBar.transform;

        // Moving Mini Rocket Icon
        GameObject mRk = new GameObject("Mini_Rocket_Icon");
        mRk.transform.SetParent(tray.transform, false);
        mRk.transform.localPosition = new Vector3(-5.8f, 0f, -0.08f);
        CreateFlatQuad(mRk.transform, "Rk_Body", Vector3.zero, new Vector3(0.55f, 0.28f, 1f), Color.white, false, 36);
        CreateFlatQuad(mRk.transform, "Rk_Nose", new Vector3(0.32f, 0f, -0.01f), new Vector3(0.18f, 0.20f, 1f), CyberCyan, false, 37);
        CreateFlatQuad(mRk.transform, "Rk_Flame", new Vector3(-0.36f, 0f, -0.01f), new Vector3(0.28f, 0.16f, 1f), TelemetryGold, false, 35);
        miniRocketIcon = mRk.transform;

        // Milestone Ticks (25%, 50%, 75%)
        float[] tickFracs = { 0.25f, 0.50f, 0.75f };
        for (int i = 0; i < tickFracs.Length; i++)
        {
            float tx = Mathf.Lerp(-5.8f, 5.8f, tickFracs[i]);
            CreateFlatQuad(tray.transform, "Tick_" + i, new Vector3(tx, 0f, -0.01f), new Vector3(0.05f, 0.26f, 1f), TelemetryGold, false, 32);
            CreateText(tray.transform, "Tick_Text_" + i, new Vector3(tx, 0.25f, -0.05f), $"{tickFracs[i] * 100f:F0}%", 0.038f, 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.gray, 33);
        }

        // HUD Readouts in Tray (Large fonts)
        GameObject timeObj = CreateText(tray.transform, "HUD_Time", new Vector3(-5.8f, 0.48f, -0.05f), "TIME: 00:00 / 02:00", 0.052f, 30, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white, 34);
        hudTimeText = timeObj.GetComponent<TextMesh>();

        GameObject distObj = CreateText(tray.transform, "HUD_Distance", new Vector3(-0.8f, 0.48f, -0.05f), "DISTANCE: 0 / 384,400 km", 0.052f, 30, FontStyle.Bold, TextAnchor.MiddleLeft, CyberCyan, 34);
        hudDistanceText = distObj.GetComponent<TextMesh>();

        GameObject sciObj = CreateText(tray.transform, "HUD_Science", new Vector3(3.8f, 0.48f, -0.05f), "SCI YIELD: 0 pts", 0.052f, 30, FontStyle.Bold, TextAnchor.MiddleLeft, TelemetryGold, 34);
        hudScienceText = sciObj.GetComponent<TextMesh>();

        // Large Warp Speed Button (1X / 2X / 4X / 8X)
        GameObject speedBtn = new GameObject("BTN_WarpSpeed");
        speedBtn.transform.SetParent(tray.transform, false);
        speedBtn.transform.localPosition = new Vector3(6.8f, 0.48f, -0.05f);
        BoxCollider sbc = speedBtn.AddComponent<BoxCollider>();
        sbc.size = new Vector3(2.1f, 0.52f, 1.0f);
        CreateFlatQuad(speedBtn.transform, "Speed_Bg", Vector3.zero, new Vector3(2.1f, 0.52f, 1f), CyberCyanDark, false, 34);
        GameObject sText = CreateText(speedBtn.transform, "Speed_Text", new Vector3(0f, 0f, -0.02f), "WARP: 1X", 0.046f, 26, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, 35);
        hudSpeedButtonText = sText.GetComponent<TextMesh>();

        // 3. Floating Toast Banner
        BuildToastBanner();
    }

    private void BuildToastBanner()
    {
        statusToastBanner = new GameObject("Cruise_Toast_Banner");
        statusToastBanner.transform.SetParent(cruiseUIRoot.transform, false);
        statusToastBanner.transform.localPosition = new Vector3(0f, 3.35f, -2.5f);

        CreateFlatQuad(statusToastBanner.transform, "Toast_Bg", Vector3.zero, new Vector3(13.5f, 0.75f, 1f), new Color(0.02f, 0.06f, 0.12f, 0.94f), false, 45);
        CreateFlatQuad(statusToastBanner.transform, "Toast_Border", new Vector3(0f, 0f, 0.02f), new Vector3(13.58f, 0.83f, 1f), CyberCyan, false, 44);

        GameObject tObj = CreateText(statusToastBanner.transform, "Toast_Text", new Vector3(0f, 0f, -0.05f), "", 0.056f, 32, FontStyle.Bold, TextAnchor.MiddleCenter, NominalGreen, 46);
        statusToastText = tObj.GetComponent<TextMesh>();

        statusToastBanner.SetActive(false);
    }

    private void ShowToast(string message, Color color)
    {
        if (statusToastBanner != null && statusToastText != null)
        {
            statusToastText.text = message;
            statusToastText.color = color;
            statusToastBanner.SetActive(true);
            toastTimer = 4.0f;
        }
    }

    private void UpdateToastBanner(float dt)
    {
        if (toastTimer > 0f)
        {
            toastTimer -= dt;
            if (toastTimer <= 0f && statusToastBanner != null)
            {
                statusToastBanner.SetActive(false);
            }
        }
    }

    private void TriggerCameraShake(float magnitude, float duration)
    {
        cameraShakeMagnitude = magnitude;
        cameraShakeDuration = duration;
    }

    private void UpdateCameraShake()
    {
        if (cameraShakeDuration > 0f && Camera.main != null)
        {
            cameraShakeDuration -= Time.deltaTime;
            float rx = (UnityEngine.Random.value * 2f - 1f) * cameraShakeMagnitude;
            float ry = (UnityEngine.Random.value * 2f - 1f) * cameraShakeMagnitude;
            Camera.main.transform.position = originalCameraPos + new Vector3(rx, ry, 0f);
        }
        else if (Camera.main != null && originalCameraPos != Vector3.zero)
        {
            Camera.main.transform.position = originalCameraPos;
        }
    }

    // =========================================================================
    // 10. PROCEDURAL AUDIO GENERATION
    // =========================================================================
    private void InitProceduralAudio()
    {
        audioSourceSFX = gameObject.AddComponent<AudioSource>();
        audioSourceSFX.playOnAwake = false;

        audioSourceRumble = gameObject.AddComponent<AudioSource>();
        audioSourceRumble.playOnAwake = false;
        audioSourceRumble.loop = true;
        audioSourceRumble.volume = 0.40f;

        chimeClip = CreateNASAChimeClip();
        alertKlaxonClip = CreateAlertKlaxonClip();
        thrusterRoarClip = CreateThrusterRoarClip();
        rcsPuffClip = CreateRcsPuffClip();
        rebootBeepClip = CreateComputerBeepClip();
        gasHissClip = CreateGasHissClip();
        victoryClip = CreateVictoryFanfareClip();
    }

    private void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (audioSourceSFX != null && clip != null)
        {
            audioSourceSFX.PlayOneShot(clip, volume);
        }
    }

    private AudioClip CreateNASAChimeClip()
    {
        int rate = 44100;
        float dur = 0.45f;
        int count = (int)(rate * dur);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t * 6f);
            float f = (t < 0.12f) ? 587.33f : 880.00f; // D5 -> A5 Chime
            samples[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.40f;
        }
        AudioClip c = AudioClip.Create("NASAChime", count, 1, rate, false);
        c.SetData(samples, 0);
        return c;
    }

    private AudioClip CreateAlertKlaxonClip()
    {
        int rate = 44100;
        float dur = 0.90f;
        int count = (int)(rate * dur);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float sweep = Mathf.PingPong(t * 12f, 1f);
            float f = Mathf.Lerp(440f, 880f, sweep);
            float env = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            samples[i] = (Mathf.Sin(2f * Mathf.PI * f * t) > 0f ? 0.40f : -0.40f) * env;
        }
        AudioClip c = AudioClip.Create("AlertKlaxon", count, 1, rate, false);
        c.SetData(samples, 0);
        return c;
    }

    private AudioClip CreateThrusterRoarClip()
    {
        int rate = 44100;
        float dur = 2.2f;
        int count = (int)(rate * dur);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float lowRumble = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.50f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.30f;
            float hiss = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
            float env = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            samples[i] = (lowRumble + hiss) * env * 0.65f;
        }
        AudioClip c = AudioClip.Create("ThrusterRoar", count, 1, rate, false);
        c.SetData(samples, 0);
        return c;
    }

    private AudioClip CreateRcsPuffClip()
    {
        int rate = 44100;
        float dur = 0.35f;
        int count = (int)(rate * dur);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t * 16f);
            float hiss = (UnityEngine.Random.value * 2f - 1f) * env * 0.45f;
            samples[i] = hiss;
        }
        AudioClip c = AudioClip.Create("RcsPuff", count, 1, rate, false);
        c.SetData(samples, 0);
        return c;
    }

    private AudioClip CreateComputerBeepClip()
    {
        int rate = 44100;
        float dur = 0.28f;
        int count = (int)(rate * dur);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float f = (t < 0.14f) ? 1200f : 1600f;
            float env = Mathf.Exp(-t * 12f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.35f;
        }
        AudioClip c = AudioClip.Create("ComputerBeep", count, 1, rate, false);
        c.SetData(samples, 0);
        return c;
    }

    private AudioClip CreateGasHissClip()
    {
        int rate = 44100;
        float dur = 0.75f;
        int count = (int)(rate * dur);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
            float noise = (UnityEngine.Random.value * 2f - 1f) * env * 0.30f;
            samples[i] = noise;
        }
        AudioClip c = AudioClip.Create("GasHiss", count, 1, rate, false);
        c.SetData(samples, 0);
        return c;
    }

    private AudioClip CreateVictoryFanfareClip()
    {
        int rate = 44100;
        float dur = 1.9f;
        int count = (int)(rate * dur);
        float[] samples = new float[count];
        float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f };
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            int n = Mathf.Clamp((int)(t * 2.5f), 0, freqs.Length - 1);
            float env = Mathf.Exp(-(t % 0.40f) * 4f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * env * 0.45f;
        }
        AudioClip c = AudioClip.Create("VictoryFanfare", count, 1, rate, false);
        c.SetData(samples, 0);
        return c;
    }

    // =========================================================================
    // 11. 14 NASA HISTORICAL RESEARCH EVENTS FROM ATTACHED PDF
    // =========================================================================
    private void Initialize14NASAEvents()
    {
        allEventsPool.Clear();

        // 1. Apollo 11: Program Alarms 1201 & 1202
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_Apollo11_ComputerAlarm",
            missionSource = "NASA APOLLO 11 HISTORICAL RESEARCH (JULY 1969)",
            eventTitle = "PROGRAM ALARMS 1201 & 1202: CPU OVERFLOW",
            description = "Radar hardware is flooding the Apollo Guidance Computer with spurious interrupts,\ntriggering 1201/1202 executive overflow alarms! Critical descent guidance tasks risk being dropped.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "BYPASS RENDEZVOUS RADAR & FLUSH QUEUE",
                    prosText = "Restores 100% CPU cycles immediately; zero propellant consumed.",
                    consText = "Temporarily blinds secondary rendezvous radar sensors (-10 Science).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = 0f, scienceDelta = -10f,
                    animType = CruiseAnimationType.AvionicsReboot,
                    resolutionMessage = "Radar task purged! Guidance computer load normalized to 12%."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "SWITCH TO MANUAL PILOT ATTITUDE HOLD",
                    prosText = "Commander takes manual stick; preserves full scientific telemetry (+25 Sci).",
                    consText = "Consumes manual RCS attitude hold propellant (-35kg Fuel).",
                    fuelDelta = -35f, hullDelta = 0f, powerDelta = 0f, scienceDelta = 25f,
                    animType = CruiseAnimationType.RcsPuffs,
                    resolutionMessage = "Manual control assumed! Attitude held steady on descent corridor."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "OVERCLOCK PROCESSOR WITH AUXILIARY BUS",
                    prosText = "Processes all pending radar tasks while maintaining computer guidance.",
                    consText = "High electrical surge strains avionics bus (-15% Power, -5% Hull).",
                    fuelDelta = 0f, hullDelta = -5f, powerDelta = -15f, scienceDelta = 10f,
                    animType = CruiseAnimationType.FlameSurge,
                    resolutionMessage = "Auxiliary power routed! Core processor cleared computational backlog."
                }
            }
        });

        // 2. Apollo 13: Cryogenic Oxygen Tank No. 2 Rupture
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_Apollo13_OxygenTank",
            missionSource = "NASA APOLLO 13 ANOMALY REPORT (APRIL 1970)",
            eventTitle = "CRYO OXYGEN TANK NO. 2 RUPTURE & BUS POWER COLLAPSE",
            description = "Oxygen Tank No. 2 inside the Service Module has ruptured violently! Electrical power generation,\nfuel cells, and consumable oxygen supplies are rapidly bleeding into deep space.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "ISOLATE SERVICE MODULE & USE LM LIFEBOAT",
                    prosText = "Guarantees crew survival; preserves Command Module entry batteries at 100%.",
                    consText = "Must rely exclusively on low-thrust Lunar Module descent engine (-15 Sci).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = 0f, scienceDelta = -15f,
                    animType = CruiseAnimationType.PowerStandby,
                    resolutionMessage = "Isolation valves sealed! Lunar Module lifeboat sustained transit power."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "OVER-PRESSURIZE AUXILIARY O2 INTO FUEL CELLS",
                    prosText = "Restores immediate electrical bus power and cabin heating (+20% Power).",
                    consText = "Burns through critical reactant mass; high stress on plumbing (-50kg Fuel, -10% Hull).",
                    fuelDelta = -50f, hullDelta = -10f, powerDelta = 20f, scienceDelta = 0f,
                    animType = CruiseAnimationType.FlameSurge,
                    resolutionMessage = "Auxiliary O2 routed! Fuel cells online, but reserves heavily depleted."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "DEEP POWER-DOWN & DRIFT FREE-RETURN LOOP",
                    prosText = "Saves 85% of remaining battery power; gravity slingshot guarantees return.",
                    consText = "Cabin temperature drops to freezing 3°C; heavy moisture condensation (-10 Sci).",
                    fuelDelta = 0f, hullDelta = -5f, powerDelta = 10f, scienceDelta = -10f,
                    animType = CruiseAnimationType.ThermalRoll,
                    resolutionMessage = "Non-essential systems shut down! Spacecraft drifting on free-return loop."
                }
            }
        });

        // 3. Apollo 13: Incompatible LiOH CO2 Canisters
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_Apollo13_CO2_Scrubber",
            missionSource = "NASA APOLLO 13 CARBON DIOXIDE TECHNICAL BRIEF",
            eventTitle = "CABIN CO2 CRITICALLY RISING: LiOH INCOMPATIBILITY",
            description = "Lunar Module air scrubbers are saturated with 3 astronauts aboard! Carbon dioxide is passing\n15 mmHg. Square LiOH canisters from the Command Module do not fit round LM sockets.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "IMPROVISE ADAPTER (BAGS, DUCT TAPE & HOSE)",
                    prosText = "Authentic NASA field innovation cleans cabin air to 1 mmHg safely for zero cost!",
                    consText = "Takes 25 minutes of assembly time (-5 Science).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = 0f, scienceDelta = -5f,
                    animType = CruiseAnimationType.LifeSupportHiss,
                    resolutionMessage = "The 'Mailbox' adapter constructed! CO2 scrubbed back to nominal 1 mmHg."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "PURGE CABIN ATMOSPHERE WITH REACTION GAS",
                    prosText = "Instant 100% flush of carbon dioxide from cabin volume.",
                    consText = "Vents valuable pressurized gas mass into space (-40kg Fuel).",
                    fuelDelta = -40f, hullDelta = 0f, powerDelta = 0f, scienceDelta = 0f,
                    animType = CruiseAnimationType.RcsPuffs,
                    resolutionMessage = "Cabin atmosphere purged! Gas vented overboard into deep vacuum."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "OVERDRIVE BACKUP CATALYTIC REGENERATOR",
                    prosText = "Hands-off automated scrubber loop; astronauts remain rested.",
                    consText = "High continuous electrical heating drains battery bank (-20% Power).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = -20f, scienceDelta = 10f,
                    animType = CruiseAnimationType.PowerStandby,
                    resolutionMessage = "Catalytic loop engaged! Air scrubbed at high electrical power expense."
                }
            }
        });

        // 4. Apollo 11: Landing Site Boulder Field
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_Apollo11_LandingSite",
            missionSource = "NASA APOLLO 11 LUNAR SURFACE JOURNAL",
            eventTitle = "BOULDER FIELD HAZARD: WEST CRATER TERRAIN",
            description = "Automated descent trajectory is heading straight into an uncharted crater strewn with\ncar-sized boulders! Touchdown on this terrain will tip or collapse the lunar module.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "MANUAL PITCH-OVER & FLY OVER CRATER FIELD",
                    prosText = "Pristine smooth touchdown on safe regolith; avoids fatal gear collapse (+30 Sci).",
                    consText = "Consumes deep reserve propellant down to low fuel buzzer (-60kg Fuel).",
                    fuelDelta = -60f, hullDelta = 0f, powerDelta = 0f, scienceDelta = 30f,
                    animType = CruiseAnimationType.FlameSurge,
                    resolutionMessage = "Armstrong flew past the boulder field! Safe touchdown site secured."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "ENGAGE LASER SCANNER & AUTO LOCAL DIVERT",
                    prosText = "Guidance algorithm identifies 10m flat pocket with moderate fuel burn (-25kg Fuel).",
                    consText = "High computation and lidar pulse load strains avionics bus (-10% Power).",
                    fuelDelta = -25f, hullDelta = 0f, powerDelta = -10f, scienceDelta = 15f,
                    animType = CruiseAnimationType.AvionicsReboot,
                    resolutionMessage = "Lidar terrain scan complete! Descent trajectory diverted to smooth clearing."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "TOUCHDOWN ON CRATER RIM WITH DAMPED LEGS",
                    prosText = "Zero extra fuel expended; saves 100% of propellant for ascent stage.",
                    consText = "Heavy boulder impact shocks landing struts and engine bells (-22% Hull).",
                    fuelDelta = 0f, hullDelta = -22f, powerDelta = 0f, scienceDelta = 0f,
                    animType = CruiseAnimationType.RcsPuffs,
                    resolutionMessage = "Crater rim touchdown absorbed! Landing struts suffered heavy compression."
                }
            }
        });

        // 5. Artemis I: Avcoat Heat Shield Char Loss
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_ArtemisI_HeatShield",
            missionSource = "NASA ARTEMIS I POST-FLIGHT ENGINEERING STUDY",
            eventTitle = "AVCOAT HEAT SHIELD EROSION & ABLATIVE CHAR ANOMALY",
            description = "Telemetry sensors report unexpected localized char loss and erosion across Avcoat ablative\ntiles. Lunar return entry velocity (11 km/s) threatens severe thermal boundary breach.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "SHAPE SKIP-ENTRY TRAJECTORY TO BLEED HEAT",
                    prosText = "Artemis skip-entry cuts peak thermal flux by 40%, safeguarding tiles (+25 Sci).",
                    consText = "Requires precision RCS burns and aerodynamic corridor shaping (-30kg Fuel).",
                    fuelDelta = -30f, hullDelta = 0f, powerDelta = 0f, scienceDelta = 25f,
                    animType = CruiseAnimationType.FlameSurge,
                    resolutionMessage = "Skip-entry maneuver executed! Spacecraft bled kinetic energy in upper atmosphere."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "DEPLOY MULTI-LAYER THERMAL SHROUD BLANKET",
                    prosText = "Forms a secondary radiative barrier across vulnerable heat shield seams.",
                    consText = "Jettisons forward secondary sensor package (-15 Science, -10% Power).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = -10f, scienceDelta = -15f,
                    animType = CruiseAnimationType.RcsPuffs,
                    resolutionMessage = "Thermal blanket deployed! Exposed seams covered before high-heat entry."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "HOLD BALLISTIC TRAJECTORY & PULSE COLD GAS",
                    prosText = "Maintains exact pinpoint ballistic recovery corridor.",
                    consText = "High thermal soak degrades outer honeycomb composite matrix (-20% Hull).",
                    fuelDelta = 0f, hullDelta = -20f, powerDelta = 0f, scienceDelta = 5f,
                    animType = CruiseAnimationType.ThermalRoll,
                    resolutionMessage = "Ballistic profile held! Boundary cold gas pulsed, but tile charring observed."
                }
            }
        });

        // 6. Deep Space Network: Communication Dropout
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_DSN_CommunicationDropout",
            missionSource = "NASA SPACE COMMUNICATIONS & DSN SPECIFICATION",
            eventTitle = "DEEP SPACE NETWORK (DSN) OCCULTATION & PACKET LOSS",
            description = "Interplanetary transit distance and celestial occultation sever direct radio line-of-sight.\nReal-time telemetry uplink to Earth ground stations is interrupted during a trajectory update.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "ENGAGE DELAY/DISRUPTION TOLERANT NETWORKING",
                    prosText = "DTN store-and-forward architecture buffers all scientific packets (+25 Sci).",
                    consText = "Fills onboard solid-state memory buffer; delays real-time confirmation.",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = 0f, scienceDelta = 25f,
                    animType = CruiseAnimationType.AvionicsReboot,
                    resolutionMessage = "DTN protocols active! All mission data buffered for delayed burst transmission."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "BOOST HIGH-GAIN DISH TRANSMITTER TO 100W",
                    prosText = "Amplified high-gain beam pierces cosmic noise; maintains direct link.",
                    consText = "Continuous transmitter power consumption drains battery bank (-20% Power).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = -20f, scienceDelta = 5f,
                    animType = CruiseAnimationType.PowerStandby,
                    resolutionMessage = "Transmitter boosted to 100W! Telemetry signal locked onto Goldstone station."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "REORIENT DISH VIA CONTINUOUS RCS SLEW",
                    prosText = "Steers parabolic dish to track relay satellite constellation.",
                    consText = "Attitude thruster firings burn reaction control fuel (-25kg Fuel).",
                    fuelDelta = -25f, hullDelta = 0f, powerDelta = 0f, scienceDelta = 10f,
                    animType = CruiseAnimationType.RcsPuffs,
                    resolutionMessage = "Antenna slewed toward orbital relay! Ground tracking link restored."
                }
            }
        });

        // 7. Space Radiation: Coronal Mass Ejection (CME)
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_Radiation_CME",
            missionSource = "NASA SPACE RADIATION & HELIOPHYSICS RESEARCH",
            eventTitle = "CORONAL MASS EJECTION: HIGH-ENERGY PROTON WAVE",
            description = "Spacecraft dosimeters detect an incoming high-energy Solar Particle Event (SPE) from an\nX-class solar flare! Ionizing radiation threatens single-event upsets (bit flips) and hull ionization.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "ORIENT ENGINE BULKHEAD TOWARD SUN (STORM BUNKER)",
                    prosText = "Heavy rocket engine bells and propellant mass absorb 90% of ionizing flux.",
                    consText = "Solar wings must be rotated out of sunlight into total eclipse (-25% Power).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = -25f, scienceDelta = 10f,
                    animType = CruiseAnimationType.ThermalRoll,
                    resolutionMessage = "Spacecraft bunkered! Engine mass shielded sensitive avionics from proton blast."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "ENGAGE TRIPLE-MODULAR REDUNDANT VOTING LOGIC",
                    prosText = "TMR rad-hard microprocessors auto-correct bit flips in flight memory (+20 Sci).",
                    consText = "Heavy computing load raises chip temperatures, causing fatigue (-12% Power, -5% Hull).",
                    fuelDelta = 0f, hullDelta = -5f, powerDelta = -12f, scienceDelta = 20f,
                    animType = CruiseAnimationType.AvionicsReboot,
                    resolutionMessage = "TMR voting logic engaged! Memory bit-flips intercepted and neutralized."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "EXECUTE LATERAL DOG-LEG EVASION BURN",
                    prosText = "Moves spacecraft outside central particle corridor, protecting solar cells.",
                    consText = "Substantial Delta-V expenditure to modify heliocentric orbital plane (-55kg Fuel).",
                    fuelDelta = -55f, hullDelta = 0f, powerDelta = 0f, scienceDelta = 0f,
                    animType = CruiseAnimationType.FlameSurge,
                    resolutionMessage = "Dog-leg evasion burn complete! Trajectory shifted clear of the coronal core."
                }
            }
        });

        // 8. Mars Global Dust Storm (The Opportunity Event)
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_Mars_DustStorm",
            missionSource = "NASA OPPORTUNITY & VIKING 1 HISTORICAL ANALYSIS",
            eventTitle = "PLANET-ENCIRCLING MARTIAN DUST STORM (85% FLUX LOSS)",
            description = "Approaching the target planet, atmospheric dust opacity (tau) skyrockets! Fine red dust coats\nphotovoltaic arrays, dropping available solar generation by 85% into critical blackout.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "SWITCH TO MMRTG NUCLEAR CORE & POWER CONSERVE",
                    prosText = "Plutonium-238 decay provides steady 110W power immune to storms and dust!",
                    consText = "Must power down external high-draw optical spectrometer sensors (-15 Sci).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = 0f, scienceDelta = -15f,
                    animType = CruiseAnimationType.PowerStandby,
                    resolutionMessage = "MMRTG nuclear bus active! Continuous electrical and thermal power secured."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "BLOW PANELS CLEAN WITH NITROGEN GAS JETS",
                    prosText = "Clears dust off photovoltaic surfaces; restores 65% of solar power output.",
                    consText = "Consumes pressurized cold-gas RCS mass reserves (-30kg Fuel).",
                    fuelDelta = -30f, hullDelta = 0f, powerDelta = 15f, scienceDelta = 5f,
                    animType = CruiseAnimationType.RcsPuffs,
                    resolutionMessage = "Nitrogen gas burst fired! Clinging regolith dust blown clear of solar arrays."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "DRAIN BATTERIES & ACCELERATE FLIGHT INSERTION",
                    prosText = "Pushes through dust cloud quickly; keeps all science spectrometers online (+15 Sci).",
                    consText = "Deep battery discharge strains cell longevity below nominal cutoff (-30% Power).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = -30f, scienceDelta = 15f,
                    animType = CruiseAnimationType.FlameSurge,
                    resolutionMessage = "High-speed insertion burn completed! Science packages preserved on batteries."
                }
            }
        });

        // 9. Planetary Dust: Gimbal Regolith Abrasion
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_Regolith_Gimbal_Abrasion",
            missionSource = "NASA LUNAR & MARTIAN REGOLITH STUDY",
            eventTitle = "ELECTROSTATIC REGOLITH ABRASION ON GIMBALS",
            description = "Razor-sharp, electrostatically charged planetary dust grains without wind erosion have\ninfiltrated rocket nozzle gimbal actuators, causing severe mechanical friction and motor stiffness!",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "INJECT HIGH-PRESSURE SYNTHETIC LUBRICANT",
                    prosText = "Flushes grit from gimbal ball-bearings and restores smooth ±8° steering range.",
                    consText = "Consumes onboard maintenance and consumable supplies (-10 Science).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = 0f, scienceDelta = -10f,
                    animType = CruiseAnimationType.LifeSupportHiss,
                    resolutionMessage = "Gimbal bearings lubricated! Full thrust vector control restored."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "LOCK GIMBAL & STEER VIA DIFFERENTIAL RCS",
                    prosText = "Prevents actuator motor burnout and eliminates mechanical gear stripping.",
                    consText = "Higher propellant consumption for every trajectory correction burn (-35kg Fuel).",
                    fuelDelta = -35f, hullDelta = 0f, powerDelta = 0f, scienceDelta = 0f,
                    animType = CruiseAnimationType.RcsPuffs,
                    resolutionMessage = "Main nozzle locked at center! Attitude control handed over to RCS thrusters."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "HIGH-FREQUENCY MOTOR VIBRATION PULSE",
                    prosText = "Quick zero-propellant fix; mechanical vibration dislodges clinging grit.",
                    consText = "Gear tooth micro-pitting weakens thrust mount structural margin (-15% Hull).",
                    fuelDelta = 0f, hullDelta = -15f, powerDelta = 0f, scienceDelta = 5f,
                    animType = CruiseAnimationType.AvionicsReboot,
                    resolutionMessage = "Vibration cycle completed! Dust shaken loose from mechanical gears."
                }
            }
        });

        // 10. Curiosity / Perseverance: Sky Crane Radar Glitch
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_SkyCrane_Radar_Glitch",
            missionSource = "NASA CURIOSITY & PERSEVERANCE EDL RESEARCH",
            eventTitle = "TERMINAL DESCENT RADAR MULTIPATH NOISE",
            description = "During descent staging, Doppler radar altimeters detect conflicting ground reflections due to\nsurface dust plumes! False altitude returns threaten premature retro-rocket shutdown.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "SWITCH TO TERRAIN RELATIVE NAVIGATION (TRN)",
                    prosText = "Optical cameras match terrain to orbital maps for sub-meter safe touchdown (+35 Sci).",
                    consText = "Heavy real-time GPU image matching drains avionics bus (-15% Power).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = -15f, scienceDelta = 35f,
                    animType = CruiseAnimationType.AvionicsReboot,
                    resolutionMessage = "TRN optical mapping locked! Spacecraft diverted away from surface hazards."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "THROTTLE RETRO-ROCKETS INTO HOVER BUCKET",
                    prosText = "Suspends vehicle at safe altitude until radar filters out multipath noise.",
                    consText = "High fuel burn while hovering in gravitational field (-50kg Fuel).",
                    fuelDelta = -50f, hullDelta = 0f, powerDelta = 0f, scienceDelta = 0f,
                    animType = CruiseAnimationType.FlameSurge,
                    resolutionMessage = "Hover bucket executed! Radar altimeter re-locked onto solid ground."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "DEFAULT TO INERTIAL DEAD-RECKONING DESCENT",
                    prosText = "Zero radar dependency; zero extra electrical power consumption.",
                    consText = "Faster touchdown velocity creates higher structural landing shock (-18% Hull).",
                    fuelDelta = 0f, hullDelta = -18f, powerDelta = 0f, scienceDelta = 5f,
                    animType = CruiseAnimationType.RcsPuffs,
                    resolutionMessage = "Inertial profile flown! Accelerometers guided touchdown through radar blackout."
                }
            }
        });

        // 11. Apollo 13: Freezing Cabin & Condensation
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_Apollo13_Condensation",
            missionSource = "NASA APOLLO 13 THERMAL MANAGEMENT REPORT",
            eventTitle = "CABIN FREEZING DROP & AVIONICS CONDENSATION SOAK",
            description = "Powering down electrical systems caused cabin temperatures to drop to 3°C! Heavy moisture\ncondensation has formed water droplets on circuit breakers and navigation electronics.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "INITIATE PASSIVE THERMAL 'BARBECUE ROLL'",
                    prosText = "Evenly distributes solar warmth across hull; heats cabin for zero power or fuel!",
                    consText = "Continuous 0.3 RPM rotation creates slight radio dish pointing jitter (-5 Sci).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = 0f, scienceDelta = -5f,
                    animType = CruiseAnimationType.ThermalRoll,
                    resolutionMessage = "Barbecue roll initiated! Solar heat warmed cabin and cleared moisture."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "ENERGIZE DE-ICE HEATERS WITH BATTERIES",
                    prosText = "Rapid thermal heating completely evaporates water from critical avionics panels.",
                    consText = "Significant battery discharge threatens reserves for destination burn (-22% Power).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = -22f, scienceDelta = 10f,
                    animType = CruiseAnimationType.FlameSurge,
                    resolutionMessage = "De-ice heaters energized! Electronics dried before electrical power-up."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "USE DESICCANT SPONGES & VENT CABIN MOISTURE",
                    prosText = "Removes 95% of moisture without consuming any battery electrical power.",
                    consText = "Minor loss of cabin atmosphere and reaction gas mass (-20kg Fuel).",
                    fuelDelta = -20f, hullDelta = 0f, powerDelta = 0f, scienceDelta = 0f,
                    animType = CruiseAnimationType.LifeSupportHiss,
                    resolutionMessage = "Cabin moisture manually wiped and vented to space vacuum."
                }
            }
        });

        // 12. Apollo 13: Critical Water Depletion
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_Apollo13_Water_Rationing",
            missionSource = "NASA APOLLO 13 WATER MANAGEMENT LOGS",
            eventTitle = "CRITICAL WATER SHORTAGE: ELECTRONICS VS CREW",
            description = "Sublimator cooling water reserves have dropped by 60%! Water is desperately needed both for\nelectronics evaporator cooling and crew hydration across the remaining flight.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "STRICT RATIONING & UNDER-VOLT INVERTERS",
                    prosText = "Guarantees water reserves last through destination; protects computers from overheat.",
                    consText = "Crew dehydration and fatigue slow telemetry calculation response (-10 Science).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = 0f, scienceDelta = -10f,
                    animType = CruiseAnimationType.LifeSupportHiss,
                    resolutionMessage = "Water rationed to 200 ml/day! Sublimator cooling stabilized for flight."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "DIVERT HEAT LOOPS TO HULL EXTERIOR RADIATOR",
                    prosText = "Bypasses evaporative water cooling entirely; unlimited thermal dissipation.",
                    consText = "External radiator panels exposed to micrometeoroid damage risk (-12% Hull).",
                    fuelDelta = 0f, hullDelta = -12f, powerDelta = 0f, scienceDelta = 5f,
                    animType = CruiseAnimationType.ThermalRoll,
                    resolutionMessage = "Heat loops diverted to exterior skin! Evaporative water consumption halted."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "PULSE FUEL CELL WITH H2/O2 FOR BYPRODUCT WATER",
                    prosText = "Chemical reaction yields 15 liters of pure drinking and cooling water.",
                    consText = "Consumes critical cryogenic reactant gas reserves (-40kg Fuel).",
                    fuelDelta = -40f, hullDelta = 0f, powerDelta = 10f, scienceDelta = 0f,
                    animType = CruiseAnimationType.FlameSurge,
                    resolutionMessage = "Fuel cell pulsed! Byproduct pure water replenished cooling reservoirs."
                }
            }
        });

        // 13. MOXIE: In-Situ Oxygen Synthesis
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_MOXIE_Electrolysis",
            missionSource = "NASA MOXIE MARS 2020 RESEARCH STUDY",
            eventTitle = "MOXIE ISRU OXYGEN SYNTHESIS: THERMAL GRADIENT",
            description = "MOXIE is heating to 800°C to extract breathable O2 from atmospheric CO2 via solid oxide\nelectrolysis. A thermal gradient creates slight back-pressure resistance in the ceramic stack.",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "CALIBRATE GAS FLOW & OPTIMIZE CATHODE HEAT",
                    prosText = "Produces 12 g/hr pure oxygen; monumental planetary science milestone (+40 Sci).",
                    consText = "High continuous electrical heating draws significant battery power (-16% Power).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = -16f, scienceDelta = 40f,
                    animType = CruiseAnimationType.AvionicsReboot,
                    resolutionMessage = "MOXIE tuned to optimal temperature! Pure oxygen flowing into test reservoir."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "PURGE CATHODE WITH PRESSURIZED NITROGEN",
                    prosText = "Instantly clears back-pressure and protects delicate platinum catalyst.",
                    consText = "Depletes onboard cold-gas propellant storage (-25kg Fuel).",
                    fuelDelta = -25f, hullDelta = 0f, powerDelta = 0f, scienceDelta = 10f,
                    animType = CruiseAnimationType.RcsPuffs,
                    resolutionMessage = "Nitrogen purge fired! Catalytic back-pressure cleared cleanly."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "THROTTLE MOXIE DOWN TO 50% SAFE BASELINE",
                    prosText = "Zero risk of thermal ceramic cracking; zero extra battery draw.",
                    consText = "Scientific yield reduced by half (-20 Science).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = 0f, scienceDelta = -20f,
                    animType = CruiseAnimationType.PowerStandby,
                    resolutionMessage = "MOXIE throttled to conservative 50% production ceiling."
                }
            }
        });

        // 14. Ingenuity: Aerial Rotor Spin Vibration
        allEventsPool.Add(new CruiseEventData
        {
            eventId = "EV_Ingenuity_Rotor_Vibration",
            missionSource = "NASA INGENUITY MARS HELICOPTER FLIGHT DATA",
            eventTitle = "INGENUITY COAXIAL ROTOR 2,400 RPM DYNAMIC IMBALANCE",
            description = "Spinning up the twin carbon-fiber coaxial rotors to 2,400 RPM inside the cruise test bay\ncreates a 1% blade imbalance, sending high-frequency harmonic vibrations through the hull!",
            choices = new CruiseEventChoice[]
            {
                new CruiseEventChoice
                {
                    choiceTitle = "RE-FLASH CONTROLLER WITH SWASHPLATE BALANCING",
                    prosText = "Dynamic swashplate trim algorithm neutralizes vibration; unlocks flight (+45 Sci).",
                    consText = "Heavy telemetry calibration load consumes processor power (-10% Power).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = -10f, scienceDelta = 45f,
                    animType = CruiseAnimationType.AvionicsReboot,
                    resolutionMessage = "Dynamic swashplate balance loaded! Coaxial blades spinning smoothly."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "DAMPEN CRADLE WITH PNEUMATIC ISOLATORS",
                    prosText = "Absorbs 100% of vibrational energy away from main spacecraft frame.",
                    consText = "Adds structural deadweight, slightly lowering hull margin (-10% Hull).",
                    fuelDelta = 0f, hullDelta = -10f, powerDelta = 0f, scienceDelta = 5f,
                    animType = CruiseAnimationType.LifeSupportHiss,
                    resolutionMessage = "Pneumatic dampeners engaged! Rotor vibration isolated from spacecraft."
                },
                new CruiseEventChoice
                {
                    choiceTitle = "LIMIT ROTOR VELOCITY TO 1,800 RPM SAFE CEILING",
                    prosText = "Zero mechanical stress on composite blades; safe conservative profile.",
                    consText = "Limits scout flight altitude and operational radius on destination (-25 Sci).",
                    fuelDelta = 0f, hullDelta = 0f, powerDelta = 0f, scienceDelta = -25f,
                    animType = CruiseAnimationType.PowerStandby,
                    resolutionMessage = "Rotor speed capped at 1,800 RPM! Operations limited to low-altitude hover."
                }
            }
        });
    }

    // =========================================================================
    // 12. UTILITY CREATION HELPERS
    // =========================================================================
    private GameObject CreateFlatQuad(Transform parent, string name, Vector3 pos, Vector3 scale, Color col, bool addCollider = false, int sortingOrder = 0)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localScale = scale;

        Collider c = q.GetComponent<Collider>();
        if (!addCollider && c != null) Destroy(c);

        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        Material m = new Material(sh);
        m.color = col;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);

        Renderer r = q.GetComponent<Renderer>();
        r.material = m;
        r.sortingOrder = sortingOrder;
        return q;
    }

    private GameObject CreateText(Transform parent, string name, Vector3 pos, string text, float charSize, int fontSize, FontStyle style, TextAnchor anchor, Color col, int sortingOrder = 0)
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
        if (anchor == TextAnchor.UpperLeft || anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.LowerLeft)
            tm.alignment = TextAlignment.Left;
        else if (anchor == TextAnchor.UpperRight || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.LowerRight)
            tm.alignment = TextAlignment.Right;
        else
            tm.alignment = TextAlignment.Center;
        tm.color = col;

        Renderer r = tm.GetComponent<Renderer>();
        r.sortingOrder = sortingOrder;
        return obj;
    }

    private void SetRendererColor(Renderer r, Color c)
    {
        if (r == null || r.material == null) return;
        r.material.color = c;
        if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", c);
    }
}
