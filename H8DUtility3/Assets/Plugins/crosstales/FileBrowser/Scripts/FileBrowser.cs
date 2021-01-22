using System.Linq;
using UnityEngine;

namespace Crosstales.FB
{
   [System.Serializable]
   public class OnOpenFilesCompleted : UnityEngine.Events.UnityEvent<bool, string, string>
   {
   }

   [System.Serializable]
   public class OnOpenFoldersCompleted : UnityEngine.Events.UnityEvent<bool, string, string>
   {
   }

   [System.Serializable]
   public class OnSaveFileCompleted : UnityEngine.Events.UnityEvent<bool, string>
   {
   }

   internal static class WrapperHolder
   {
      #region Variables

      public static Wrapper.IFileBrowser PlatformWrapper { get; private set; }

      #endregion


      #region Constructor

      static WrapperHolder()
      {
//#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
#if UNITY_EDITOR_WIN
         if (Util.Helper.isEditor && !Util.Config.NATIVE_WINDOWS)
#else
         if (Util.Helper.isEditor)
#endif
         {
#if UNITY_EDITOR
            PlatformWrapper = new Wrapper.FileBrowserEditor();
#endif
         }
         else if (Util.Helper.isMacOSPlatform)
         {
#if UNITY_STANDALONE_OSX
            PlatformWrapper = new Wrapper.FileBrowserMac();
#endif
         }
         else if (Util.Helper.isWindowsPlatform || Util.Helper.isWindowsEditor)
         {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            PlatformWrapper = new Wrapper.FileBrowserWindows();
#endif
         }
         else if (Util.Helper.isLinuxPlatform)
         {
#if UNITY_STANDALONE_LINUX
            PlatformWrapper = new Wrapper.FileBrowserLinux();
#endif
         }
         else if (Util.Helper.isWSAPlatform)
         {
#if UNITY_WSA && !UNITY_EDITOR && ENABLE_WINMD_SUPPORT
            PlatformWrapper = new Wrapper.FileBrowserWSA();
#endif
         }
         else
         {
            PlatformWrapper = new Wrapper.FileBrowserGeneric();
         }

         if (Util.Config.DEBUG)
            Debug.Log(PlatformWrapper);
      }

      #endregion
   }

   /// <summary>Native file browser various actions like open file, open folder and save file.</summary>
   [ExecuteInEditMode]
   [DisallowMultipleComponent]
   [HelpURL("https://www.crosstales.com/media/data/assets/FileBrowser/api/class_crosstales_1_1_f_b_1_1_file_browser.html")]
   public class FileBrowser : MonoBehaviour
   {
      #region Variables

      /// <summary>Don't destroy gameobject during scene switches (default: true).</summary>
      [Header("Behaviour Settings"), Tooltip("Don't destroy gameobject during scene switches (default: true).")]
      public bool DontDestroy = true;

      private static GameObject go;
      private static FileBrowser instance;
      private static bool loggedOnlyOneInstance = false;

      private static string lastOpenSingleFile;
      private static string[] lastOpenFiles;
      private static string lastOpenSingleFolder;
      private static string[] lastOpenFolders;
      private static string lastSaveFile;

      #endregion


      #region Properties

      /// <summary>Returns the singleton instance of this class.</summary>
      /// <returns>Singleton instance of this class.</returns>
      public static FileBrowser Instance
      {
         get
         {
            if (instance == null)
               instance = new GameObject("FileBrowser").AddComponent<FileBrowser>();

            return instance;
         }
      }

      /// <summary>Indicates if this wrapper can open multiple files.</summary>
      /// <returns>Wrapper can open multiple files.</returns>
      public static bool canOpenMultipleFiles
      {
         get { return WrapperHolder.PlatformWrapper.canOpenMultipleFiles; }
      }

      /// <summary>Indicates if this wrapper can open multiple folders.</summary>
      /// <returns>Wrapper can open multiple folders.</returns>
      public static bool canOpenMultipleFolders
      {
         get { return WrapperHolder.PlatformWrapper.canOpenMultipleFolders; }
      }

      /// <summary>Indicates if this wrapper is supporting the current platform.</summary>
      /// <returns>True if this wrapper supports current platform.</returns>
      public static bool isPlatformSupported
      {
         get { return WrapperHolder.PlatformWrapper.isPlatformSupported; }
      }

      /// <summary>Returns the file from the last "OpenSingleFile"-action.</summary>
      /// <returns>File from the last "OpenSingleFile"-action.</returns>
      public static string CurrentOpenSingleFile { get; private set; }

