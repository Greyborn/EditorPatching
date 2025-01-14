using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;

using Greyborn.Library.Editor;

using HarmonyLib;

using UnityEditor;

using UnityEditorInternal;

using UnityEngine;

using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace needle.EditorPatching
{
   public class PatchManagerSettingsProvider : SettingsProvider
   {
      private readonly IDictionary<string, IList<IManagedPatch>> sortedPatches =
         new Dictionary<string, IList<IManagedPatch>>();

      private Vector2 scrollPosition;

      public PatchManagerSettingsProvider(
         string path,
         SettingsScope scopes,
         IEnumerable<string> keywords = null)
         : base(path, scopes, keywords)
      {
      }

      [SettingsProvider]
      public static SettingsProvider CreatePatchManagerSettingsProvider()
      {
         try
         {
            // PatchManagerSettings.instance.Save();
            return new PatchManagerSettingsProvider("Project/Harmony Editor Patching", SettingsScope.Project);
         }
         catch (Exception e)
         {
            Debug.Log(e);
         }

         return null;
      }

      public override void OnGUI(string searchContext)
      {
         DrawHarmonyMetadata();
         DrawHorizontalSplitLine();

         using (var changeScope = new EditorGUI.ChangeCheckScope())
         {
            using (var scrollScope = new EditorGUILayout.ScrollViewScope(this.scrollPosition))
            {
               this.scrollPosition = scrollScope.scrollPosition;

               var managedPatches = PatchManager.KnownPatches;
               if ((managedPatches != null) && (managedPatches.Count > 0))
               {
                  var sorted = this.GetSorted(managedPatches);
                  foreach (var kvp in sorted)
                  {
                     // var stateKey = "HarmonyPatchGroup_" + kvp.Key;
                     // var state = EditorGUILayout.Foldout(SessionState.GetBool(stateKey, false), kvp.Key);
                     // SessionState.SetBool(stateKey, state);
                     // if (!state)
                     // {
                     //    continue;
                     // }

                     EditorGUILayout.Space();

                     var rect = GUILayoutUtility.GetRect(GUIContent.none, Style.GroupHeader);
                     EditorGUI.LabelField(rect, kvp.Key, Style.GroupHeader);
                     foreach (var patch in kvp.Value)
                     {
                        DrawPatchControls(patch);
                     }

                     EditorGUILayout.Space();
                  }
               }
            }

            DrawFooter();

            // foreach (var patch in managedPatches)
            // {
            //    DrawPatchUI(patch, skipInactive);
            // }

            if (changeScope.changed)
            {
               PatchManagerSettings.instance.Save();
            }
         }
      }

      private static void DrawFooter()
      {
         GUILayout.FlexibleSpace();

         using (new EditorGUILayout.HorizontalScope(Style.Footer))
         {
            GUILayout.FlexibleSpace();
            PatchManager.AllowDebugLogs = GUILayout.Toggle(PatchManager.AllowDebugLogs, "Debug Log");
            var logPath = PatchManager.HarmonyLogPath;
            if (File.Exists(logPath) && GUILayout.Button("Open Harmony Log", Style.FooterButton))
            {
               Process.Start(logPath);
            }

            // if(GUILayout.Button("Refresh Patch List", GUILayout.Width(180)))
            // {
            //     PatchesCollector.CollectPatches();
            // }
         }
      }

      private static void DrawHarmonyMetadata()
      {
         EditorGUILayout.Space();

         var version = typeof(Harmony).Assembly;
         GUILayout.Label(
            version.GetCustomAttribute<AssemblyDescriptionAttribute>().Description,
            Style.HarmonyDescription);

         GUILayout.Label(
            // ReSharper disable once UseStringInterpolation
            string.Format(
               "{0} \u2014 {1} \u2014 {2}",
               version.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion,
               version.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkDisplayName,
               version.GetCustomAttribute<AssemblyConfigurationAttribute>().Configuration),
            Style.HarmonyVersion);

         EditorGUILayout.Space();
      }

      private static void DrawHorizontalSplitLine()
      {
         var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));

         if (Event.current.type == EventType.Repaint)
         {
            GUI.Label(rect, GUIContent.none, Style.Footer);
         }
      }

      private static void DrawPatchControls(IManagedPatch patch)
      {
         EditorGUILayout.Space();

         var rect = GUILayoutUtility.GetRect(GUIContent.none, Style.GroupHeader);
         var toggleRect = new Rect(rect.x, rect.y, 32, 18);
         var oldValue = patch.IsActive;
         var newValue = Imgui.SlideToggle(toggleRect, oldValue);

         var title = PatchManager.IsWaitingForLoad(patch.Id) ? $"{patch.Name} [loading]" : patch.Name;

         rect.xMin = toggleRect.xMax + 4;
         EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
         if (GUI.Button(rect, title, oldValue ? Style.PatchActive : Style.PatchInactive))
         {
            PingPatchFileOrAssembly(patch);
         }

         using (new EditorGUI.DisabledScope(!oldValue))
         using (new EditorGUILayout.VerticalScope(Style.Metadata))
         {
            if (!string.IsNullOrEmpty(patch.Description))
            {
               GUILayout.Label(patch.Description, Style.PatchDescription);
               EditorGUILayout.Space();
            }

            GUI.Label(GUILayoutUtility.GetRect(GUIContent.none, Style.PatchId), patch.Id, Style.PatchId);

            GUI.Label(
               GUILayoutUtility.GetRect(GUIContent.none, Style.PatchId),
               patch.GetType().Name,
               Style.PatchType);
         }

         if (oldValue != newValue)
         {
            if (newValue)
            {
               patch.EnablePatch();
            }
            else
            {
               patch.DisablePatch();
            }

            ToggleSelection();
         }

         async void ToggleSelection()
         {
            var sel = Selection.activeObject;
            Selection.activeObject = null;
            if (!sel)
            {
               return;
            }

            await Task.Delay(10);
            InternalEditorUtility.RepaintAllViews();
            Selection.activeObject = sel;
         }
      }

      private static void LogFoundInAssembly(IManagedPatch patch, string searchString, Object context)
      {
         Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            context,
            $"The \"{patch.Name}\" patch was found in the \"{searchString}\" assembly.");
      }

      private static void LogFoundInPackage(IManagedPatch patch, PackageInfo packageInfo, Object context)
      {
         Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            context,
            $"The \"{patch.Name}\" patch was found in the \"{packageInfo.displayName}\" package.");
      }

      private static void LogNotFound(IManagedPatch patch)
      {
         Debug.LogFormat(
            LogType.Warning,
            LogOption.NoStacktrace,
            null,
            $"Unable to locate the \"{patch.Id}\" patch.\n    Group: {patch.Group}\n    Name: {patch.Name}\n    Description: {patch.Description}");
      }

      private static void PingPatchFileOrAssembly(IManagedPatch patch)
      {
         if (PatchManager.TryGetFilePathForPatch(patch.Id, out var path))
         {
            // The patch is in a standard script Asset.
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
            return;
         }

         var type = Type.GetType(patch.Id);
         if (type == null)
         {
            Debug.LogError($"Unable to get type from patch ID: \"{patch.Id}\"");
            return;
         }

         var searchString = type.Assembly.GetName().Name;
         var assets = AssetDatabase.FindAssets(searchString, new[] { "Packages" });
         if (assets.Length <= 0)
         {
            // Assembly wasn't found among Packages.
            LogNotFound(patch);
            return;
         }

         if (assets.Length > 1)
         {
            Debug.LogWarning($"Multiple package assets match \"{searchString}\"");
         }

         var assemblyPath = AssetDatabase.GUIDToAssetPath(assets[0]);
         var assemblyAsset = AssetDatabase.LoadAssetAtPath<Object>(assemblyPath);
         if (assemblyAsset != null)
         {
            EditorGUIUtility.PingObject(assemblyAsset);
            LogFoundInAssembly(patch, searchString, assemblyAsset);
            return;
         }

         // The assembly couldn't be pinged. Try the package.
         var packageInfo = PackageInfo.FindForAssetPath(assemblyPath);
         var packageAsset = AssetDatabase.LoadAssetAtPath<Object>(packageInfo.assetPath);
         if (packageAsset != null)
         {
            EditorGUIUtility.PingObject(packageAsset);
            LogFoundInPackage(patch, packageInfo, packageAsset);
            return;
         }

         LogNotFound(patch);
      }

      private IDictionary<string, IList<IManagedPatch>> GetSorted(IEnumerable<IManagedPatch> patches)
      {
         this.sortedPatches.Clear();
         foreach (var patch in patches.OrderBy(p => p.Group))
         {
            void AddToGroup(string group)
            {
               if (!this.sortedPatches.ContainsKey(group))
               {
                  this.sortedPatches.Add(
                     group,
                     new List<IManagedPatch>
                        {
                           patch,
                        });
               }
               else
               {
                  this.sortedPatches[group].Add(patch);
               }
            }

            AddToGroup(patch.Group ?? "NoGroup");
         }

         return this.sortedPatches;
      }

      private static class Style
      {
         public static readonly GUIStyle Footer = new GUIStyle("ProjectBrowserBottomBarBg")
            {
               padding = new RectOffset(3, 3, 4, 3),
            };

         public static readonly GUIStyle FooterButton = new GUIStyle(EditorStyles.miniButton)
            {
               margin = new RectOffset(7, 3, 0, 0),
            };

         public static readonly GUIStyle GroupHeader = new GUIStyle(EditorStyles.label)
            {
               fixedHeight = 18,
               fontStyle = FontStyle.Bold,
               margin = new RectOffset(13, 3, 2, 2), // 3,3,2,2
            };

         public static readonly GUIStyle HarmonyDescription = new GUIStyle(EditorStyles.label)
            {
               padding = new RectOffset(13, 3, 1, 1),
               wordWrap = true,
            };

         private static readonly Color c2 = EditorStyles.miniLabel.normal.textColor;

         public static readonly GUIStyle HarmonyVersion = new GUIStyle(EditorStyles.miniLabel)
            {
               padding = new RectOffset(13, 3, 1, 1),
               normal =
                  {
                     textColor = new Color(c2.r, c2.g, c2.b, 0.5f),
                  },
            };

         public static readonly GUIStyle Metadata = new GUIStyle(EditorStyles.helpBox)
            {
               margin = new RectOffset(49, 3, 2, 2), // 3,3,2,2
            };

         public static readonly GUIStyle PatchActive = new GUIStyle(EditorStyles.label)
            {
               fontStyle = FontStyle.Normal,
               padding = new RectOffset(1, 1, 0, 1), // 1,1,0,0
            };

         public static readonly GUIStyle PatchDescription = new GUIStyle(EditorStyles.label)
            {
               wordWrap = true,
            };

         public static readonly GUIStyle PatchId = new GUIStyle(EditorStyles.miniLabel)
            {
               alignment = TextAnchor.UpperLeft,
               stretchWidth = true,
            };

         public static readonly GUIStyle PatchInactive = new GUIStyle(PatchActive)
            {
               normal =
                  {
                     textColor = Color.gray,
                  },
               active =
                  {
                     textColor = Color.gray,
                  },
               hover =
                  {
                     textColor = Color.gray,
                  },
            };

         public static readonly GUIStyle PatchType = new GUIStyle(EditorStyles.miniLabel)
            {
               alignment = TextAnchor.UpperLeft,
               stretchWidth = true,
            };
      }
   }
}
