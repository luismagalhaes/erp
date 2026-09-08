namespace Erp.Common;

/// <summary>
/// Names shared by every ERP process: the API host, the UI and the services. They describe the
/// contract of the access token, so they cannot live inside one application.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Only two roles exist: a SuperAdmin configures the tenant, everyone else is a User.
    /// </summary>
    public static class Roles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string User = "User";

        public static readonly string[] All = [SuperAdmin, User];
    }

    /// <summary>Claim types as Duende emits them, in their short form.</summary>
    public static class Claims
    {
        public const string Role = "role";
        public const string Name = "name";
        public const string Subject = "sub";
        public const string Scope = "scope";
    }

    /// <summary>
    /// Response headers the API writes and the UI reads. They are a contract between the two
    /// processes, so neither side may own the name.
    /// </summary>
    public static class Headers
    {
        /// <summary>How many validation errors the generated SAF-T file carries.</summary>
        public const string SaftValidationErrors = "X-Saft-Validation-Errors";

        /// <summary>How many products were exported without a known cost.</summary>
        public const string InventoryProductsWithoutCost = "X-Inventory-Products-Without-Cost";

        /// <summary>How many validation errors the generated inventory file carries.</summary>
        public const string InventoryValidationErrors = "X-Inventory-Validation-Errors";
    }

    /// <summary>Limits applied to the OData listing endpoints the data grids call.</summary>
    public static class ODataQueryLimits
    {
        /// <summary>
        /// The ceiling for a page, applied when the caller asks for no <c>$top</c>. It keeps one
        /// request from reading a whole table.
        /// </summary>
        public const int MaxPageSize = 500;
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
        /// decided by role and by company membership.
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
}
