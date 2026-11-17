using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dornell.BIM
{
    /// <summary>
    /// Ensures .buildingData assets imported before URP was indexed on a fresh clone
    /// are automatically reimported once with the URP Lit shader.
    /// </summary>
    [InitializeOnLoad]
    public static class UrpBuildingMaterialFixer
    {
        private const string SessionKey = "Dornell.BIM.UrpBuildingMaterialFixer.Checked";

        static UrpBuildingMaterialFixer()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += CheckAndUpgradeIfNeeded;
        }

        [MenuItem("Tools/INSA/Reimport Buildings for URP")]
        public static void ReimportAllBuildings()
        {
            const string buildingsFolder = "Assets/INSA/Prefabs/Buildings";
            if (!Directory.Exists(buildingsFolder))
                return;

            string[] guids = AssetDatabase.FindAssets("", new[] { buildingsFolder });
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".buildingData", System.StringComparison.OrdinalIgnoreCase))
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[UrpBuildingMaterialFixer] Reimported .buildingData assets for URP.");
        }

        private static void CheckAndUpgradeIfNeeded()
        {
            if (AssetDatabase.LoadAssetAtPath<Shader>("Packages/com.unity.render-pipelines.universal/Shaders/Lit.shader") == null)
                return;

            var sample = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/INSA/Prefabs/Buildings/B01_RDC.buildingData");
            if (sample == null)
                return;

            var renderer = sample.GetComponentInChildren<MeshRenderer>(true);
            if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null)
                return;

            if (renderer.sharedMaterial.shader.name == "Standard")
            {
                ReimportAllBuildings();
            }
        }
    }
}