      /// <summary>Returns the array of files from the last "OpenFiles"-action.</summary>
      /// <returns>Array of files from the last "OpenFiles"-action.</returns>
      public static string[] CurrentOpenFiles { get; private set; }

      /// <summary>Returns the folder from the last "OpenSingleFolder"-action.</summary>
      /// <returns>Folder from the last "OpenSingleFolder"-action.</returns>
      public static string CurrentOpenSingleFolder { get; private set; }

      /// <summary>Returns the array of folders from the last "OpenFolders"-action.</summary>
      /// <returns>Array of folders from the last "OpenFolders"-action.</returns>
      public static string[] CurrentOpenFolders { get; private set; }

      /// <summary>Returns the file from the last "SaveFile"-action.</summary>
      /// <returns>File from the last "SaveFile"-action.</returns>
      public static string CurrentSaveFile { get; private set; }

      #endregion


      #region Events

      [Header("Events")] public OnOpenFilesCompleted OnOpenFilesCompleted;
      public OnOpenFoldersCompleted OnOpenFoldersCompleted;
      public OnSaveFileCompleted OnSaveFileCompleted;

      public delegate void OpenFilesStart();

      public delegate void OpenFilesComplete(bool selected, string singleFile, string[] files);

      public delegate void OpenFoldersStart();

      public delegate void OpenFoldersComplete(bool selected, string singleFolder, string[] folders);

      public delegate void SaveFileStart();

      public delegate void SaveFileComplete(bool selected, string file);

      private static OpenFilesStart _onOpenFilesStart;
      private static OpenFilesComplete _onOpenFilesComplete;

      private static OpenFoldersStart _onOpenFoldersStart;
      private static OpenFoldersComplete _onOpenFoldersComplete;

      private static SaveFileStart _onSaveFileStart;
      private static SaveFileComplete _onSaveFileComplete;

      /// <summary>An event triggered whenever "OpenFiles" is started.
      public static event OpenFilesStart OnOpenFilesStart
      {
         add { _onOpenFilesStart += value; }
         remove { _onOpenFilesStart -= value; }
      }

      /// <summary>An event triggered whenever "OpenFiles" is completed.
      public static event OpenFilesComplete OnOpenFilesComplete
      {
         add { _onOpenFilesComplete += value; }
         remove { _onOpenFilesComplete -= value; }
      }

      /// <summary>An event triggered whenever "OpenFolders" is started.
      public static event OpenFoldersStart OnOpenFoldersStart
      {
         add { _onOpenFoldersStart += value; }
         remove { _onOpenFoldersStart -= value; }
      }

      /// <summary>An event triggered whenever "OpenFolders" is completed.
      public static event OpenFoldersComplete OnOpenFoldersComplete
      {
         add { _onOpenFoldersComplete += value; }
         remove { _onOpenFoldersComplete -= value; }
      }

      /// <summary>An event triggered whenever "SaveFile" is started.
      public static event SaveFileStart OnSaveFileStart
      {
         add { _onSaveFileStart += value; }
         remove { _onSaveFileStart -= value; }
      }

      /// <summary>An event triggered whenever "SaveFile" is completed.
      public static event SaveFileComplete OnSaveFileComplete
      {
         add { _onSaveFileComplete += value; }
         remove { _onSaveFileComplete -= value; }
      }

      #endregion


      #region MonoBehaviour methods

      public void OnEnable()
      {
         if (instance == null)
         {
            instance = this;

            go = gameObject;

            go.name = Util.Constants.FB_SCENE_OBJECT_NAME;

            if (!Util.Helper.isEditorMode && DontDestroy)
               DontDestroyOnLoad(transform.root.gameObject);


            if (Util.Config.DEBUG)
               Debug.LogWarning("Using new instance!", this);
         }
         else
         {
            if (!Util.Helper.isEditorMode && DontDestroy && instance != this)
            {
               if (!loggedOnlyOneInstance)
               {
                  Debug.LogWarning("Only one active instance of '" + Util.Constants.FB_SCENE_OBJECT_NAME + "' allowed in all scenes!" + System.Environment.NewLine + "This object will now be destroyed.", this);
                  loggedOnlyOneInstance = true;
               }

               Destroy(gameObject, 0.2f);
            }

            if (Util.Config.DEBUG)
               Debug.LogWarning("Using old instance!", this);
         }
      }

