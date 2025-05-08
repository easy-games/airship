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

namespace Tayx.Graphy.Resim {
    public class G_ResimGraph : G_Graph {
        #region Variables -> Serialized Private

        [SerializeField] private Image m_imageGraph = null;

        [SerializeField] private Shader ShaderFull = null;
        [SerializeField] private Shader ShaderLight = null;

        // This keeps track of whether Init() has run or not
        [SerializeField] private bool m_isInitialized = false;

        #endregion

        public NetworkStatistics networkStatistics;
        public Text clientSendRate;
        public Text clientRecvRate;
        public Text pingText;

        #region Variables -> Private

        private GraphyManager m_graphyManager = null;

        private G_ResimMonitor m_resimMonitor = null;

        private int m_resolution = 150;

        private G_GraphShader m_shaderGraph = null;

        private int[] m_fpsArray;

        private int m_highestFps;

        #endregion

        #region Methods -> Unity Callbacks

        private void Update() {
            UpdateGraph();

            this.pingText.text = $"{Math.Round(NetworkTime.rtt * 1000)}ms";
            this.clientSendRate.text = $"{Mirror.Utils.PrettyBytes(this.networkStatistics.clientSentBytesPerSecond)}/s";
            this.clientRecvRate.text = $"{Mirror.Utils.PrettyBytes(this.networkStatistics.clientReceivedBytesPerSecond)}/s";
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

            short fps = m_resimMonitor.CurrentResim;

            int currentMaxFps = 0;

            for (int i = 0; i <= m_resolution - 1; i++) {
                if (i >= m_resolution - 1) {
                    m_fpsArray[i] = fps;
                }
                else {
                    m_fpsArray[i] = m_fpsArray[i + 1];
                }

                // Store the highest fps to use as the highest point in the graph

                if (currentMaxFps < m_fpsArray[i]) {
                    currentMaxFps = m_fpsArray[i];
                }
            }

            // m_highestFps = m_highestFps < 1 || m_highestFps <= currentMaxFps ? currentMaxFps : m_highestFps - 1;
            //
            // m_highestFps = m_highestFps > 0 ? m_highestFps : 1;
            m_highestFps = 100;

            if (m_shaderGraph.ShaderArrayValues == null) {
                m_fpsArray = new int[m_resolution];
                m_shaderGraph.ShaderArrayValues = new float[m_resolution];
            }

            for (int i = 0; i <= m_resolution - 1; i++) {
                m_shaderGraph.ShaderArrayValues[i] = m_fpsArray[i] / (float)m_highestFps;
            }

            // Update the material values

            m_shaderGraph.UpdatePoints();

            m_shaderGraph.Average = m_resimMonitor.AverageFPS / m_highestFps;
            m_shaderGraph.UpdateAverage();

            m_shaderGraph.GoodThreshold = 0;
            m_shaderGraph.CautionThreshold = 0;
            m_shaderGraph.UpdateThresholds();
        }

        protected override void CreatePoints() {
            if (m_shaderGraph.ShaderArrayValues == null || m_fpsArray.Length != m_resolution) {
                m_fpsArray = new int[m_resolution];
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

            m_resimMonitor = GetComponent<G_ResimMonitor>();

            m_shaderGraph = new G_GraphShader { Image = m_imageGraph };

            UpdateParameters();

            m_isInitialized = true;
        }

        #endregion
    }
}