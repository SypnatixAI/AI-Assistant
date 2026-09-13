USE [AssistantCoreDb];

-- Le ProtectedClientState existant a ete calcule avec un hash non protege par
-- une cle secrete. Cette valeur est irreversible : elle ne peut pas etre
-- transformee vers le nouveau format HMAC sans connaitre le clientState
-- d'origine, qui n'a jamais ete persiste. Les abonnements concernes sont donc
-- marques pour etre recrees proprement (suppression cote Microsoft Graph puis
-- nouvel abonnement avec un nouveau clientState) par le service de
-- maintenance existant, qui declenche deja une reconciliation complete dans
-- ce cas pour ne pas perdre silencieusement une notification manquee pendant
-- la bascule.
UPDATE [dbo].[Microsoft365Subscription]
SET [LastErrorCode] = 'ClientStateMigrationRequired',
    [Status] = 'RenewalRequired',
    [UpdatedAt] = SYSDATETIMEOFFSET()
WHERE [ProtectedClientState] IS NOT NULL
    AND [Status] NOT IN ('Revoked', 'RevocationRequired');