      public void Update()
      {
         if (lastOpenSingleFile != CurrentOpenSingleFile)
         {
            lastOpenSingleFile = CurrentOpenSingleFile;
            bool selected = !string.IsNullOrEmpty(lastOpenSingleFile);

            onOpenFilesComplete(selected, lastOpenSingleFile, new[] {lastOpenSingleFile});
         }

         if (lastOpenFiles != CurrentOpenFiles)
         {
            lastOpenFiles = CurrentOpenFiles;
            bool selected = false;
            string singleFile = null;

            if (lastOpenFiles != null && lastOpenFiles.Length > 0)
            {
               selected = true;
               singleFile = lastOpenFiles[0];
            }

            onOpenFilesComplete(selected, singleFile, lastOpenFiles);
         }

         if (lastOpenSingleFolder != CurrentOpenSingleFolder)
         {
            lastOpenSingleFolder = CurrentOpenSingleFolder;
            bool selected = !string.IsNullOrEmpty(lastOpenSingleFolder);

            onOpenFoldersComplete(selected, lastOpenSingleFolder, new[] {lastOpenSingleFolder});
         }

         if (lastOpenFolders != CurrentOpenFolders)
         {
            lastOpenFolders = CurrentOpenFolders;
            bool selected = false;
            string singleFolder = null;


            if (lastOpenFolders != null && lastOpenFolders.Length > 0)
            {
               selected = !string.IsNullOrEmpty(lastOpenFolders[0]);
               singleFolder = lastOpenFolders[0];
            }

            onOpenFoldersComplete(selected, singleFolder, lastOpenFolders);
         }

         if (lastSaveFile != CurrentSaveFile)
         {
            lastSaveFile = CurrentSaveFile;
            bool selected = !string.IsNullOrEmpty(lastSaveFile);

            onSaveFileComplete(selected, lastSaveFile);
         }

         if (Util.Helper.isEditorMode)
         {
            if (go != null)
            {
               if (Util.Config.ENSURE_NAME)
                  go.name = Util.Constants.FB_SCENE_OBJECT_NAME; //ensure name
            }
         }
      }

      #endregion


      #region Public methods

      /// <summary>Open native file browser for a single file.</summary>
      /// <param name="extension">Allowed extension, e.g. "png" (optional)</param>
      /// <returns>Returns a string of the chosen file. Empty string when cancelled</returns>
      public static string OpenSingleFile(string extension = "*")
      {
         return OpenSingleFile(Util.Constants.TEXT_OPEN_FILE, string.Empty, getFilter(extension));
      }

      /// <summary>Open native file browser for a single file.</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="extensions">Allowed extensions, e.g. "png" (optional)</param>
      /// <returns>Returns a string of the chosen file. Empty string when cancelled</returns>
      public static string OpenSingleFile(string title, string directory, params string[] extensions)
      {
         return OpenSingleFile(title, directory, getFilter(extensions));
      }

      /// <summary>Open native file browser for a single file.</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="extensions">List of extension filters (optional)</param>
      /// <returns>Returns a string of the chosen file. Empty string when cancelled</returns>
      public static string OpenSingleFile(string title, string directory, params ExtensionFilter[] extensions)
      {
         onOpenFilesStart();

         CurrentOpenSingleFile = WrapperHolder.PlatformWrapper.OpenSingleFile(title, directory, extensions);
         lastOpenSingleFile = null;

         return CurrentOpenSingleFile;
      }

      /// <summary>Open native file browser for multiple files.</summary>
      /// <param name="extension">Allowed extension, e.g. "png" (optional)</param>
      /// <returns>Returns a string of the chosen file. Empty string when cancelled</returns>
      public static string[] OpenFiles(string extension = "*")
      {
         return OpenFiles(Util.Constants.TEXT_OPEN_FILES, string.Empty, getFilter(extension));
      }

      /// <summary>Open native file browser for multiple files.</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="extensions">Allowed extensions, e.g. "png" (optional)</param>
      /// <returns>Returns array of chosen files. Zero length array when cancelled</returns>
      public static string[] OpenFiles(string title, string directory, params string[] extensions)
      {
         return OpenFiles(title, directory, getFilter(extensions));
      }

      /// <summary>Open native file browser for multiple files.</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="extensions">List of extension filters (optional)</param>
      /// <returns>Returns array of chosen files. Zero length array when cancelled</returns>
      public static string[] OpenFiles(string title, string directory, params ExtensionFilter[] extensions)
      {
         onOpenFilesStart();

         CurrentOpenFiles = WrapperHolder.PlatformWrapper.OpenFiles(title, directory, extensions, true);
         lastOpenFiles = null;

         return CurrentOpenFiles;
      }

      /// <summary>Open native folder browser for a single folder.</summary>
      /// <returns>Returns a string of the chosen folder. Empty string when cancelled</returns>
      public static string OpenSingleFolder()
      {
         return OpenSingleFolder(Util.Constants.TEXT_OPEN_FOLDER);
      }

