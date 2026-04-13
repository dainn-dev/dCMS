namespace dCMS.Messaging.Contracts;

/// <summary>Wire / topic version label (e.g. <c>OrderPlaced.v1</c>) for envelope metadata (DAI-303 / DAI-304).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class MessageVersionAttribute : Attribute
{
    public MessageVersionAttribute(string version) => Version = version;

    public string Version { get; }
}
