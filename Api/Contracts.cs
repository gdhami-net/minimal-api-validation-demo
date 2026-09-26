using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ValidationDemo.Api;

// ---------------------------------------------------------------------------
// Body types. Every annotation here is plain System.ComponentModel.DataAnnotations
// — nothing in this file knows that AddValidation() exists.
// ---------------------------------------------------------------------------

public record OrderRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Reference { get; init; } = "";

    [Range(1, 500)]
    public int Quantity { get; init; }

    [EmailAddress]
    public string? Email { get; init; }

    public AddressRequest? Address { get; init; }

    public List<LineRequest> Lines { get; init; } = [];
}

public record AddressRequest
{
    [Required(AllowEmptyStrings = false)]
    public string City { get; init; } = "";

    [StringLength(8)]
    public string? Postcode { get; init; }
}

public record LineRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Sku { get; init; } = "";

    [Range(1, 99)]
    public int Qty { get; init; }
}

// The same basket three ways. The only difference between them is the type of
// the collection property.
public record BasketWithList
{
    [Required(AllowEmptyStrings = false)]
    public string Owner { get; init; } = "";

    public List<LineRequest> Lines { get; init; } = [];
}

public record BasketWithArray
{
    [Required(AllowEmptyStrings = false)]
    public string Owner { get; init; } = "";

    public LineRequest[] Lines { get; init; } = [];
}

public record BasketWithMap
{
    [Required(AllowEmptyStrings = false)]
    public string Owner { get; init; } = "";

    public Dictionary<string, LineRequest> Lines { get; init; } = [];
}

// The other collection shapes, to show that the array is the odd one out and not
// "collections in general".
public record BasketWithIList { public IList<LineRequest> Lines { get; init; } = []; }
public record BasketWithICollection { public ICollection<LineRequest> Lines { get; init; } = []; }
public record BasketWithIReadOnlyList { public IReadOnlyList<LineRequest> Lines { get; init; } = []; }
public record BasketWithHashSet { public HashSet<LineRequest> Lines { get; init; } = []; }

// Attributes written on the positional parameters of a record, with no
// [property:] target. The compiler leaves them on the constructor parameter;
// PositionalRecordTests asserts that and the endpoint still rejects bad input.
public record ShipmentRequest(
    [Required(AllowEmptyStrings = false)] string Carrier,
    [Range(1, 99)] int Parcels);

public record DateRange : IValidatableObject
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (To < From)
        {
            yield return new ValidationResult("To must be on or after From.", [nameof(To)]);
        }
    }
}

// The same cross-property rule, on a type that also has a property-level rule.
public record BookingRequest : IValidatableObject
{
    [Required(AllowEmptyStrings = false)]
    public string Room { get; init; } = "";

    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (To < From)
        {
            yield return new ValidationResult("To must be on or after From.", [nameof(To)]);
        }
    }
}

public sealed class SlugAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
        => value is string s && s.Length > 0 && s.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-');

    public override string FormatErrorMessage(string name)
        => $"{name} must be lowercase letters, digits and dashes.";
}

public record ArticleRequest
{
    [Slug]
    public string Slug { get; init; } = "";
}

// [Required] on a non-nullable int and on a nullable int, side by side.
public record CountRequest
{
    [Required]
    public int Quantity { get; init; }

    [Required]
    public int? Batch { get; init; }
}

// The wire name and the C# name differ, and [Display] renames the field in the
// message but not in the key.
public record PersonRequest
{
    [JsonPropertyName("full_name")]
    [Display(Name = "Customer name")]
    [Required(AllowEmptyStrings = false)]
    public string Name { get; init; } = "";
}

// Self-referencing, for the MaxDepth ceiling.
public record ChainRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Label { get; init; } = "";

    public ChainRequest? Next { get; init; }
}

public record struct PageArgs(
    [Range(1, 1000)] int Page,
    [Range(1, 100)] int Size);

public record UploadRequest
{
    [Required(AllowEmptyStrings = false)]
    public string Title { get; init; } = "";

    [Range(1, 10)]
    public int Copies { get; init; }
}