      /// <summary>
      /// Open native folder browser for a single folder.
      /// NOTE: Title is not supported under Windows and UWP (WSA)!
      /// </summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory (default: current, optional)</param>
      /// <returns>Returns a string of the chosen folder. Empty string when cancelled</returns>
      public static string OpenSingleFolder(string title, string directory = "")
      {
         onOpenFoldersStart();

         CurrentOpenSingleFolder = WrapperHolder.PlatformWrapper.OpenSingleFolder(title, directory);
         lastOpenSingleFolder = null;

         return CurrentOpenSingleFolder;
      }

      /// <summary>
      /// Open native folder browser for multiple folders.
      /// NOTE: Title and multiple folder selection are not supported under Windows and UWP (WSA)!
      /// </summary>
      /// <returns>Returns array of chosen folders. Zero length array when cancelled</returns>
      public static string[] OpenFolders()
      {
         return OpenFolders(Util.Constants.TEXT_OPEN_FOLDERS);
      }

      /// <summary>
      /// Open native folder browser for multiple folders.
      /// NOTE: Title and multiple folder selection are not supported under Windows and UWP (WSA)!
      /// </summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory (default: current, optional)</param>
      /// <returns>Returns array of chosen folders. Zero length array when cancelled</returns>
      public static string[] OpenFolders(string title, string directory = "")
      {
         onOpenFoldersStart();

         CurrentOpenFolders = WrapperHolder.PlatformWrapper.OpenFolders(title, directory, true);
         lastOpenFolders = null;

         return CurrentOpenFolders;
      }

      /// <summary>Open native save file browser.</summary>
      /// <param name="defaultName">Default file name (optional)</param>
      /// <param name="extension">File extensions, e.g. "png" (optional)</param>
      /// <returns>Returns chosen file. Empty string when cancelled</returns>
      public static string SaveFile(string defaultName = "", string extension = "*")
      {
         return SaveFile(Util.Constants.TEXT_SAVE_FILE, string.Empty, defaultName, getFilter(extension));
      }

      /// <summary>Open native save file browser.</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="defaultName">Default file name</param>
      /// <param name="extensions">File extensions, e.g. "png" (optional)</param>
      /// <returns>Returns chosen file. Empty string when cancelled</returns>
      public static string SaveFile(string title, string directory, string defaultName, params string[] extensions)
      {
         return SaveFile(title, directory, defaultName, getFilter(extensions));
      }

      /// <summary>Open native save file browser</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="defaultName">Default file name</param>
      /// <param name="extensions">List of extension filters (optional)</param>
      /// <returns>Returns chosen file. Empty string when cancelled</returns>
      public static string SaveFile(string title, string directory, string defaultName, params ExtensionFilter[] extensions)
      {
         onSaveFileStart();

         CurrentSaveFile = WrapperHolder.PlatformWrapper.SaveFile(title, directory, string.IsNullOrEmpty(defaultName) ? Util.Constants.TEXT_SAVE_FILE_NAME : defaultName, extensions);
         lastSaveFile = null;

         return CurrentSaveFile;
      }

      /// <summary>Asynchronously opens native file browser for a single file.</summary>
      /// <param name="extension">Allowed extension, e.g. "png" (optional)</param>
      /// <returns>Returns a string of the chosen file. Empty string when cancelled</returns>
      public static void OpenSingleFileAsync(string extension = "*")
      {
         OpenSingleFileAsync(Util.Constants.TEXT_OPEN_FILE, string.Empty, getFilter(extension));
      }

      /// <summary>Asynchronously opens native file browser for a single file.</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="extensions">Allowed extensions, e.g. "png" (optional)</param>
      /// <returns>Returns a string of the chosen file. Empty string when cancelled</returns>
      public static void OpenSingleFileAsync(string title, string directory, params string[] extensions)
      {
         OpenSingleFileAsync(title, directory, getFilter(extensions));
      }

      /// <summary>Asynchronously opens native file browser for a single file.</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="extensions">List of extension filters (optional)</param>
      /// <returns>Returns a string of the chosen file. Empty string when cancelled</returns>
      public static void OpenSingleFileAsync(string title, string directory, params ExtensionFilter[] extensions)
      {
         onOpenFilesStart();

         WrapperHolder.PlatformWrapper.OpenFilesAsync(title, directory, extensions, false, paths => setOpenFiles(paths));
      }

