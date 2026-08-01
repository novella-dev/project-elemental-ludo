using ElementalLudo.Tokens;
using UnityEditor;
using UnityEngine;

public static class SetupAllTokens
{
    [MenuItem("Elemental Ludo/Assign All Token Models")]
    public static void AssignAll()
    {
        AssignModel("RedPlayerStyle", "fire.glb",
            new Vector3(-90f, 0f, 0f), 0.68f, 0.98f, Color.white);

        AssignModel("BluePlayerStyle", "WaterDrop.glb",
            new Vector3(-90f, -90f, 0f), 0.68f, 0.98f,
            new Color(0.08f, 0.25f, 0.45f, 1f));

        AssignModel("YellowPlayerStyle", "lightning.glb",
            new Vector3(-90f, 0f, 0f), 0.68f, 0.98f, Color.white);

        AssignModel("GreenPlayerStyle", "plant.glb",
            new Vector3(-90f, 0f, 0f), 0.68f, 0.98f, Color.white);

        AssetDatabase.Refresh();
        Debug.Log("All four token models assigned. Delete Assets/Editor/SetupAllTokens.cs when done.");
    }

    private static void AssignModel(
        string styleAssetName,
        string modelFileName,
        Vector3 euler,
        float footprint,
        float height,
        Color tint)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Models/{modelFileName}");

        if (model == null)
        {
            Debug.LogError(
                $"{modelFileName} not loaded. Ensure glTFast is installed.");

            return;
        }

        PlayerStyle style = AssetDatabase.LoadAssetAtPath<PlayerStyle>(
            $"Assets/Data/PlayerStyles/{styleAssetName}.asset");

        if (style == null)
        {
            Debug.LogError($"{styleAssetName}.asset not found.");

            return;
        }

        SerializedObject so = new SerializedObject(style);
        so.FindProperty("tokenModel").objectReferenceValue = model;
        so.FindProperty("tokenModelEulerAngles").vector3Value = euler;
        so.FindProperty("tokenModelFootprint").floatValue = footprint;
        so.FindProperty("tokenModelHeight").floatValue = height;
        so.FindProperty("tokenModelTint").colorValue = tint;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(style);

        Debug.Log($"{styleAssetName}: assigned {modelFileName}.");
    }
}
