using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Terrain/Detail Ruleset", fileName = "NewTerrainDetailRuleset")]
public class TerrainDetailRuleset : ScriptableObject
{
    public HeightRangeMode        heightMode       = HeightRangeMode.Absolute;
    public float                  sampledHeightMin = 0f;
    public float                  sampledHeightMax = 10000f;
    public List<TerrainDetailRule> rules           = new List<TerrainDetailRule>();
}
