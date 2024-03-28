using System;
using System.Reflection;
using UnityEditor.DeviceSimulation;
using UnityEngine;
using UnityEngine.UIElements;

    public sealed class AirshipDeviceSimulator : DeviceSimulatorPlugin
    {
        readonly FieldInfo _mainField;
        readonly PropertyInfo _deviceIndexProperty;
        readonly FieldInfo _devicesField;
        readonly UnityEngine.Object _simulatorWindow;

        public override string title => "Device Switcher";

        public AirshipDeviceSimulator() {
            var assembly = Assembly.GetAssembly(typeof(DeviceSimulator));
            var windowType = assembly.GetType("UnityEditor.DeviceSimulation.SimulatorWindow");
            _mainField = windowType.GetField("m_Main", BindingFlags.Instance | BindingFlags.NonPublic);
            var mainType = assembly.GetType("UnityEditor.DeviceSimulation.DeviceSimulatorMain");
            _deviceIndexProperty = mainType.GetProperty("deviceIndex", BindingFlags.Instance | BindingFlags.Public);
            _devicesField = mainType.GetField("m_Devices", BindingFlags.Instance | BindingFlags.NonPublic);
            _simulatorWindow = Resources.FindObjectsOfTypeAll(windowType)[0];
        }

        public override VisualElement OnCreateUI() {
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;

            var previousButton = new RepeatButton(SelectPreviousSimulatorDevice, 500, 250);
            var nextButton = new RepeatButton(SelectNextSimulatorDevice, 500, 250);
            previousButton.text = "Previous";
            nextButton.text = "Next";
            previousButton.AddToClassList("unity-button");
            nextButton.AddToClassList("unity-button");

            root.Add(previousButton);
            root.Add(nextButton);

            return root;
        }

        private void SelectPreviousSimulatorDevice() => SelectDeviceWithOffset(-1);

        private void SelectNextSimulatorDevice() => SelectDeviceWithOffset(1);

        private void SelectDeviceWithOffset(int offset)
        {
            var main = _mainField.GetValue(_simulatorWindow);
            var devices = _devicesField.GetValue(main) as Array;

            var index = (int)_deviceIndexProperty.GetValue(main);
            index = (int)Mathf.Repeat(index + offset, devices!.Length);

            _deviceIndexProperty.SetValue(main, index);
        }
    }