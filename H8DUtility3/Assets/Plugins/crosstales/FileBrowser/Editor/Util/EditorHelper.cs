#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Crosstales.FB.EditorUtil
{
   /// <summary>Editor helper class.</summary>
   public abstract class EditorHelper : Common.EditorUtil.BaseEditorHelper
   {
      #region Static variables

      /// <summary>Start index inside the "GameObject"-menu.</summary>
      //public const int GO_ID = 28;
      public const int GO_ID = 20;

      /// <summary>Start index inside the "Tools"-menu.</summary>
      public const int MENU_ID = 11018; // 1, T = 20, R = 18

      private static Texture2D logo_asset;
      private static Texture2D logo_asset_small;

      private static Texture2D icon_file;

      #endregion


      #region Static properties

      public static Texture2D Logo_Asset
      {
         get { return loadImage(ref logo_asset, "logo_asset_pro.png"); }
      }

      public static Texture2D Logo_Asset_Small
      {
         get { return loadImage(ref logo_asset_small, "logo_asset_small_pro.png"); }
      }

      public static Texture2D Icon_File
      {
         get { return loadImage(ref icon_file, "icon_file.png"); }
      }

      #endregion


      #region Static methods

      /// <summary>Instantiates a prefab.</summary>
      /// <param name="prefabName">Name of the prefab.</param>
      public static void InstantiatePrefab(string prefabName)
      {
         PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath("Assets" + EditorConfig.PREFAB_PATH + prefabName + ".prefab", typeof(GameObject)));
         UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
      }

      /// <summary>Checks if the 'FileBrowser'-prefab is in the scene.</summary>
      /// <returns>True if the 'FileBrowser'-prefab is in the scene.</returns>
      public static bool isFileBrowserInScene
      {
         get { return GameObject.Find(Util.Constants.FB_SCENE_OBJECT_NAME) != null; }
      }

      /// <summary>Loads an image as Texture2D from 'Editor Default Resources'.</summary>
      /// <param name="logo">Logo to load.</param>
      /// <param name="fileName">Name of the image.</param>
      /// <returns>Image as Texture2D from 'Editor Default Resources'.</returns>
      private static Texture2D loadImage(ref Texture2D logo, string fileName)
      {
         if (logo == null)
         {
#if CT_DEVELOP
            logo = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets" + EditorConfig.ASSET_PATH + "Icons/" + fileName, typeof(Texture2D));
#else
                logo = (Texture2D)EditorGUIUtility.Load("crosstales/FileBrowser/" + fileName);
#endif

            if (logo == null)
            {
               Debug.LogWarning("Image not found: " + fileName);
            }
         }

         return logo;
      }

      #endregion
   }
}
#endif
// © 2019-2020 crosstales LLC (https://www.crosstales.com)