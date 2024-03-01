using UnityEngine;
using System.Collections;
using System.IO;
using System;
using Object = UnityEngine.Object;

[LuauAPI]
public class CameraScreenshotRecorder : MonoBehaviour{
	public enum SaveFolder {
		ApplicationData,
		PicturesFolder,
		Documents,
	}

	public SaveFolder saveFolder = SaveFolder.PicturesFolder;
	public bool shouldSaveCaptures = true;
	public int resWidth = 1920;
	public int resHeight = 1080;
	
	private const int resDepth = 24;
	private static Texture2D screenShot;
	private static RenderTexture rt;

	public delegate void OnPictureTaken(Texture2D screenshot);
	public static OnPictureTaken onPictureTaken;
	
	public static Texture2D GetScreenshotTexture {
		get {
			return screenShot;
		}
	}
	
	public string ScreenShotName(int width, int height) {
		return FolderName + string.Format("screen_{0}x{1}_{2}.png", 
		                     width, height, 
		                     System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
	}

	public string ScreenShotName(string filename) {
		return FolderName + filename + ".png";
	}
	
	public string FolderName{
		get {
			string folderPath = Application.persistentDataPath;
			switch (saveFolder) {
				case SaveFolder.PicturesFolder:
					folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
					break;
				case SaveFolder.Documents:
					folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
					break;
			};
			return string.Format("{0}/screenshots/", folderPath);
		}
	}

	private void InitFolder() {
		if (!Directory.Exists (FolderName)) {
			Directory.CreateDirectory(FolderName);
		}
	}

	public void TakeScreenshot(string fileName = "", int superSampleSize = 1) {
		InitFolder();
		StartCoroutine(TakeScreenshotCo(fileName, superSampleSize));
	}

	private IEnumerator TakeScreenshotCo(string fileName = "", int superSampleSize = 1) {
		//Have to capture at end of frame for ScreenCapture to work
		yield return new WaitForEndOfFrame();
		screenShot = ScreenCapture.CaptureScreenshotAsTexture(superSampleSize);
		SaveScreenshot(fileName, superSampleSize);
	}


	public void TakeCameraScreenshot(Camera camera, string fileName = "", int superSampleSize = 1) {
		InitFolder();
		StartCoroutine(TakeCameraScreenshotCo(camera, fileName, superSampleSize));
	}

	public IEnumerator TakeCameraScreenshotCo(Camera camera, string fileName = "", int superSampleSize = 1) {
		bool enabled = camera.enabled;
		
		//Have to capture at end of frame for ScreenCapture to work
		yield return new WaitForEndOfFrame();
		
		try{
			screenShot = new Texture2D(resWidth * superSampleSize, resHeight * superSampleSize, TextureFormat.RGB24, false);
			rt = new RenderTexture(resWidth, resHeight, resDepth);
			camera.enabled = true;
			camera.targetTexture = rt;
			camera.Render();
			RenderTexture.active = rt;
		}
		catch(Exception e){
			Debug.LogError("Error saving: " + e.Message);
		}

		//Save the render textures data to a texture2D
		screenShot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
		camera.targetTexture = null;
		RenderTexture.active = null;
		rt.Release();
		rt = null;
		
		if (shouldSaveCaptures) {
			SaveScreenshot(fileName, superSampleSize);
		}
			
		if(onPictureTaken != null){
			onPictureTaken(screenShot);
		}
		camera.enabled = enabled;
	}

	private void SaveScreenshot(string fileName, int superSampleSize) {
		if (!screenShot || screenShot.width <= 0) {
			return;
		}
		try {
			//Debug.Log("Screenshotsize: " + screenShot.width +", " + screenShot.height + " RenderTexture: " + camera.targetTexture.width + ", " + camera.targetTexture.height);
			string filePath = string.IsNullOrEmpty(fileName)
				? ScreenShotName(resWidth * superSampleSize, resHeight * superSampleSize)
				: ScreenShotName(fileName);
			byte[] bytes = screenShot.EncodeToPNG();
			File.WriteAllBytes(filePath, bytes);
			Debug.Log(string.Format("Saved screenshot to: {0}", filePath));
			screenShot.Apply();
		} catch (Exception e) {
			Debug.LogError("Error saving: " + e.Message);
		}
	}
}



