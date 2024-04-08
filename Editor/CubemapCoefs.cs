using System;
using System.Drawing.Printing;
using System.Xml;
using UnityEditor;
using UnityEngine;

public class SphericalHarmonicPostProcessor : AssetPostprocessor {
    private int order = 3; // First 3 bands
    private int numCoeffs;
    
    void OnPostprocessCubemap(Cubemap texture) {
       
        if (texture == null) return;

        numCoeffs = order * order;
        Cubemap cubemap = texture;
        Vector4[] coefficients = CalculateSphericalHarmonicCoefficients(cubemap, order);

        //Get the full path and filename without extension
        string path = assetPath;
        int index = path.LastIndexOf(".");
        path = path.Substring(0, index);

        string filename = path + ".xml";
        WriteCoefficientsToXml(coefficients, filename);
        Debug.Log("Writing coefficients to " + filename);
    }

    private Vector4[] CalculateSphericalHarmonicCoefficients(Cubemap cubemap, int order) {
        int resolution = cubemap.width;
        Vector4[] coefficients = new Vector4[numCoeffs];
        
        int numSamples = 2500;
        
        Vector3[] directions = new Vector3[numSamples];
        float goldenRatio = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
        float angleIncrement = Mathf.PI * 2 * goldenRatio;

        for (int i = 0; i < numSamples; i++) {
            float t = (float)i / numSamples;
            float inclination = Mathf.Acos(1 - 2 * t);
            float azimuth = angleIncrement * i;

            float x = Mathf.Sin(inclination) * Mathf.Cos(azimuth);
            float y = Mathf.Sin(inclination) * Mathf.Sin(azimuth);
            float z = Mathf.Cos(inclination);

            directions[i] = new Vector3(x, y, z);
        }

        //Run through each sample, work out the face direction
        //and accumulate the SH coefficients
        for (int i = 0; i < numSamples; i++) {
            Vector3 dir = directions[i];

            CubemapFace face;
            int x;
            int y;

            GetDirectionFromCubemapFaceAndUV(dir, resolution, out face, out x, out y);

            Color radiance = cubemap.GetPixel((CubemapFace)face, x, y);
    
            for (int l = 0; l < order; l++) {
                for (int m = -l; m <= l; m++) {
                    int ix = l * (l + 1) + m;
                    Vector4 f = EvaluateSH(l, m, dir) * (Vector4)radiance;
                    coefficients[ix] += f;
                }
            }
        }
        
        for (int i = 0; i < numCoeffs; i++) {
            coefficients[i] /= 500;
        }
        
        return coefficients;
    }

    private float EvaluateSH(int l, int m, Vector3 direction) {
        float x = direction.x;
        float y = direction.y;
        float z = direction.z;
        float sqrtPi = Mathf.Sqrt(Mathf.PI);

        if (l == 0 && m == 0) {
            return 0.5f * Mathf.Sqrt(1.0f / Mathf.PI);
        }
        else if (l == 1) {
            if (m == -1) {
                return sqrtPi * y / 2;
            }
            else if (m == 0) {
                return sqrtPi * z / 2;
            }
            else if (m == 1) {
                return sqrtPi * x / 2;
            }
        }
        else if (l == 2) {
            if (m == -2) {
                return sqrtPi * (x * y) / 4;
            }
            else if (m == -1) {
                return sqrtPi * (y * z) / 4;
            }
            else if (m == 0) {
                return sqrtPi * (-x * x - y * y + 2 * z * z) / 4;
            }
            else if (m == 1) {
                return sqrtPi * (z * x) / 4;
            }
            else if (m == 2) {
                return sqrtPi * (x * x - y * y) / 4;
            }
        }

        return 0.0f;
    }

    private void WriteCoefficientsToXml(Vector4[] coefficients, string outputPath) {
        XmlDocument xmlDoc = new XmlDocument();
        XmlElement rootElement = xmlDoc.CreateElement("SphericalHarmonicCoefficients");
        xmlDoc.AppendChild(rootElement);

        for (int i = 0; i < numCoeffs; i++) {
            XmlElement coeffElement = xmlDoc.CreateElement("Coefficient");
            coeffElement.SetAttribute("index", i.ToString());
            coeffElement.SetAttribute("value", coefficients[i].ToString());

            rootElement.AppendChild(coeffElement);
        }

        xmlDoc.Save(outputPath);

        //Refresh the asset database
        // AssetDatabase.Refresh();
    }

    private CubemapFace GetFaceBasedOnDir(Vector3 dir) {
        float absX = Mathf.Abs(dir.x);
        float absY = Mathf.Abs(dir.y);
        float absZ = Mathf.Abs(dir.z);

        if (absX > absY && absX > absZ) {
            if (dir.x > 0) {
                return CubemapFace.PositiveX;
            }
            else {
                return CubemapFace.NegativeX;
            }
        }
        else if (absY > absX && absY > absZ) {
            if (dir.y > 0) {
                return CubemapFace.PositiveY;
            }
            else {
                return CubemapFace.NegativeY;
            }
        }
        else {
            if (dir.z > 0) {
                return CubemapFace.PositiveZ;
            }
            else {
                return CubemapFace.NegativeZ;
            }
        }
        
    }

    private void GetDirectionFromCubemapFaceAndUV(Vector3 direction, int resolution, out CubemapFace face, out int x, out int y) {

        face = GetFaceBasedOnDir(direction);
        x = 0;
        y = 0;
        switch (face) {
            case CubemapFace.PositiveX: //+X uses YZ
            x = (int)(direction.y * 0.5f + 0.5f) * resolution;
            y = (int)(direction.z * 0.5f + 0.5f) * resolution;
            break;
            case CubemapFace.NegativeX:
            x = (int)(-direction.y * 0.5f + 0.5f) * resolution;
            y = (int)(direction.z * 0.5f + 0.5f) * resolution;
            break;
            case CubemapFace.PositiveY:
            x = (int)(direction.x * 0.5f + 0.5f) * resolution;
            y = (int)(direction.z * 0.5f + 0.5f) * resolution;
            
            break;
            case CubemapFace.NegativeY:
            x = (int)(direction.x * 0.5f + 0.5f) * resolution;
            y = (int)(-direction.z * 0.5f + 0.5f) * resolution;
            break;
            case CubemapFace.PositiveZ:
            x = (int)(direction.x * 0.5f + 0.5f) * resolution;
            y = (int)(direction.y * 0.5f + 0.5f) * resolution;
            break;
            case CubemapFace.NegativeZ:
            x = (int)(direction.x * 0.5f + 0.5f) * resolution;
            y = (int)(-direction.y * 0.5f + 0.5f) * resolution;
            break;
            

        }
    }
     
}