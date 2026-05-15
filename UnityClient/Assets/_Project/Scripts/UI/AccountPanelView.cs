using UnityEngine;
using UnityEngine.UI;

namespace TokenForge.Client.UI
{
    public sealed class AccountPanelView : UiBinderBase
    {
        [SerializeField] public Text statusLabel;
        [SerializeField] public InputField baseUrlInput;
        [SerializeField] public InputField emailInput;
        [SerializeField] public InputField displayNameInput;
        [SerializeField] public InputField passwordInput;
        [SerializeField] public Button loginButton;
        [SerializeField] public Button signupButton;
        [SerializeField] public Button logoutButton;
        [SerializeField] public Button refreshUserButton;

        private ApprovedActivityAnalysisViewModel viewModel;

        public override void Bind(ApprovedActivityAnalysisViewModel viewModel, System.Action requestRender)
        {
            base.Bind(viewModel, requestRender);
            this.viewModel = viewModel;

            if (passwordInput != null)
            {
                passwordInput.contentType = InputField.ContentType.Password;
                passwordInput.ForceLabelUpdate();
            }

            ReplaceClick(loginButton, () =>
            {
                if (this.viewModel == null)
                {
                    return;
                }

                this.viewModel.SetSafeSyncBaseUrl(baseUrlInput != null ? baseUrlInput.text : string.Empty);
                RunViewModelAction(async () =>
                {
                    var result = await this.viewModel.LoginAsync(emailInput != null ? emailInput.text : string.Empty, passwordInput != null ? passwordInput.text : string.Empty);
                    if (result.IsSuccess && passwordInput != null)
                    {
                        passwordInput.text = string.Empty;
                    }
                });
            });
            ReplaceClick(signupButton, () =>
            {
                if (this.viewModel == null)
                {
                    return;
                }

                this.viewModel.SetSafeSyncBaseUrl(baseUrlInput != null ? baseUrlInput.text : string.Empty);
                RunViewModelAction(async () =>
                {
                    var result = await this.viewModel.SignupAsync(
                        emailInput != null ? emailInput.text : string.Empty,
                        passwordInput != null ? passwordInput.text : string.Empty,
                        displayNameInput != null ? displayNameInput.text : string.Empty);
                    if (result.IsSuccess && passwordInput != null)
                    {
                        passwordInput.text = string.Empty;
                    }
                });
            });
            ReplaceClick(logoutButton, () => RunViewModelAction(async () =>
            {
                var result = await this.viewModel.LogoutAsync();
                if (result.IsSuccess && passwordInput != null)
                {
                    passwordInput.text = string.Empty;
                }
            }));
            ReplaceClick(refreshUserButton, () =>
            {
                if (this.viewModel != null)
                {
                    this.viewModel.SetSafeSyncBaseUrl(baseUrlInput != null ? baseUrlInput.text : string.Empty);
                }

                RunViewModelAction(() => this.viewModel.LoadCurrentUserAsync());
            });

            Render();
        }

        public void Render()
        {
            SetText(statusLabel, BootstrapUiTextFormatter.AuthStatus(viewModel));
            if (viewModel != null && baseUrlInput != null && !baseUrlInput.isFocused)
            {
                baseUrlInput.text = viewModel.SafeSyncBaseUrl;
            }

            SetButton(loginButton, viewModel != null && viewModel.CanSubmitLogin && !viewModel.IsAuthRequestInProgress);
            SetButton(signupButton, viewModel != null && viewModel.CanSubmitSignup && !viewModel.IsAuthRequestInProgress);
            SetButton(logoutButton, viewModel != null && viewModel.CanLogout && !viewModel.IsAuthRequestInProgress);
            SetButton(refreshUserButton, viewModel != null && viewModel.CanRefreshUser && !viewModel.IsAuthRequestInProgress);
        }

        protected override bool ValidateRequiredReferencesInternal(ref string error)
        {
            var valid = true;
            valid &= Require(statusLabel, nameof(statusLabel), ref error);
            valid &= Require(baseUrlInput, nameof(baseUrlInput), ref error);
            valid &= Require(emailInput, nameof(emailInput), ref error);
            valid &= Require(displayNameInput, nameof(displayNameInput), ref error);
            valid &= Require(passwordInput, nameof(passwordInput), ref error);
            valid &= Require(loginButton, nameof(loginButton), ref error);
            valid &= Require(signupButton, nameof(signupButton), ref error);
            valid &= Require(logoutButton, nameof(logoutButton), ref error);
            valid &= Require(refreshUserButton, nameof(refreshUserButton), ref error);
            return valid;
        }
    }
}
