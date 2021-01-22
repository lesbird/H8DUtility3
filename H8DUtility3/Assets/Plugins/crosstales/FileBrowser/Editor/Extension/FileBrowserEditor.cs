#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Crosstales.FB.EditorUtil;

namespace Crosstales.FB.EditorExtension
{
   /// <summary>Custom editor for the 'FileBrowser'-class.</summary>
   [InitializeOnLoad]
   [CustomEditor(typeof(FileBrowser))]
   public class FileBrowserEditor : Editor
   {
      #region Variables

      private FileBrowser script;
      private string path = null;

      #endregion


      #region Static constructor

      static FileBrowserEditor()
      {
         EditorApplication.hierarchyWindowItemOnGUI += hierarchyItemCB;
      }

      #endregion


      #region Editor methods

      public void OnEnable()
      {
         script = (FileBrowser)target;

         EditorApplication.update += onUpdate;
         //TRManager.OnQuotaUpdate += onUpdateQuota;

         //onUpdate();
      }

      public void OnDisable()
      {
         EditorApplication.update -= onUpdate;
         //TRManager.OnQuotaUpdate -= onUpdateQuota;
      }

      public override void OnInspectorGUI()
      {
         DrawDefaultInspector();

         EditorHelper.SeparatorUI();

         if (script.isActiveAndEnabled)
         {
            if (!FileBrowser.isPlatformSupported)
            {
               EditorGUILayout.HelpBox("The current platform is not supported in builds!", MessageType.Error);
            }
            else
            {
               GUILayout.Label("Test-Drive", EditorStyles.boldLabel);

               if (Util.Helper.isEditorMode)
               {
                  GUILayout.Space(6);

                  if (GUILayout.Button(new GUIContent(" Open Single File", EditorUtil.EditorHelper.Icon_File, "Opens a single file.")))
                     path = FileBrowser.OpenSingleFile();

                  GUILayout.Space(6);

                  if (GUILayout.Button(new GUIContent(" Open Single Folder", EditorUtil.EditorHelper.Icon_Folder, "Opens a single folder.")))
                     path = FileBrowser.OpenSingleFolder();

                  GUILayout.Space(6);

                  if (GUILayout.Button(new GUIContent(" Save File", EditorUtil.EditorHelper.Icon_Save, "Saves a file.")))
                     path = FileBrowser.SaveFile();

                  GUILayout.Space(6);

                  GUILayout.Label("Path: " + (string.IsNullOrEmpty(path) ? "nothing selected" : path));

                  GUILayout.Space(6);
               }
               else
               {
                  EditorGUILayout.HelpBox("Disabled in Play-mode!", MessageType.Info);
               }
            }
         }
         else
         {
            EditorGUILayout.HelpBox("Script is disabled!", MessageType.Info);
         }
      }

      #endregion


      #region Private methods

      private void onUpdate()
      {
         Repaint();
      }

      private void onUpdateQuota(int e)
      {
         //Debug.Log("Quota: " + e, this);
         Repaint();
      }

      private static void hierarchyItemCB(int instanceID, Rect selectionRect)
      {
         if (EditorConfig.HIERARCHY_ICON)
         {
            GameObject go = EditorUtility.InstanceIDToObject(instanceID) as GameObject;

            if (go != null && go.GetComponent<FileBrowser>())
            {
               Rect r = new Rect(selectionRect);
               r.x = r.width - 4;

               GUI.Label(r, EditorHelper.Logo_Asset_Small);
            }
         }
      }

      #endregion
   }
}
#endif
// © 2020 crosstales LLC (https://www.crosstales.com)