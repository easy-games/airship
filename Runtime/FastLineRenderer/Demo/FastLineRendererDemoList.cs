using UnityEngine;

using System.Collections.Generic;

namespace DigitalRuby.FastLineRenderer
{
    /// <summary>
    /// Demo script that creates lines from a list of points
    /// </summary>
    public class FastLineRendererDemoList : MonoBehaviour
    {
        /// <summary>
        /// Fast line renderer script
        /// </summary>
        [Tooltip("Fast line renderer script")]
        public FastLineRenderer LineRenderer;

        private void CreateContinuousLineFromList()
        {
            // reset the line renderer - if you are appending to an existing line, don't call reset, as you would lost all the previous points
            LineRenderer.Reset();

            // generate zig zag
            List<Vector3> points = new List<Vector3>();
            for (int i = 0; i < 50; i++)
            {
                points.Add(new Vector3(-200 + (i * 10.0f), 100 + (150.0f * -(i % 2)), 0.0f));
            }

            // animation time per segment - set to 0 to have the whole thing appear at once
            const float animationTime = 0.1f;

            // create properties - do this once, before your loop
            FastLineRendererProperties props = new FastLineRendererProperties();
            props.Radius = 0.1f;
            props.LineJoin = FastLineRendererLineJoin.Round;

            // add the list of line points
            LineRenderer.AddLine(props, points, (FastLineRendererProperties _props) =>
            {
                // random color
                props.Color = new Color32((byte)Random.Range(0, 256), (byte)Random.Range(0, 256), (byte)Random.Range(0, 256), byte.MaxValue);

                // animate this line segment in later
                props.AddCreationTimeSeconds(animationTime);

                // increase the radius
                props.Radius += 0.05f;
            }, true, true);

            // must call apply to make changes permanent
            LineRenderer.Apply();
        }

        private void CreateDistinctSegmentsFromList()
        {
            // we don't want to reset the line renderer here, because we are using the same line renderer from the CreateContinuousLineFromList method and we want to keep those points
            // LineRenderer.Reset();

            // generate random start and end points
            List<Vector3> points = new List<Vector3>();
            for (int i = 0; i < 10; i++)
            {
                float x = Random.Range(-150.0f, 150.0f);
                float y = Random.Range(-100.0f, 100.0f);
                points.Add(new Vector3(x, y, 0.0f));

                x = Random.Range(-150.0f, 150.0f);
                y = Random.Range(-100.0f, 100.0f);
                points.Add(new Vector3(x, y, 0.0f));
            }

            // animation time per segment - set to 0 to have the whole thing appear at once
            const float animationTime = 1.0f;

            // create properties - do this once, before your loop
            // note you don't need to set the join for distinct segments added via the AddLine or AddLines call
            FastLineRendererProperties props = new FastLineRendererProperties();

            LineRenderer.AddLines(props, points, (FastLineRendererProperties _props) =>
            {
                // random color
                props.Color = new Color32((byte)Random.Range(0, 256), (byte)Random.Range(0, 256), (byte)Random.Range(0, 256), byte.MaxValue);

                // random radius
                props.Radius = Random.Range(0.25f, 1.0f);

                // animate this line segment in later
                props.AddCreationTimeSeconds(animationTime);
            }, true, true);            

            // must call apply to make changes permanent
            LineRenderer.Apply();
        }

        private void CreateTestLinesOnSpacebar()
        {
            // animation time per segment - set to 0 to have the whole thing appear at once
            const float animationTime = 0.1f;

            // reset the line renderer - if you are appending to an existing line, don't call reset, as you would lost all the previous points
            LineRenderer.Reset();

            // generate zig zag
            List<Vector3> points = new List<Vector3>();
            for (int i = 0; i < 50; i++)
            {
                points.Add(new Vector3(-200 + (i * 10.0f), 100 + (150.0f * -(i % 2)), 0.0f));
            }

            FastLineRendererProperties props = new FastLineRendererProperties();
            props.Radius = 0.1f;
            props.LineJoin = FastLineRendererLineJoin.Round;

            // add the list of line points
            LineRenderer.AddLine(props, points, (FastLineRendererProperties _props) =>
            {
                // random color
                _props.Color = new Color32((byte)Random.Range(0, 256), (byte)Random.Range(0, 256), (byte)Random.Range(0, 256), byte.MaxValue);

                // animate this line segment in later
                _props.AddCreationTimeSeconds(animationTime);

                // increase the radius
                _props.Radius += 0.05f;
            }, true, true);

            // must call apply to make changes permanent
            LineRenderer.Apply();
        }

        private void Start()
        {
            CreateContinuousLineFromList();
            CreateDistinctSegmentsFromList();
            CreateTestLinesOnSpacebar();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                CreateTestLinesOnSpacebar();
            }
        }
    }
}