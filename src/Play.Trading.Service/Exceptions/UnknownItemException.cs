using System;

namespace Play.Trading.Service.Exceptions;

[Serializable]
internal class UnknownItemException : Exception
{
    public Guid CatalogItemId { get; set; }

    public UnknownItemException(Guid catalogItemId) : base($"Unknown item'{catalogItemId}'")
    {
        this.CatalogItemId = catalogItemId;
    }
}