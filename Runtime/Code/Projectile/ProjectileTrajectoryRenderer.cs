using System;
using FishNet;
using UnityEngine;

namespace Code.Projectile
{
    [RequireComponent(typeof(LineRenderer))][LuauAPI]
    public class ProjectileTrajectoryRenderer : MonoBehaviour
    {
        private Vector3[] segments;
        
        private int numSegments = 0;
        public int maxIterations = 10000;
        public int maxSegmentCount = 300;
        public int segmentStepModulo = 10;

        private bool drawingEnabled;

        private Vector3 startPos;
        private Vector3 startVel;
        private float startDrag = 0;
        private float startGravity = 0;

        private LineRenderer lineRenderer;

        private readonly RaycastHit[] raycastHits = new RaycastHit[1];

        [SerializeField]
        private LayerMask trajectoryLayerMask;

        public void UpdateInfo(Vector3 startingPoint, Vector3 velocity, float drag, float gravity)
        {
            this.startPos = startingPoint;
            this.startVel = velocity;
            this.startDrag = drag;
            this.startGravity = gravity;
            this.drawingEnabled = true;
        }

        public void SetDrawingEnabled(bool enabled)
        {
            this.drawingEnabled = enabled;
        }

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
        }


        private void LateUpdate()
        {
            if (this.drawingEnabled)
            {
                this.lineRenderer.enabled = true;
                SimulatePath();
            } else
            {
                this.lineRenderer.enabled = false;
            }
        }

        private void SimulatePath()
        {
            float timestep = Time.fixedDeltaTime;

            float stepDrag = 1 - this.startDrag * timestep;
            Vector3 velocity = this.startVel;
            float gravity = this.startGravity * timestep;
            Vector3 position = this.startPos;
    
            if (segments == null || segments.Length != maxSegmentCount)
            {
                segments = new Vector3[maxSegmentCount];
            }

            segments[0] = position;
            numSegments = 1;
            
            for (int i = 0; i < maxIterations && numSegments < maxSegmentCount; i += segmentStepModulo)
            {
                velocity.y += gravity;
                position += velocity * timestep;
                
                segments[numSegments] = position;
                numSegments++;
            }
            
            Draw();
        }

        private void Draw()
        {
            lineRenderer.transform.position = segments[0];
            
            lineRenderer.positionCount = numSegments;
            for (int i = 0; i < numSegments; i++)
            {
                lineRenderer.SetPosition(i, segments[i]);
            }
        }
    }
}