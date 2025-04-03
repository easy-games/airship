using UnityEngine;
using System.Collections;
using System.IO;
using System;
using Object = UnityEngine.Object;

[LuauAPI]
public class CameraScreenshotResponse{
	public string path = "";
	public int filesize = 0;
	public string extension = "";
}

[LuauAPI(LuauContext.Protected)]
public class InternalCameraScreenshotRecorder : Singleton<InternalCameraScreenshotRecorder> {
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
	
	public string ScreenShotName(int width, int height, bool png) {
		return FolderName + string.Format("screen_{0}x{1}_{2}.{3}", 
		                     width, height, 
		                     System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"), png?"png":"jpg");
	}

	public string ScreenShotName(string filename, bool png) {
		return FolderName + filename + (png?".png":".jpg");
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
			return string.Format("{0}/Airship/", folderPath);
		}
	}

	private void InitFolder() {
		if (!Directory.Exists (FolderName)) {
			Directory.CreateDirectory(FolderName);
		}
	}

	public static void TakeScreenshot(string fileName = "", int superSampleSize = 1, bool png = true) {
		Instance.InitFolder();
		Instance.StartCoroutine(Instance.TakeScreenshotCo(fileName, superSampleSize, png));
	}

	private IEnumerator TakeScreenshotCo(string fileName = "", int superSampleSize = 1, bool png = true) {
		//Have to capture at end of frame for ScreenCapture to work
		yield return new WaitForEndOfFrame();
		screenShot = ScreenCapture.CaptureScreenshotAsTexture(superSampleSize);
		SaveScreenshot(fileName, superSampleSize, png);
	}


	public static void TakeCameraScreenshot(Camera camera, string fileName = "", int superSampleSize = 1) {
		Instance.InitFolder();
		Instance.StartCoroutine(Instance.TakeCameraScreenshotCo(camera, fileName, superSampleSize));
	}

	public IEnumerator TakeCameraScreenshotCo(Camera camera, string fileName = "", int superSampleSize = 1) {
		bool enabled = camera.enabled;
		
		// Have to capture at end of frame for ScreenCapture to work
		yield return new WaitForEndOfFrame();
		
		try {
			screenShot = new Texture2D(resWidth * superSampleSize, resHeight * superSampleSize, TextureFormat.RGB24, false);
			rt = new RenderTexture(resWidth, resHeight, resDepth);
			camera.enabled = true;
			camera.targetTexture = rt;
			camera.Render();
			RenderTexture.active = rt;
		} catch(Exception e) {
			Debug.LogError("Error saving: " + e.Message);
		}

		// Save the render textures data to a texture2D
		screenShot.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
		camera.targetTexture = null;
		RenderTexture.active = null;
		rt.Release();
		rt = null;
		
		if (shouldSaveCaptures) {
			SaveScreenshot(fileName, superSampleSize, true);
		}
			
		if(onPictureTaken != null){
			onPictureTaken(screenShot);
		}
		camera.enabled = enabled;
	}

	private void SaveScreenshot(string fileName, int superSampleSize, bool png) {
		if (!screenShot || screenShot.width <= 0) {
			return;
		}
		SaveTexture(screenShot, fileName, png);
		screenShot.Apply();
	}

	public CameraScreenshotResponse SaveRenderTexture(RenderTexture rt, string fileName, bool png){
		if(!rt){
			return new CameraScreenshotResponse();
		}
		RenderTexture.active = rt;
		var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
		texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
		RenderTexture.active = null;
		return SaveTexture(texture, fileName, png);
	}

	public CameraScreenshotResponse SaveTexture(Texture2D texture, string fileName, bool png){
		try {
			//Debug.Log("Saving Texture size: " + texture.width +", " + texture.height);
			string filePath = string.IsNullOrEmpty(fileName)
				? ScreenShotName(texture.width, texture.height, png)
				: ScreenShotName(fileName, png);
			byte[] bytes = png ? texture.EncodeToPNG() : texture.EncodeToJPG();
			string directoryPath = Path.GetDirectoryName(filePath);
			if(!Directory.Exists(directoryPath)){
				Directory.CreateDirectory(directoryPath);
			}
			File.WriteAllBytes(filePath, bytes);
			Debug.Log(string.Format("Saved screenshot to: {0}", filePath));
			return new CameraScreenshotResponse(){filesize = bytes.Length, extension = Path.GetExtension(filePath), path = filePath};
		} catch (Exception e) {
			Debug.LogError("Error saving texture: " + e.Message);
		}
		return new CameraScreenshotResponse();
	}
}



