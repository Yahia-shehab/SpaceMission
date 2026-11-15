using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    // High-tech NASA Cyber-Cyan & Deep Space color palette
    private static readonly Color CyberCyan        = new Color(0.00f, 0.898f, 1.00f); // #00E5FF Vibrant Cyber Cyan
    private static readonly Color CyberCyanDark    = new Color(0.00f, 0.450f, 0.65f);
    private static readonly Color OrbitGold        = new Color(1.00f, 0.820f, 0.20f); // #FFD133 Telemetry Gold
    private static readonly Color GoGreen          = new Color(0.18f, 0.850f, 0.45f); // #2ED973 Nominal Green
    private static readonly Color StationFrameDark = new Color(0.05f, 0.08f, 0.14f, 1f); // Titanium Alloy Window Frame
    private static readonly Color RailingSteel     = new Color(0.10f, 0.15f, 0.22f, 1f); // Observation deck railing
    private static readonly Color SuitDark         = new Color(0.02f, 0.04f, 0.08f, 1f); // Astronaut Commander Silhouette

    private int selectedMenuIndex = 0;
    private readonly string[] menuItems = {
        "CONTINUE MISSION",
        "NEW EXPEDITION",
        "MISSION SELECT",
        "FLIGHT HANDBOOK",
        "SIMULATOR SETTINGS",
        "QUIT SIMULATOR"
    };

    private GameObject[] menuItemObjects;
    private TextMesh[] menuItemTextMeshes;
    private GameObject[] menuIndicatorObjects;
    private GameObject menuSelectorPlate;

    // 3D Spinning Earth components
    private Transform earthGlobeTransform;
    private Transform earthCloudsTransform;
    private Transform[] spaceDustTransforms;

    private AudioSource sfxSource;
    private AudioSource ambientSource;
    private AudioClip blipClip;
    private AudioClip selectClip;
    private AudioClip spaceDroneClip;

    private GameObject handbookModalObj;
    private GameObject missionSelectModalObj;
    private GameObject optionsModalObj;
    private GameObject fadeOverlayObj;
    private Renderer fadeOverlayRenderer;
    private bool isTransitioning = false;

    // Procedural graphic textures
    private Texture2D rocketIconTex;
    private Texture2D orbitEmblemTex;
    private Texture2D astronautTex;

    void Awake()
    {
        SetupCamera();
        GenerateProceduralGraphicAssets();
        InitProceduralAudio();
    }

    void Start()
    {
        BuildPanoramicSpaceBackdrop();
        BuildSpinningEarthSystem();
        BuildSpaceStationWindowFrame();
        BuildObservationRailingAndFloor();
        BuildAstronautSilhouette();
        BuildTopTitleHeader();
        BuildLeftMenuList();
        BuildBottomLeftTelemetryCard();
        BuildModals();
        BuildFadeOverlay();
        UpdateMenuVisuals();
    }

    void Update()
    {
        if (isTransitioning) return;

        // 1. Continuous orbital animations (Earth rotates faster, cloud drift, floating stardust)
        AnimateSpaceAtmosphere(Time.deltaTime);

        // 2. ALWAYS handle mouse interaction (enables clicking close buttons, destination cards, menu items)
        HandleMouseInteraction();

        // 3. Handle modal keyboard shortcuts (Escape, B, Enter, Space)
        bool modalOpen = (handbookModalObj != null && handbookModalObj.activeSelf) ||
                         (missionSelectModalObj != null && missionSelectModalObj.activeSelf) ||
                         (optionsModalObj != null && optionsModalObj.activeSelf);

        if (modalOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                CloseAllModals();
            }
            return; // Pause background menu keyboard navigation while modal is active
        }

        // 4. Background menu keyboard navigation (when no modal is open)
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            ChangeMenuSelection(-1);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            ChangeMenuSelection(1);
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.A))
        {
            ActivateSelectedMenu();
        }
    }

    // =========================================================================
    // 1. CAMERA, PROCEDURAL ASSETS & PROCEDURAL AUDIO
    // =========================================================================
    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            camObj.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.orthographicSize = 5.6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.015f, 0.030f, 0.065f, 1f);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.transform.rotation = Quaternion.identity;
    }

    private void GenerateProceduralGraphicAssets()
    {
        rocketIconTex = GenerateStylizedRocketTexture();
        orbitEmblemTex = GenerateOrbitalInsigniaTexture();
        astronautTex = GenerateAstronautSilhouetteTexture();
    }

    private void InitProceduralAudio()
    {
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.playOnAwake = false;
        ambientSource.loop = true;
        ambientSource.volume = 0.25f;

        blipClip = CreateTelemetryBlipClip(1100f, 0.045f, 0.18f);
        selectClip = CreateNASAChimeClip();
        spaceDroneClip = CreateSpaceDroneClip();

        if (ambientSource != null && spaceDroneClip != null)
        {
            ambientSource.clip = spaceDroneClip;
            ambientSource.Play();
        }
    }

    private AudioClip CreateTelemetryBlipClip(float frequency, float duration, float vol)
    {
        int sampleRate = 44100;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = 1.0f - (t / duration);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * env * vol;
        }
        AudioClip clip = AudioClip.Create("Blip", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateNASAChimeClip()
    {
        int sampleRate = 44100;
        float duration = 0.40f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Exp(-t * 8f);
            float f = (t < 0.10f) ? 659.25f : 987.77f; // E5 -> B5 High-tech chime
            samples[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.75f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.25f) * env * 0.35f;
        }
        AudioClip clip = AudioClip.Create("NASAChime", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private AudioClip CreateSpaceDroneClip()
    {
        int sampleRate = 22050;
        float duration = 4.0f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float drone = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.18f;
            float shimmer = Mathf.Sin(2f * Mathf.PI * 220f * t + Mathf.Sin(2f * Mathf.PI * 0.5f * t)) * 0.06f;
            samples[i] = (drone + shimmer) * 0.14f;
        }
        AudioClip clip = AudioClip.Create("SpaceDrone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // =========================================================================
    // 2. PROCEDURAL HIGH-RES SPACE BACKDROP (NEBULA & STARS)
    // =========================================================================
    private void BuildPanoramicSpaceBackdrop()
    {
        int texW = 960;
        int texH = 540;
        Texture2D bgTex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        bgTex.filterMode = FilterMode.Bilinear;
        bgTex.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[texW * texH];

        Color cSpaceDark   = new Color(0.010f, 0.020f, 0.045f);
        Color cNebulaCyan  = new Color(0.015f, 0.120f, 0.200f);
        Color cNebulaBlue  = new Color(0.035f, 0.070f, 0.180f);
        Color cFloorDark   = new Color(0.022f, 0.040f, 0.070f);

        System.Random starRnd = new System.Random(42);
        bool[,] stars = new bool[texW, texH];
        for (int s = 0; s < 380; s++)
        {
            int sx = starRnd.Next(0, texW);
            int sy = starRnd.Next((int)(texH * 0.32f), texH);
            stars[sx, sy] = true;
        }

        for (int y = 0; y < texH; y++)
        {
            float v = (float)y / (texH - 1);

            for (int x = 0; x < texW; x++)
            {
                float u = (float)x / (texW - 1);

                Color col;
                if (v > 0.35f) // Upper window: Cosmic nebula & stars
                {
                    float nebHarmonic = Mathf.Sin(u * 5f + v * 3f) * Mathf.Cos(u * 3f - v * 4f) * 0.5f + 0.5f;
                    col = Color.Lerp(cSpaceDark, Color.Lerp(cNebulaBlue, cNebulaCyan, u), nebHarmonic * 0.50f);

                    if (stars[x, y])
                    {
                        float starBright = (float)(starRnd.NextDouble() * 0.4 + 0.6);
                        col += new Color(starBright * 0.9f, starBright, starBright);
                    }
                }
                else // Lower area: Polished space station deck reflecting Earth's blue radiance
                {
                    float floorFrac = v / 0.35f;
                    col = cFloorDark;

                    float paneU = Mathf.Repeat(u * 4f, 1f);
                    if (paneU > 0.06f && paneU < 0.94f)
                    {
                        float refl = (1f - Mathf.Abs(paneU - 0.5f) * 2f) * 0.08f * floorFrac;
                        col += new Color(refl * 0.25f, refl * 0.85f, refl * 1.15f);
                    }
                }

                pixels[y * texW + x] = col;
            }
        }

        bgTex.SetPixels32(pixels);
        bgTex.Apply();

        CreateTexturedQuad(transform, "Panoramic_Space_Backdrop", new Vector3(0f, 0f, 5.0f), new Vector3(22f, 12.5f, 1f), bgTex, 0);

        // Floating dynamic space dust / stardust particles
        GameObject dustGroup = new GameObject("Space_Dust_Group");
        dustGroup.transform.SetParent(transform, false);
        spaceDustTransforms = new Transform[40];
        System.Random dRnd = new System.Random(1337);
        for (int i = 0; i < spaceDustTransforms.Length; i++)
        {
            float rx = (float)(dRnd.NextDouble() * 20.0 - 10.0);
            float ry = (float)(dRnd.NextDouble() * 11.0 - 5.5);
            float rSize = (float)(dRnd.NextDouble() * 0.040 + 0.020);
            GameObject dObj = CreateFlatQuad(dustGroup.transform, "Dust_" + i, new Vector3(rx, ry, 2.5f), new Vector3(rSize, rSize, 1f), new Color(0.40f, 0.85f, 1.0f, 0.45f), false, 4);
            spaceDustTransforms[i] = dObj.transform;
        }
    }

    // =========================================================================
    // 3. 3D SLOWLY SPINNING EARTH GLOBE & CLOUD SYSTEM
    // =========================================================================
    private void BuildSpinningEarthSystem()
    {
        // Positioned in the center-right observation bay (restoring the layout from 2 edits ago)
        GameObject earthRoot = new GameObject("Earth_Planetary_System");
        earthRoot.transform.SetParent(transform, false);
        earthRoot.transform.localPosition = new Vector3(2.6f, -3.2f, 3.6f);

        // 1. Globe Sphere
        GameObject globeObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        globeObj.name = "Earth_Globe_Mesh";
        globeObj.transform.SetParent(earthRoot.transform, false);
        globeObj.transform.localPosition = Vector3.zero;
        globeObj.transform.localScale = new Vector3(9.2f, 9.2f, 9.2f);
        globeObj.transform.localRotation = Quaternion.Euler(18f, 210f, -12f);

        Collider gc = globeObj.GetComponent<Collider>();
        if (gc != null) Destroy(gc); // Never block UI raycasts

        Texture2D earthMap = GenerateEarthEquirectangularTexture();
        Material globeMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture"));
        globeMat.mainTexture = earthMap;
        globeObj.GetComponent<Renderer>().material = globeMat;
        globeObj.GetComponent<Renderer>().sortingOrder = 2;
        earthGlobeTransform = globeObj.transform;

        // 2. Cloud Sphere (swirling weather belts rotating independently)
        GameObject cloudsObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cloudsObj.name = "Earth_Clouds_Mesh";
        cloudsObj.transform.SetParent(earthRoot.transform, false);
        cloudsObj.transform.localPosition = Vector3.zero;
        cloudsObj.transform.localScale = new Vector3(9.38f, 9.38f, 9.38f);
        cloudsObj.transform.localRotation = Quaternion.Euler(18f, 190f, -12f);

        Collider cc = cloudsObj.GetComponent<Collider>();
        if (cc != null) Destroy(cc);

        Texture2D cloudsMap = GenerateEarthCloudsTexture();
        Material cloudsMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent"));
        cloudsMat.mainTexture = cloudsMap;
        cloudsObj.GetComponent<Renderer>().material = cloudsMat;
        cloudsObj.GetComponent<Renderer>().sortingOrder = 3;
        earthCloudsTransform = cloudsObj.transform;

        // 3. Glowing Atmospheric Limb Halo Quad
        Texture2D haloTex = GenerateAtmosphereHaloTexture();
        CreateTexturedQuad(earthRoot.transform, "Earth_Atmosphere_Halo", new Vector3(0f, 0f, -0.05f), new Vector3(10.6f, 10.6f, 1f), haloTex, 4);
    }

    private Texture2D GenerateEarthEquirectangularTexture()
    {
        int w = 512;
        int h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;
        Color32[] px = new Color32[w * h];

        Color cOceanDeep    = new Color(0.04f, 0.12f, 0.28f);
        Color cOceanShallow = new Color(0.06f, 0.32f, 0.45f);
        Color cLandGreen    = new Color(0.10f, 0.38f, 0.22f);
        Color cLandMountain = new Color(0.35f, 0.32f, 0.20f);
        Color cNightCity    = new Color(1.00f, 0.85f, 0.35f);

        for (int y = 0; y < h; y++)
        {
            float lat = ((float)y / (h - 1) - 0.5f) * Mathf.PI;
            float cosLat = Mathf.Cos(lat);

            for (int x = 0; x < w; x++)
            {
                float lon = ((float)x / (w - 1)) * 2f * Mathf.PI;

                float n1 = Mathf.Sin(lon * 2f) * Mathf.Cos(lat * 3f);
                float n2 = Mathf.Sin(lon * 4f + 1.2f) * Mathf.Cos(lat * 5f - 0.6f) * 0.5f;
                float n3 = Mathf.Sin(lon * 8f - 2.1f) * Mathf.Sin(lat * 7f) * 0.25f;
                float landElev = n1 + n2 + n3;

                Color col;
                if (landElev > 0.15f)
                {
                    float elev = (landElev - 0.15f) / 0.8f;
                    col = Color.Lerp(cLandGreen, cLandMountain, elev);
                }
                else if (landElev > 0.05f)
                {
                    col = cOceanShallow;
                }
                else
                {
                    col = cOceanDeep;
                }

                float sunAngle = Mathf.Cos(lon - 1.2f) * cosLat;
                if (sunAngle < 0f)
                {
                    float nightFactor = Mathf.Clamp01(-sunAngle * 3f);
                    col = Color.Lerp(col, col * 0.12f, nightFactor);

                    if (landElev > 0.18f && (x * 13 + y * 29) % 17 == 0)
                    {
                        col = Color.Lerp(col, cNightCity, nightFactor * 0.85f);
                    }
                }

                px[y * w + x] = col;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private Texture2D GenerateEarthCloudsTexture()
    {
        int w = 512;
        int h = 256;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;
        Color32[] px = new Color32[w * h];

        Color cloudCol = new Color(0.92f, 0.96f, 1.0f);

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1);
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / (w - 1);

                float c1 = Mathf.Sin(u * 14f + v * 6f) * Mathf.Cos(u * 8f - v * 12f);
                float c2 = Mathf.Sin(u * 24f - v * 18f) * 0.45f;
                float cloudVal = c1 + c2;

                if (cloudVal > 0.25f)
                {
                    float alpha = Mathf.Clamp01((cloudVal - 0.25f) / 0.55f) * 0.65f;
                    px[y * w + x] = new Color(cloudCol.r, cloudCol.g, cloudCol.b, alpha);
                }
                else
                {
                    px[y * w + x] = new Color(0, 0, 0, 0);
                }
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private Texture2D GenerateAtmosphereHaloTexture()
    {
        int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];

        Color cyanHalo = new Color(0.00f, 0.898f, 1.00f);
        float cx = size * 0.5f;
        float cy = size * 0.5f;
        float outerR = size * 0.48f;
        float innerR = size * 0.40f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (r >= innerR && r <= outerR)
                {
                    float t = (r - innerR) / (outerR - innerR);
                    float alpha = Mathf.Sin(t * Mathf.PI) * 0.60f;
                    px[y * size + x] = new Color(cyanHalo.r, cyanHalo.g, cyanHalo.b, alpha);
                }
                else
                {
                    px[y * size + x] = new Color(0, 0, 0, 0);
                }
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // =========================================================================
    // 4. SPACE STATION OBSERVATION WINDOW & INTERIOR DECK
    // =========================================================================
    private void BuildSpaceStationWindowFrame()
    {
        GameObject frameRoot = new GameObject("Space_Station_Window_Grid");
        frameRoot.transform.SetParent(transform, false);

        float[] vertXs = { -9.6f, -4.8f, 0f, 4.8f, 9.6f };
        for (int i = 0; i < vertXs.Length; i++)
        {
            CreateFlatQuad(frameRoot.transform, "Strut_V_" + i, new Vector3(vertXs[i], 0.8f, 1.0f), new Vector3(0.10f, 9.8f, 1f), StationFrameDark, false, 8);
        }

        float[] horizYs = { 4.8f, 2.3f, -0.2f };
        for (int i = 0; i < horizYs.Length; i++)
        {
            CreateFlatQuad(frameRoot.transform, "Strut_H_" + i, new Vector3(0f, horizYs[i], 1.0f), new Vector3(21f, 0.09f, 1f), StationFrameDark, false, 8);
        }

        CreateFlatQuad(frameRoot.transform, "Window_Bulkhead", new Vector3(0f, -0.95f, 0.8f), new Vector3(22f, 0.32f, 1f), StationFrameDark, false, 10);
    }

    private void BuildObservationRailingAndFloor()
    {
        GameObject railRoot = new GameObject("Deck_Railing_Group");
        railRoot.transform.SetParent(transform, false);

        // 1. Solid Interior Observation Deck Ground Floor (matching Pokemon LeafGreen reference)
        float floorTopY = -1.35f;
        float floorBottomY = -6.0f;
        float floorHeight = floorTopY - floorBottomY; // 4.65f
        float floorCenterY = (floorTopY + floorBottomY) * 0.5f; // -3.675f

        // Solid dark polished floor base
        CreateFlatQuad(railRoot.transform, "Observation_Deck_Ground", new Vector3(0f, floorCenterY, 0.5f), new Vector3(24f, floorHeight, 1f), new Color(0.025f, 0.040f, 0.070f, 1f), false, 11);

        // Glossy floor reflections (Earth's blue light reflecting off polished tiles)
        for (int i = 0; i < 5; i++)
        {
            float rx = -3.5f + i * 2.2f;
            float rWidth = 1.3f + i * 0.25f;
            CreateFlatQuad(railRoot.transform, "Floor_Reflection_" + i, new Vector3(rx, floorCenterY + 0.8f, 0.48f), new Vector3(rWidth, floorHeight * 0.60f, 1f), new Color(0.00f, 0.35f, 0.65f, 0.18f), false, 11);
        }

        // Perspective tile grid lines across the floor (matching reference)
        float[] tileXs = { -8.5f, -5.5f, -2.5f, 0.5f, 3.5f, 6.5f, 9.5f };
        for (int i = 0; i < tileXs.Length; i++)
        {
            CreateFlatQuad(railRoot.transform, "Tile_Line_V_" + i, new Vector3(tileXs[i], floorCenterY, 0.45f), new Vector3(0.025f, floorHeight, 1f), new Color(0.06f, 0.12f, 0.20f, 0.70f), false, 11);
        }
        float[] tileYs = { -1.9f, -2.8f, -3.8f, -5.0f };
        for (int i = 0; i < tileYs.Length; i++)
        {
            CreateFlatQuad(railRoot.transform, "Tile_Line_H_" + i, new Vector3(0f, tileYs[i], 0.45f), new Vector3(24f, 0.025f, 1f), new Color(0.06f, 0.12f, 0.20f, 0.70f), false, 11);
        }

        // Window ledge / sill casing separating glass from floor
        CreateFlatQuad(railRoot.transform, "Window_Sill_Base", new Vector3(0f, floorTopY + 0.05f, 0.4f), new Vector3(24f, 0.22f, 1f), new Color(0.04f, 0.06f, 0.10f, 1f), false, 12);
        CreateFlatQuad(railRoot.transform, "Window_Sill_Cyan_Accent", new Vector3(0f, floorTopY + 0.14f, 0.38f), new Vector3(24f, 0.025f, 1f), CyberCyanDark, false, 13);

        // Observation Railing on the ground
        CreateFlatQuad(railRoot.transform, "Rail_Top_Bar", new Vector3(0f, floorTopY + 0.60f, 0.35f), new Vector3(24f, 0.08f, 1f), RailingSteel, false, 14);
        CreateFlatQuad(railRoot.transform, "Rail_Cyan_Glow", new Vector3(0f, floorTopY + 0.63f, 0.34f), new Vector3(24f, 0.015f, 1f), CyberCyan, false, 14);
        CreateFlatQuad(railRoot.transform, "Rail_Mid_Bar", new Vector3(0f, floorTopY + 0.25f, 0.35f), new Vector3(24f, 0.06f, 1f), RailingSteel, false, 14);

        float[] postXs = { -8.4f, -5.6f, -2.8f, 0f, 2.8f, 5.6f, 8.4f };
        for (int i = 0; i < postXs.Length; i++)
        {
            CreateFlatQuad(railRoot.transform, "Rail_Post_" + i, new Vector3(postXs[i], floorTopY + 0.30f, 0.32f), new Vector3(0.07f, 0.65f, 1f), RailingSteel, false, 14);
        }

        CreateFlatQuad(transform, "Bulkhead_L", new Vector3(-9.8f, 0f, -0.2f), new Vector3(1.4f, 12f, 1f), StationFrameDark, false, 15);
        CreateFlatQuad(transform, "Bulkhead_R", new Vector3( 9.8f, 0f, -0.2f), new Vector3(1.4f, 12f, 1f), StationFrameDark, false, 15);
    }

    private void BuildAstronautSilhouette()
    {
        Vector3 figPos = new Vector3(-0.35f, -0.55f, 0.2f);
        CreateTexturedQuad(transform, "Astronaut_Commander_Silhouette", figPos, new Vector3(1.65f, 2.65f, 1f), astronautTex, 15);
    }

    // =========================================================================
    // 5. TOP TITLE HEADER (CYBER-CYAN AEROSPACE ARCHITECT TYPOGRAPHY)
    // =========================================================================
    private void BuildTopTitleHeader()
    {
        GameObject titleRoot = new GameObject("Title_Header_Group");
        titleRoot.transform.SetParent(transform, false);

        // Glowing Rocket Crest above title
        CreateTexturedQuad(titleRoot.transform, "Title_Rocket_Icon", new Vector3(0f, 4.45f, -1.0f), new Vector3(0.52f, 0.52f, 1f), rocketIconTex, 30);

        // Title Line 1: SPACE MISSION
        CreateText(titleRoot.transform, "Title_Main_1", new Vector3(0f, 3.82f, -1.0f), "S P A C E   M I S S I O N", 0.080f, 46, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, 30);

        // Title Line 2: ORBITAL ARCHITECT (Vibrant Cyber Cyan)
        CreateText(titleRoot.transform, "Title_Main_2", new Vector3(0f, 3.12f, -1.0f), "O R B I T A L   A R C H I T E C T", 0.098f, 54, FontStyle.Bold, TextAnchor.MiddleCenter, CyberCyan, 30);

        // Cyber divider rules meeting at an orbital insignia
        CreateFlatQuad(titleRoot.transform, "Rule_L", new Vector3(-2.8f, 2.50f, -1.0f), new Vector3(3.4f, 0.025f, 1f), CyberCyanDark, false, 30);
        CreateFlatQuad(titleRoot.transform, "Rule_R", new Vector3( 2.8f, 2.50f, -1.0f), new Vector3(3.4f, 0.025f, 1f), CyberCyanDark, false, 30);
        CreateTexturedQuad(titleRoot.transform, "Center_Emblem", new Vector3(0f, 2.50f, -1.05f), new Vector3(0.38f, 0.38f, 1f), orbitEmblemTex, 31);
    }

    // =========================================================================
    // 6. LEFT MENU LIST (CYBER-CYAN SELECTION HIGHLIGHTS)
    // =========================================================================
    private void BuildLeftMenuList()
    {
        GameObject menuRoot = new GameObject("Left_Menu_Group");
        menuRoot.transform.SetParent(transform, false);

        int count = menuItems.Length;
        menuItemObjects = new GameObject[count];
        menuItemTextMeshes = new TextMesh[count];
        menuIndicatorObjects = new GameObject[count];

        float startY = 1.30f;
        float spacingY = 0.58f;
        float menuX = -6.40f;

        // Translucent cyber-cyan selector backplate
        menuSelectorPlate = CreateFlatQuad(menuRoot.transform, "Menu_Selector_Plate", new Vector3(menuX + 1.8f, startY, -0.7f), new Vector3(5.2f, 0.50f, 1f), new Color(0.00f, 0.35f, 0.55f, 0.45f), false, 25);

        for (int i = 0; i < count; i++)
        {
            float y = startY - i * spacingY;
            GameObject itemObj = new GameObject("MenuItem_" + i);
            itemObj.transform.SetParent(menuRoot.transform, false);
            itemObj.transform.localPosition = new Vector3(menuX, y, -0.8f);

            GameObject indObj = CreateTexturedQuad(itemObj.transform, "Indicator_" + i, new Vector3(-0.35f, 0f, 0f), new Vector3(0.28f, 0.28f, 1f), rocketIconTex, 30);
            menuIndicatorObjects[i] = indObj;

            GameObject textObj = new GameObject("Text_" + i);
            textObj.transform.SetParent(itemObj.transform, false);
            textObj.transform.localPosition = Vector3.zero;
            TextMesh tm = textObj.AddComponent<TextMesh>();
            tm.text = menuItems[i];
            tm.fontSize = 34;
            tm.characterSize = 0.055f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleLeft;
            tm.alignment = TextAlignment.Left;
            tm.color = (i == 0) ? CyberCyan : new Color(0.85f, 0.92f, 1.0f);
            tm.GetComponent<Renderer>().sortingOrder = 30;
            menuItemTextMeshes[i] = tm;

            BoxCollider col = itemObj.AddComponent<BoxCollider>();
            col.size = new Vector3(5.2f, 0.52f, 2.0f);
            col.center = new Vector3(2.0f, 0f, 0f);

            menuItemObjects[i] = itemObj;
        }
    }

    private void UpdateMenuVisuals()
    {
        float startY = 1.30f;
        float spacingY = 0.58f;
        float menuX = -6.40f;

        if (menuSelectorPlate != null)
        {
            menuSelectorPlate.transform.localPosition = new Vector3(menuX + 1.8f, startY - selectedMenuIndex * spacingY, -0.7f);
        }

        for (int i = 0; i < menuItems.Length; i++)
        {
            bool isSelected = (i == selectedMenuIndex);
            if (menuIndicatorObjects[i] != null)
                menuIndicatorObjects[i].SetActive(isSelected);

            if (menuItemTextMeshes[i] != null)
            {
                menuItemTextMeshes[i].color = isSelected ? CyberCyan : new Color(0.85f, 0.92f, 1.0f);
                menuItemTextMeshes[i].characterSize = isSelected ? 0.060f : 0.054f;
            }
        }
    }

    private void ChangeMenuSelection(int dir)
    {
        int prev = selectedMenuIndex;
        selectedMenuIndex = (selectedMenuIndex + dir + menuItems.Length) % menuItems.Length;
        if (prev != selectedMenuIndex)
        {
            PlaySfx(blipClip);
            UpdateMenuVisuals();
        }
    }

    // =========================================================================
    // 7. BOTTOM-LEFT NASA FLIGHT DIRECTOR TELEMETRY CARD (FITS THE GAME AUTHENTICALLY)
    // =========================================================================
    private void BuildBottomLeftTelemetryCard()
    {
        GameObject cardRoot = new GameObject("NASA_Telemetry_HUD");
        cardRoot.transform.SetParent(transform, false);
        Vector3 cardCenter = new Vector3(-6.20f, -3.70f, -0.6f);
        float cardW = 5.2f;
        float cardH = 1.70f;

        // Dark navy aerospace glass body
        CreateFlatQuad(cardRoot.transform, "Card_Bg", cardCenter, new Vector3(cardW, cardH, 1f), new Color(0.02f, 0.06f, 0.12f, 0.92f), false, 25);

        // Cyber-cyan glowing outer border
        CreateFlatQuad(cardRoot.transform, "Border_T", cardCenter + new Vector3(0f,  cardH * 0.5f, -0.05f), new Vector3(cardW + 0.04f, 0.035f, 1f), CyberCyan, false, 28);
        CreateFlatQuad(cardRoot.transform, "Border_B", cardCenter + new Vector3(0f, -cardH * 0.5f, -0.05f), new Vector3(cardW + 0.04f, 0.035f, 1f), CyberCyan, false, 28);
        CreateFlatQuad(cardRoot.transform, "Border_L", cardCenter + new Vector3(-cardW * 0.5f, 0f, -0.05f), new Vector3(0.035f, cardH + 0.04f, 1f), CyberCyan, false, 28);
        CreateFlatQuad(cardRoot.transform, "Border_R", cardCenter + new Vector3( cardW * 0.5f, 0f, -0.05f), new Vector3(0.035f, cardH + 0.04f, 1f), CyberCyan, false, 28);

        // Header accent: NASA Space Flight Telemetry
        CreateText(cardRoot.transform, "Hud_Header", cardCenter + new Vector3(0f, 0.60f, -0.1f), "★ NASA MISSION CONTROL TELEMETRY ★", 0.040f, 26, FontStyle.Bold, TextAnchor.MiddleCenter, OrbitGold, 32);
        CreateFlatQuad(cardRoot.transform, "Hud_Header_Rule", cardCenter + new Vector3(0f, 0.42f, -0.08f), new Vector3(4.6f, 0.02f, 1f), CyberCyanDark, false, 30);

        // Monospace-style aerospace telemetry readouts
        string labels =
            "MISSION\n" +
            "VEHICLE\n" +
            "TARGET\n" +
            "READINESS";

        string values =
            "NASA ODYSSEY ARCHITECT\n" +
            "FALCON 9 / ARTEMIS HEAVY\n" +
            "LOW EARTH ORBIT (LEO)\n" +
            "<color=#2ED973>SYSTEMS GO FOR LAUNCH (100%)</color>";

        // Column 1: Labels in glowing Cyber Cyan
        CreateText(cardRoot.transform, "Hud_Labels", cardCenter + new Vector3(-2.30f, 0.24f, -0.1f), labels, 0.042f, 28, FontStyle.Bold, TextAnchor.UpperLeft, CyberCyan, 32);

        // Column 2: Values in crisp white / status green
        CreateText(cardRoot.transform, "Hud_Values", cardCenter + new Vector3(-0.55f, 0.24f, -0.1f), values, 0.042f, 28, FontStyle.Bold, TextAnchor.UpperLeft, Color.white, 32);
    }

    // =========================================================================
    // 8. PROCEDURAL GRAPHIC ASSETS GENERATORS
    // =========================================================================
    private Texture2D GenerateStylizedRocketTexture()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 cyan = new Color32(0, 229, 255, 255);
        Color32 white = new Color32(255, 255, 255, 255);

        float cx = 32f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - cx);
                bool isRk = false;

                if (y >= 44 && y <= 58)
                {
                    float w = (58f - y) * 0.70f;
                    if (dx <= w) isRk = true;
                }
                if (y >= 16 && y < 44 && dx <= 10f) isRk = true;
                if (y >= 10 && y <= 24)
                {
                    float finW = 10f + (24f - y) * 0.90f;
                    if (dx <= finW) isRk = true;
                }
                if (y >= 4 && y < 10 && dx <= 6f) isRk = true;

                if (isRk)
                {
                    px[y * size + x] = (dx <= 4f && y >= 20 && y <= 40) ? white : cyan;
                }
                else
                {
                    px[y * size + x] = clear;
                }
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private Texture2D GenerateOrbitalInsigniaTexture()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[size * size];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 cyan = new Color32(0, 229, 255, 255);
        Color32 gold = new Color32(255, 209, 51, 255);

        float cx = 32f;
        float cy = 32f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (r >= 22f && r <= 26f)
                {
                    px[y * size + x] = cyan;
                }
                else if (r <= 9f)
                {
                    px[y * size + x] = gold;
                }
                else if (Mathf.Abs(y - cy) <= 2.0f && r <= 22f && r >= 9f)
                {
                    px[y * size + x] = cyan;
                }
                else
                {
                    px[y * size + x] = clear;
                }
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    private Texture2D GenerateAstronautSilhouetteTexture()
    {
        int w = 128;
        int h = 192;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        Color32[] px = new Color32[w * h];
        Color32 clear = new Color32(0, 0, 0, 0);
        Color32 dark = SuitDark;

        float cx = 64f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x - cx;
                bool isFig = false;

                if (y >= 135 && y <= 178)
                {
                    float dy = y - 156f;
                    if ((dx * dx) / (20f * 20f) + (dy * dy) / (20f * 20f) <= 1.0f)
                        isFig = true;
                }
                if (y >= 124 && y < 135 && Mathf.Abs(dx) <= 14f)
                {
                    isFig = true;
                }
                if (y >= 65 && y < 124)
                {
                    float torsoHalfW = 24f + (124f - y) * 0.12f;
                    float leftBound = -torsoHalfW - 10f;
                    float rightBound = torsoHalfW;
                    if (dx >= leftBound && dx <= rightBound)
                        isFig = true;
                }
                if (y >= 85 && y <= 118)
                {
                    if (Mathf.Abs(dx) >= 18f && Mathf.Abs(dx) <= 35f)
                        isFig = true;
                }
                if (y >= 0 && y < 65)
                {
                    if (Mathf.Abs(dx) <= 25f)
                        isFig = true;
                }

                px[y * w + x] = isFig ? dark : clear;
            }
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    // =========================================================================
    // 9. MODALS: FLIGHT HANDBOOK, MISSION SELECT & OPTIONS (VERY LARGE & BOLD TEXT)
    // =========================================================================
    private void BuildModals()
    {
        BuildHandbookModal();
        BuildMissionSelectModal();
        BuildOptionsModal();
    }

    private void BuildHandbookModal()
    {
        handbookModalObj = new GameObject("Modal_Flight_Handbook");
        handbookModalObj.transform.SetParent(transform, false);
        handbookModalObj.transform.localPosition = new Vector3(0f, 0f, -2.5f);

        CreateFlatQuad(handbookModalObj.transform, "Hb_Dark_Dim", Vector3.zero, new Vector3(25f, 14.5f, 1f), new Color(0.01f, 0.02f, 0.05f, 0.88f), true, 40);
        CreateFlatQuad(handbookModalObj.transform, "Hb_Card_Bg", Vector3.zero, new Vector3(16.0f, 9.4f, 1f), new Color(0.04f, 0.08f, 0.16f, 0.98f), false, 42);

        Color hbBorder = OrbitGold;
        CreateFlatQuad(handbookModalObj.transform, "Hb_Border_T", new Vector3(0f,  4.70f, -0.05f), new Vector3(16.04f, 0.06f, 1f), hbBorder, false, 43);
        CreateFlatQuad(handbookModalObj.transform, "Hb_Border_B", new Vector3(0f, -4.70f, -0.05f), new Vector3(16.04f, 0.06f, 1f), hbBorder, false, 43);
        CreateFlatQuad(handbookModalObj.transform, "Hb_Border_L", new Vector3(-8.00f, 0f, -0.05f), new Vector3(0.06f, 9.44f, 1f), hbBorder, false, 43);
        CreateFlatQuad(handbookModalObj.transform, "Hb_Border_R", new Vector3( 8.00f, 0f, -0.05f), new Vector3(0.06f, 9.44f, 1f), hbBorder, false, 43);

        CreateText(handbookModalObj.transform, "Hb_Title", new Vector3(0f, 3.95f, -0.1f), "★ NASA FLIGHT DIRECTOR'S OPERATIONAL HANDBOOK ★", 0.076f, 44, FontStyle.Bold, TextAnchor.MiddleCenter, OrbitGold, 45);

        string col1 =
            "<color=#00E5FF><b>1. TSIOLKOVSKY & DELTA-V LAWS:</b></color>\n" +
            "  • LEO Target: 1,600 m/s (Low Earth Orbit)\n" +
            "  • Moon Target: 2,800 m/s (Lunar Polar Insertion)\n" +
            "  • Mars Target: 3,800 m/s (Deep Space Trans-Mars)\n" +
            "  • Heavy payloads cut Delta-V exponentially!\n" +
            "    Always maintain positive dV margin for success.\n\n" +
            "<color=#FFD133><b>2. POWER ARCHITECTURE & ECLIPSE:</b></color>\n" +
            "  • Solar arrays yield 0W during 180° umbra shadow.\n" +
            "  • Mars solar flux drops sharply to 45% sunlight.\n" +
            "  • MMRTG nuclear core provides 24/7 continuous\n" +
            "    power even in total planetary shadow.\n" +
            "  • Size battery watt-hours for blackout survival!";

        string col2 =
            "<color=#FF5555><b>3. TRANSONIC HAZARDS & STAGING:</b></color>\n" +
            "  • Max-Q (Y=5.8-8.8): Throttle to 65% [S] or\n" +
            "    install Grid Fins to withstand shear stress.\n" +
            "  • MECO (Y=10.2): Press [SPACE] to decouple\n" +
            "    booster & ignite vacuum upper stage engine.\n" +
            "  • Vacuum (Y=12.2): Press [SPACE] to jettison\n" +
            "    fairing shroud & deploy ROSA solar wings.\n" +
            "  • Trajectory (Y=21): Use [A / D] to center corridor.\n\n" +
            "<color=#2ED973><b>4. ZERO-BUILD & 1-CLICK PRESETS:</b></color>\n" +
            "  • Custom Rockets: Build from scratch anytime!\n" +
            "  • 1-Click Loadout: Click [AUTO-RECOMMEND] for\n" +
            "    NASA flight-certified stage configs.";

        CreateText(handbookModalObj.transform, "Hb_Col1", new Vector3(-7.30f, 2.95f, -0.1f), col1, 0.058f, 34, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, 45);
        CreateText(handbookModalObj.transform, "Hb_Col2", new Vector3( 0.35f, 2.95f, -0.1f), col2, 0.058f, 34, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, 45);

        GameObject closeBtn = new GameObject("BTN_Close_Handbook");
        closeBtn.transform.SetParent(handbookModalObj.transform, false);
        closeBtn.transform.localPosition = new Vector3(0f, -3.75f, -0.1f);
        BoxCollider bc = closeBtn.AddComponent<BoxCollider>();
        bc.size = new Vector3(6.5f, 0.90f, 2.0f);
        CreateFlatQuad(closeBtn.transform, "Bg", Vector3.zero, new Vector3(6.5f, 0.90f, 1f), new Color(0.90f, 0.40f, 0.10f), false, 45);
        CreateText(closeBtn.transform, "Close_Label", new Vector3(0f, 0f, -0.05f), "✕ CLOSE MANUAL & RETURN", 0.055f, 32, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, 46);

        handbookModalObj.SetActive(false);
    }

    private void BuildMissionSelectModal()
    {
        missionSelectModalObj = new GameObject("Modal_Mission_Select");
        missionSelectModalObj.transform.SetParent(transform, false);
        missionSelectModalObj.transform.localPosition = new Vector3(0f, 0f, -2.5f);

        CreateFlatQuad(missionSelectModalObj.transform, "Ms_Dim", Vector3.zero, new Vector3(25f, 14.5f, 1f), new Color(0.01f, 0.02f, 0.05f, 0.88f), true, 40);
        CreateFlatQuad(missionSelectModalObj.transform, "Ms_Card_Bg", Vector3.zero, new Vector3(16.5f, 9.8f, 1f), new Color(0.04f, 0.08f, 0.16f, 0.98f), false, 42);

        CreateText(missionSelectModalObj.transform, "Ms_Title", new Vector3(0f, 4.05f, -0.1f), "★ SELECT EXPEDITION DESTINATION ★", 0.080f, 48, FontStyle.Bold, TextAnchor.MiddleCenter, CyberCyan, 45);

        string[] planetNames = { "LOW EARTH ORBIT (LEO)", "LUNAR POLAR ORBIT", "MARS DEEP SPACE ORBIT" };
        string[] planetBadges = { "EXPEDITION 01", "EXPEDITION 02", "EXPEDITION 03" };

        // Substantially formatted, large-text mission details
        string[] planetDetails = {
            "<color=#00E5FF><b>BUDGET:</b></color>      <b>$1000M</b>\n" +
            "<color=#00E5FF><b>REQUIRED dV:</b></color> <b>1,600 m/s</b>\n" +
            "<color=#00E5FF><b>SOLAR FLUX:</b></color>  <b>100% Sunlight</b>\n" +
            "<color=#00E5FF><b>SCIENCE:</b></color>     <b>80 Points</b>\n" +
            "<color=#2ED973><b>ENVIRONMENT:</b></color> Earth Magnetosphere",

            "<color=#FFD133><b>BUDGET:</b></color>      <b>$1400M</b>\n" +
            "<color=#FFD133><b>REQUIRED dV:</b></color> <b>2,800 m/s</b>\n" +
            "<color=#FFD133><b>SOLAR FLUX:</b></color>  <b>85% Sunlight</b>\n" +
            "<color=#FFD133><b>SCIENCE:</b></color>     <b>120 Points</b>\n" +
            "<color=#E67E22><b>HAZARD:</b></color>      Van Allen Belts",

            "<color=#FF6B4A><b>BUDGET:</b></color>      <b>$1850M</b>\n" +
            "<color=#FF6B4A><b>REQUIRED dV:</b></color> <b>3,800 m/s</b>\n" +
            "<color=#FF6B4A><b>SOLAR FLUX:</b></color>  <b>45% (Deep Space)</b>\n" +
            "<color=#FF6B4A><b>SCIENCE:</b></color>     <b>180 Points</b>\n" +
            "<color=#9B59B6><b>CORE NEEDED:</b></color> Nuclear MMRTG / Ion"
        };
        Color[] planetCols = { CyberCyan, OrbitGold, new Color(1.0f, 0.42f, 0.28f) };

        for (int i = 0; i < 3; i++)
        {
            float cx = -5.1f + i * 5.1f;
            GameObject pCard = new GameObject("BTN_DestCard_" + i);
            pCard.transform.SetParent(missionSelectModalObj.transform, false);
            pCard.transform.localPosition = new Vector3(cx, 0.45f, -0.1f);
            BoxCollider bc = pCard.AddComponent<BoxCollider>();
            bc.size = new Vector3(4.8f, 5.0f, 2.0f);

            // Card background & glowing border
            CreateFlatQuad(pCard.transform, "Bg", Vector3.zero, new Vector3(4.8f, 5.0f, 1f), new Color(0.06f, 0.12f, 0.22f, 0.95f), false, 44);
            CreateFlatQuad(pCard.transform, "Border", new Vector3(0f, 0f, 0.02f), new Vector3(4.88f, 5.08f, 1f), planetCols[i], false, 43);

            // Badge Header (Expedition 01)
            CreateText(pCard.transform, "Badge", new Vector3(0f, 2.05f, -0.05f), planetBadges[i], 0.046f, 28, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.70f, 0.85f, 0.95f), 46);

            // Destination Title (Large, Bold 34pt)
            CreateText(pCard.transform, "Title", new Vector3(0f, 1.55f, -0.05f), planetNames[i], 0.056f, 34, FontStyle.Bold, TextAnchor.MiddleCenter, planetCols[i], 46);

            // Card Details (Substantially larger text 32pt, properly padded)
            CreateText(pCard.transform, "Details", new Vector3(-2.10f, 1.05f, -0.05f), planetDetails[i], 0.054f, 32, FontStyle.Normal, TextAnchor.UpperLeft, Color.white, 46);

            // Launch button pill container (Unscaled, prominent)
            GameObject launchPill = new GameObject("BTN_LaunchPill_" + i);
            launchPill.transform.SetParent(pCard.transform, false);
            launchPill.transform.localPosition = new Vector3(0f, -1.80f, -0.05f);
            CreateFlatQuad(launchPill.transform, "Pill_Bg", Vector3.zero, new Vector3(3.9f, 0.78f, 1f), planetCols[i], false, 46);
            CreateText(launchPill.transform, "PillText", new Vector3(0f, 0f, -0.02f), "SELECT DESTINATION", 0.052f, 30, FontStyle.Bold, TextAnchor.MiddleCenter, Color.black, 47);
        }

        // Close Button
        GameObject closeBtn = new GameObject("BTN_Close_MissionSelect");
        closeBtn.transform.SetParent(missionSelectModalObj.transform, false);
        closeBtn.transform.localPosition = new Vector3(0f, -3.95f, -0.1f);
        BoxCollider cBc = closeBtn.AddComponent<BoxCollider>();
        cBc.size = new Vector3(6.5f, 0.90f, 2.0f);
        CreateFlatQuad(closeBtn.transform, "Bg", Vector3.zero, new Vector3(6.5f, 0.90f, 1f), new Color(0.90f, 0.40f, 0.10f), false, 45);
        CreateText(closeBtn.transform, "Close_Label", new Vector3(0f, 0f, -0.05f), "✕ RETURN TO MAIN MENU", 0.055f, 32, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, 46);

        missionSelectModalObj.SetActive(false);
    }

    private void BuildOptionsModal()
    {
        optionsModalObj = new GameObject("Modal_Options");
        optionsModalObj.transform.SetParent(transform, false);
        optionsModalObj.transform.localPosition = new Vector3(0f, 0f, -2.5f);

        CreateFlatQuad(optionsModalObj.transform, "Opt_Dim", Vector3.zero, new Vector3(25f, 14.5f, 1f), new Color(0.01f, 0.02f, 0.05f, 0.88f), true, 40);
        CreateFlatQuad(optionsModalObj.transform, "Opt_Card_Bg", Vector3.zero, new Vector3(14.2f, 8.2f, 1f), new Color(0.04f, 0.08f, 0.16f, 0.98f), false, 42);

        Color optBorder = CyberCyan;
        CreateFlatQuad(optionsModalObj.transform, "Opt_Border_T", new Vector3(0f,  4.10f, -0.05f), new Vector3(14.24f, 0.06f, 1f), optBorder, false, 43);
        CreateFlatQuad(optionsModalObj.transform, "Opt_Border_B", new Vector3(0f, -4.10f, -0.05f), new Vector3(14.24f, 0.06f, 1f), optBorder, false, 43);
        CreateFlatQuad(optionsModalObj.transform, "Opt_Border_L", new Vector3(-7.10f, 0f, -0.05f), new Vector3(0.06f, 8.24f, 1f), optBorder, false, 43);
        CreateFlatQuad(optionsModalObj.transform, "Opt_Border_R", new Vector3( 7.10f, 0f, -0.05f), new Vector3(0.06f, 8.24f, 1f), optBorder, false, 43);

        CreateText(optionsModalObj.transform, "Opt_Title", new Vector3(0f, 3.20f, -0.1f), "★ SIMULATOR SETTINGS & AUDIO ★", 0.074f, 42, FontStyle.Bold, TextAnchor.MiddleCenter, CyberCyan, 45);

        string optBody =
            "<color=#00E5FF>AUDIO SYSTEM:</color>  <b>PROCEDURAL REAL-TIME DSP SYNTHESIS</b> (Active)\n\n" +
            "<color=#00E5FF>AUDIO CHANNELS:</color> <b>SURROUND STEREO</b> (Atmosphere + Engine Synthesis)\n\n" +
            "<color=#00E5FF>RENDER ENGINE:</color> <b>UNITY 6 URP / FULL HD 1080P 60FPS</b>\n\n" +
            "<color=#00E5FF>PHYSICS MODEL:</color> <b>TSIOLKOVSKY ROCKET EQUATION FLIGHT DYNAMICS</b>\n\n" +
            "<color=#FFD133>FLIGHT CONTROLS:</color> [W / S] Throttle  |  [A / D] Gimbal Steering  |  [SPACE] Staging";

        CreateText(optionsModalObj.transform, "Opt_Body", new Vector3(0f, 0.60f, -0.1f), optBody, 0.056f, 32, FontStyle.Normal, TextAnchor.MiddleCenter, Color.white, 45);

        GameObject closeBtn = new GameObject("BTN_Close_Options");
        closeBtn.transform.SetParent(optionsModalObj.transform, false);
        closeBtn.transform.localPosition = new Vector3(0f, -2.80f, -0.1f);
        BoxCollider bc = closeBtn.AddComponent<BoxCollider>();
        bc.size = new Vector3(6.5f, 0.90f, 2.0f);
        CreateFlatQuad(closeBtn.transform, "Bg", Vector3.zero, new Vector3(6.5f, 0.90f, 1f), new Color(0.90f, 0.40f, 0.10f), false, 45);
        CreateText(closeBtn.transform, "Close_Label", new Vector3(0f, 0f, -0.05f), "✕ RETURN TO MAIN MENU", 0.055f, 32, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, 46);

        optionsModalObj.SetActive(false);
    }

    private void BuildFadeOverlay()
    {
        fadeOverlayObj = CreateFlatQuad(transform, "Screen_Fade_Overlay", new Vector3(0f, 0f, -5f), new Vector3(26f, 14f, 1f), Color.black, false, 100);
        fadeOverlayRenderer = fadeOverlayObj.GetComponent<Renderer>();
        fadeOverlayObj.SetActive(false);
    }

    // =========================================================================
    // 10. INTERACTION & TRANSITIONS
    // =========================================================================
    private void HandleMouseInteraction()
    {
        if (Camera.main == null) return;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f);

        if (hits != null && hits.Length > 0)
        {
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                Transform hitT = h.collider.transform;
                string n = hitT.gameObject.name;
                string parentName = hitT.parent != null ? hitT.parent.gameObject.name : "";

                // Close Button Click (Checks both parent button and any child)
                bool isCloseBtn = (n == "BTN_Close_Handbook" || parentName == "BTN_Close_Handbook" ||
                                   n == "BTN_Close_MissionSelect" || parentName == "BTN_Close_MissionSelect" ||
                                   n == "BTN_Close_Options" || parentName == "BTN_Close_Options");

                // Dim Background Click (Clicking outside modal closes it)
                bool isDimBg = (n == "Hb_Dark_Dim" || n == "Ms_Dim" || n == "Opt_Dim");

                if ((isCloseBtn || isDimBg) && Input.GetMouseButtonDown(0))
                {
                    CloseAllModals();
                    return;
                }

                // Destination Card or Pill Click
                bool isDestCard = n.StartsWith("BTN_DestCard_") || parentName.StartsWith("BTN_DestCard_") ||
                                  n.StartsWith("BTN_LaunchPill_") || parentName.StartsWith("BTN_LaunchPill_");

                if (isDestCard && Input.GetMouseButtonDown(0))
                {
                    StartMissionDirectly();
                    return;
                }

                // Main Menu List Item Hover & Click
                if (n.StartsWith("MenuItem_"))
                {
                    string idxStr = n.Substring("MenuItem_".Length);
                    if (int.TryParse(idxStr, out int idx))
                    {
                        if (selectedMenuIndex != idx)
                        {
                            selectedMenuIndex = idx;
                            PlaySfx(blipClip);
                            UpdateMenuVisuals();
                        }
                        if (Input.GetMouseButtonDown(0))
                        {
                            ActivateSelectedMenu();
                            return;
                        }
                    }
                }
            }
        }
    }

    private void ActivateSelectedMenu()
    {
        PlaySfx(selectClip);
        switch (selectedMenuIndex)
        {
            case 0: // CONTINUE MISSION
            case 1: // NEW EXPEDITION
                StartMissionDirectly();
                break;
            case 2: // MISSION SELECT
                OpenMissionSelectModal();
                break;
            case 3: // FLIGHT HANDBOOK
                OpenHandbookModal();
                break;
            case 4: // SIMULATOR SETTINGS
                OpenOptionsModal();
                break;
            case 5: // QUIT SIMULATOR
                QuitApplication();
                break;
        }
    }

    private void StartMissionDirectly()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        PlaySfx(selectClip);
        StartCoroutine(TransitionToSpaceMissionScene());
    }

    private IEnumerator TransitionToSpaceMissionScene()
    {
        if (fadeOverlayObj != null && fadeOverlayRenderer != null)
        {
            fadeOverlayObj.SetActive(true);
            float elapsed = 0f;
            float duration = 0.55f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Clamp01(elapsed / duration);
                fadeOverlayRenderer.material.color = new Color(0f, 0f, 0f, a);
                if (ambientSource != null) ambientSource.volume = 0.25f * (1f - a);
                yield return null;
            }
        }

        SceneManager.LoadScene("SpaceMissionScene");
    }

    private void OpenHandbookModal()
    {
        CloseAllModals();
        if (handbookModalObj != null) handbookModalObj.SetActive(true);
    }

    private void OpenMissionSelectModal()
    {
        CloseAllModals();
        if (missionSelectModalObj != null) missionSelectModalObj.SetActive(true);
    }

    private void OpenOptionsModal()
    {
        CloseAllModals();
        if (optionsModalObj != null) optionsModalObj.SetActive(true);
    }

    private void CloseAllModals()
    {
        PlaySfx(blipClip);
        if (handbookModalObj != null) handbookModalObj.SetActive(false);
        if (missionSelectModalObj != null) missionSelectModalObj.SetActive(false);
        if (optionsModalObj != null) optionsModalObj.SetActive(false);
    }

    private void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // =========================================================================
    // 11. CONTINUOUS SPACE ANIMATIONS (FASTER EARTH ROTATION, CLOUDS & STARDUST)
    // =========================================================================
    private void AnimateSpaceAtmosphere(float dt)
    {
        // 1. Noticeably faster, smooth and majestic Earth globe axial rotation
        if (earthGlobeTransform != null)
        {
            earthGlobeTransform.Rotate(Vector3.up, -3.5f * dt, Space.Self);
        }

        // 2. Swirling cloud atmospheric layer rotating at slightly faster rate (wind shear)
        if (earthCloudsTransform != null)
        {
            earthCloudsTransform.Rotate(Vector3.up, -4.8f * dt, Space.Self);
        }

        // 3. Floating stardust drift across the observation window
        if (spaceDustTransforms != null)
        {
            for (int i = 0; i < spaceDustTransforms.Length; i++)
            {
                if (spaceDustTransforms[i] == null) continue;
                Vector3 p = spaceDustTransforms[i].localPosition;
                p.x += (0.15f + (i % 4) * 0.08f) * dt;
                p.y += Mathf.Sin(Time.time * 0.8f + i) * 0.06f * dt;
                if (p.x > 11.0f) { p.x = -11.0f; p.y = (float)(new System.Random(i + (int)(Time.time * 10)).NextDouble() * 11.0 - 5.5); }
                spaceDustTransforms[i].localPosition = p;
            }
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    // =========================================================================
    // 12. PRIMITIVE FACTORY HELPERS
    // =========================================================================
    private GameObject CreateFlatQuad(Transform parent, string name, Vector3 pos, Vector3 scale, Color col, bool addCollider = false, int sortingOrder = 0)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localScale = scale;

        if (!addCollider)
        {
            Collider c = q.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }
        else
        {
            Collider oldC = q.GetComponent<Collider>();
            if (oldC != null) Destroy(oldC);
            BoxCollider bc = q.AddComponent<BoxCollider>();
            bc.size = new Vector3(1f, 1f, 3.0f);
        }

        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        Material m = new Material(sh);
        m.color = col;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);

        Renderer r = q.GetComponent<Renderer>();
        r.material = m;
        if (sortingOrder != 0) r.sortingOrder = sortingOrder;
        return q;
    }

    private GameObject CreateTexturedQuad(Transform parent, string name, Vector3 pos, Vector3 scale, Texture2D tex, int sortingOrder = 0)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        q.transform.SetParent(parent, false);
        q.transform.localPosition = pos;
        q.transform.localScale = scale;

        Collider c = q.GetComponent<Collider>();
        if (c != null) Destroy(c);

        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
        Material m = new Material(sh);
        m.mainTexture = tex;
        m.color = Color.white;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);

        Renderer r = q.GetComponent<Renderer>();
        r.material = m;
        if (sortingOrder != 0) r.sortingOrder = sortingOrder;
        return q;
    }

    private TextMesh CreateText(Transform parent, string name, Vector3 pos, string text, float charSize, int fontSize, FontStyle style, TextAnchor anchor, Color col, int sortingOrder = 0)
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
        tm.alignment = (anchor == TextAnchor.MiddleCenter || anchor == TextAnchor.UpperCenter) ? TextAlignment.Center : TextAlignment.Left;
        tm.color = col;
        tm.richText = true;
        if (sortingOrder != 0) tm.GetComponent<Renderer>().sortingOrder = sortingOrder;
        return tm;
    }
}