      /// <summary>Asynchronously opens native file browser for multiple files.</summary>
      /// <param name="multiselect">Allow multiple file selection (default: true, optional)</param>
      /// <param name="extensions">Allowed extensions, e.g. "png" (optional)</param>
      /// <returns>Returns array of chosen files. Zero length array when cancelled</returns>
      public static void OpenFilesAsync(bool multiselect = true, params string[] extensions)
      {
         OpenFilesAsync(multiselect ? Util.Constants.TEXT_OPEN_FILES : Util.Constants.TEXT_OPEN_FILE, string.Empty, multiselect, getFilter(extensions));
      }

      /// <summary>Asynchronously opens native file browser for multiple files.</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="multiselect">Allow multiple file selection (default: true, optional)</param>
      /// <param name="extensions">Allowed extensions, e.g. "png" (optional)</param>
      /// <returns>Returns array of chosen files. Zero length array when cancelled</returns>
      public static void OpenFilesAsync(string title, string directory, bool multiselect = true, params string[] extensions)
      {
         OpenFilesAsync(title, directory, multiselect, getFilter(extensions));
      }

      /// <summary>Asynchronously opens native file browser for multiple files.</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="multiselect">Allow multiple file selection (default: true, optional)</param>
      /// <param name="extensions">List of extension filters (optional)</param>
      /// <returns>Returns array of chosen files. Zero length array when cancelled</returns>
      public static void OpenFilesAsync(string title, string directory, bool multiselect = true, params ExtensionFilter[] extensions)
      {
         onOpenFilesStart();

         WrapperHolder.PlatformWrapper.OpenFilesAsync(title, directory, extensions, multiselect, paths => setOpenFiles(paths));
      }

      /// <summary>Asynchronously opens native folder browser for a single folder.</summary>
      /// <returns>Returns a string of the chosen folder. Empty string when cancelled</returns>
      public static void OpenSingleFolderAsync()
      {
         OpenSingleFolderAsync(Util.Constants.TEXT_OPEN_FOLDER);
      }

      /// <summary>
      /// Asynchronously opens native folder browser for a single folder.
      /// NOTE: Title is not supported under Windows and UWP (WSA)!
      /// </summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory (default: current, optional)</param>
      /// <returns>Returns a string of the chosen folder. Empty string when cancelled</returns>
      public static void OpenSingleFolderAsync(string title, string directory = "")
      {
         onOpenFoldersStart();

         WrapperHolder.PlatformWrapper.OpenFoldersAsync(title, directory, false, paths => setOpenFolders(paths));
      }

      /// <summary>Asynchronously opens native folder browser for multiple folders.</summary>
      /// <param name="multiselect">Allow multiple folder selection (default: true, optional)</param>
      /// <returns>Returns array of chosen folders. Zero length array when cancelled</returns>
      public static void OpenFoldersAsync(bool multiselect = true)
      {
         OpenFoldersAsync(Util.Constants.TEXT_OPEN_FOLDERS, string.Empty, multiselect);
      }

      /// <summary>Asynchronously opens native folder browser for multiple folders.</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory (default: current, optional)</param>
      /// <param name="multiselect">Allow multiple folder selection (default: true, optional)</param>
      /// <returns>Returns array of chosen folders. Zero length array when cancelled</returns>
      public static void OpenFoldersAsync(string title, string directory = "", bool multiselect = true)
      {
         onOpenFoldersStart();

         WrapperHolder.PlatformWrapper.OpenFoldersAsync(title, directory, multiselect, paths => setOpenFolders(paths));
      }

      /// <summary>Asynchronously opens native save file browser.</summary>
      /// <param name="defaultName">Default file name (optional)</param>
      /// <param name="extension">File extension, e.g. "png" (optional)</param>
      /// <returns>Returns chosen file. Empty string when cancelled</returns>
      public static void SaveFileAsync(string defaultName = "", string extension = "*")
      {
         SaveFileAsync(Util.Constants.TEXT_SAVE_FILE, string.Empty, defaultName, getFilter(extension));
      }

      /// <summary>Asynchronously opens native save file browser.</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="defaultName">Default file name</param>
      /// <param name="extensions">File extensions, e.g. "png" (optional)</param>
      /// <returns>Returns chosen file. Empty string when cancelled</returns>
      public static void SaveFileAsync(string title, string directory, string defaultName, params string[] extensions)
      {
         SaveFileAsync(title, directory, defaultName, getFilter(extensions));
      }

      /// <summary>Asynchronously opens native save file browser (async)</summary>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="defaultName">Default file name</param>
      /// <param name="extensions">List of extension filters (optional)</param>
      /// <returns>Returns chosen file. Empty string when cancelled</returns>
      public static void SaveFileAsync(string title, string directory, string defaultName, params ExtensionFilter[] extensions)
      {
         onSaveFileStart();

         WrapperHolder.PlatformWrapper.SaveFileAsync(title, directory, string.IsNullOrEmpty(defaultName) ? Util.Constants.TEXT_SAVE_FILE_NAME : defaultName, extensions, paths => setSaveFile(paths));
      }

