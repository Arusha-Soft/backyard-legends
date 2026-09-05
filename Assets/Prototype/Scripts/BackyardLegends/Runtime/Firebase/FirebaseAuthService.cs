using System;
using System.Threading.Tasks;
using UnityEngine;

namespace BackyardLegends.Runtime.Firebase
{
    public sealed class FirebaseAuthService : MonoBehaviour
    {
        public static FirebaseAuthService Instance { get; private set; }

        private global::Firebase.Auth.FirebaseAuth auth;
        private AuthUserSnapshot currentUser = AuthUserSnapshot.None;
        private string lastError = string.Empty;
        private bool ensureInFlight;

        public AuthUserSnapshot CurrentUser => currentUser;
        public string LastError => lastError;
        public bool IsBusy { get; private set; }

        public event Action<AuthUserSnapshot> StateChanged;

        public static FirebaseAuthService GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindFirstObjectByType<FirebaseAuthService>();
            if (existing != null)
            {
                existing.EnsureSingleton();
                return existing;
            }

            var go = new GameObject("Firebase Auth Service");
            var service = go.AddComponent<FirebaseAuthService>();
            service.EnsureSingleton();
            return service;
        }

        private void Awake()
        {
            EnsureSingleton();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                if (auth != null)
                {
                    auth.StateChanged -= HandleAuthStateChanged;
                }

                Instance = null;
            }
        }

        private void EnsureSingleton()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async Task<AuthUserSnapshot> EnsureSignedInAsync()
        {
            if (ensureInFlight)
            {
                while (ensureInFlight)
                {
                    await Task.Yield();
                }

                return currentUser;
            }

            ensureInFlight = true;
            IsBusy = true;
            lastError = string.Empty;

            try
            {
                if (!await FirebaseBootstrap.EnsureInitializedAsync())
                {
                    SetUser(AuthUserSnapshot.None);
                    return currentUser;
                }

                BindAuth();
                if (auth.CurrentUser != null)
                {
                    await RefreshFromFirebaseUserAsync(auth.CurrentUser);
                    return currentUser;
                }

                var result = await auth.SignInAnonymouslyAsync();
                await RefreshFromFirebaseUserAsync(result.User);
                return currentUser;
            }
            catch (Exception ex)
            {
                lastError = DescribeException(ex);
                Debug.LogWarning($"Firebase anonymous sign-in failed: {lastError}");
                SetUser(AuthUserSnapshot.None);
                return currentUser;
            }
            finally
            {
                IsBusy = false;
                ensureInFlight = false;
            }
        }

        public async Task<AuthUserSnapshot> LinkWithGoogleAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return await LinkWithProviderAsync("google.com", "Google");
#else
            lastError = "Google sign-in is only available on Android devices.";
            Debug.LogWarning(lastError);
            return currentUser;
#endif
        }

        public async Task<AuthUserSnapshot> LinkWithAppleAsync()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return await LinkWithProviderAsync("apple.com", "Apple");
#else
            lastError = "Apple sign-in is only available on iOS devices.";
            Debug.LogWarning(lastError);
            return currentUser;
