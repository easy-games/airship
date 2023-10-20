using System;
using System.Drawing.Printing;
using System.Xml;
using UnityEditor;
using UnityEngine;

public class SphericalHarmonicPostProcessor : AssetPostprocessor
{
    private int order = 3; // First 3 bands
    private int numCoeffs;


    void OnPostprocessCubemap(Cubemap texture)
    {
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

    private Vector4[] CalculateSphericalHarmonicCoefficients(Cubemap cubemap, int order)
    {
        int resolution = cubemap.width;
        Vector4[] coefficients = new Vector4[numCoeffs];

        int step = resolution / 64;
        if (step < 1)
        {
            step = 1;
        }

        for (int face = 0; face < 6; face++)
        {
            for (int y = 0; y < resolution; y += step)
            {
                for (int x = 0; x < resolution; x += step)
                {
                    Vector3 direction = GetDirectionFromCubemapFaceAndUV(face, x, y, resolution);
                    Color radiance = cubemap.GetPixel((CubemapFace)face, x, y);
                    Color.RGBToHSV(radiance, out float h, out float s, out float v);
                    radiance = Color.HSVToRGB(h, s, v);

                    for (int l = 0; l < order; l++)
                    {
                        for (int m = -l; m <= l; m++)
                        {
                            int index = l * (l + 1) + m;
                            Vector4 f = EvaluateSH(l, m, direction) * (Vector4)radiance;
                            coefficients[index] += f;
                        }
                    }
                }
            }
        }

        float smallerResolution = resolution / step;

        float weight = 4.0f * Mathf.PI / (smallerResolution * smallerResolution * 6);
        for (int i = 0; i < numCoeffs; i++)
        {
            coefficients[i] *= weight;
        }

        // Compute total energy
        float totalEnergy = 0.0f;
        for (int i = 0; i < numCoeffs; i++)
        {
            totalEnergy += coefficients[i].magnitude;
        }

        // Normalize coefficients
        float normalizationFactor = 1.0f / Mathf.Max(totalEnergy, 1e-5f);  // Prevent divide by zero
        for (int i = 0; i < numCoeffs; i++)
        {
            coefficients[i] *= normalizationFactor;
        }

        return coefficients;
    }


    private void WriteCoefficientsToXml(Vector4[] coefficients, string outputPath)
    {
        XmlDocument xmlDoc = new XmlDocument();
        XmlElement rootElement = xmlDoc.CreateElement("SphericalHarmonicCoefficients");
        xmlDoc.AppendChild(rootElement);

        for (int i = 0; i < numCoeffs; i++)
        {
            XmlElement coeffElement = xmlDoc.CreateElement("Coefficient");
            coeffElement.SetAttribute("index", i.ToString());
            coeffElement.SetAttribute("value", coefficients[i].ToString());
         
            rootElement.AppendChild(coeffElement);
        }

        xmlDoc.Save(outputPath);
    }

    private Vector3 GetDirectionFromCubemapFaceAndUV(int face, int x, int y, int resolution)
    {
        float u = (x + 0.5f) / resolution * 2.0f - 1.0f;
        float v = (y + 0.5f) / resolution * 2.0f - 1.0f;

        Vector3 direction = Vector3.zero;

        switch (face)
        {
            case 0: // Positive X
                direction = new Vector3(1, -v, -u);
                break;
            case 1: // Negative X
                direction = new Vector3(-1, -v, u);
                break;
            case 2: // Positive Y
                direction = new Vector3(u, 1, v);
                break;
            case 3: // Negative Y
                direction = new Vector3(u, -1, -v);
                break;
            case 4: // Positive Z
                direction = new Vector3(u, -v, 1);
                break;
            case 5: // Negative Z
                direction = new Vector3(-u, -v, -1);
                break;
        }

        return direction.normalized;
    }

    private float EvaluateSH(int l, int m, Vector3 direction)
    {
        float x = direction.x;
        float y = direction.y;
        float z = direction.z;
        float sqrtPi = Mathf.Sqrt(Mathf.PI);

        if (l == 0 && m == 0)
        {
            return 0.5f * Mathf.Sqrt(1.0f / Mathf.PI);
        }
        else if (l == 1)
        {
            if (m == -1)
            {
                return sqrtPi * y / 2;
            }
            else if (m == 0)
            {
                return sqrtPi * z / 2;
            }
            else if (m == 1)
            {
                return sqrtPi * x / 2;
            }
        }
        else if (l == 2)
        {
            if (m == -2)
            {
                return sqrtPi * (x * y) / 4;
            }
            else if (m == -1)
            {
                return sqrtPi * (y * z) / 4;
            }
            else if (m == 0)
            {
                return sqrtPi * (-x * x - y * y + 2 * z * z) / 4;
            }
            else if (m == 1)
            {
                return sqrtPi * (z * x) / 4;
            }
            else if (m == 2)
            {
                return sqrtPi * (x * x - y * y) / 4;
            }
        }

        return 0.0f;
    }
}