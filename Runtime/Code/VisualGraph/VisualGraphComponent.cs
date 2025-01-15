using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

[LuauAPI]
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
    private List<Vector3> values = new List<Vector3>();
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
                AddValues(new Vector3(
                    UnityEngine.Random.Range(0f,1f),
                    UnityEngine.Random.Range(0f,1f),
                    UnityEngine.Random.Range(0f,1f)));
            }
        }
        if(testGraph){
            this.InitTexture();
            values = new List<Vector3>();
            for(int i=0; i<this.dataResolution; i++){
                // values.Add(new Vector3(
                //     (Mathf.Sin(i) + 1) / 2, 
                //     (Mathf.Cos(i) + 1) / 2, 
                //     (Mathf.Tan(i) + 1) / 2));
                var delta = i/(float)this.dataResolution;
                values.Add(new Vector3(
                    delta,
                    1-delta,
                    Mathf.Sin(delta)));
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
    /// <param name="newValue"> 0 - 1 value on the graph</param>
    public void AddValue(float newValue){
        if(this.values.Count > dataResolution){
            this.values.RemoveAt(0);
        }
        this.values.Add(new Vector3(newValue, 0,0));
        UpdateMesh();
    }

    /// <summary>
    /// Adds a value to the end of the data
    /// </summary>
    /// <param name="newValue"> 0 - 1 value for A B C lines on the graph</param>
    public void AddValues(Vector3 newValue){
        if(this.values.Count > dataResolution){
            this.values.RemoveAt(0);
        }
        this.values.Add(newValue);
        UpdateMesh();
    }

    private void Contain(float value){
        this.minValue = math.min(value, this.minValue);
        this.maxValue = math.max(value, this.maxValue);
    }

    public void     UpdateMesh(){
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
            this.Contain(this.values[i].x);
            this.Contain(this.values[i].y);
            this.Contain(this.values[i].z);
            log+= "Value " + i + ": " + this.values[i] + " \n";
        }

        if(logValues){
            print(log);
        }

        int firstValueIndex = this.dataResolution-this.values.Count;
        for(int i=0; i<this.dataResolution; i++){
            int valueIndex = math.clamp(i-firstValueIndex, 0, this.values.Count-1);
            this.dataTex.SetPixel(i,1, new Color(
                (this.values[valueIndex].x-minValue) / (this.maxValue-minValue),
                (this.values[valueIndex].y-minValue) / (this.maxValue-minValue),
                (this.values[valueIndex].z-minValue) / (this.maxValue-minValue)));
        }
        this.dataTex.Apply();
        image.texture = dataTex;
    }

    public void SetLineColor(Color colorA){
        this.image.material.SetColor("_LineColorA", colorA);
    }

    public void SetLineColors(Color colorA, Color colorB, Color colorC){
        this.image.material.SetColor("_LineColorA", colorA);
        this.image.material.SetColor("_LineColorB", colorB);
        this.image.material.SetColor("_LineColorC", colorC);
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
