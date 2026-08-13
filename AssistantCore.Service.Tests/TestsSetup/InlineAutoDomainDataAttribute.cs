using AutoFixture.Xunit2;

namespace AssistantCore.Service.Tests;

internal sealed class InlineAutoDomainDataAttribute : InlineAutoDataAttribute
{
    public InlineAutoDomainDataAttribute(object? value)
        : base(new AutoDomainDataAttribute(), [value!])
    {
    }

    public InlineAutoDomainDataAttribute(params object[] values)
        : base(new AutoDomainDataAttribute(), values)
    {
    }
}
