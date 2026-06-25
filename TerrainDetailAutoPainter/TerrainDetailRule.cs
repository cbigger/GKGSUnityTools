using UnityEngine;

[System.Serializable]
public class TerrainDetailRule
{
    public string name        = "New Rule";
    public Color  editorColor = Color.green;
    public bool   foldout     = true;

    // The detail prototype this rule controls.
    // The ruleset owns the full prototype definition – it is written to the
    // terrain's detailPrototypes array at paint time, so no prior setup is needed.
    public DetailPrototype detailPrototype = new DetailPrototype();

    // Maximum density written when the rule fully matches (0 – 255).
    [Range(0, 255)] public int maxDensity = 8;

    public float heightMin     = 0f;
    public float heightMax     = 10000f;
    public float heightFalloff = 0f;

    [Range(0f, 90f)] public float slopeMin     = 0f;
    [Range(0f, 90f)] public float slopeMax     = 35f;
    [Range(0f, 45f)] public float slopeFalloff = 5f;
}
