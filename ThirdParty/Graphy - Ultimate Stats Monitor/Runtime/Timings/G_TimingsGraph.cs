/* ---------------------------------------
 * Author:          Martin Pane (martintayx@gmail.com) (@tayx94)
 * Contributors:    https://github.com/Tayx94/graphy/graphs/contributors
 * Project:         Graphy - Ultimate Stats Monitor
 * Date:            15-Dec-17
 * Studio:          Tayx
 *
 * Git repo:        https://github.com/Tayx94/graphy
 *
 * This project is released under the MIT license.
 * Attribution is not required, but it is always welcomed!
 * -------------------------------------*/

using System;
using Mirror;
using Tayx.Graphy.Graph;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Tayx.Graphy.Resim {
    public class G_TimingsGraph : G_Graph {
        #region Variables -> Serialized Private

        [SerializeField] private Image m_imageGraph = null;

        [SerializeField] private Shader ShaderFull = null;
        [SerializeField] private Shader ShaderLight = null;

        // This keeps track of whether Init() has run or not
        [SerializeField] private bool m_isInitialized = false;

        #endregion
        
        public Text gpuTime;
        public Text cpuMainTime;
        public Text cpuTotalTime;
        public Text memory;

        public long lastMemorySize = 0;
        public bool memoryReduced = false;
        // public Text clientRecvRate;
        // public Text pingText;

        #region Variables -> Private

        private GraphyManager m_graphyManager = null;
        
        private int m_resolution = 150;

        private G_GraphShader m_shaderGraph = null;

        private int[] m_gcHistory;
        
        FrameTiming[] m_FrameTimings = new FrameTiming[100];

        #endregion

        #region Methods -> Unity Callbacks

        private void Update() {
            if (!FrameTimingManager.IsFeatureEnabled()) return;
            
            UpdateGraph();

            FrameTimingManager.CaptureFrameTimings();
            var ret = FrameTimingManager.GetLatestTimings((uint)m_FrameTimings.Length, m_FrameTimings);
            if (ret == 0) return;

            var totalGpu = 0d;
            var mainCpu = 0d;
            var totalCpu = 0d;
            foreach (var timing in m_FrameTimings) {
                totalGpu += timing.gpuFrameTime;
                mainCpu += timing.cpuMainThreadFrameTime;
                totalCpu += timing.cpuFrameTime;
            }

            
            this.gpuTime.text = $"{(totalGpu / m_FrameTimings.Length):F1}ms";
            this.cpuMainTime.text = $"{(mainCpu / m_FrameTimings.Length):F1}ms";
            this.cpuTotalTime.text = $"{(totalCpu / m_FrameTimings.Length):F1}ms";
            var newMemorySize = GC.GetTotalMemory(false);
            memory.text = $"{Mirror.Utils.PrettyBytes(newMemorySize)}";
            memoryReduced = newMemorySize < lastMemorySize;
            lastMemorySize = newMemorySize;
        }

        #endregion

        #region Methods -> Public

        public void UpdateParameters() {
            if (m_shaderGraph == null) {
                // TODO: While Graphy is disabled (e.g. by default via Ctrl+H) and while in Editor after a Hot-Swap,
                // the OnApplicationFocus calls this while m_shaderGraph == null, throwing a NullReferenceException
                return;
            }

            switch (m_graphyManager.GraphyMode) {
                case GraphyManager.Mode.FULL:
                    m_shaderGraph.ArrayMaxSize = G_GraphShader.ArrayMaxSizeFull;
                    m_shaderGraph.Image.material = new Material(ShaderFull);
                    break;

                case GraphyManager.Mode.LIGHT:
                    m_shaderGraph.ArrayMaxSize = G_GraphShader.ArrayMaxSizeLight;
                    m_shaderGraph.Image.material = new Material(ShaderLight);
                    break;
            }

            m_shaderGraph.InitializeShader();

            m_resolution = m_graphyManager.FpsGraphResolution;

            CreatePoints();
        }

        #endregion

        #region Methods -> Protected Override

        protected override void UpdateGraph() {
            // Since we no longer initialize by default OnEnable(), 
            // we need to check here, and Init() if needed
            if (!m_isInitialized) {
                Init();
            }

            var reduced = this.memoryReduced ? 1 : 0;
            for (int i = 0; i <= m_resolution - 1; i++) {
                if (i >= m_resolution - 1) {
                    m_gcHistory[i] = reduced;
                }
                else {
                    m_gcHistory[i] = m_gcHistory[i + 1];
                }
            }
            
            if (m_shaderGraph.ShaderArrayValues == null) {
                m_gcHistory = new int[m_resolution];
                m_shaderGraph.ShaderArrayValues = new float[m_resolution];
            }
            for (int i = 0; i <= m_resolution - 1; i++) {
                m_shaderGraph.ShaderArrayValues[i] = m_gcHistory[i];
            }

            // Update the material values

            m_shaderGraph.UpdatePoints();

            m_shaderGraph.Average = 1;
            m_shaderGraph.UpdateAverage();

            m_shaderGraph.GoodThreshold = 1;
            m_shaderGraph.CautionThreshold = 0;
            m_shaderGraph.UpdateThresholds();
        }

        protected override void CreatePoints() {
            if (m_shaderGraph.ShaderArrayValues == null || m_gcHistory.Length != m_resolution) {
                m_gcHistory = new int[m_resolution];
                m_shaderGraph.ShaderArrayValues = new float[m_resolution];
            }

            for (int i = 0; i < m_resolution; i++) {
                m_shaderGraph.ShaderArrayValues[i] = 0;
            }

            m_shaderGraph.GoodColor = m_graphyManager.GoodFPSColor;
            m_shaderGraph.CautionColor = m_graphyManager.CautionFPSColor;
            m_shaderGraph.CriticalColor = m_graphyManager.CriticalFPSColor;

            m_shaderGraph.UpdateColors();

            m_shaderGraph.UpdateArray();
        }

        #endregion

        #region Methods -> Private

        private void Init() {
            m_graphyManager = transform.root.GetComponentInChildren<GraphyManager>();
            
            m_shaderGraph = new G_GraphShader { Image = m_imageGraph };

            UpdateParameters();

            m_isInitialized = true;
        }

        #endregion
    }
}