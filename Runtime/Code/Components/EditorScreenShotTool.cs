using UnityEngine;

namespace Code.Components {
    public class EditorScreenShotTool : MonoBehaviour {
        public string folderPath = "~/Desktop/";
        private void Update(){
            if(Input.GetMouseButtonDown(0)) { // capture screen shot on left mouse button down
                if (!System.IO.Directory.Exists(folderPath)) // if this path does not exist yet
                    System.IO.Directory.CreateDirectory(folderPath);  // it will get created

                var screenshotName =
                    "Screenshot_" +
                    System.DateTime.Now.ToString("dd-MM-yyyy-HH-mm-ss") + // puts the current time right into the screenshot name
                    ".png"; // put you're favorite data format here
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(folderPath, screenshotName),2); // takes the sceenshot, the "2" is for the scaled resolution, you can put this to 600 but it will take really long to scale the image up
                Debug.Log(folderPath + screenshotName); // You get instant feedback in the console
            }
        }
    }
}