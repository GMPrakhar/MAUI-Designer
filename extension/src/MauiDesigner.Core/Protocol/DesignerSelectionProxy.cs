using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace MauiDesigner.Core.Protocol
{
    [TypeConverter(typeof(DesignerGridDefinitionValueConverter))]
    public sealed class DesignerGridDefinitionValue
    {
        public DesignerGridDefinitionValue(string? serializedValue)
        {
            SerializedValue = serializedValue;
        }

        public string? SerializedValue { get; }

        public override string ToString() => "(Collection)";
    }

    public sealed class DesignerGridDefinitionValueConverter : TypeConverter
    {
        public override bool CanConvertTo(
            ITypeDescriptorContext? context,
            Type? destinationType) =>
            destinationType == typeof(string) ||
            base.CanConvertTo(context, destinationType);

        public override object? ConvertTo(
            ITypeDescriptorContext? context,
            CultureInfo? culture,
            object? value,
            Type destinationType)
        {
            if (destinationType == typeof(string) &&
                value is DesignerGridDefinitionValue)
            {
                return "(Collection)";
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    public sealed class DesignerEnumValue
    {
        public DesignerEnumValue(string? value)
        {
            Value = value;
        }

        public string? Value { get; }

        public override string ToString() => Value ?? string.Empty;
    }

    internal sealed class DesignerEnumValueConverter : TypeConverter
    {
        private readonly StandardValuesCollection _values;

        public DesignerEnumValueConverter(IEnumerable<string> values)
        {
            _values = new StandardValuesCollection(
                values.Select(value => new DesignerEnumValue(value)).ToArray());
        }

        public override bool CanConvertFrom(
            ITypeDescriptorContext? context,
            Type sourceType) =>
            sourceType == typeof(string) ||
            base.CanConvertFrom(context, sourceType);

        public override bool CanConvertTo(
            ITypeDescriptorContext? context,
            Type? destinationType) =>
            destinationType == typeof(string) ||
            base.CanConvertTo(context, destinationType);

        public override object? ConvertFrom(
            ITypeDescriptorContext? context,
            CultureInfo? culture,
            object value) =>
            value is string text
                ? new DesignerEnumValue(text)
                : base.ConvertFrom(context, culture, value);

        public override object? ConvertTo(
            ITypeDescriptorContext? context,
            CultureInfo? culture,
            object? value,
            Type destinationType) =>
            destinationType == typeof(string) && value is DesignerEnumValue enumValue
                ? enumValue.Value ?? string.Empty
                : base.ConvertTo(context, culture, value, destinationType);

        public override StandardValuesCollection GetStandardValues(
            ITypeDescriptorContext? context) =>
            _values;

        public override bool GetStandardValuesExclusive(
            ITypeDescriptorContext? context) =>
            true;

        public override bool GetStandardValuesSupported(
            ITypeDescriptorContext? context) =>
            true;
    }

    public class DesignerSelectionProxy : ICustomTypeDescriptor
    {
        private readonly PropertyDescriptorCollection _properties;
        private readonly Action<string, string?> _propertyChanged;
        private string? _rowDefinitions;
        private string? _columnDefinitions;

        public DesignerSelectionProxy(
            DesignerSelectionSnapshot snapshot,
            Action<string, string?> propertyChanged,
            Type? gridDefinitionsEditorType = null)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            if (propertyChanged == null)
            {
                throw new ArgumentNullException(nameof(propertyChanged));
            }

            _propertyChanged = propertyChanged;
            bool useClrGridProperties = gridDefinitionsEditorType is not null;
            var properties = snapshot.Properties
                .Where(property =>
                    !useClrGridProperties ||
                    !DesignerRemotePropertyDescriptor.IsGridDefinitionsProperty(property))
                .Select(property => (PropertyDescriptor)new DesignerRemotePropertyDescriptor(
                    property,
                    propertyChanged,
                    gridDefinitionsEditorType))
                .ToList();
            if (useClrGridProperties)
            {
                PropertyDescriptorCollection reflected =
                    TypeDescriptor.GetProperties(GetType());
                foreach (DesignerPropertySnapshot property in snapshot.Properties)
                {
                    if (property.Name == nameof(RowDefinitions))
                    {
                        _rowDefinitions = property.Value;
                        properties.Add(reflected[nameof(RowDefinitions)]!);
                    }
                    else if (property.Name == nameof(ColumnDefinitions))
                    {
                        _columnDefinitions = property.Value;
                        properties.Add(reflected[nameof(ColumnDefinitions)]!);
                    }
                }
            }

            _properties = new PropertyDescriptorCollection(
                properties.ToArray(),
                readOnly: true);
        }

        public DesignerSelectionSnapshot Snapshot { get; }

        [Category("Layout")]
        [DisplayName(nameof(RowDefinitions))]
        public virtual DesignerGridDefinitionValue RowDefinitions
        {
            get => new DesignerGridDefinitionValue(_rowDefinitions);
            set
            {
                _rowDefinitions = value?.SerializedValue;
                _propertyChanged(nameof(RowDefinitions), _rowDefinitions);
            }
        }

        [Category("Layout")]
        [DisplayName(nameof(ColumnDefinitions))]
        public virtual DesignerGridDefinitionValue ColumnDefinitions
        {
            get => new DesignerGridDefinitionValue(_columnDefinitions);
            set
            {
                _columnDefinitions = value?.SerializedValue;
                _propertyChanged(nameof(ColumnDefinitions), _columnDefinitions);
            }
        }

        public AttributeCollection GetAttributes() => AttributeCollection.Empty;

        public string GetClassName() => Snapshot.DisplayName;

        public string GetComponentName() => Snapshot.DisplayName;

        public TypeConverter GetConverter() => new TypeConverter();

        public EventDescriptor? GetDefaultEvent() => null;

        public PropertyDescriptor? GetDefaultProperty() => null;

        public object? GetEditor(Type editorBaseType) => null;

        public EventDescriptorCollection GetEvents() => EventDescriptorCollection.Empty;

        public EventDescriptorCollection GetEvents(Attribute[]? attributes) =>
            EventDescriptorCollection.Empty;

        public PropertyDescriptorCollection GetProperties() => _properties;

        public PropertyDescriptorCollection GetProperties(Attribute[]? attributes) => _properties;

        public object GetPropertyOwner(PropertyDescriptor? propertyDescriptor) => this;

        private sealed class DesignerRemotePropertyDescriptor : PropertyDescriptor
        {
            private static readonly IReadOnlyDictionary<string, Type> SupportedTypes =
                new Dictionary<string, Type>(StringComparer.Ordinal)
                {
                    [typeof(bool).FullName] = typeof(bool),
                    [typeof(byte).FullName] = typeof(byte),
                    [typeof(short).FullName] = typeof(short),
                    [typeof(int).FullName] = typeof(int),
                    [typeof(long).FullName] = typeof(long),
                    [typeof(float).FullName] = typeof(float),
                    [typeof(double).FullName] = typeof(double),
                    [typeof(decimal).FullName] = typeof(decimal),
                    [typeof(string).FullName] = typeof(string)
                };

            private readonly Action<string, string?> _propertyChanged;
            private readonly Type? _editorType;
            private readonly TypeConverter? _converter;
            private string? _value;

            public DesignerRemotePropertyDescriptor(
                DesignerPropertySnapshot property,
                Action<string, string?> propertyChanged,
                Type? gridDefinitionsEditorType)
                : base(
                    property.Name,
                    CreateAttributes(property, gridDefinitionsEditorType))
            {
                _propertyChanged = propertyChanged;
                _editorType = IsGridDefinitionsProperty(property)
                    ? gridDefinitionsEditorType
                    : null;
                _converter = property.EnumValues is { Count: > 0 }
                    ? new DesignerEnumValueConverter(property.EnumValues)
                    : null;
                _value = property.Value;
                PropertyType = _editorType is not null
                    ? typeof(DesignerGridDefinitionValue)
                    : _converter is not null
                        ? typeof(DesignerEnumValue)
                    : SupportedTypes.TryGetValue(property.ValueType, out Type? type)
                        ? type
                        : typeof(string);
            }

            public override Type ComponentType => typeof(DesignerSelectionProxy);

            public override bool IsReadOnly =>
                Attributes[typeof(ReadOnlyAttribute)] is ReadOnlyAttribute attribute &&
                attribute.IsReadOnly;

            public override Type PropertyType { get; }

            public override TypeConverter Converter =>
                _converter ?? base.Converter;

            public override object? GetEditor(Type editorBaseType)
            {
                if (_editorType is not null &&
                    editorBaseType.IsAssignableFrom(_editorType))
                {
                    return Activator.CreateInstance(_editorType);
                }

                return base.GetEditor(editorBaseType);
            }

            public override bool CanResetValue(object component) => !IsReadOnly && _value is not null;

            public override object? GetValue(object? component)
            {
                if (PropertyType == typeof(DesignerGridDefinitionValue))
                {
                    return new DesignerGridDefinitionValue(_value);
                }

                if (PropertyType == typeof(DesignerEnumValue))
                {
                    return new DesignerEnumValue(_value);
                }

                if (_value is null || PropertyType == typeof(string))
                {
                    return _value;
                }

                try
                {
                    return TypeDescriptor.GetConverter(PropertyType)
                        .ConvertFromInvariantString(_value);
                }
                catch (Exception exception) when (
                    exception is FormatException or NotSupportedException)
                {
                    return PropertyType.IsValueType
                        ? Activator.CreateInstance(PropertyType)
                        : null;
                }
            }

            public override void ResetValue(object component) => SetValue(component, null);

            public override void SetValue(object? component, object? value)
            {
                if (IsReadOnly)
                {
                    return;
                }

                string? serialized = value switch
                {
                    null => null,
                    string text => text,
                    DesignerGridDefinitionValue definitions =>
                        definitions.SerializedValue,
                    DesignerEnumValue enumValue => enumValue.Value,
                    IFormattable formattable => formattable.ToString(
                        null,
                        CultureInfo.InvariantCulture),
                    _ => TypeDescriptor.GetConverter(PropertyType)
                        .ConvertToInvariantString(value)
                };
                _value = serialized;
                _propertyChanged(Name, serialized);
                OnValueChanged(component, EventArgs.Empty);
            }

            public override bool ShouldSerializeValue(object component) => false;

            private static Attribute[] CreateAttributes(
                DesignerPropertySnapshot property,
                Type? gridDefinitionsEditorType)
            {
                var attributes = new List<Attribute>
                {
                    new CategoryAttribute(property.Category),
                    new DisplayNameAttribute(property.Name),
                    new ReadOnlyAttribute(property.IsReadOnly)
                };
                if (gridDefinitionsEditorType is not null &&
                    IsGridDefinitionsProperty(property))
                {
                    attributes.Add(new EditorAttribute(
                        gridDefinitionsEditorType.AssemblyQualifiedName!,
                        "System.Drawing.Design.UITypeEditor, System.Drawing"));
                }

                return attributes.ToArray();
            }

            internal static bool IsGridDefinitionsProperty(
                DesignerPropertySnapshot property) =>
                property.Name == "RowDefinitions" ||
                property.Name == "ColumnDefinitions";
        }
    }
}
