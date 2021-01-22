#if UNITY_EDITOR
using UnityEditor;

namespace Crosstales.FB.EditorTask
{
   /// <summary>Show the configuration window on the first launch.</summary>
   public class Launch : AssetPostprocessor
   {
      public static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
      {
         foreach (string str in importedAssets)
         {
            if (str.Contains(EditorUtil.EditorConstants.ASSET_UID.ToString()))
            {
               //Debug.Log("Launch window");

               Common.EditorTask.SetupResources.Setup();
               SetupResources.Setup();

               EditorIntegration.ConfigWindow.ShowWindow(4);

               break;
            }
         }
      }
   }
}
#endif
// © 2019-2020 crosstales LLC (https://www.crosstales.com)