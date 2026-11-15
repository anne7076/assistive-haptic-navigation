using System.Collections;
using System.Threading;
using hapticDriver;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Thread-safe, hardware-agnostic bridge between Unity and the 4-motor haptic belt driver.
/// Can be dropped into any VR scene (XR Origin, First-Person rig, or TP5 Cylinder).
/// </summary>
public class VibrationManager_minimal : MonoBehaviour
{
    [Header("Hardware Connection")]
    [Tooltip("Serial port assigned to the haptic controller (e.g., COM4 on Windows).")]
    public string devicePort = "COM4";

    [Tooltip("Enable to simulate driver output without physical serial hardware.")]
    public bool useMock = true;

    [Tooltip("Enable to log every driver packet change to the Console (disable in VR for best frame rate).")]
    public bool verboseDriverLogs = false;

    [Tooltip("Keep this manager alive across scene loads (useful when switching VR scenes).")]
    public bool dontDestroyOnLoad = false;

    [Tooltip("Packet send interval in milliseconds (hardware default: 40 ms).")]
    [Range(20, 200)]
    public int sendIntervalMs = 40;

    [Header("Physical Motor Channel Mapping (0..3)")]
    [Tooltip("Hardware channel index (0-3) wired to the Front motor.")]
    [Range(0, 3)] public int frontChannel = 0;

    [Tooltip("Hardware channel index (0-3) wired to the Back motor.")]
    [Range(0, 3)] public int backChannel = 1;

    [Tooltip("Hardware channel index (0-3) wired to the Left motor.")]
    [Range(0, 3)] public int leftChannel = 2;

    [Tooltip("Hardware channel index (0-3) wired to the Right motor.")]
    [Range(0, 3)] public int rightChannel = 3;

    [Header("Live Raw Hardware Channels (0 - 254)")]
    [Range(0, 254)] public byte motor_1 = 0;
    [Range(0, 254)] public byte motor_2 = 0;
    [Range(0, 254)] public byte motor_3 = 0;
    [Range(0, 254)] public byte motor_4 = 0;

    // Driver reserves 255 as EndMarker, so 254 is the maximum valid PWM byte.
    private const byte MaxMotorIntensity = 254;

    private readonly object _driverLock = new object();
    private readonly byte[] _channelBuffer = new byte[4];
    private readonly byte[] _lastSentChannels = new byte[4] { 255, 255, 255, 255 };
    private Driver _driver;
    private Timer _callbackTimer;

    private void Awake()
    {
        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        InitDriver();
    }

    /// <summary>
    /// Opens the serial/mock driver, initializes a clean 0-packet, and starts the periodic emitter timer.
    /// </summary>
    public void InitDriver()
    {
        Disconnect();

        lock (_driverLock)
        {
            _driver = new Driver(devicePort, useMock);
            _driver.SetVerbose(verboseDriverLogs);
            _driver.SetMessage(BuildPacket(0, 0, 0, 0));
            ResetDirtyCache();
            _callbackTimer = new Timer(EmitterCallback, null, 0, sendIntervalMs);
        }
    }

    /// <summary>
    /// Safely stops the emitter timer, zeros all motors, and closes the serial port.
    /// </summary>
    public void Disconnect()
    {
        lock (_driverLock)
        {
            if (_callbackTimer != null)
            {
                _callbackTimer.Dispose();
                _callbackTimer = null;
            }

            if (_driver != null)
            {
                _driver.SetVerbose(false);
                _driver.SetMessage(BuildPacket(0, 0, 0, 0));
                _driver.SendMessage();
                _driver.Close();
                _driver = null;
            }

            motor_1 = motor_2 = motor_3 = motor_4 = 0;
            ResetDirtyCache();
        }
    }

    /// <summary>
    /// Maps directional intensities (Front, Back, Left, Right) onto the configured physical channels (0..3)
    /// and updates the driver only when at least one channel changes.
    /// </summary>
    public void SetDirectionalIntensities(byte front, byte back, byte left, byte right)
    {
        _channelBuffer[0] = 0;
        _channelBuffer[1] = 0;
        _channelBuffer[2] = 0;
        _channelBuffer[3] = 0;

        AssignChannelMax(frontChannel, front);
        AssignChannelMax(backChannel, back);
        AssignChannelMax(leftChannel, left);
        AssignChannelMax(rightChannel, right);

        SetMotorIntensities(_channelBuffer[0], _channelBuffer[1], _channelBuffer[2], _channelBuffer[3]);
    }

