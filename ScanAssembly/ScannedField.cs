using System.Reflection;

namespace ScanAssembly;

public class ScannedField : IComparable<ScannedField>, IChangeScanner<ScannedField>
{
    public ScannedField() { }

    internal ScannedField(FieldInfo f)
    {
        Name        = f.Name;
        TypeName    = f.FieldType.FullName ?? f.FieldType.Name;
        IsStatic    = f.IsStatic;
        IsReadOnly  = f.IsInitOnly;
        IsConstant  = f.IsLiteral;
        IsPublic    = f.IsPublic;
        IsProtected = f.IsFamily;
    }

    public string Name        { get; set; } = "";
    public string TypeName    { get; set; } = "";
    public bool   IsStatic    { get; set; }
    public bool   IsConstant  { get; set; }
    public bool   IsReadOnly  { get; set; }
    public bool   IsPublic    { get; set; }
    public bool   IsProtected { get; set; }

    public int CompareTo(ScannedField? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (ReferenceEquals(null, other))
        {
            return 1;
        }

        return string.Compare(Name, other.Name, StringComparison.Ordinal);
    }

    public IEnumerable<ScanChange> GetChangesFrom(ScannedField original)
    {
        if (!Name.Equals(original.Name, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The original field name '{original.Name}' does not match the current field name '{Name}'."
            );
        }

        var tag = $"field {Name}";

        if (IsStatic != original.IsStatic)
        {
            if (!original.IsStatic)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now a static field.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now an instance field.");
            }
        }

        if (!TypeName.Equals(original.TypeName, StringComparison.Ordinal))
        {
            yield return new ScanChange(
                ScanChangeSeverity.Major,
                $"{tag} has had the type changed from '{original.TypeName}' to '{TypeName}'."
            );
        }

        if (IsConstant != original.IsConstant)
        {
            if (!original.IsConstant)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now a constant value.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is no longer a constant value.");
            }
        }

        if (IsReadOnly != original.IsReadOnly)
        {
            if (!original.IsReadOnly)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is now a readonly field.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is no longer a readonly field.");
            }
        }

        if (IsPublic != original.IsPublic)
        {
            if (!original.IsPublic)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is now public.");
            }
            else
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is no longer public.");
            }
        }

        if (IsProtected != original.IsProtected)
        {
            if (!original.IsProtected)
            {
                yield return new ScanChange(ScanChangeSeverity.Minor, $"{tag} is now protected.");
            }
            else if (!IsPublic)
            {
                yield return new ScanChange(ScanChangeSeverity.Major, $"{tag} is no longer protected.");
            }
        }
    }
}
