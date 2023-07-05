using UnityEngine;
using System.Collections;

public class LightFlicker : MonoBehaviour {
	
	
	public bool flicker = true;
	
	public float flickerIntensity = 0.5f;
	
	private float baseIntensity;
	private Light lightComp;
	
	
	void Awake()
	{
		lightComp = gameObject.GetComponent<Light>();
		baseIntensity = lightComp.intensity;
	}
	
	
	void Update ()
	{
		if (flicker)
		{
			float noise = Mathf.PerlinNoise(Random.Range(0.0f, 1000.0f), Time.time);
			lightComp.intensity = Mathf.Lerp( baseIntensity - flickerIntensity , baseIntensity, noise );
		}
	}
}
