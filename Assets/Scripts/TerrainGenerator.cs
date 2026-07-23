using UnityEngine;

public class DuneGenerator : MonoBehaviour
{
    [Header("Editor Settings")]
    [Tooltip("Update terrain instantly as you drag sliders. Turn off if the editor lags!")]
    [SerializeField] private bool autoUpdate = true;

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

    // Unity calls this whenever a value changes in the Inspector
    private void OnValidate()
    {
        if (autoUpdate && targetTerrain != null && targetTerrain.terrainData != null)
        {
            // We use delayCall to avoid Unity throwing errors about modifying TerrainData during serialization
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= GenerateSafe; // Prevent queued up calls from stacking
            UnityEditor.EditorApplication.delayCall += GenerateSafe;
            #endif
        }
    }

    #if UNITY_EDITOR
    private void GenerateSafe()
    {
        // Ensure the script hasn't been deleted before the delayed call runs
        if (this == null || targetTerrain == null) return;
        Generate();
    }
    #endif

    [ContextMenu("Generate Dunes")]
    public void Generate()
    {
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
                float xCoord = (float)x / width * scale + noiseOffset.x;
                float yCoord = (float)y / length * scale + noiseOffset.y;

                float p = Mathf.PerlinNoise(xCoord, yCoord);
                float ridge = 1f - Mathf.Abs(p * 2f - 1f);
                float duneHeight = Mathf.Pow(ridge, ridgeSharpness);

                heights[x, y] = duneHeight * (heightMultiplier / terrainData.size.y);
            }
        }

        terrainData.SetHeights(0, 0, heights);
    }
}