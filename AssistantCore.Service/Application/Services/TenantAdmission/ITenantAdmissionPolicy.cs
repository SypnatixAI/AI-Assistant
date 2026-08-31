using AssistantCore.Repository.Domain.Enums;

namespace AssistantCore.Service.Application.Services.TenantAdmission;

/// <summary>
/// Decide si un membre peut utiliser AssistantCore selon l'etat de configuration
/// Microsoft 365 de son organisation. tenantAdmin sert a demarrer la configuration ;
/// une fois celle-ci terminee, il n'est plus une condition d'admission generale
/// (principe du moindre privilege). Le role interne Admin implique deja tenantAdmin
/// (voir OrganizationRoleResolver), donc cette politique ne relit jamais les app
/// roles Entra directement.
/// </summary>
public interface ITenantAdmissionPolicy
{
    TenantAdmissionResult Evaluate(OrganizationRole memberRole, bool isOnboardingComplete);
}
