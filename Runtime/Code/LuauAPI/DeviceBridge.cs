using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;


public enum AirshipDeviceType {
    Tablet,
    Phone,
    Desktop
}

[LuauAPI]
public class DeviceBridge {
    public static bool isTablet;

    private static FieldInfo mainField;
    private static PropertyInfo deviceIndexProperty;
    private static FieldInfo devicesField;
    private static UnityEngine.Object simulatorWindow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void OnLoad() {
        // Simulator Reflection
#if UNITY_EDITOR
        var assembly = Assembly.GetAssembly(typeof(DeviceSimulator));
        var windowType = assembly.GetType("UnityEditor.DeviceSimulation.SimulatorWindow");
        mainField = windowType.GetField("m_Main", BindingFlags.Instance | BindingFlags.NonPublic);
        var mainType = assembly.GetType("UnityEditor.DeviceSimulation.DeviceSimulatorMain");
        deviceIndexProperty = mainType.GetProperty("deviceIndex", BindingFlags.Instance | BindingFlags.Public);
        devicesField = mainType.GetField("m_Devices", BindingFlags.Instance | BindingFlags.NonPublic);
        var resources = Resources.FindObjectsOfTypeAll(windowType);
        if (resources.Length > 0) {
            simulatorWindow = Resources.FindObjectsOfTypeAll(windowType)[0];
        }
#endif
    }

    private static float DeviceDiagonalSizeInInches() {
        float screenWidth = Screen.width / Screen.dpi;
        float screenHeight = Screen.height / Screen.dpi;
        float diagonalInches = Mathf.Sqrt(Mathf.Pow(screenWidth, 2) + Mathf.Pow(screenHeight, 2));

        return diagonalInches;
    }

    public static AirshipDeviceType GetDeviceType() {
#if UNITY_IOS
    bool deviceIsIpad = UnityEngine.iOS.Device.generation.ToString().Contains("iPad");
            if (deviceIsIpad)
            {
                return AirshipDeviceType.Tablet;
            }
            bool deviceIsIphone = UnityEngine.iOS.Device.generation.ToString().Contains("iPhone");
            if (deviceIsIphone)
            {
                return AirshipDeviceType.Phone;
            }
#elif UNITY_ANDROID

        float aspectRatio = Mathf.Max(Screen.width, Screen.height) / Mathf.Min(Screen.width, Screen.height);
        bool isTablet = (DeviceDiagonalSizeInInches() > 6.5f && aspectRatio < 2f);

        if (isTablet)
        {
            return AirshipDeviceType.Tablet;
        }
        else
        {
            return AirshipDeviceType.Phone;
        }
#endif

#if UNITY_EDITOR
        if (simulatorWindow != null) {
            var main = mainField.GetValue(simulatorWindow);
            var devices = devicesField.GetValue(main) as Array;

            var index = (int)deviceIndexProperty.GetValue(main);
            var device = devices.GetValue(index);
            // Debug.Log("simulator device: " + device);

            var deviceName = device.ToString();
            if (deviceName.Contains("iPad")) {
                return AirshipDeviceType.Tablet;
            } else if (deviceName.Contains("iPhone")) {
                return AirshipDeviceType.Phone;
            } else {
                return AirshipDeviceType.Phone;
            }
        }
#endif
        return AirshipDeviceType.Desktop;
    }
}