using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Runtime controller for switching simulated visual impairments cleanly via Inspector
/// or keyboard hotkeys (Keys 0-7). Keeps geometric defects disabled by default while
/// swapping materials on the first-person screen quad, making it portable across scenes.
/// </summary>
public class VisualDefectController : MonoBehaviour
{
    public enum VisualDefectMode
    {
        None = 0,
        HemianopsiaUnilateral = 1,
        HemianopsiaBilateral = 2,
        CentralScotoma = 3,
        TunnelVision = 4,
        Parcellaire = 5,
        ParcellaireVoronoi = 6,
        LowAcuityBlur = 7
    }

    [Header("Active Visual Impairment")]
    public VisualDefectMode activeMode = VisualDefectMode.None;

    [Tooltip("Allow switching defects at runtime using number keys 0-7.")]
    public bool enableNumberKeyHotkeys = true;

    [Tooltip("Show current visual defect label in the HUD.")]
    public bool showDefectHUD = true;

    [Header("Scene References (Auto-Resolved if Empty)")]
    public GameObject shaderDefectsRoot;
    public Renderer shaderScreenQuad;

    [Header("Defect Materials (URP Shader Screen Quad)")]
    public Material matHemianopsia;
    public Material matCentralScotoma;
    public Material matTunnelVision;
    public Material matParcellaire;
    public Material matParcellaireVoronoi;
    public Material matLowAcuityBlur;

    [Header("Legacy Geometric Defect Roots (Disabled in Shader Mode)")]
    public GameObject physicalParcellaire;
    public GameObject physicalHemianopsia;
    public GameObject physicalTunnel;

    private VisualDefectMode _lastAppliedMode = (VisualDefectMode)(-1);

