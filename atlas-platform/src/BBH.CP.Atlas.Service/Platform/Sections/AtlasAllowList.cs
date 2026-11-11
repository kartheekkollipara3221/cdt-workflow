using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>The reason why a submitted value is not used (and no error is given).</summary>
public enum AtlasIgnoreReason { ServerSet, Unchanged }

/// <summary>
/// Checks which submitted values a save can use.
/// </summary>
/// <remarks>
/// The check ignores the server-set fields: system columns, Computed targets and effect targets.
/// A hidden or disabled field can only send its stored value. The compare uses the column type.
/// A changed value of such a field gives the error NotAllowed.
/// The caller supplies the option check.
/// </remarks>
public static class AtlasAllowList
{
    /// <summary>Returns the submitted values that the save can use, by field.</summary>
    /// <param name="previous">The stored values of the record, by column field.</param>
    /// <param name="isServerSet">Returns true for a field that the server sets.</param>
    /// <param name="optionError">Returns an error when the value is not an allowed option, or null.</param>
    /// <param name="errors">The list that gets the errors of the check.</param>
    /// <param name="ignored">Gets each ignored field and the reason.</param>
    /// <exception cref="ArgumentException">A submitted name is not a field of the form.</exception>
    public static Dictionary<AtlasFormField, string?> Check(
        IReadOnlyDictionary<string, string?> submitted,
        AtlasFormFields fields,
        AtlasTable table,
        IReadOnlyDictionary<string, string?> previous,
        Func<AtlasFormField, bool> isServerSet,
        Func<AtlasFormField, string?, string?, CPPageError?> optionError,
        List<CPPageError> errors,
        Action<AtlasFormField, AtlasIgnoreReason>? ignored = null)
    {
        ArgumentNullException.ThrowIfNull(submitted);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(isServerSet);
        ArgumentNullException.ThrowIfNull(optionError);
        ArgumentNullException.ThrowIfNull(errors);

        Dictionary<AtlasFormField, string?> accepted = [];
        foreach ((string name, string? value) in submitted)
        {
            AtlasFormField field = fields.Find(name)
                ?? throw new ArgumentException($"'{name}' is not a field of the form.", nameof(submitted));
            if (isServerSet(field))
            {
                ignored?.Invoke(field, AtlasIgnoreReason.ServerSet);
                continue;
            }

            string? stored = previous.GetValueOrDefault(field.Field);
            if (!field.Visible || field.Disabled)
            {
                if (AtlasValueConverter.SameValue(value, stored, table.Find(field.Field)))
                    ignored?.Invoke(field, AtlasIgnoreReason.Unchanged);
                else
                    errors.Add(new CPPageError { Field = field.Name, Code = "NotAllowed", Message = $"{field.Label ?? field.Name} cannot be changed." });
                continue;
            }

            if (optionError(field, value, stored) is { } error)
            {
                errors.Add(error);
                continue;
            }

            accepted[field] = value;
        }

        return accepted;
    }
}
