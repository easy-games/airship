using System;
using System.IO;
using UnityEngine;

namespace Code.Components {
    public class EditorScreenShotTool : MonoBehaviour {
        public Environment.SpecialFolder folder = Environment.SpecialFolder.MyPictures;
        public string localFolderPath = "UnityScreenshots";
        public string fileName = "Screenshot";
        public bool addFilenameDateTime = true;
        [Range(1,600)]
        public int superScaleSize = 2;
        public bool transparentBackground = false;

        private string GetFileName(){
            return fileName + 
            (addFilenameDateTime ? "_" +DateTime.Now.ToString("dd-MM-yyyy-HH-mm-ss") : "") + // puts the current time right into the screenshot name
            ".png"; // File format
        }

        private string CreateFolder(){
            string folderPath = Path.Combine(Environment.GetFolderPath(this.folder), this.localFolderPath);
            if (!Directory.Exists(folderPath)) // if this path does not exist yet
                Directory.CreateDirectory(folderPath);  // it will get created

            return folderPath;
        }

        private void Update(){
            // if(Input.GetMouseButtonDown(0)) { // capture screen shot on left mouse button down
            //     if(transparentBackground){
            //         TakeScreenshotRenderTransparent();
            //     } else{
            //         TakeScreenshotRender();
            //     }
            // }
        }

        public void TakeScreenshotRender() {
            var folderPath = CreateFolder();
            var screenshotName = GetFileName();
            ScreenCapture.CaptureScreenshot(Path.Combine(folderPath, screenshotName), superScaleSize); // takes the sceenshot, the "2" is for the scaled resolution, you can put this to 600 but it will take really long to scale the image up
            Debug.Log("Saved screenshot to: " + folderPath + screenshotName); // You get instant feedback in the console
        }

        public void TakeScreenshotRenderTransparent() {
            var folderPath = CreateFolder();
            var screenshotName = GetFileName();

            var camera = Camera.main;
            var width = Screen.width;
            var height = Screen.height;
            Texture2D scrTexture = new Texture2D(width, height, TextureFormat.ARGB32, false); 
            RenderTexture scrRenderTexture = new RenderTexture(scrTexture.width, scrTexture.height, 24);
            RenderTexture camRenderTexture = camera.targetTexture;

            camera.targetTexture = scrRenderTexture;
            camera.Render();
            camera.targetTexture = camRenderTexture;

            RenderTexture.active = scrRenderTexture;
            scrTexture.ReadPixels(new Rect(0, 0, scrTexture.width, scrTexture.height), 0, 0);
            scrTexture.Apply();

            File.WriteAllBytes(Path.Combine(folderPath, screenshotName), scrTexture.EncodeToPNG());
        }
    }
}