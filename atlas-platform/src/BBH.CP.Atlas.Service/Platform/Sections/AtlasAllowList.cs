using BBH.CP.Atlas.Data.DTO;
using BBH.CP.Atlas.Data.Platform;

namespace BBH.CP.Atlas.Service.Platform.Sections;

/// <summary>The reason why a submitted value is not used (and no error is given).</summary>
public enum AtlasIgnoreReason { ServerSet, Unchanged }

/// <summary>
/// The allow list of a save (spec 01, 5.9 step 4; review SV-08). Server-set fields (system columns, Computed targets,
/// effect targets) are ignored. A hidden or disabled field may only send its stored value (typed compare, review
/// DA-06); a change gives NotAllowed. The option check (review SV-01) is given by the caller.
/// </summary>
public static class AtlasAllowList
{
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
