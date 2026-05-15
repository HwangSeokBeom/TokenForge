using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public abstract class UiBinderBase : MonoBehaviour
    {
        private Action requestRender;

        public virtual void Bind(ApprovedActivityAnalysisViewModel viewModel, Action requestRender)
        {
            this.requestRender = requestRender;
        }

        public bool ValidateRequiredReferences(out string error)
        {
            error = string.Empty;
            return ValidateRequiredReferencesInternal(ref error);
        }

        protected abstract bool ValidateRequiredReferencesInternal(ref string error);

        protected static bool Require(UnityEngine.Object value, string fieldName, ref string error)
        {
            if (value != null)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(error))
            {
                error = fieldName + " is not assigned.";
            }

            return false;
        }

        protected void RenderAll()
        {
            requestRender?.Invoke();
        }

        protected async void RunViewModelAction(Func<Task> action)
        {
            if (action == null)
            {
                return;
            }

            Task task;
            try
            {
                task = action();
            }
            catch (Exception)
            {
                RenderAll();
                return;
            }

            RenderAll();

            try
            {
                await task;
            }
            finally
            {
                RenderAll();
            }
        }

        protected static void SetText(Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        protected static void SetButton(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        protected static void ReplaceClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (action != null)
            {
                button.onClick.AddListener(action);
            }
        }
    }
}
