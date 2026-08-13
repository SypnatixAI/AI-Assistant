using AutoFixture;
using AssistantCore.Repository.Domain.Entities;

namespace AssistantCore.Service.Tests;

internal sealed class DomainCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        foreach (var behavior in fixture.Behaviors
                     .OfType<ThrowingRecursionBehavior>()
                     .ToArray())
        {
            fixture.Behaviors.Remove(behavior);
        }

        fixture.Behaviors.Add(new OmitOnRecursionBehavior());
        fixture.Customize<Organization>(composer =>
            composer.Without(organization => organization.Members));
        fixture.Customize<OrganizationMember>(composer =>
            composer.Without(member => member.Organization));
        fixture.Customize<Conversation>(composer => composer
            .Without(conversation => conversation.Organization)
            .Without(conversation => conversation.OwnerMember)
            .Without(conversation => conversation.Messages));
        fixture.Customize<Message>(composer => composer
            .Without(message => message.Conversation)
            .Without(message => message.Sources));
        fixture.Customize<MessageSource>(composer =>
            composer.Without(source => source.Message));
    }
}
