using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Terrain/Paint Ruleset", fileName = "NewTerrainRuleset")]
public class TerrainPaintRuleset : ScriptableObject
{
    public HeightRangeMode        heightMode       = HeightRangeMode.Absolute;
    public float                  sampledHeightMin = 0f;
    public float                  sampledHeightMax = 10000f;
    public List<TerrainPaintRule> rules            = new List<TerrainPaintRule>();
}