#endif
        }

        public async Task<AuthUserSnapshot> RegisterWithEmailPasswordAsync(string email, string password)
        {
            email = (email ?? string.Empty).Trim();
            password = password ?? string.Empty;
            if (!ValidateEmailPassword(email, password, requireStrongPassword: true))
            {
                return currentUser;
            }

            IsBusy = true;
            lastError = string.Empty;

            try
            {
                if (!await FirebaseBootstrap.EnsureInitializedAsync())
                {
                    lastError = "Firebase is not available.";
                    return currentUser;
                }

                BindAuth();
                if (auth.CurrentUser == null)
                {
                    await EnsureSignedInAsync();
                }

                var credential = global::Firebase.Auth.EmailAuthProvider.GetCredential(email, password);

                if (auth.CurrentUser != null && auth.CurrentUser.IsAnonymous)
                {
                    try
                    {
                        var linked = await auth.CurrentUser.LinkWithCredentialAsync(credential);
                        await MaybeSetDisplayNameFromEmailAsync(linked.User, email);
                        await RefreshFromFirebaseUserAsync(auth.CurrentUser);
                        Debug.Log($"Linked email/password; uid={currentUser.Uid}");
                        return currentUser;
                    }
                    catch (Exception linkEx)
                    {
                        if (!IsEmailAlreadyInUse(linkEx) && !IsCredentialAlreadyInUse(linkEx))
                        {
                            throw;
                        }

                        lastError = "That email is already registered. Use Sign In instead.";
                        Debug.LogWarning(lastError);
                        return currentUser;
                    }
                }

                var created = await auth.CreateUserWithEmailAndPasswordAsync(email, password);
                await MaybeSetDisplayNameFromEmailAsync(created.User, email);
                await RefreshFromFirebaseUserAsync(created.User);
                Debug.Log($"Registered email/password; uid={currentUser.Uid}");
                return currentUser;
            }
            catch (Exception ex)
            {
                lastError = DescribeException(ex);
                Debug.LogWarning($"Email register failed: {lastError}");
                return currentUser;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<AuthUserSnapshot> SignInWithEmailPasswordAsync(string email, string password)
        {
            email = (email ?? string.Empty).Trim();
            password = password ?? string.Empty;
            if (!ValidateEmailPassword(email, password, requireStrongPassword: false))
            {
                return currentUser;
            }

            IsBusy = true;
            lastError = string.Empty;

            try
            {
                if (!await FirebaseBootstrap.EnsureInitializedAsync())
                {
                    lastError = "Firebase is not available.";
                    return currentUser;
                }

                BindAuth();
                var result = await auth.SignInWithEmailAndPasswordAsync(email, password);
                await RefreshFromFirebaseUserAsync(result.User);
                Debug.Log($"Signed in with email/password; uid={currentUser.Uid}");
                return currentUser;
            }
            catch (Exception ex)
            {
                lastError = DescribeException(ex);
                Debug.LogWarning($"Email sign-in failed: {lastError}");
                return currentUser;
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task SignOutAsync()
        {
            if (!FirebaseBootstrap.IsAvailable || auth == null)
            {
                SetUser(AuthUserSnapshot.None);
                return;
            }

            try
            {
                auth.SignOut();
                SetUser(AuthUserSnapshot.None);
                await EnsureSignedInAsync();
            }
            catch (Exception ex)
            {
                lastError = DescribeException(ex);
                Debug.LogWarning($"Firebase sign-out failed: {lastError}");
            }
        }

        private async Task<AuthUserSnapshot> LinkWithProviderAsync(string providerId, string label)
        {
            IsBusy = true;
            lastError = string.Empty;

            try
            {
                if (!await FirebaseBootstrap.EnsureInitializedAsync())
                {
                    lastError = "Firebase is not available.";
                    return currentUser;
                }

                BindAuth();
                if (auth.CurrentUser == null)
                {
                    await EnsureSignedInAsync();
                }

                if (auth.CurrentUser == null)
                {
                    lastError = "No Firebase user to link.";
                    return currentUser;
                }

                var providerData = new global::Firebase.Auth.FederatedOAuthProviderData
                {
                    ProviderId = providerId
                };
                var provider = new global::Firebase.Auth.FederatedOAuthProvider(providerData);

                try
                {
                    var result = await auth.CurrentUser.LinkWithProviderAsync(provider);
                    await RefreshFromFirebaseUserAsync(result.User);
                    Debug.Log($"Linked {label}; uid={currentUser.Uid}");
                    return currentUser;
                }
                catch (Exception linkEx)
                {
                    if (!IsCredentialAlreadyInUse(linkEx))
                    {
                        throw;
                    }

                    // OAuth provider sessions are one-shot; open a fresh provider flow to sign into the existing account.
                    Debug.LogWarning($"{label} credential already in use; signing into existing account.");
                    var retryProvider = new global::Firebase.Auth.FederatedOAuthProvider(
                        new global::Firebase.Auth.FederatedOAuthProviderData { ProviderId = providerId });
                    var signedIn = await auth.SignInWithProviderAsync(retryProvider);
                    await RefreshFromFirebaseUserAsync(signedIn.User);
                    lastError = $"{label} was already linked to another account. Signed into that account.";
                    return currentUser;
                }
            }
            catch (Exception ex)
            {
                lastError = DescribeException(ex);
                Debug.LogWarning($"Link with {label} failed: {lastError}");
                return currentUser;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void BindAuth()
        {
            if (auth != null)
            {
                return;
            }

            auth = global::Firebase.Auth.FirebaseAuth.DefaultInstance;
            auth.StateChanged += HandleAuthStateChanged;
        }

        private void HandleAuthStateChanged(object sender, EventArgs e)
        {
            if (auth == null)
            {
                return;
            }

            _ = RefreshFromFirebaseUserAsync(auth.CurrentUser);
        }

        private async Task RefreshFromFirebaseUserAsync(global::Firebase.Auth.FirebaseUser user)
        {
            if (user == null)
            {
                SetUser(AuthUserSnapshot.None);
                return;
            }

            var displayName = user.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    displayName = user.Email;
                }
                else
                {
                    displayName = user.IsAnonymous ? "Guest" : "Player";
                }
            }

            var platform = ResolvePlatform();
            var rating = 1000;
            try
            {
                var profile = await UserProfileService.EnsureUserProfileAsync(
                    user.UserId,
                    displayName,
                    platform,
                    user.IsAnonymous);
                if (profile != null)
                {
                    displayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? displayName : profile.DisplayName;
                    rating = profile.Rating;
                    platform = string.IsNullOrWhiteSpace(profile.Platform) ? platform : profile.Platform;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"User profile sync skipped: {DescribeException(ex)}");
            }

            SetUser(new AuthUserSnapshot(user.UserId, displayName, user.IsAnonymous, platform, rating));
        }

        private void SetUser(AuthUserSnapshot snapshot)
        {
            currentUser = snapshot ?? AuthUserSnapshot.None;
            StateChanged?.Invoke(currentUser);
        }

        private static string ResolvePlatform()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return "android";
#elif UNITY_IOS && !UNITY_EDITOR
            return "ios";
#elif UNITY_EDITOR
            return "editor";
#else
            return Application.platform.ToString().ToLowerInvariant();
#endif
        }

        private bool ValidateEmailPassword(string email, string password, bool requireStrongPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || email.IndexOf('@') < 1)
            {
                lastError = "Enter a valid email address.";
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                lastError = "Enter a password.";
                return false;
            }

            if (requireStrongPassword && password.Length < 6)
            {
                lastError = "Password must be at least 6 characters.";
                return false;
            }

            return true;
        }

        private static async Task MaybeSetDisplayNameFromEmailAsync(global::Firebase.Auth.FirebaseUser user, string email)
        {
            if (user == null || !string.IsNullOrWhiteSpace(user.DisplayName))
            {
                return;
            }

            var at = email.IndexOf('@');
            var name = at > 0 ? email.Substring(0, at) : email;
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            await user.UpdateUserProfileAsync(new global::Firebase.Auth.UserProfile
            {
                DisplayName = name
            });
        }

        private static bool IsEmailAlreadyInUse(Exception ex)
        {
            if (ex is AggregateException aggregate)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    if (IsEmailAlreadyInUse(inner))
                    {
                        return true;
                    }
                }
            }

            if (ex is global::Firebase.FirebaseException firebaseEx)
            {
                return firebaseEx.ErrorCode == (int)global::Firebase.Auth.AuthError.EmailAlreadyInUse;
            }

            var message = ex.Message ?? string.Empty;
            return message.IndexOf("email-already-in-use", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("already in use", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCredentialAlreadyInUse(Exception ex)
        {
            if (ex is AggregateException aggregate)
            {
                foreach (var inner in aggregate.Flatten().InnerExceptions)
                {
                    if (IsCredentialAlreadyInUse(inner))
                    {
                        return true;
                    }
                }
            }

            if (ex is global::Firebase.FirebaseException firebaseEx)
            {
                return firebaseEx.ErrorCode == (int)global::Firebase.Auth.AuthError.CredentialAlreadyInUse
                       || firebaseEx.ErrorCode == (int)global::Firebase.Auth.AuthError.AccountExistsWithDifferentCredentials;
            }

            var message = ex.Message ?? string.Empty;
            return message.IndexOf("already in use", StringComparison.OrdinalIgnoreCase) >= 0
                   || message.IndexOf("credential-already-in-use", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string DescribeException(Exception ex)
        {
            if (ex is AggregateException aggregate)
            {
                ex = aggregate.Flatten().InnerException ?? ex;
            }

            return ex?.Message ?? "Unknown error";
        }
    }
}
