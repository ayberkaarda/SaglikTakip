namespace SaglikTakip.Business;

/// <summary>
/// Raised when an operation violates a business rule. The message is user facing (Turkish)
/// and can be shown to the user as is.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException()
    {
    }

    public BusinessRuleException(string message)
        : base(message)
    {
    }

    public BusinessRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
