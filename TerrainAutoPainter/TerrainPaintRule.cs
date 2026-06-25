using UnityEngine;

[System.Serializable]
public class TerrainPaintRule
{
    public string      name        = "New Rule";
    public Color       editorColor = Color.white;
    public bool        foldout     = true;

    public TerrainLayer terrainLayer = null;

    public float heightMin     = 0f;
    public float heightMax     = 10000f;
    public float heightFalloff = 0f;

    [Range(0f, 90f)]  public float slopeMin     = 0f;
    [Range(0f, 90f)]  public float slopeMax     = 90f;
    [Range(0f, 45f)]  public float slopeFalloff = 5f;
}

public enum HeightRangeMode { Absolute, LocalSample, GlobalSample }
