namespace Erp.Identity.Common.Constants
{
    public static class Constants
    {
        public static class Roles
        {
            public const string SuperAdmin = "SuperAdmin";
            public const string User = "User";

            public static readonly string[] All =
            [
                SuperAdmin,
                User
            ];
        }

        /// <summary>
        /// Claim types as Duende emits them. The APIs read the short names, so their JWT
        /// validation is configured with these instead of the WS-Federation defaults.
        /// </summary>
        public static class Claims
        {
            public const string Role = "role";
            public const string Name = "name";
            public const string Subject = "sub";

            /// <summary>
            /// The user's preferred UI culture (e.g. "pt-PT", "en-US"), issued into the id_token so
            /// every UI can localize without a round trip to the Identity host.
            /// </summary>
            public const string Locale = "locale";
        }

        /// <summary>
        /// Supported UI cultures and the default one. Deliberately duplicated from
        /// <c>Erp.Common.Constants.Localization</c> — the Identity host and its projects never
        /// reference <c>Erp.Common</c> — so keep the two in agreement by hand if either changes.
        /// </summary>
        public static class Localization
        {
            public const string DefaultCulture = "pt-PT";
            public const string PtPt = "pt-PT";
            public const string EnUs = "en-US";

            public static readonly string[] SupportedCultures = [PtPt, EnUs];

            /// <summary>Name of the cookie used to remember the culture of an unauthenticated visitor.</summary>
            public const string CultureCookieName = ".Erp.Culture";
        }

        public static class Scopes
        {
            public const string OpenId = "openid";
            public const string Profile = "profile";
            public const string Email = "email";
            public const string OfflineAccess = "offline_access";

            /// <summary>
            /// Every business module is served by one API, so access is granted in two levels
            /// instead of one pair per module. What a caller may reach inside the API is then
            /// decided by role and company membership, not by a per module scope.
            /// </summary>
            public const string ErpRead = "erp.read";
            public const string ErpWrite = "erp.write";

            /// <summary>Queueing emails. Granted only to services, never to the user facing client.</summary>
            public const string ErpNotificationSend = "erp.notification.send";

            /// <summary>Reading users from the Identity host, to assign them to companies.</summary>
            public const string ErpIdentityRead = "erp.identity.read";

            /// <summary>
            /// Identity scope, not an API scope: it carries the role claim into the id_token so the
            /// UI can hide what a user may not reach. The API reads roles from the access token,
            /// where the API resources already declare them.
            /// </summary>
            public const string Roles = "roles";
        }

        public static class ScopeDisplayNames
        {
            public const string ErpRead = "ERP — Read";
            public const string ErpWrite = "ERP — Write";
            public const string NotificationSend = "Notification — Send";
            public const string IdentityRead = "Identity — Read";
            public const string Roles = "Roles";
        }

        public static class ApiResources
        {
            /// <summary>
            /// Every business module is served by one host, so they share a single audience.
            /// Access is separated by scope, not by resource.
            /// </summary>
            public const string ErpApi = "erp-api";
            public const string ErpApiDisplayName = "ERP API";

            /// <summary>The users API served by the Identity host itself.</summary>
            public const string IdentityApi = "identity-api";
            public const string IdentityApiDisplayName = "Identity API";
        }

        public static class Clients
        {
            public const string BlazorWasmClientId = "blazor-wasm";
            public const string BlazorWasmClientName = "ERP — UI";
            public const string HttpsLocalhost7019 = "https://localhost:7019";
            public const string HttpLocalhost5191 = "http://localhost:5191";
            public const string LoginCallbackPath = "/authentication/login-callback";
            public const string LogoutCallbackPath = "/authentication/logout-callback";

            /// <summary>The Identity host itself, when it queues emails on the notification service.</summary>
            public const string IdentityServiceClientId = "identity-service";
            public const string IdentityServiceClientName = "Identity Service (M2M)";
            public const string IdentityServiceSecret = "identity-service-secret";
        }

        /// <summary>
        /// reCAPTCHA v3 only kicks in once a form has been abused a few times — the first attempts
        /// from a given IP go through with no challenge at all, invisible or otherwise.
        /// </summary>
        public static class Recaptcha
        {
            /// <summary>Attempts allowed before reCAPTCHA starts being required.</summary>
            public const int AttemptThreshold = 3;

            /// <summary>How far back an attempt still counts towards the threshold.</summary>
            public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

            public const string SignInAction = "signin";
            public const string SignUpAction = "signup";
        }
    }
}