      /// <summary>Find files inside a path.</summary>
      /// <param name="path">Path to find the files</param>
      /// <param name="isRecursive">Recursive search (default: false, optional)</param>
      /// <param name="extensions">Extensions for the file search, e.g. "png" (optional)</param>
      /// <returns>Returns array of the found files inside the path (alphabetically ordered). Zero length array when an error occured.</returns>
      public static string[] GetFiles(string path, bool isRecursive = false, params string[] extensions)
      {
         return Util.Helper.GetFiles(path, isRecursive, extensions);
      }

      /// <summary>Find files inside a path.</summary>
      /// <param name="path">Path to find the files</param>
      /// <param name="isRecursive">Recursive search</param>
      /// <param name="extensions">List of extension filters for the search (optional)</param>
      /// <returns>Returns array of the found files inside the path. Zero length array when an error occured.</returns>
      public static string[] GetFiles(string path, bool isRecursive, params ExtensionFilter[] extensions)
      {
         return GetFiles(path, isRecursive, extensions.SelectMany(extensionFilter => extensionFilter.Extensions).ToArray());
      }

      /// <summary>Find directories inside.</summary>
      /// <param name="path">Path to find the directories</param>
      /// <param name="isRecursive">Recursive search (default: false, optional)</param>
      /// <returns>Returns array of the found directories inside the path. Zero length array when an error occured.</returns>
      public static string[] GetDirectories(string path, bool isRecursive = false)
      {
         return Util.Helper.GetDirectories(path, isRecursive);
      }
/*
      /// <summary>
      /// Find all logical drives.
      /// </summary>
      /// <returns>Returns array of the found drives. Zero length array when an error occured.</returns>
      public static string[] GetDrives()
      {
         return Util.Helper.GetDrives();
      }
*/
      /// <summary>
      /// Find all logical drives.
      /// </summary>
      /// <returns>Returns array of the found drives. Zero length array when an error occured.</returns>
      public static string[] GetDrives() //TODO replace with "Util.Helper.GetDrives" in the next version
      {
         if (Util.Helper.isWebPlatform && !Util.Helper.isEditor)
         {
            Debug.LogWarning("'GetDrives' is not supported for the current platform!");
         }
         else if (Util.Helper.isWSABasedPlatform && !Util.Helper.isEditor)
         {
#if UNITY_WSA && !UNITY_EDITOR && ENABLE_WINMD_SUPPORT
            Crosstales.FB.FileBrowserWSAImpl fbWsa = new Crosstales.FB.FileBrowserWSAImpl();
            fbWsa.isBusy = true;
            UnityEngine.WSA.Application.InvokeOnUIThread(() => { fbWsa.GetDrives(); }, false);

            do
            {
              //wait
            } while (fbWsa.isBusy);

            return fbWsa.Selection.ToArray();
#endif
         }
         else
         {
#if !UNITY_WSA || UNITY_EDITOR
            try
            {
               return System.IO.Directory.GetLogicalDrives();
            }
            catch (System.Exception ex)
            {
               Debug.LogWarning("Could not scan the path for directories: " + ex);
            }
#endif
         }

         return new string[0];
      }

      #region Legacy

      /// <summary>Open native file browser for multiple files.</summary>
      /// <param name="cb">Callback for the async operation.</param>
      /// <param name="multiselect">Allow multiple file selection (default: true, optional)</param>
      /// <param name="extensions">Allowed extensions, e.g. "png" (optional)</param>
      /// <returns>Returns array of chosen files. Zero length array when cancelled</returns>
      [System.Obsolete("This method is deprecated, please use it without the callback.")]
      public static void OpenFilesAsync(System.Action<string[]> cb, bool multiselect = true, params string[] extensions)
      {
         OpenFilesAsync(cb, multiselect ? Util.Constants.TEXT_OPEN_FILES : Util.Constants.TEXT_OPEN_FILE, string.Empty, multiselect, getFilter(extensions));
      }

      /// <summary>Open native file browser for multiple files.</summary>
      /// <param name="cb">Callback for the async operation.</param>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="multiselect">Allow multiple file selection (default: true, optional)</param>
      /// <param name="extensions">Allowed extensions, e.g. "png" (optional)</param>
      /// <returns>Returns array of chosen files. Zero length array when cancelled</returns>
      [System.Obsolete("This method is deprecated, please use it without the callback.")]
      public static void OpenFilesAsync(System.Action<string[]> cb, string title, string directory, bool multiselect = true, params string[] extensions)
      {
         OpenFilesAsync(cb, title, directory, multiselect, getFilter(extensions));
      }

