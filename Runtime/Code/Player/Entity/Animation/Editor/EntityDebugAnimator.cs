using Animancer;
using Player.Entity;
using UnityEngine;

public class EntityDebugAnimator : MonoBehaviour {
    [Header("References")]
    public CoreEntityAnimator anim;
    public Transform vfxHolder;
    
    [Header("Templates")]
    public GameObject vfxPrefabToSpawn;
    public AnimationClip testClip;
    
    [Header("Variables")]
    public float timeBetweenPlaysInSeconds = 1;
    public float playbackSpeed = 1;
    public float transitionDuration = 0;
    
    private float lastTimePlayed = 0;
    private GameObject currentVFX;
    private AnimancerState currentState;
    private bool playing = false;
    

    // Update is called once per frame
    void Update()
    {
        
        if (Time.time - lastTimePlayed > timeBetweenPlaysInSeconds+1) {
            PlayEffect();
        } else if (playing && Time.time - lastTimePlayed > timeBetweenPlaysInSeconds) {
            StopEffect();
        }
    }

    private void PlayEffect() {
        StopEffect();
        
        lastTimePlayed = Time.time;
        playing = true;
        
        
        //Animation
        if (currentState != null) {
            currentState.Stop();
        }
        currentState = anim.handsLayer1.Play(testClip, transitionDuration);
        currentState.Speed = playbackSpeed;
        
        //VFX
        currentVFX = Instantiate(vfxPrefabToSpawn, vfxHolder.transform);
        currentVFX.SetActive(true);
    }

    private void StopEffect() {
        playing = false;
        anim.handsLayer1.SetWeight(0);
        if (currentVFX) {
            DestroyImmediate(currentVFX);
            currentVFX = null;
        }
    }
}
