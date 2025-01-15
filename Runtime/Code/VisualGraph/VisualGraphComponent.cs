using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;


[ExecuteInEditMode]
public class VisualGraphComponent : MonoBehaviour {
    [Header("References")]
    public RawImage image;

    [Header("Variables")]
    [Range(16, 1080)]
    public int dataResolution = 128;

    [Header("Debugging")]
    public bool logValues = false;
    public bool testGraphOverTime = false;
    public bool testGraph = false;

    private Texture2D dataTex;
    private List<float> values = new List<float>();
    private float lastTestTime = 0;
    public float minValue {get; private set;} = 0;
    public float maxValue {get; private set;} = 1;

    private void OnEnable() {
        InitTexture();
        this.values.Clear();
    }

    // Update is called once per frame
    void Update() {
        if(testGraphOverTime){
            if(Time.time - this.lastTestTime > 1) {
                this.lastTestTime = Time.time;
                AddValue(UnityEngine.Random.Range(0f,1f));
            }
        }
        if(testGraph){
            this.InitTexture();
            values = new List<float>();
            for(int i=0; i<this.dataResolution; i++){
                values.Add((Mathf.Sin(i) + 1) / 2);
                //values.Add(.5f);
            }
            UpdateMesh();
            testGraph = false;
        }
    }

    private void InitTexture(){
        dataTex = new Texture2D(dataResolution, 1);
        dataTex.filterMode  = FilterMode.Point;
        image.material.mainTexture = dataTex;
        image.material.SetInt("_MaxValues", dataResolution);
    }

    /// <summary>
    /// Adds a value to the end of the data
    /// </summary>
    /// <param name="delta"> 0 - 1 value on the graph</param>
    public void AddValue(float delta){
        if(this.values.Count > dataResolution){
            this.values.RemoveAt(0);
        }
        this.values.Add(delta);
        UpdateMesh();
    }

    public void UpdateMesh(){
        if(!dataTex){
            InitTexture();
        }
        if(values.Count == 0){
            return;
        }
        this.minValue = 0;
        this.maxValue = 1;
        string log = "Visual Graph Values:\n";
        for(int i=0; i<this.values.Count; i++){
            this.minValue = math.min(this.values[i], this.minValue);
            this.maxValue = math.max(this.values[i], this.maxValue);
            log+= "Value " + i + ": " + this.values[i] + " \n";
        }
        if(logValues){
            print(log);
        }
        
        // var maxColors = this.mesh.vertices.Length;
        // var newColors = new Color[maxColors];
        // var UVs = this.mesh.uv;
        // for(int i=0; i<this.mesh.vertices.Length; i++){
        //     var valueIndex = Mathf.FloorToInt(UVs[i].x * this.maxValues);
        //     var value = this.values[Mathf.Clamp(valueIndex, 0, this.values.Count-1)];
        //     newColors[i] = new Color(0,0,0, value);
        // }
        // this.mesh.colors = newColors;
        // this.filter.sharedMesh = this.mesh;
        int firstValueIndex = this.dataResolution-this.values.Count;
        for(int i=0; i<this.dataResolution; i++){
            int valueIndex = math.clamp(i-firstValueIndex, 0, this.values.Count-1);
            this.dataTex.SetPixel(i,1, new Color((this.values[valueIndex]-minValue) / (this.maxValue-minValue),0,0,0));
        }
        this.dataTex.Apply();
        image.texture = dataTex;
    }

    public void SetLineColor(Color newColor){
        this.image.material.SetColor("_LineColorA", newColor);
    }

    // private void OnDrawGizmos() {
    //     var colors = this.mesh.colors;
    //     var points = this.mesh.vertices;
    //     Color oldColor = Gizmos.color;
    //     for(int i=0; i<this.mesh.colors.Length; i++){
    //         Gizmos.color = colors[i];
    //         Gizmos.DrawSphere(transform.TransformPoint(points[i]), .1f);
    //     }
    //     Gizmos.color = oldColor;
    // }
}