      /// <summary>Open native file browser for multiple files (async).</summary>
      /// <param name="cb">Callback for the async operation.</param>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="multiselect">Allow multiple file selection (default: true, optional)</param>
      /// <param name="extensions">List of extension filters (optional)</param>
      /// <returns>Returns array of chosen files. Zero length array when cancelled</returns>
      [System.Obsolete("This method is deprecated, please use it without the callback.")]
      public static void OpenFilesAsync(System.Action<string[]> cb, string title, string directory, bool multiselect = true, params ExtensionFilter[] extensions)
      {
         WrapperHolder.PlatformWrapper.OpenFilesAsync(title, directory, extensions, multiselect, cb);
      }

      /// <summary>Open native folder browser for multiple folders (async).</summary>
      /// <param name="cb">Callback for the async operation.</param>
      /// <param name="multiselect">Allow multiple folder selection (default: true, optional)</param>
      /// <returns>Returns array of chosen folders. Zero length array when cancelled</returns>
      [System.Obsolete("This method is deprecated, please use it without the callback.")]
      public static void OpenFoldersAsync(System.Action<string[]> cb, bool multiselect = true)
      {
         OpenFoldersAsync(cb, Util.Constants.TEXT_OPEN_FOLDERS, string.Empty, multiselect);
      }

      /// <summary>Open native folder browser for multiple folders (async).</summary>
      /// <param name="cb">Callback for the async operation.</param>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory (default: current, optional)</param>
      /// <param name="multiselect">Allow multiple folder selection (default: true, optional)</param>
      /// <returns>Returns array of chosen folders. Zero length array when cancelled</returns>
      [System.Obsolete("This method is deprecated, please use it without the callback.")]
      public static void OpenFoldersAsync(System.Action<string[]> cb, string title, string directory = "", bool multiselect = true)
      {
         WrapperHolder.PlatformWrapper.OpenFoldersAsync(title, directory, multiselect, cb);
      }

      /// <summary>Open native save file browser</summary>
      /// <param name="cb">Callback for the async operation.</param>
      /// <param name="defaultName">Default file name (optional)</param>
      /// <param name="extension">File extension, e.g. "png" (optional)</param>
      /// <returns>Returns chosen file. Empty string when cancelled</returns>
      [System.Obsolete("This method is deprecated, please use it without the callback.")]
      public static void SaveFileAsync(System.Action<string> cb, string defaultName = "", string extension = "*")
      {
         SaveFileAsync(cb, Util.Constants.TEXT_SAVE_FILE, string.Empty, defaultName, getFilter(extension));
      }

      /// <summary>Open native save file browser</summary>
      /// <param name="cb">Callback for the async operation.</param>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="defaultName">Default file name</param>
      /// <param name="extensions">File extensions, e.g. "png" (optional)</param>
      /// <returns>Returns chosen file. Empty string when cancelled</returns>
      [System.Obsolete("This method is deprecated, please use it without the callback.")]
      public static void SaveFileAsync(System.Action<string> cb, string title, string directory, string defaultName, params string[] extensions)
      {
         SaveFileAsync(cb, title, directory, defaultName, getFilter(extensions));
      }

      /// <summary>Open native save file browser (async).</summary>
      /// <param name="cb">Callback for the async operation.</param>
      /// <param name="title">Dialog title</param>
      /// <param name="directory">Root directory</param>
      /// <param name="defaultName">Default file name</param>
      /// <param name="extensions">List of extension filters (optional)</param>
      /// <returns>Returns chosen file. Empty string when cancelled</returns>
      [System.Obsolete("This method is deprecated, please use it without the callback.")]
      public static void SaveFileAsync(System.Action<string> cb, string title, string directory, string defaultName, params ExtensionFilter[] extensions)
      {
         WrapperHolder.PlatformWrapper.SaveFileAsync(title, directory, string.IsNullOrEmpty(defaultName) ? Util.Constants.TEXT_SAVE_FILE_NAME : defaultName, extensions, cb);
      }

      #endregion

      #endregion


      #region Private methods

      private static void setOpenFiles(params string[] paths)
      {
         CurrentOpenFiles = paths;
         lastOpenFiles = null;
      }

      private static void setOpenFolders(params string[] paths)
      {
         CurrentOpenFolders = paths;
         lastOpenFolders = null;
      }

      private static void setSaveFile(params string[] paths)
      {
         if (paths != null && paths.Length > 0)
         {
            CurrentSaveFile = paths[0];
            lastSaveFile = null;
         }
      }

