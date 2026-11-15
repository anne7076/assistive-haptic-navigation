using System;
using System.Globalization;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Lightweight experimental evaluation logger (TP7-TP12 validation & Soutenance metrics).
/// Tracks elapsed time, distance walked, obstacle collision count, wall-contact duration,
/// and waypoint progress without coupling to locomotion or haptic hardware code.
/// Press [R] to reset the current trial and [L] to export a row to CSV.
/// </summary>
public class ExperimentMetricsLogger : MonoBehaviour
{
    [Header("Optional Scene Components (Auto-Resolved)")]
    public HapticObstacleFeedback hapticFeedback;
    public VisualDefectController visualDefects;
    public Navigation navigation;

    [Header("Collision Filtering")]
    [Tooltip("Minimum delay (s) between distinct collision counts to prevent multi-bounce inflation.")]
    public float collisionCooldown = 0.45f;

    [Tooltip("Surface normals with |y| below this threshold count as vertical obstacles (walls/pillars), ignoring the floor.")]
    [Range(0.1f, 0.9f)]
    public float maxObstacleNormalY = 0.6f;

    [Header("Waypoint Audio Cue (Separate From Haptic Motors)")]
    [Tooltip("Plays a subtle procedural chime when a Navigation waypoint is reached.")]
    public bool playWaypointChime = true;

    [Header("Export & HUD")]
    public bool showMetricsHUD = true;
    [Tooltip("In the Unity Editor, save CSV inside <Project>/ExperimentLogs/ instead of AppData.")]
    public bool saveInProjectFolder = true;
    public string csvFolderName = "ExperimentLogs";
    public string csvFileName = "haptic_experiment_trials.csv";

    public float ElapsedTime { get; private set; }
    public float DistanceWalked { get; private set; }
    public int CollisionCount { get; private set; }
    public float WallContactSeconds { get; private set; }
    public bool TrialCompleted { get; private set; }

    private Vector3 _lastPosition;
    private float _lastCollisionTime = -10f;
    private float _lastChimeTime = -10f;
    private int _lastObservedWaypointIndex;
    private bool _contactRecordedThisFixedStep;
    private AudioSource _audioSource;
    private AudioClip _chimeClip;
    private string _statusNotice = "";
    private float _statusNoticeTimer;

    private void Awake()
    {
        if (hapticFeedback == null) hapticFeedback = GetComponent<HapticObstacleFeedback>();
        if (visualDefects == null) visualDefects = GetComponent<VisualDefectController>();
        if (navigation == null) navigation = GetComponent<Navigation>();

        SetupProceduralAudio();
    }

    private void Start()
    {
        ResetTrial(resetNavigation: false);
    }

    private void Update()
    {
        if (!TrialCompleted)
        {
            ElapsedTime += Time.deltaTime;
            float stepDist = Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(_lastPosition.x, 0f, _lastPosition.z)
            );
            DistanceWalked += stepDist;
        }
        _lastPosition = transform.position;

        MonitorWaypoints();
        HandleHotkeys();

        if (_statusNoticeTimer > 0f)
        {
            _statusNoticeTimer -= Time.deltaTime;
            if (_statusNoticeTimer <= 0f) _statusNotice = "";
        }
    }

    private void FixedUpdate()
    {
        _contactRecordedThisFixedStep = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (TrialCompleted || !IsObstacleCollision(collision))
            return;

        if (Time.time - _lastCollisionTime >= collisionCooldown)
        {
            CollisionCount++;
            _lastCollisionTime = Time.time;
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        if (TrialCompleted || _contactRecordedThisFixedStep || !IsObstacleCollision(collision))
            return;

        WallContactSeconds += Time.fixedDeltaTime;
        _contactRecordedThisFixedStep = true;
    }

    /// <summary>
    /// Resets all trial metrics to zero for starting a new experimental run.
    /// </summary>
    public void ResetTrial(bool resetNavigation = true)
    {
        if (resetNavigation && navigation != null)
        {
            navigation.ResetWaypoints();
        }

        ElapsedTime = 0f;
        DistanceWalked = 0f;
        CollisionCount = 0;
        WallContactSeconds = 0f;
        TrialCompleted = false;
        _lastPosition = transform.position;
        _lastCollisionTime = -10f;
        _lastChimeTime = -10f;
        _lastObservedWaypointIndex = navigation != null ? navigation.currentWaypointIndex : 0;
        SetStatusNotice("Trial reset");
    }

    /// <summary>
    /// Resolves the output CSV file path (project ExperimentLogs folder in Editor, persistentDataPath in builds).
    /// </summary>
    public string GetCsvFilePath()
    {
#if UNITY_EDITOR
        if (saveInProjectFolder)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string folder = Path.Combine(projectRoot, csvFolderName);
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, csvFileName);
        }
