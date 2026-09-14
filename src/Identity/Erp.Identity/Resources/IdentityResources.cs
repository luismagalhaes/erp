namespace Erp.Identity.Resources;

/// <summary>
/// Marker type used to resolve <c>IStringLocalizer&lt;IdentityResources&gt;</c> against
/// <c>Resources/IdentityResources.resx</c> (neutral = pt-PT) and its
/// <c>IdentityResources.en-US.resx</c> satellite. Holds every string specific to the Identity UI
/// — Account pages, Backoffice, Home, layout — independent of the equivalent shared-resources
/// class Erp.Main uses, since the Identity host does not reference that project.
/// </summary>
public interface IdentityResources;