      private static ExtensionFilter[] getFilter(params string[] extensions)
      {
         if (extensions != null && extensions.Length > 0)
         {
            //Debug.Log("Extension: " + extensions[0]);

            if (extensions.Length == 1 && "*".Equals(extensions[0]))
            {
               //Debug.Log("Wildcard!");
               return null;
            }

            ExtensionFilter[] filter = new ExtensionFilter[extensions.Length];

            for (int ii = 0; ii < extensions.Length; ii++)
            {
               var extension = string.IsNullOrEmpty(extensions[ii]) ? "*" : extensions[ii];

               if (extension.Equals("*"))
               {
                  filter[ii] = new ExtensionFilter(Util.Constants.TEXT_ALL_FILES, Util.Helper.isMacOSEditor ? string.Empty : extension);
               }
               else
               {
                  filter[ii] = new ExtensionFilter(extension, extension);
               }
            }

            if (Util.Config.DEBUG)
               Debug.Log("getFilter: " + filter.CTDump());

            return filter;
         }

         //Debug.Log("Wildcard!");
         return null;
      }

      private void makeSureInstanceExists()
      {
         //do nothing
      }

      #endregion


      #region Event-trigger methods

      private static void onOpenFilesStart()
      {
         if (Util.Config.DEBUG)
            Debug.Log("onOpenFilesStart");

         Instance.makeSureInstanceExists();

         if (_onOpenFilesStart != null)
            _onOpenFilesStart();
      }

      private static void onOpenFilesComplete(bool selected, string singleFile, string[] files)
      {
         if (Util.Config.DEBUG)
            Debug.Log("onOpenFilesComplete: " + selected);

         if (!Util.Helper.isEditorMode && Instance.OnOpenFilesCompleted != null)
         {
            string fileList = files != null && files.Length > 0 ? string.Join(";", files) : null;

            Instance.OnOpenFilesCompleted.Invoke(selected, singleFile, fileList);
         }

         if (_onOpenFilesComplete != null)
            _onOpenFilesComplete(selected, singleFile, files);
      }

      private static void onOpenFoldersStart()
      {
         if (Util.Config.DEBUG)
            Debug.Log("onOpenFoldersStart");

         if (_onOpenFoldersStart != null)
            _onOpenFoldersStart();
      }

      private static void onOpenFoldersComplete(bool selected, string singleFolder, string[] folders)
      {
         if (Util.Config.DEBUG)
            Debug.Log("onOpenFoldersComplete: " + selected);

         if (!Util.Helper.isEditorMode && Instance.OnOpenFoldersCompleted != null)
         {
            string folderList = folders != null && folders.Length > 0 ? string.Join(";", folders) : null;

            Instance.OnOpenFoldersCompleted.Invoke(selected, singleFolder, folderList);
         }

         if (_onOpenFoldersComplete != null)
            _onOpenFoldersComplete(selected, singleFolder, folders);
      }

      private static void onSaveFileStart()
      {
         if (Util.Config.DEBUG)
            Debug.Log("onSaveFileStart");

         if (_onSaveFileStart != null)
            _onSaveFileStart();
      }

      private static void onSaveFileComplete(bool selected, string file)
      {
         if (Util.Config.DEBUG)
            Debug.Log("onSaveFileComplete: " + selected + " - " + file);

         if (!Util.Helper.isEditorMode && Instance.OnSaveFileCompleted != null)
            Instance.OnSaveFileCompleted.Invoke(selected, file);

         if (_onSaveFileComplete != null)
            _onSaveFileComplete(selected, file);
      }

      #endregion
   }

   /// <summary>Filter for extensions.</summary>
   public struct ExtensionFilter
   {
      public string Name;
      public string[] Extensions;

      public ExtensionFilter(string filterName, params string[] filterExtensions)
      {
         Name = filterName;
         Extensions = filterExtensions;
      }

      public override string ToString()
      {
         System.Text.StringBuilder result = new System.Text.StringBuilder();

         result.Append(GetType().Name);
         result.Append(Util.Constants.TEXT_TOSTRING_START);

         result.Append("Name='");
         result.Append(Name);
         result.Append(Util.Constants.TEXT_TOSTRING_DELIMITER);

         result.Append("Extensions='");
         result.Append(Extensions.CTDump());
         result.Append(Util.Constants.TEXT_TOSTRING_DELIMITER_END);

         result.Append(Util.Constants.TEXT_TOSTRING_END);

         return result.ToString();
      }
   }
}
// © 2017-2020 crosstales LLC (https://www.crosstales.com)