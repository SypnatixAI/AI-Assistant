using AutoFixture;
using AutoFixture.Xunit2;

namespace AssistantCore.Service.Tests;

internal sealed class AutoDomainDataAttribute : AutoDataAttribute
{
    public AutoDomainDataAttribute()
        : base(CreateFixture)
    {
    }

    private static IFixture CreateFixture()
    {
        var fixture = new Fixture();
        fixture.Customize(new DomainCustomization());
        return fixture;
    }
}
