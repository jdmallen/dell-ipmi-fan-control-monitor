using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace JDMallen.IPMITempMonitor.Validation;

/// <summary>
///     Validates that a string property contains a syntactically valid regular
///     expression pattern. An invalid pattern would otherwise throw only later, when
///     <see cref="Regex" /> is constructed at runtime; this surfaces the problem at
///     startup instead.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ValidRegexAttribute : ValidationAttribute
{
	protected override ValidationResult? IsValid(
		object? value,
		ValidationContext validationContext)
	{
		// Null/empty handling is the responsibility of [Required]; treat as valid here.
		if (value is not string pattern || string.IsNullOrEmpty(pattern))
		{
			return ValidationResult.Success;
		}

		try
		{
			_ = Regex.Match(string.Empty, pattern);

			return ValidationResult.Success;
		}
		catch (ArgumentException ex)
		{
			return new ValidationResult(
				$"'{pattern}' is not a valid regular expression: {ex.Message}",
				[validationContext.MemberName ?? nameof(value)]);
		}
	}
}
