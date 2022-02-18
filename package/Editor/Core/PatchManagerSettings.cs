using System.Collections.Generic;

using UnityEngine;

namespace needle.EditorPatching
{
	[FilePath("ProjectSettings/HarmonyPatchSettings.asset", FilePathAttribute.Location.ProjectFolder)]
	internal class PatchManagerSettings : ScriptableSingleton<PatchManagerSettings>
	{
		public bool DebugLog;

		[SerializeField] private List<string> enabledPatchIds = new List<string>();
		[SerializeField] private List<string> disabledPatchIds = new List<string>();

		internal static bool HasPersistentSetting(string id) =>
			instance.enabledPatchIds.Contains(id) || instance.disabledPatchIds.Contains(id);

		internal static bool PersistentActive(EditorPatchProvider prov)
		{
			if (prov == null) return false;
			var id = prov.ID();
			return instance.enabledPatchIds.Contains(id) ||
			       (prov.ActiveByDefault && !instance.disabledPatchIds.Contains(id));
		}

		internal static bool PersistentActive(string id)
		{
			// foreach (var entry in instance.enabledPatchIds) Debug.Log("current active " + entry);
			return instance.enabledPatchIds.Contains(id);
		}

		internal static bool PersistentInactive(string id)
		{
			return instance.disabledPatchIds.Contains(id);
		}

		internal static void Clear(bool save)
		{
			instance.enabledPatchIds.Clear();
			instance.disabledPatchIds.Clear();
			if (save) instance.Save();
		}

		internal static void SetPersistentActive(string id, bool active)
		{
			// Debug.Log(id + " -> " + active);
			if (active)
			{
				if (instance.disabledPatchIds.Contains(id)) instance.disabledPatchIds.Remove(id);
				if (!instance.enabledPatchIds.Contains(id)) instance.enabledPatchIds.Add(id);
			}
			else
			{
				if (instance.enabledPatchIds.Contains(id)) instance.enabledPatchIds.Remove(id);
				if (!instance.disabledPatchIds.Contains(id)) instance.disabledPatchIds.Add(id);
			}

			instance.Save();
		}

		public void Save() => Save(true);
	}
}
