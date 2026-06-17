using System.Security.Claims;
using FlexCms.Framework.Auth;
using FlexCms.InvestPro.Data;
using FlexCms.InvestPro.Services;
using Microsoft.AspNetCore.Mvc;

namespace FlexCms.InvestPro.Controllers;

/// <summary>
/// Shared caller-identity helpers for InvestPro controllers.
/// Two responsibilities:
/// <list type="bullet">
/// <item><see cref="CurrentUserId"/> — the calling app-user id, parsed from
///   <see cref="ClaimTypes.NameIdentifier"/>. Used for audit columns
///   (InitiatedByUserId, ClosedByUserId, etc.).</item>
/// <item><see cref="ResolveActingPartnerAsync"/> — for vote endpoints (close /
///   reopen / approval decisions). Binds the caller's user id to a
///   Partner row so we can stop trusting <c>PartnerId</c> from the form.
///   SuperAdmin is allowed to vote AS any partner (operational ops/recovery
///   workflow) but every other role can only vote as their own partner.</item>
/// </list>
/// </summary>
public abstract class InvestProControllerBase : Controller
{
    protected Guid? CurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    protected bool IsSuperAdmin()
        => User.IsInRole(FcmsRoles.SuperAdmin)
        || User.IsInRole(FcmsRoles.SuperAdmin.ToUpperInvariant());

    /// <summary>
    /// Resolve which partner the caller is allowed to vote AS.
    /// <list type="bullet">
    /// <item>Non-SuperAdmin: looks up the partner whose <c>UserId</c> matches
    ///   the caller; <paramref name="formPartnerId"/> is IGNORED. If the form
    ///   supplied a different partner, the call still succeeds with the
    ///   caller's own partner — defeats the "vote as someone else" IDOR.</item>
    /// <item>SuperAdmin: uses <paramref name="formPartnerId"/> as-is. Admins
    ///   often record votes on behalf of partners who don't have a user
    ///   account (paper consent, phone consent, etc.).</item>
    /// </list>
    /// Returns <c>(partnerId, error)</c>. <c>partnerId</c> is null when the
    /// call should fail (e.g. non-admin user with no partner record).
    /// </summary>
    protected async Task<(Guid? partnerId, string? error)> ResolveActingPartnerAsync(
        PartnerService partners, Guid? formPartnerId, CancellationToken ct)
    {
        if (IsSuperAdmin())
        {
            if (formPartnerId is null || formPartnerId == Guid.Empty)
                return (null, "SuperAdmin must select a partner to vote on behalf of.");
            return (formPartnerId, null);
        }

        var userId = CurrentUserId();
        if (userId is null)
            return (null, "Cannot determine the calling user.");

        var partner = await partners.GetByUserIdAsync(userId.Value, ct);
        if (partner is null)
            return (null, "Your user account is not linked to any partner record.");

        return (partner.Id, null);
    }
}
