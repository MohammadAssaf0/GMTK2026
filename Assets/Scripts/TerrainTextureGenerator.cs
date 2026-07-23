using UnityEngine;
using System.IO;

public class LiveSandTexture : MonoBehaviour
{
    [Header("Target Asset")]
    [SerializeField] private TerrainLayer terrainLayer;

    [Header("Resolution Settings")]
    [Range(32, 256)] 
    [SerializeField] private int previewResolution = 128;
    [Tooltip("The resolution used when you right-click and select Bake High-Res Textures.")]
    [SerializeField] private int bakeResolution = 1024;
    
    [Header("Base Ripple Shape")]
    [Tooltip("How many ripples stretch across the texture.")]
    [SerializeField] private float baseFrequency = 15f;
    [Tooltip("Stretches the noise on one axis to simulate wind direction. 1 = round bumps, 0.2 = long streaks.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float windStretch = 0.3f;
    [Tooltip("How sharp the sand crests are. Higher = sharper, pinched peaks.")]
    [Range(0.5f, 5f)]
    [SerializeField] private float ridgePinch = 1.5f;

    [Header("Micro Disruption (Organic Noise)")]
    [Tooltip("How much smaller the secondary disruption noise is compared to the base ripples.")]
    [SerializeField] private float disruptionFrequencyMultiplier = 2.5f;
    [Tooltip("How much the smaller noise breaks up the main ripples. 0 = perfect waves, 1 = total chaos.")]
    [Range(0f, 1f)]
    [SerializeField] private float disruptionBlend = 0.25f;

    [Header("Color & Normal Maps")]
    [SerializeField] private Color sandHighlightColor = new Color(0.88f, 0.78f, 0.60f);
    [SerializeField] private Color sandShadowColor = new Color(0.75f, 0.65f, 0.45f);
    [Range(0.1f, 10f)] 
    [SerializeField] private float normalBumpStrength = 2f;
    [Tooltip("Controls the steepness of the normal map vector calculation.")]
    [Range(0.5f, 5f)]
    [SerializeField] private float normalZDepth = 2f;

    [Header("Export Settings")]
    [Tooltip("Folder will be created in your root Assets folder if it doesn't exist.")]
    [SerializeField] private string exportFolderName = "Texture of the sand";

    private void OnValidate()
    {
        if (terrainLayer == null) return;
        GenerateTextures(previewResolution, false);
    }

    [ContextMenu("Bake High-Res Textures")]
    public void BakeTextures()
    {
        if (terrainLayer == null)
        {
            Debug.LogError("Assign a Terrain Layer to bake textures.");
            return;
        }
        GenerateTextures(bakeResolution, true);
    }

    private void GenerateTextures(int resolution, bool saveToDisk)
    {
        Texture2D albedoTexture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        Texture2D normalTexture = new Texture2D(resolution, resolution, TextureFormat.RGB24, false);
        
        float[,] heightMap = new float[resolution, resolution];
        
        // Pass 1: Generate the heightmap using the exposed parameters
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                float u = (float)x / resolution;
                float v = (float)y / resolution;
                
                float baseRipples = CalculateSeamlessNoise(u, v, baseFrequency);
                float disruption = CalculateSeamlessNoise(u, v, baseFrequency * disruptionFrequencyMultiplier);

                // Blend the two noise layers based on the Inspector slider
                heightMap[x, y] = Mathf.Lerp(baseRipples, disruption, disruptionBlend);
            }
        }

        // Pass 2: Calculate Colors and Normals
        for (int x = 0; x < resolution; x++)
        {
            for (int y = 0; y < resolution; y++)
            {
                float currentHeight = heightMap[x, y];
                
                // Albedo
                Color pixelColor = Color.Lerp(sandShadowColor, sandHighlightColor, currentHeight);
                albedoTexture.SetPixel(x, y, pixelColor);

                // Normal (wrapping array indices for seamless edge reading)
                int leftX = (x - 1 + resolution) % resolution;
                int rightX = (x + 1) % resolution;
                int downY = (y - 1 + resolution) % resolution;
                int upY = (y + 1) % resolution;

                float heightLeft = heightMap[leftX, y];
                float heightRight = heightMap[rightX, y];
                float heightDown = heightMap[x, downY];
                float heightUp = heightMap[x, upY];

                Vector3 normalVector = new Vector3(
                    (heightLeft - heightRight) * normalBumpStrength, 
                    (heightDown - heightUp) * normalBumpStrength, 
                    normalZDepth
                ).normalized;
                
                Color normalPixel = new Color(
                    normalVector.x * 0.5f + 0.5f, 
                    normalVector.y * 0.5f + 0.5f, 
                    normalVector.z * 0.5f + 0.5f
                );
                
                normalTexture.SetPixel(x, y, normalPixel);
            }
        }

        albedoTexture.Apply();
        normalTexture.Apply();

        terrainLayer.diffuseTexture = albedoTexture;
        terrainLayer.normalMapTexture = normalTexture;

        if (saveToDisk)
        {
            SaveTextures(albedoTexture, normalTexture);
        }
    }

    private float CalculateSeamlessNoise(float u, float v, float frequency)
    {
        // Stretch the noise to simulate directional wind
        float freqU = frequency;
        float freqV = frequency * windStretch; 

        float s = u * freqU;
        float t = v * freqV;

        float noise00 = Mathf.PerlinNoise(s, t);
        float noise10 = Mathf.PerlinNoise(s - freqU, t);
        float noise01 = Mathf.PerlinNoise(s, t - freqV);
        float noise11 = Mathf.PerlinNoise(s - freqU, t - freqV);

        float xBlend1 = Mathf.Lerp(noise00, noise10, u);
        float xBlend2 = Mathf.Lerp(noise01, noise11, u);
        float p = Mathf.Lerp(xBlend1, xBlend2, v);
    
        // Fold the noise into ridges (maps 0-1 to -1 to 1, then takes absolute value)
        float ridge = 1f - Mathf.Abs(p * 2f - 1f);
    
        // Pinch the ridges using the exposed inspector value
        return Mathf.Pow(ridge, ridgePinch); 
    }

    private void SaveTextures(Texture2D albedo, Texture2D normal)
    {
        // Build the safe path
        string directoryPath = Path.Combine(Application.dataPath, exportFolderName);
        
        // Ensure the directory exists so Unity doesn't throw a write exception
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        string albedoPath = Path.Combine(directoryPath, "FinalSand_Albedo.png");
        string normalPath = Path.Combine(directoryPath, "FinalSand_Normal.png");
        
        File.WriteAllBytes(albedoPath, albedo.EncodeToPNG());
        File.WriteAllBytes(normalPath, normal.EncodeToPNG());

        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        #endif
        
        Debug.Log($"Textures saved to: {albedoPath} and {normalPath}");
    }
}