    /// <summary>
    /// Sets the 4 raw hardware channel intensities (clamped to 0-254).
    /// Performs dirty checking so unchanged frames do not spam the driver or Console.
    /// </summary>
    public void SetMotorIntensities(byte ch0, byte ch1, byte ch2, byte ch3)
    {
        motor_1 = ClampIntensity(ch0);
        motor_2 = ClampIntensity(ch1);
        motor_3 = ClampIntensity(ch2);
        motor_4 = ClampIntensity(ch3);

        if (motor_1 == _lastSentChannels[0] &&
            motor_2 == _lastSentChannels[1] &&
            motor_3 == _lastSentChannels[2] &&
            motor_4 == _lastSentChannels[3])
        {
            return;
        }

        _lastSentChannels[0] = motor_1;
        _lastSentChannels[1] = motor_2;
        _lastSentChannels[2] = motor_3;
        _lastSentChannels[3] = motor_4;

        lock (_driverLock)
        {
            if (_driver != null)
            {
                _driver.SetVerbose(verboseDriverLogs);
                _driver.SetMessage(BuildPacket(motor_1, motor_2, motor_3, motor_4));
            }
        }
    }

    /// <summary>
    /// Immediately stops all four motors.
    /// </summary>
    public void StopAllMotors()
    {
        SetMotorIntensities(0, 0, 0, 0);
    }

    /// <summary>
    /// Plays a sequential test pulse (Front -> Right -> Back -> Left) to verify physical belt wiring.
    /// </summary>
    public void PlayTestSequence()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[VibrationManager] Test sequence can only run in Play Mode.");
            return;
        }
        StopAllCoroutines();
        StartCoroutine(TestSequenceCoroutine());
    }

    private IEnumerator TestSequenceCoroutine()
    {
        const byte testPwm = 180;
        const float stepSeconds = 0.35f;

        SetDirectionalIntensities(testPwm, 0, 0, 0); yield return new WaitForSeconds(stepSeconds);
        SetDirectionalIntensities(0, 0, 0, testPwm); yield return new WaitForSeconds(stepSeconds);
        SetDirectionalIntensities(0, testPwm, 0, 0); yield return new WaitForSeconds(stepSeconds);
        SetDirectionalIntensities(0, 0, testPwm, 0); yield return new WaitForSeconds(stepSeconds);
        StopAllMotors();
    }

    private void AssignChannelMax(int channelIndex, byte value)
    {
        int idx = Mathf.Clamp(channelIndex, 0, 3);
        byte clamped = ClampIntensity(value);
        if (clamped > _channelBuffer[idx])
        {
            _channelBuffer[idx] = clamped;
        }
    }

    private void ResetDirtyCache()
    {
        _lastSentChannels[0] = 255;
        _lastSentChannels[1] = 255;
        _lastSentChannels[2] = 255;
        _lastSentChannels[3] = 255;
    }

    private void EmitterCallback(object state)
    {
        lock (_driverLock)
        {
            _driver?.SendMessage();
        }
    }

    private static byte ClampIntensity(byte value)
    {
        return value > MaxMotorIntensity ? MaxMotorIntensity : value;
    }

    private static byte[] BuildPacket(byte m1, byte m2, byte m3, byte m4)
    {
        return new byte[5] { m1, m2, m3, m4, Driver.EndMarker };
    }

    private void OnDestroy()
    {
        Disconnect();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(VibrationManager_minimal))]
public class VibrationManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        var instance = (VibrationManager_minimal)target;
        GUILayout.Space(8);

        if (GUILayout.Button("Initialize / Reconnect Driver"))
            instance.InitDriver();

        if (GUILayout.Button("Play Test Pulse (F -> R -> B -> L)"))
            instance.PlayTestSequence();

        if (GUILayout.Button("Stop All Motors"))
            instance.StopAllMotors();

        if (GUILayout.Button("Disconnect"))
            instance.Disconnect();
    }
}
#endif
