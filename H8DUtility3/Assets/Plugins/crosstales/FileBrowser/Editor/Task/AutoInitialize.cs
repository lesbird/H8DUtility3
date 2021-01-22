#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Crosstales.FB.EditorUtil;

namespace Crosstales.FB.EditorTask
{
   /// <summary>Automatically adds the necessary FileBrowser-prefabs to the current scene.</summary>
   [InitializeOnLoad]
   public class AutoInitialize
   {
      #region Variables

      private static Scene currentScene;

      #endregion


      #region Constructor

      static AutoInitialize()
      {
#if UNITY_2018_1_OR_NEWER
         EditorApplication.hierarchyChanged += hierarchyWindowChanged;
#else
         EditorApplication.hierarchyWindowChanged += hierarchyWindowChanged;
#endif
      }

      #endregion


      #region Private static methods

      private static void hierarchyWindowChanged()
      {
         if (currentScene != EditorSceneManager.GetActiveScene())
         {
            if (EditorConfig.PREFAB_AUTOLOAD)
            {
               if (!EditorHelper.isFileBrowserInScene)
                  EditorHelper.InstantiatePrefab(Util.Constants.FB_SCENE_OBJECT_NAME);
            }

            currentScene = EditorSceneManager.GetActiveScene();
         }
      }

      #endregion
   }
}
#endif
// © 2020 crosstales LLC (https://www.crosstales.com)