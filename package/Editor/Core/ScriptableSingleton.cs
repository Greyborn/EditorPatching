﻿#if !UNITY_2020_1_OR_NEWER

using System.IO;
using System.Linq;

using UnityEditorInternal;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace needle.EditorPatching
{
    public class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        private static T s_Instance;

        public static T instance
        {
            get
            {
                if (!s_Instance) CreateAndLoad();
                return s_Instance;
            }
        }

        protected ScriptableSingleton()
        {
            if (s_Instance)
                Debug.LogError("ScriptableSingleton already exists. Did you query the singleton in a constructor?");
            else
            {
                object casted = this;
                s_Instance = casted as T;
                System.Diagnostics.Debug.Assert(s_Instance != null);
            }
        }

        private static void CreateAndLoad()
        {
            System.Diagnostics.Debug.Assert(s_Instance == null);

            // Load
            var filePath = GetFilePath();
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(filePath))
                InternalEditorUtility.LoadSerializedFileAndForget(filePath);
#endif
            if (s_Instance == null)
            {
                var t = CreateInstance<T>();
                t.hideFlags = HideFlags.HideAndDontSave;
            }

            System.Diagnostics.Debug.Assert(s_Instance != null);
        }

        protected virtual void Save(bool saveAsText)
        {
            if (!s_Instance)
            {
                Debug.Log("Cannot save ScriptableSingleton: no instance!");
                return;
            }

            var filePath = GetFilePath();
            if (string.IsNullOrEmpty(filePath)) return;
            var folderPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(folderPath) && !string.IsNullOrEmpty(folderPath))
                Directory.CreateDirectory(folderPath);
#if UNITY_EDITOR
            InternalEditorUtility.SaveToSerializedFileAndForget(new[] {s_Instance}, filePath, saveAsText);
#endif
        }

        private static string GetFilePath()
        {
            var type = typeof(T);
            var attributes = type.GetCustomAttributes(true);
            return attributes.OfType<FilePathAttribute>()
                .Select(f => f.filepath)
                .FirstOrDefault();
        }
    }
}
#endif
