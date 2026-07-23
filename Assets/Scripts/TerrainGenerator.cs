using UnityEngine;

public class DuneGenerator : MonoBehaviour
{
    [Header("Terrain Reference")]
    [SerializeField] private Terrain targetTerrain;

    [Header("Dune Parameters")]
    [Tooltip("How stretched out the dunes are. Lower = larger, wider dunes.")]
    [SerializeField] private float scale = 3f;

    [Tooltip("Maximum height of the dunes in Unity units.")]
    [SerializeField] private float heightMultiplier = 20f;

    [Tooltip("How sharply pinched the crests are. Higher = sharper ridges.")]
    [SerializeField] private float ridgeSharpness = 2.5f;

    [Tooltip("Change these values to 'scroll' through the infinite noise map to find a seed you like.")]
    [SerializeField] private Vector2 noiseOffset;

    // This attribute lets you right-click the component in the Inspector to run the function without pressing Play.
    [ContextMenu("Generate Dunes")]
    public void Generate()
    {
        // Fallback if you drop the script directly onto the Terrain object
        if (targetTerrain == null)
        {
            targetTerrain = GetComponent<Terrain>();
            if (targetTerrain == null)
            {
                Debug.LogError("No Terrain assigned! Please assign a terrain in the inspector.");
                return;
            }
        }

        TerrainData terrainData = targetTerrain.terrainData;
        int width = terrainData.heightmapResolution;
        int length = terrainData.heightmapResolution;
        float[,] heights = new float[width, length];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < length; y++)
            {
                // Calculate coordinates
                float xCoord = (float)x / width * scale + noiseOffset.x;
                float yCoord = (float)y / length * scale + noiseOffset.y;

                // 1. Get raw Perlin noise (0 to 1)
                float p = Mathf.PerlinNoise(xCoord, yCoord);

                // 2. Fold into sharp ridges
                float ridge = 1f - Mathf.Abs(p * 2f - 1f);

                // 3. Pinch the crests using the power function
                float duneHeight = Mathf.Pow(ridge, ridgeSharpness);

                // Unity's SetHeights expects values between 0.0 and 1.0. 
                // We divide our desired height by the terrain's maximum possible Y size to normalize it.
                heights[x, y] = duneHeight * (heightMultiplier / terrainData.size.y);
            }
        }

        // Push the array to the terrain
        terrainData.SetHeights(0, 0, heights);
        Debug.Log("Dunes successfully generated!");
    }
}