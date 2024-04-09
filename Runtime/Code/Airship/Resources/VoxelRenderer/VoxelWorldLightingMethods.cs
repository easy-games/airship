using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VoxelWorldStuff;

public partial class VoxelWorld : Singleton<VoxelWorld>
{
    private Mutex radiosityProbeSamplesMutex = new Mutex();
    public float CalculateCheapSunAtPoint(Vector3 point, Vector3 normal)
    {
        float dot = Vector3.Dot(renderSettings._negativeSunDirectionNormalized, normal);
        if (dot < 0)
        {
            return 0; //fully occluded
        }

        int hit = RaycastVoxelForLighting(point, renderSettings._negativeSunDirectionNormalized, 50);
        if (hit == 0)
        {
            return dot;
        }
        return 0;
    }

    public float CalculateSunShadowAtPoint(Vector3 point, int faceAxis, Vector3 normal)
    {
        if (Vector3.Dot(renderSettings._negativeSunDirectionNormalized, normal) < 0)
        {
            return 0; //fully occluded
        }

        if (samplesX == null) return 0;

        Vector3[] sampleList;

        switch (faceAxis)
        {
            case 0: sampleList = samplesX; break;
            default:
            case 1: sampleList = samplesY; break;
            case 2: sampleList = samplesZ; break;
        }

        int numSamples = 0;
        float numHits = 0;
        foreach (Vector3 offset in sampleList)
        {
            int hit = RaycastVoxelForLighting(point + offset, renderSettings._negativeSunDirectionNormalized, 40);

            //2 doesnt count
            if (hit == 1)
            {
                numSamples += 1;
                numHits += 1;
            }
            else if (hit == 0)
            {
                numSamples += 1;
            }

        }

        if (numHits == 0)
        {

            return 0;
        }
        else
        {

            return (numHits / numSamples);
        }

    }
    
    /*
    public float CalculatePointLightColorAtPoint(Vector3 samplePoint, Vector3 normal, LightReference lightRef)
    {
        Vector3 dirToLight = samplePoint - lightRef.position;
        Vector3 lightDir = dirToLight.normalized;

        float NoL = Vector3.Dot(-lightDir, normal);
        if (NoL < 0)
        {
            return 0;  //wrong face
        }

        float distance = dirToLight.magnitude;
        float lightRange = lightRef.radius;
        if (distance > lightRange) //keep attenuating out for ~1 block
        {
            return 0; //out of range
        }

        //color atten
        float distanceNorm = distance / lightRange;
        float falloff = distanceNorm * distanceNorm * distanceNorm;
        falloff = 1.0f - falloff;

        falloff *= NoL;

        return falloff; //not occluded
    }

    public Color CalculatePointLightColorAtPointShadow(Vector3 samplePoint, Vector3 normal, LightReference lightRef)
    {
        Vector3 dirToLight = samplePoint - lightRef.position;
        Vector3 lightDir = dirToLight.normalized;

        float NoL = Vector3.Dot(-lightDir, normal);
        if (NoL < 0)
        {
            return new Color(0, 0, 0, 0);  //wrong face
        }

        float distance = dirToLight.magnitude;
        float lightRange = lightRef.radius;
        if (distance > lightRange)
        {
            return new Color(0, 0, 0, 0); //out of range
        }

        int hit = RaycastVoxelForLighting(samplePoint, -lightDir, distance);
        //int hit = RaycastVoxelForLighting(lightRef.position, lightDir, Mathf.Floor(distance)- 1);

        if (hit != 0)
        {
            return new Color(0, 0, 0, 0);  //Part of shadow
        }

        //color atten

        float distanceNorm = distance / lightRange;
        float falloff = distanceNorm * distanceNorm * distanceNorm;
        falloff = 1.0f - falloff;

        falloff *= NoL;

        return lightRef.color * falloff; //not occluded
    }
    */