#endif
        return Path.Combine(Application.persistentDataPath, csvFileName);
    }

    /// <summary>
    /// Appends the current trial metrics as a row into a CSV file.
    /// </summary>
    public void ExportTrialToCsv()
    {
        string path = GetCsvFilePath();
        bool writeHeader = !File.Exists(path);

        string hapticsState = hapticFeedback != null && hapticFeedback.hapticsEnabled ? "ON" : "OFF";
        string visionLabel = visualDefects != null ? visualDefects.GetActiveModeLabel() : "Unknown";
        int wpDone = navigation != null ? navigation.currentWaypointIndex : 0;
        int wpTotal = navigation != null && navigation.waypoints != null ? navigation.waypoints.Count : 0;

        string line = string.Join(",",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            hapticsState,
            $"\"{visionLabel}\"",
            ElapsedTime.ToString("F2", CultureInfo.InvariantCulture),
            DistanceWalked.ToString("F2", CultureInfo.InvariantCulture),
            CollisionCount.ToString(CultureInfo.InvariantCulture),
            WallContactSeconds.ToString("F2", CultureInfo.InvariantCulture),
            $"{wpDone}/{wpTotal}"
        );

        try
        {
            using (StreamWriter sw = new StreamWriter(path, append: true))
            {
                if (writeHeader)
                {
                    sw.WriteLine("Timestamp,Scene,Haptics,VisualImpairment,ElapsedSec,DistanceMeters,Collisions,WallContactSec,Waypoints");
                }
                sw.WriteLine(line);
            }

            Debug.Log($"[ExperimentMetricsLogger] Saved trial to: {path}");
            SetStatusNotice($"Saved CSV ({CollisionCount} hits)");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ExperimentMetricsLogger] Could not write CSV: {ex.Message}");
            SetStatusNotice("CSV export failed");
        }
    }

    private bool IsObstacleCollision(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (Mathf.Abs(collision.GetContact(i).normal.y) < maxObstacleNormalY)
                return true;
        }
        return false;
    }

    private void MonitorWaypoints()
    {
        if (TrialCompleted || navigation == null || navigation.waypoints == null || navigation.waypoints.Count == 0)
            return;

        int clampedIndex = Mathf.Min(navigation.currentWaypointIndex, navigation.waypoints.Count);
        if (clampedIndex > _lastObservedWaypointIndex)
        {
            _lastObservedWaypointIndex = clampedIndex;

            if (playWaypointChime && _audioSource != null && _chimeClip != null && Time.time - _lastChimeTime >= 0.25f)
            {
                _audioSource.PlayOneShot(_chimeClip, 0.25f);
                _lastChimeTime = Time.time;
            }

            if (clampedIndex >= navigation.waypoints.Count)
            {
                TrialCompleted = true;
                SetStatusNotice("Course completed! Press [L] to save CSV");
            }
        }
    }

    private void SetupProceduralAudio()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;

        // Synthesize a gentle 150 ms C5 sine chime at runtime (zero asset dependencies).
        const int sampleRate = 22050;
        const float duration = 0.15f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Sin(Mathf.PI * (i / (float)sampleCount));
            samples[i] = Mathf.Sin(2f * Mathf.PI * 523.25f * t) * env * 0.4f;
        }

        _chimeClip = AudioClip.Create("WaypointChime", sampleCount, 1, sampleRate, false);
        _chimeClip.SetData(samples, 0);
    }

    private void HandleHotkeys()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        if (kb.rKey.wasPressedThisFrame) ResetTrial();
        else if (kb.lKey.wasPressedThisFrame) ExportTrialToCsv();
#else
        if (Input.GetKeyDown(KeyCode.R)) ResetTrial();
        else if (Input.GetKeyDown(KeyCode.L)) ExportTrialToCsv();
#endif
    }

    private void SetStatusNotice(string msg)
    {
        _statusNotice = msg;
        _statusNoticeTimer = 3.0f;
    }

    private void OnGUI()
    {
        if (!showMetricsHUD) return;

        int wpDone = navigation != null ? navigation.currentWaypointIndex : 0;
        int wpTotal = navigation != null && navigation.waypoints != null ? navigation.waypoints.Count : 0;
        string wpText = wpTotal > 0 ? $"{Mathf.Min(wpDone, wpTotal)}/{wpTotal}" : "Free Roam";

        GUILayout.BeginArea(new Rect(15, Screen.height - 135, 310, 120), GUI.skin.box);
        GUILayout.Label("<b>Experiment Telemetry</b>  <size=11>([R] Reset | [L] Log CSV)</size>");
        GUILayout.Label($"Time: {ElapsedTime:0.0} s   |   Distance: {DistanceWalked:0.0} m");
        GUILayout.Label($"Collisions: <b>{CollisionCount}</b>   |   Wall Contact: {WallContactSeconds:0.00} s");
        GUILayout.Label($"Waypoints: {wpText}   {(_statusNotice.Length > 0 ? $"<color=#FFFF88>[{_statusNotice}]</color>" : "")}");
        GUILayout.EndArea();
    }
}
