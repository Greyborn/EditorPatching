using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace needle.EditorPatching
{
    public abstract class ManagedPatchBase : IManagedPatch
    {
        public string Id { get; protected set; }
        public bool IsActive { get; protected set; }
        public string Name { get; protected set; }
        public string Description { get; protected set; }
        
        private MethodBase canEnableCallback;

        protected abstract bool OnEnablePatch();
        protected abstract bool OnDisablePatch();
        
        protected void OnCreated()
        {            
            if (PatchManagerSettings.PersistentActive(this.Id) && !PatchManager.IsActive(this.Id))
            {
                EnablePatch();
            }
        }

        protected void ApplyMeta(PatchMeta meta)
        {
            if (meta == null) return;
            Description = meta.Description;
            if (meta.CanEnableCallbackType != null && !string.IsNullOrEmpty(meta.CanEnableCallbackMethod))
            {
                canEnableCallback = meta.CanEnableCallbackType?.GetMethod(meta.CanEnableCallbackMethod, BindingFlags.Static | BindingFlags.NonPublic);
                // Debug.Log(canEnableCallback + ", " + meta.CanEnableCallbackMethod);
            }
        }

        public void EnablePatch()
        {
            if (IsActive) return;
            HandleActivationRequest();
        }

        public void DisablePatch()
        {
            requestedActivation = false;
            if (!IsActive) return;
            if (OnDisablePatch())
            {
                requestedActivation = false;
                IsActive = false;
                PatchManagerSettings.SetPersistentActive(this.Id, false);
                InternalEditorUtility.RepaintAllViews();  
            }
        }

        private bool requestedActivation; 

        private async void HandleActivationRequest() 
        {
            if (requestedActivation) return;
            requestedActivation = true;
            while (requestedActivation && EditorApplication.isCompiling || EditorApplication.isUpdating) await Task.Delay(1);
            while(canEnableCallback != null && !(bool)canEnableCallback.Invoke(null, null)) await Task.Delay(1);
            // while (!EditorApplication.isPlaying && requestedActivation && !Utils.GUISkinHasLoaded()) await Task.Delay(1);
            if (!requestedActivation || IsActive) return;
            if (PatchManager.IsActive(this.Id)) return; 
            requestedActivation = false;
            IsActive = true;
            // Debug.Log("ENABLE " + Id);
            if (OnEnablePatch())
            {
                PatchManagerSettings.SetPersistentActive(this.Id, true);
                InternalEditorUtility.RepaintAllViews();
            }
            // try
            // {
            // }
            // catch (AmbiguousMatchException e)
            // {
            //     Debug.LogError("Can not enable patch " + Id + " because of " + nameof(AmbiguousMatchException));
            //     Debug.LogException(e);
            // }
        }

        public static readonly string ManagedPatchPostfix = "_" + typeof(ManagedPatchIndependent).FullName;
    }
}