    Vector2[] Sunflower(int n, float alpha = 0, bool geodesic = false)
    {
        float phi = (1 + Mathf.Sqrt(5)) / 2;//golden ratio
        float angle_stride = 360 * phi;
        float radius(float k, float n, float b)
        {
            return k > n - b ? 1 : Mathf.Sqrt(k - 0.5f) / Mathf.Sqrt(n - (b + 1) / 2);
        }

        int b = (int)(alpha * Mathf.Sqrt(n));  //# number of boundary points

        List<Vector2> points = new List<Vector2>();
        for (int k = 0; k < n; k++)
        {
            float r = radius(k, n, b);
            float theta = geodesic ? k * 360 * phi : k * angle_stride;
            float x = !float.IsNaN(r * Mathf.Cos(theta)) ? r * Mathf.Cos(theta) : 0;
            float y = !float.IsNaN(r * Mathf.Sin(theta)) ? r * Mathf.Sin(theta) : 0;
            points.Add(new Vector2(x, y));
        }
        return points.ToArray();
    }

    public static Vector3[] GenerateRaySamples(Vector3 normal, int sampleCount)
    {
        Vector3[] samples = GenerateHemiSphereSamples(sampleCount);
        Quaternion rotMatrix = Quaternion.FromToRotation(Vector3.up, normal);
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = rotMatrix * samples[i];
        }
        return samples;
    }

 
 

    private void GenerateSphereSamples(int n)
    {
        //Generate even samples over a sphere, using the Fibonacci Spiral method

        sphereSampleVectors = new Vector3[64];

        float inc = Mathf.PI * (3 - Mathf.Sqrt(5));
        float off = 2.0f / n;
        for (int k = 0; k < n; k++)
        {
            float y = k * off - 1 + (off / 2);
            float r = Mathf.Sqrt(1 - y * y);
            float phi = k * inc;
            float x = Mathf.Cos(phi) * r;
            float z = Mathf.Sin(phi) * r;
            sphereSampleVectors[k] = new Vector3(x, y, z);
        }
    }

    private static Vector3[] GenerateHemiSphereSamples(int n)
    {
        //Generate even samples over a sphere, using the Fibonacci Spiral method

        Vector3[] samples = new Vector3[64];

        float inc = Mathf.PI * (3 - Mathf.Sqrt(5));
        float off = 1.0f / n;
        for (int k = 0; k < n; k++)
        {
            float y = k * off + (off / 2);
            float r = Mathf.Sqrt(1 - y * y);
            float phi = k * inc;
            float x = Mathf.Cos(phi) * r;
            float z = Mathf.Sin(phi) * r;
            samples[k] = new Vector3(x, y, z);
        }
        return samples;
    }

    // x+z flat circle disc of offsets 
    public void CreateSamples()
    {
  
        List<Vector2> shadowOffsets = new(Sunflower(numSoftShadowSamples, softShadowRadius, false));

        int samples = shadowOffsets.Count;

        samplesX = new Vector3[samples];
        samplesY = new Vector3[samples];
        samplesZ = new Vector3[samples];

        for (int i = 0; i < samples; i++)
        {
            float xOffset = shadowOffsets[i].x;
            float yOffset = shadowOffsets[i].y;
            samplesX[i] = new Vector3(0, xOffset, yOffset);
            samplesY[i] = new Vector3(xOffset, 0, yOffset);
            samplesZ[i] = new Vector3(xOffset, yOffset, 0);
        }

        
        radiosityRaySamples = new Vector3[6][];
        radiosityRaySamples[0] = GenerateRaySamples(Vector3.up, numRadiosityRays);
        radiosityRaySamples[1] = GenerateRaySamples(Vector3.down, numRadiosityRays);
        radiosityRaySamples[2] = GenerateRaySamples(Vector3.left, numRadiosityRays);
        radiosityRaySamples[3] = GenerateRaySamples(Vector3.right, numRadiosityRays);
        radiosityRaySamples[4] = GenerateRaySamples(Vector3.forward, numRadiosityRays);
        radiosityRaySamples[5] = GenerateRaySamples(Vector3.back, numRadiosityRays);

        GenerateSphereSamples(numRadiosityRays);
    }

    public int Vector3ToNearestIndex(Vector3 normal)
    {
        //Check the normal for the largest component, return the appropriate index
        if (Mathf.Abs(normal.x) > Mathf.Abs(normal.y))
        {
            if (Mathf.Abs(normal.x) > Mathf.Abs(normal.z))
            {
                if (normal.x > 0)
                {
                    return 3;
                }
                else
                {
                    return 2;
                }
            }
            else
            {
                if (normal.z > 0)
                {
                    return 4;
                }
                else
                {
                    return 5;
                }
            }
        }
        else
        {
            if (Mathf.Abs(normal.y) > Mathf.Abs(normal.z))
            {
                if (normal.y > 0)
                {
                    return 0;
                }
                else
                {
                    return 1;
                }
            }
            else
            {
                if (normal.z > 0)
                {
                    return 4;
                }
                else
                {
                    return 5;
                }
            }
        }
    }
    
    public RadiosityProbeSample GetOrMakeRadiosityProbeFor(Vector3Int pos)
    {
        Vector3Int key = VoxelWorld.WorldPosToRadiosityKey(pos);


        radiosityProbeSamplesMutex.WaitOne();
        radiosityProbeSamples.TryGetValue(key, out RadiosityProbeSample value);

        if (value == null)
        {
            value = new RadiosityProbeSample(pos, Color.black);

            radiosityProbeSamples[key] = value;
        }
        radiosityProbeSamplesMutex.ReleaseMutex();

        return value;
    }
    
    public RadiosityProbeSample GetRadiosityProbeIfVisible(Vector3Int key, Vector3 pos, Vector3 normal)
    {
        
        
        RadiosityProbeSample probe = GetRadiosityProbeSampleForKey(key);

        if (probe == null)
        {
            return null;
        }


        Vector3 lightVec = pos - probe.position;
        lightVec.Normalize();
        float distance = lightVec.magnitude;

        float dot = Vector3.Dot(-lightVec, normal);
        if (dot < 0)
        {
            return null;
        }

        int hit = RaycastVoxelForLighting(pos, -lightVec, distance - 1.1f);
        
        if (hit != 0)
        {
            return null;
        }

        return probe;
    }

    public Color GetRadiosityProbeColorIfVisible(Vector3Int key, Vector3 pos, Vector3 normal)
    {
        RadiosityProbeSample probe = GetRadiosityProbeSampleForKey(key);

        if (probe == null)
        {
            return Color.black;
        }


        Vector3 lightVec = pos - probe.position;
        lightVec.Normalize();
        //float distance = lightVec.magnitude;

        //float dot = Vector3.Dot(-lightVec, normal);
        //if (dot < 0)
        //{
        //    return Color.black;
        //}

        //int hit = RaycastVoxelForLighting(pos, -lightVec, distance - 1.1f);

        //if (hit != 0)
        //{
          //  return Color.black;
        //}

        return probe.color;
    }

    public Color GetRadiosityProbeColorForWorldPoint(Vector3 pos, Vector3 normal)
    {
        if (!radiosityEnabled)
        {
            return Color.black;
        }
        //Probes sit in a little
        Vector3 offsetPos = pos - new Vector3(1.5f, 1.5f, 1.5f);

        Vector3Int snappedKey = VoxelWorld.FloorInt(offsetPos);
        Vector3Int rootCell = WorldPosToRadiosityKey(snappedKey);

        //Grab the position of the pos inside the 4x4x4 box
        Vector3 cellPos = offsetPos - (rootCell * 4);
        //Turn that into a fraction
        Vector3 weight = cellPos / 4;

        Color probe0col = GetRadiosityProbeColorIfVisible(rootCell, pos, normal);
        Color probe1col = GetRadiosityProbeColorIfVisible(rootCell + new Vector3Int(1, 0, 0), pos, normal);
        Color probe2col = GetRadiosityProbeColorIfVisible(rootCell + new Vector3Int(1, 1, 0), pos, normal);
        Color probe3col = GetRadiosityProbeColorIfVisible(rootCell + new Vector3Int(0, 1, 0), pos, normal);
                                           
        Color probe4col = GetRadiosityProbeColorIfVisible(rootCell + new Vector3Int(0, 0, 1), pos, normal);
        Color probe5col = GetRadiosityProbeColorIfVisible(rootCell + new Vector3Int(1, 0, 1), pos, normal);
        Color probe6col = GetRadiosityProbeColorIfVisible(rootCell + new Vector3Int(1, 1, 1), pos, normal);
        Color probe7col = GetRadiosityProbeColorIfVisible(rootCell + new Vector3Int(0, 1, 1), pos, normal);
        
        //
        Color finalColor = Color.Lerp(
            Color.Lerp(
                Color.Lerp(probe0col, probe1col, weight.x),
                Color.Lerp(probe3col, probe2col, weight.x),
                weight.y),
            Color.Lerp(
                Color.Lerp(probe4col, probe5col, weight.x),
                Color.Lerp(probe7col, probe6col, weight.x),
                weight.y),
            weight.z);


        return finalColor;
    }
    
    private Color MaxFunc(Color a, Color b)
    {
        return new Color(Mathf.Max(a.r, b.r), Mathf.Max(a.g, b.g), Mathf.Max(a.b, b.b));
    }

    /*
    public Color RaycastIndirectLightingAtPoint(Vector3 pos, Vector3 normal)
    {
        //cast rays in a hemisphere around normal, sum the results

        Vector3[] raySamples = radiosityRaySamples[Vector3ToNearestIndex(normal)];
        Color accumulatedLight = Color.black;
        float range = 16;
        int samples = 0;
        for (int i = 0; i < numRadiosityRays; i++)
        {
            Vector3 dir = raySamples[i];

            Color incomingLight = GetDirectWorldLightingFromRayImpact(pos, dir, range);
            
            float cosTheta = Vector3.Dot(dir, normal);

            Color final = incomingLight;// * cosTheta;
      
            accumulatedLight += final;
            samples += 1;
        }

        Color sky = SampleSphericalHarmonics(renderSettings.cubeMapSHData, normal) * renderSettings.globalAmbientBrightness;
        
        if (samples < numRadiosityRays * 0.1)
        {
            return sky;
        }

        Color b = accumulatedLight / numRadiosityRays;

        return MaxFunc(b, sky);
    }*/
   
    

    private RadiosityProbeSample GetRadiosityProbeSampleForKey(Vector3Int key)
    {
        radiosityProbeSamples.TryGetValue(key, out RadiosityProbeSample value);

        if (value != null)
        {
            return value;
        }
        return null;
    }

    private Color GetRadiosityProbeColorForKeyOrBlack(Vector3Int key)
    {
        radiosityProbeSamples.TryGetValue(key, out RadiosityProbeSample value);

        if (value != null)
        {
            return value.color;
        }
        
        return Color.black;
    }

    //Get the lit color at a given point - key for radiosity
    public (bool, Color, Color) GetWorldLightingFromRayImpact(Vector3 pos, Vector3 direction, float maxDistance, List<RadiosityProbeSample> debugSamples)
    {

        (bool hit, Vector3Int vox, float distance, Vector3 normal, Color albedo, Chunk chunk) = RaycastVoxelForRadiosity(pos, direction, maxDistance);

        //Ray hit the world
        if (hit == true)
        {

            //Nudge it off the wall
            Vector3 samplePoint = pos + direction * (distance - 0.1f);

            (Color direct, Color indirect) = CalculateLightingForWorldPoint(samplePoint, vox, normal, chunk);

            if (debugSamples != null)
            {
                //Add a sample
                debugSamples.Add(new RadiosityProbeSample(samplePoint, indirect + direct));
            }

            return (true, direct * albedo, indirect * albedo);
        }

        //hit the sky?
        //Color col = SampleSphericalHarmonics(renderSettings.cubeMapSHData, direction) * renderSettings.globalAmbientBrightness; 
        Color col = Color.black;

        return (true, col, Color.black);
    }

    /*
    public Color GetDirectWorldLightingFromRayImpact(Vector3 pos, Vector3 direction, float maxDistance)
    {

        (bool hit, Vector3Int vox, float distance, Vector3 normal, Color albedo, Chunk chunk) = RaycastVoxelForRadiosity(pos, direction, maxDistance);

        //Ray hit the world
        if (hit == true)
        {
            //Nudge it off the wall
            Vector3 samplePoint = pos + direction * (distance - 0.1f);

            Color direct = CalculateDirectLightingForWorldPoint(samplePoint, vox, normal, chunk);
            return direct * albedo;
        }

        return Color.black;
        //hit the sky?
        //Color col = SampleSphericalHarmonics(this.cubeMapSHData, direction) * globalSkyBrightness;

        //return col;
    }*/

    public (Color, Color) CalculateLightingForWorldPoint(Vector3 samplePoint, Vector3 normal)
    {
        Vector3Int sunPos = FloorInt(samplePoint);
        Chunk chunk = GetChunkByVoxel(sunPos);

        if (chunk != null)
        {
            return CalculateLightingForWorldPoint(samplePoint, sunPos, normal, chunk);
        }
        return (Color.black, Color.black);
    }

    public (Color, Color) CalculateLightingForWorldPoint(Vector3 samplePoint, Vector3Int sunPoint, Vector3 normal, Chunk chunk)
    {

        //if (radiosity)
        Color indirect = GetRadiosityProbeColorForWorldPoint(samplePoint, normal);
        Color sun = new Color();
        Color light = new Color();

        //Check sun
        float sunBright = CalculateCheapSunAtPoint(sunPoint, normal);

        if (sunBright > 0)
        {
            sun += renderSettings.sunColor * renderSettings.sunBrightness;
        }

        /*
        //calculate direct lighting
        if (chunk != null && chunk.meshPersistantData != null && chunk.meshPersistantData.detailLightArray != null)
        {
            foreach (VoxelWorld.LightReference lightRef in chunk.meshPersistantData.detailLightArray)
            {
                if (lightRef == null)
                {
                    continue;
                }
                if (lightRef.shadow == true)
                {
                    light += CalculatePointLightColorAtPointShadow(samplePoint, normal, lightRef) * globalRadiosityDirectLightAmp;
                }
                else
                {
                    float falloff = CalculatePointLightColorAtPoint(samplePoint, normal, lightRef);
                    light += lightRef.color * falloff * globalRadiosityDirectLightAmp;
                }
            }

            //Calculate hero direct lighting too
            foreach (VoxelWorld.LightReference lightRef in chunk.meshPersistantData.highQualityLightArray)
            {
                if (lightRef == null)
                {
                    continue;
                }
                if (lightRef.shadow == true)
                {
                    light += CalculatePointLightColorAtPointShadow(samplePoint, normal, lightRef) * globalRadiosityDirectLightAmp;
                }
                else
                {
                    float falloff = CalculatePointLightColorAtPoint(samplePoint, normal, lightRef);
                    light += lightRef.color * falloff * globalRadiosityDirectLightAmp;
                }
            }
        }
        */
        return (sun + light, indirect);
    }

    /*
    public Color CalculateDirectLightingForWorldPoint(Vector3 samplePoint, Vector3Int sunPoint, Vector3 normal, Chunk chunk)
    {

        Color sun = new Color();
        Color light = new Color();

        //Check sun
        float sunBright = CalculateCheapSunAtPoint(sunPoint, normal);

        if (sunBright > 0)
        {
            sun += renderSettings.sunColor * renderSettings.sunBrightness;
        }


        //calculate direct lighting
        if (chunk != null && chunk.meshPersistantData != null && chunk.meshPersistantData.detailLightArray != null)
        {
            foreach (VoxelWorld.LightReference lightRef in chunk.meshPersistantData.detailLightArray)
            {
                if (lightRef == null)
                {
                    continue;
                }
                if (lightRef.shadow == true)
                {
                    light += CalculatePointLightColorAtPointShadow(samplePoint, normal, lightRef);
                }
                else
                {
                    float falloff = CalculatePointLightColorAtPoint(samplePoint, normal, lightRef);
                    light += lightRef.color * falloff;
                }
            }

            //Calculate hero direct lighting too
            foreach (VoxelWorld.LightReference lightRef in chunk.meshPersistantData.highQualityLightArray)
            {
                if (lightRef == null)
                {
                    continue;
                }
                if (lightRef.shadow == true)
                {
                    light += CalculatePointLightColorAtPointShadow(samplePoint, normal, lightRef);
                }
                else
                {
                    float falloff = CalculatePointLightColorAtPoint(samplePoint, normal, lightRef);
                    light += lightRef.color * falloff;
                }
            }
        }

        return sun + light;
    }*/

}