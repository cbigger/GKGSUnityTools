using UnityEngine;
using UnityEditor;
using System.IO;

public class ReverseAnimationTool
{
    [MenuItem("Assets/Create Reversed Clip", false, 20)]
    static void CreateReversedClip()
    {
        // Get the selected animation clip
        AnimationClip originalClip = Selection.activeObject as AnimationClip;
        if (originalClip == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select an Animation Clip in the Project window.", "OK");
            return;
        }

        // Create a new AnimationClip
        AnimationClip reversedClip = new AnimationClip();
        reversedClip.name = originalClip.name + "_Reversed";
        reversedClip.frameRate = originalClip.frameRate;

        // Get all the curves from the original clip
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(originalClip);

        foreach (EditorCurveBinding binding in bindings)
        {
            // Get the original curve
            AnimationCurve originalCurve = AnimationUtility.GetEditorCurve(originalClip, binding);
            
            if (originalCurve != null)
            {
                // Create a new curve for the reversed clip
                AnimationCurve reversedCurve = ReverseCurve(originalCurve, originalClip.length);
                AnimationUtility.SetEditorCurve(reversedClip, binding, reversedCurve);
            }
        }

        // Copy animation events and reverse their time
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(originalClip);
        if (events.Length > 0)
        {
            AnimationEvent[] reversedEvents = new AnimationEvent[events.Length];
            for (int i = 0; i < events.Length; i++)
            {
                AnimationEvent evt = new AnimationEvent();
                evt.functionName = events[i].functionName;
                evt.stringParameter = events[i].stringParameter;
                evt.floatParameter = events[i].floatParameter;
                evt.intParameter = events[i].intParameter;
                evt.objectReferenceParameter = events[i].objectReferenceParameter;
                
                // Reverse the event time
                evt.time = originalClip.length - events[i].time;
                reversedEvents[i] = evt;
            }
            AnimationUtility.SetAnimationEvents(reversedClip, reversedEvents);
        }

        // Save the new clip as an .anim file
        string path = AssetDatabase.GetAssetPath(originalClip);
        string directory = Path.GetDirectoryName(path);
        string newPath = Path.Combine(directory, reversedClip.name + ".anim");
        
        AssetDatabase.CreateAsset(reversedClip, newPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", $"Reversed clip saved to: {newPath}", "OK");
    }

    // The actual curve-reversing logic
    static AnimationCurve ReverseCurve(AnimationCurve originalCurve, float clipLength)
    {
        Keyframe[] originalKeys = originalCurve.keys;
        Keyframe[] reversedKeys = new Keyframe[originalKeys.Length];

        for (int i = 0; i < originalKeys.Length; i++)
        {
            // Reverse the time: newTime = clipLength - oldTime
            float newTime = clipLength - originalKeys[i].time;
            
            // Flip the tangent values so the curve doesn't look broken
            float newInTangent = -originalKeys[i].outTangent;
            float newOutTangent = -originalKeys[i].inTangent;
            
            reversedKeys[originalKeys.Length - 1 - i] = new Keyframe(
                newTime, 
                originalKeys[i].value, 
                newInTangent, 
                newOutTangent
            );
        }

        AnimationCurve newCurve = new AnimationCurve(reversedKeys);
        return newCurve;
    }

    // Make sure the menu item only appears when selecting an AnimationClip
    [MenuItem("Assets/Create Reversed Clip", true)]
    static bool ValidateCreateReversedClip()
    {
        return Selection.activeObject is AnimationClip;
    }
}