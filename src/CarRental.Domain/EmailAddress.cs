namespace CarRental.Domain;

/// <summary>
/// An email-shaped value: something before an '@' and something after it. Nothing more is checked:
/// no emails are sent, so a correct address is the customer's responsibility. Stored exactly as entered.
/// </summary>
public sealed record EmailAddress
{
    public EmailAddress(string value)
    {
        var atIndex = value.IndexOf('@');
        var hasTextBeforeAndAfterAt = atIndex > 0 && atIndex < value.Length - 1;

        if (!hasTextBeforeAndAfterAt || value != value.Trim())
        {
            throw new ArgumentException($"'{value}' is not an email address.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }
}
