using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace VoxelWorldStuff
{

    //The spheres that collect light and are used as "lights" to reemit light to the world
    public class RadiosityProbe
    {
        const bool showDebugSphere = VoxelWorld.showDebugSpheres; //Consumes a buuunch of memory
        const int maxSamples = VoxelWorld.maxRadiositySamples;
        const int samplesPerStep = VoxelWorld.maxSamplesPerFrame;
        const float clampValue = VoxelWorld.radiosityRunawayClamp; //Lighting wont go over this

        VoxelWorld world;
        public Vector3 position;
        Chunk chunk;

        int rayIndex = 0;
        int sampleCount = 0;
        int sampleIndex = 0;
        Color[] directSamples = new Color[maxSamples];
        Color[] indirectSamples = new Color[maxSamples];
        Color directColor = Color.black;
        Color indirectColor = Color.black;

        float previousDirectEnergy = 0;
        float previousIndirectEnergy = 0;

        List<RadiosityProbeSample> debugSamples;
        static List<Vector3> sampleVectors;
        public bool enabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnStartup()
        {
            sampleVectors = null;
        }

        public bool debugging = false;
        //Total amount of direct lighting this probe can see
        GameObject debugSphere;
        RadiosityProbeDebug debugProbe = null;
        Material mat;

        RadiosityProbeSample sample;

        public RadiosityProbe(VoxelWorld world, Vector3 position, Chunk chunk, RadiosityProbeSample sample, bool enabled)
        {
            this.world = world;
            this.position = position;
            this.chunk = chunk;
            this.sample = sample;
            this.enabled = enabled;
            sample.position = position;
            sample.color = Color.black;

            if (sampleVectors == null)
            {
                GenerateSamples(maxSamples);
            }
        }

        private static void GenerateSamples(int n)
        {
            //Generate even samples over a sphere, using the Fibonacci Spiral method
            //Interleave
            sampleVectors = new List<Vector3>();

            float inc = Mathf.PI * (3 - Mathf.Sqrt(5));
            float off = 2.0f / n;
            for (int k = 0; k < n; k++)
            {
                float y = k * off - 1 + (off / 2);
                float r = Mathf.Sqrt(1 - y * y);
                float phi = k * inc;
                float x = Mathf.Cos(phi) * r;
                float z = Mathf.Sin(phi) * r;
                sampleVectors.Add(new Vector3(x, y, z));
            }

            //Shuffle the sampleVectors as we might only process a few of the total samples each step
            Random.InitState(0);
            for (int i = 0; i < sampleVectors.Count; i++)
            {
                Vector3 temp = sampleVectors[i];
                int randomIndex = Random.Range(i, sampleVectors.Count);
                sampleVectors[i] = sampleVectors[randomIndex];
                sampleVectors[randomIndex] = temp;
            }
        }

        private Color PowBright(Color input, float pow)
        {
            return input;
            //return new Color(input.r * pow, input.g * pow, input.b * pow);
        }

        //Add a bunch of new raycasts to the probes. 
        //We measure how much has changed since the last update, and if it hasnt changed much, we go to sleep
        public int AddSamples()
        {
            if (enabled == false)
            {
                return 0;
            }

            int count = 0;
            if (showDebugSphere == true)
            {
#pragma warning disable CS0162                
                if (debugSphere == null)
                {
                    debugSphere = world.SpawnDebugSphere(position, Color.red, 0.5f);
                    mat = debugSphere.GetComponent<Renderer>().sharedMaterial;


                    debugProbe = debugSphere.AddComponent<RadiosityProbeDebug>();
                    debugProbe.parent = this;
                    debugSamples = debugProbe.samples;
                    
                   

                }
#pragma warning restore CS0162                
            }

            for (int counter = 0; counter < samplesPerStep; counter++)
            {
                Vector3 dir = sampleVectors[rayIndex++];
                if (rayIndex >= sampleVectors.Count)
                {
                    rayIndex = 0;
                }
                const float distance = VoxelWorld.probeMaxRange;

                (bool valid, Color direct, Color indirect) = world.GetWorldLightingFromRayImpact(position, dir, distance, debugSamples);
                if (valid) //Always valid tbh
                {
              

                    directSamples[sampleIndex] = direct;
                    indirectSamples[sampleIndex] = indirect;
                    sampleIndex++;

                    if (sampleCount < maxSamples)
                    {
                        sampleCount++;
                    }
                    //wrap
                    if (sampleIndex >= maxSamples)
                    {
                        sampleIndex = 0;
                    }
                }
                count++;
            }



            return count;
        }

        public float CalculateDirectColor()
        {
            if (enabled == false)
            {
                return 0;
            }
            
            //average the colors
            directColor = Color.black;
            float energy = 0;
            if (sampleCount > 0)
            {
                int usedSamples = 0;
                for (int i = 0; i < sampleCount; i++)
                {
                    Color sample = directSamples[i];
                    if (sample.r > 0 || sample.g > 0 || sample.b > 0)
                    {
                        directColor += sample;
                        usedSamples++;
                    }

                }
                energy = directColor.maxColorComponent;
 
                if (usedSamples > 0)
                {
                   directColor /= usedSamples;

                }
                //directColor /= maxSamples;
            }


            float energyDelta = Mathf.Abs(energy - previousDirectEnergy);
            previousDirectEnergy = energy;

            return energyDelta;
        }

        public float CalculateIndirectColor()
        {
            if (enabled == false)
            {
                return 0;
            }
            
            indirectColor = Color.black;

            int usedSamples = 0;
            if (sampleCount > 0)
            {
                for (int i = 0; i < sampleCount; i++)
                {
                    Color sample = indirectSamples[i];

                    if (sample.r > 0 || sample.g > 0 || sample.b > 0)
                    {
                        indirectColor += sample;
                        usedSamples++;
                    }
                }

             
                if (usedSamples > 0)
                {
                    indirectColor /= usedSamples;
                }

                //indirectColor /= maxSamples;
            }

            //because indirectColor can spiral outta control, this stops that by capping it at ~1
            Color normalizedIndirectColor = indirectColor;

            if (true)
            {
                float max = Mathf.Max(normalizedIndirectColor.r, normalizedIndirectColor.g, normalizedIndirectColor.b);
                if (max > clampValue)
                {
                    normalizedIndirectColor.r /= max;
                    normalizedIndirectColor.g /= max;
                    normalizedIndirectColor.b /= max;
                }
            }
            float energy = new Vector3(indirectColor.r, indirectColor.g, indirectColor.b).magnitude;
            float energyDelta = Mathf.Abs(energy - previousIndirectEnergy);
            previousIndirectEnergy = energy;

            //Supress the unreachable code warning
#pragma warning disable CS0162
            if (showDebugSphere == true)
            {
                Color normalizedCol = directColor + indirectColor;
                float maxf = Mathf.Max(normalizedCol.r, normalizedCol.g, normalizedCol.b);
                if (maxf > 1)
                {
                    normalizedCol.r /= maxf;
                    normalizedCol.g /= maxf;
                    normalizedCol.b /= maxf;
                }

                if (mat)
                {
                    mat.SetColor("_Color", normalizedCol);
                }
            }
#pragma warning restore CS0162


            if (debugging == false)
            {
                sample.color = directColor + normalizedIndirectColor;
            }
            else
            {
                sample.color = Color.white;
            }

            return energyDelta;
        }

    }

    public class RadiosityProbeSample
    {
        public Vector3 position;
        public Color color;

        public RadiosityProbeSample(Vector3 position, Color color)
        {
            this.position = position;
            this.color = color;
        }
    }

}