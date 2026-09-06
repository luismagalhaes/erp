namespace Erp.Identity.Common.Constants
{
    public static class Constants
    {
        public static class Roles
        {
            public const string SuperAdmin = "SuperAdmin";
            public const string Admin = "Admin";
            public const string Manager = "Manager";
            public const string User = "User";
            public const string Accountant = "Accountant";
            public const string Auditor = "Auditor";

            public static readonly string[] All =
            [
                SuperAdmin,
                Admin,
                Manager,
                User,
                Accountant,
                Auditor
            ];
        }

        public static class AdminUser
        {
            public const string Email = "lmagalhaes@cegid.com";
            public const string FirstName = "Administrator";
            public const string LastName = "System";
            public const string Password = "Activex.01!";
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
        }

        public static class Scopes
        {
            public const string OpenId = "openid";
            public const string Profile = "profile";
            public const string Email = "email";
            public const string OfflineAccess = "offline_access";

            public const string ErpCoreRead = "erp.core.read";
            public const string ErpCoreWrite = "erp.core.write";
            public const string ErpSalesRead = "erp.sales.read";
            public const string ErpSalesWrite = "erp.sales.write";
            public const string ErpInventoryRead = "erp.inventory.read";
            public const string ErpInventoryWrite = "erp.inventory.write";
            public const string ErpPurchasingRead = "erp.purchasing.read";
            public const string ErpPurchasingWrite = "erp.purchasing.write";
            public const string ErpAccountingRead = "erp.accounting.read";
            public const string ErpAccountingWrite = "erp.accounting.write";
            public const string ErpReportingRead = "erp.reporting.read";
            public const string ErpNotificationRead = "erp.notification.read";
            public const string ErpNotificationWrite = "erp.notification.write";

            /// <summary>Queueing emails. Granted only to services, never to the user facing client.</summary>
            public const string ErpNotificationSend = "erp.notification.send";

            /// <summary>Reading users from the Identity host, to assign them to companies.</summary>
            public const string ErpIdentityRead = "erp.identity.read";
        }

        public static class ScopeDisplayNames
        {
            public const string CoreRead = "Core — Read";
            public const string CoreWrite = "Core — Write";
            public const string SalesRead = "Sales — Read";
            public const string SalesWrite = "Sales — Write";
            public const string InventoryRead = "Inventory — Read";
            public const string InventoryWrite = "Inventory — Write";
            public const string PurchasingRead = "Purchasing — Read";
            public const string PurchasingWrite = "Purchasing — Write";
            public const string AccountingRead = "Accounting — Read";
            public const string AccountingWrite = "Accounting — Write";
            public const string ReportingRead = "Reporting — Read";
            public const string NotificationRead = "Notification — Read";
            public const string NotificationWrite = "Notification — Write";
            public const string NotificationSend = "Notification — Send";
            public const string IdentityRead = "Identity — Read";
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

            public const string NotificationUiClientId = "notification-ui";
            public const string NotificationUiClientName = "ERP — Notification UI";
            public const string HttpsLocalhost7125 = "https://localhost:7125";
            public const string NotificationUiLoginCallbackPath = "/signin-oidc";
            public const string NotificationUiLogoutCallbackPath = "/signout-callback-oidc";

            public const string SalesServiceClientId = "sales-service";
            public const string SalesServiceClientName = "Sales Service (M2M)";
            public const string SalesServiceSecret = "sales-service-secret";

            public const string ReportingServiceClientId = "reporting-service";
            public const string ReportingServiceClientName = "Reporting Service (M2M)";
            public const string ReportingServiceSecret = "reporting-service-secret";

            /// <summary>The Identity host itself, when it queues emails on the notification service.</summary>
            public const string IdentityServiceClientId = "identity-service";
            public const string IdentityServiceClientName = "Identity Service (M2M)";
            public const string IdentityServiceSecret = "identity-service-secret";
        }
    }
}