    private void Awake()
    {
        AutoResolveReferences();
        ApplyMode(activeMode);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            AutoResolveReferences();
            if (shaderScreenQuad != null)
            {
                ApplyMode(activeMode);
            }
        }
    }

    private void Update()
    {
        if (enableNumberKeyHotkeys)
        {
            HandleHotkeys();
        }

        if (activeMode != _lastAppliedMode)
        {
            ApplyMode(activeMode);
        }
    }

    /// <summary>
    /// Applies the selected visual defect mode immediately to the camera overlay.
    /// </summary>
    public void ApplyMode(VisualDefectMode mode)
    {
        activeMode = mode;
        _lastAppliedMode = mode;

        // Keep legacy geometric blockers disabled so they never stack with screen shaders.
        if (physicalParcellaire != null) physicalParcellaire.SetActive(false);
        if (physicalHemianopsia != null) physicalHemianopsia.SetActive(false);
        if (physicalTunnel != null) physicalTunnel.SetActive(false);

        if (shaderDefectsRoot == null || shaderScreenQuad == null)
            return;

        if (mode == VisualDefectMode.None)
        {
            shaderDefectsRoot.SetActive(false);
            return;
        }

        Material targetMat = GetMaterialForMode(mode);
        if (targetMat == null)
        {
            shaderDefectsRoot.SetActive(false);
            return;
        }

        if (mode == VisualDefectMode.HemianopsiaUnilateral || mode == VisualDefectMode.HemianopsiaBilateral)
        {
            targetMat.SetFloat("_Unilateral", mode == VisualDefectMode.HemianopsiaUnilateral ? 1f : 0f);
        }

        shaderDefectsRoot.SetActive(true);
        shaderScreenQuad.sharedMaterial = targetMat;
    }

    /// <summary>
    /// Returns a concise human-readable name for logs, CSV export, and HUD.
    /// </summary>
    public string GetActiveModeLabel()
    {
        switch (activeMode)
        {
            case VisualDefectMode.None: return "None (Normal)";
            case VisualDefectMode.HemianopsiaUnilateral: return "Hemianopsia (Right)";
            case VisualDefectMode.HemianopsiaBilateral: return "Hemianopsia (Bilateral)";
            case VisualDefectMode.CentralScotoma: return "Central Scotoma";
            case VisualDefectMode.TunnelVision: return "Tunnel Vision";
            case VisualDefectMode.Parcellaire: return "Parcellaire";
            case VisualDefectMode.ParcellaireVoronoi: return "Parcellaire (Voronoi)";
            case VisualDefectMode.LowAcuityBlur: return "Low Acuity (Blur)";
            default: return activeMode.ToString();
        }
    }

    private Material GetMaterialForMode(VisualDefectMode mode)
    {
        switch (mode)
        {
            case VisualDefectMode.HemianopsiaUnilateral:
            case VisualDefectMode.HemianopsiaBilateral:
                return matHemianopsia;
            case VisualDefectMode.CentralScotoma:
                return matCentralScotoma;
            case VisualDefectMode.TunnelVision:
                return matTunnelVision;
            case VisualDefectMode.Parcellaire:
                return matParcellaire;
            case VisualDefectMode.ParcellaireVoronoi:
                return matParcellaireVoronoi;
            case VisualDefectMode.LowAcuityBlur:
                return matLowAcuityBlur;
            default:
                return null;
        }
    }

    private void AutoResolveReferences()
    {
        if (shaderDefectsRoot == null)
        {
            Transform found = FindChildRecursive(transform, "ShaderDefects_Screen");
            if (found != null) shaderDefectsRoot = found.gameObject;
        }

        if (shaderScreenQuad == null && shaderDefectsRoot != null)
        {
            Transform quad = FindChildRecursive(shaderDefectsRoot.transform, "ShaderScreenQuad");
            if (quad != null) shaderScreenQuad = quad.GetComponent<Renderer>();
        }

        if (physicalParcellaire == null)
            physicalParcellaire = FindChildRecursive(transform, "PhysicalDefects_Parcellaire")?.gameObject;
        if (physicalHemianopsia == null)
            physicalHemianopsia = FindChildRecursive(transform, "PhysicalDefects_Hemianopsia")?.gameObject;
        if (physicalTunnel == null)
            physicalTunnel = FindChildRecursive(transform, "PhysicalDefects_Tunnel")?.gameObject;
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root.name == targetName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildRecursive(root.GetChild(i), targetName);
            if (match != null) return match;
        }
        return null;
    }

    private void HandleHotkeys()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame) ApplyMode(VisualDefectMode.None);
        else if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) ApplyMode(VisualDefectMode.HemianopsiaUnilateral);
        else if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) ApplyMode(VisualDefectMode.HemianopsiaBilateral);
        else if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) ApplyMode(VisualDefectMode.CentralScotoma);
        else if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) ApplyMode(VisualDefectMode.TunnelVision);
        else if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) ApplyMode(VisualDefectMode.Parcellaire);
        else if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) ApplyMode(VisualDefectMode.ParcellaireVoronoi);
        else if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) ApplyMode(VisualDefectMode.LowAcuityBlur);
#else
        if (Input.GetKeyDown(KeyCode.Alpha0)) ApplyMode(VisualDefectMode.None);
        else if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyMode(VisualDefectMode.HemianopsiaUnilateral);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyMode(VisualDefectMode.HemianopsiaBilateral);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) ApplyMode(VisualDefectMode.CentralScotoma);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) ApplyMode(VisualDefectMode.TunnelVision);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) ApplyMode(VisualDefectMode.Parcellaire);
        else if (Input.GetKeyDown(KeyCode.Alpha6)) ApplyMode(VisualDefectMode.ParcellaireVoronoi);
        else if (Input.GetKeyDown(KeyCode.Alpha7)) ApplyMode(VisualDefectMode.LowAcuityBlur);
#endif
    }

    private void OnGUI()
    {
        if (!showDefectHUD) return;

        GUILayout.BeginArea(new Rect(Screen.width - 275, 15, 260, 62), GUI.skin.box);
        GUILayout.Label($"<b>Vision:</b> {GetActiveModeLabel()}");
        GUILayout.Label("<size=11>Keys [0-7] switch defect | [H] Haptics</size>");
        GUILayout.EndArea();
    }
}
