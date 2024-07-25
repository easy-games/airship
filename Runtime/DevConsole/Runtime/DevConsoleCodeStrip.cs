using Airship.DevConsole;
using UnityEngine;

public class DevConsoleCodeStrip : MonoBehaviour {
    private void Start() {
        DevConsole.OnConsoleOpened += b => {};
        DevConsole.OnConsoleClosed += b => {};
    }
}