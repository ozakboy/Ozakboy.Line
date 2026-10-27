using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ozakboy.Line.Messaging.Messages.Flex;

/// <summary>
/// 讓 <see cref="JsonSerializer"/> 走 <see cref="FlexContainer"/> 的明確寫法;讀回時依 <c>type</c> 分派。
/// Routes <see cref="JsonSerializer"/> through <see cref="FlexContainer"/>'s explicit writing; reading dispatches on <c>type</c>.
/// </summary>
internal sealed class FlexContainerJsonConverter : JsonConverter<FlexContainer>
{
    /// <inheritdoc />
    public override FlexContainer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return FlexReader.ReadContainer(document.RootElement);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, FlexContainer value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.WriteTo(writer);
    }
}

/// <summary>
/// 讓每一個具體容器型別都走明確寫法。
/// Routes every concrete container type through explicit writing.
/// </summary>
/// <remarks>
/// <see cref="FlexContainer"/> 上的 <c>[JsonConverter]</c> 不會套用到衍生型別(屬性不被繼承),
/// 直接序列化 <c>new FlexBubble()</c> 會退回反射而寫出 PascalCase 的錯誤 JSON。每個具體型別都掛了這個工廠。
/// The <c>[JsonConverter]</c> on <see cref="FlexContainer"/> does not apply to derived types (the attribute is not
/// inherited), so serialising a <c>new FlexBubble()</c> directly falls back to reflection and writes PascalCase JSON.
/// Every concrete type carries this factory.
/// </remarks>
internal sealed class FlexContainerJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert is not null && typeToConvert != typeof(FlexContainer) && typeof(FlexContainer).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter?)Activator.CreateInstance(typeof(FlexContainerJsonConverter<>).MakeGenericType(typeToConvert));
}

/// <summary>
/// 具體容器型別的轉換器:寫出走明確寫法,讀回依 <c>type</c> 分派後確認型別相符。
/// The converter for a concrete container type: writing is explicit, and reading dispatches on <c>type</c> then checks the type matches.
/// </summary>
/// <typeparam name="T">具體容器型別。The concrete container type.</typeparam>
#pragma warning disable CA1812 // 由工廠以反射具現化,分析器看不出來。Instantiated by reflection from the factory; the analyser cannot see it.
internal sealed class FlexContainerJsonConverter<T> : JsonConverter<T>
    where T : FlexContainer
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return FlexReader.ReadContainer(document.RootElement) as T
            ?? throw new JsonException($"JSON 的 type 與 {typeof(T).Name} 不符,或含有未建模的欄位。The JSON's type does not match {typeof(T).Name}, or it carries unmodelled fields.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.WriteTo(writer);
    }
}

/// <summary>
/// 讓 <see cref="JsonSerializer"/> 走 <see cref="FlexComponent"/> 的明確寫法;讀回時依 <c>type</c> 分派。
/// Routes <see cref="JsonSerializer"/> through <see cref="FlexComponent"/>'s explicit writing; reading dispatches on <c>type</c>.
/// </summary>
internal sealed class FlexComponentJsonConverter : JsonConverter<FlexComponent>
{
    /// <inheritdoc />
    public override FlexComponent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return FlexReader.ReadComponent(document.RootElement);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, FlexComponent value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.WriteTo(writer);
    }
}

/// <summary>
/// 讓每一個具體元件型別都走明確寫法。
/// Routes every concrete component type through explicit writing.
/// </summary>
/// <remarks>
/// 與 <see cref="FlexContainerJsonConverterFactory"/> 同理:屬性不被繼承,漏掛的型別會退回反射而寫出錯誤的 JSON。
/// As with <see cref="FlexContainerJsonConverterFactory"/>: the attribute is not inherited, and a type that misses it
/// falls back to reflection and writes wrong JSON.
/// </remarks>
internal sealed class FlexComponentJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert is not null && typeToConvert != typeof(FlexComponent) && typeof(FlexComponent).IsAssignableFrom(typeToConvert);

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter?)Activator.CreateInstance(typeof(FlexComponentJsonConverter<>).MakeGenericType(typeToConvert));
}

/// <summary>
/// 具體元件型別的轉換器:寫出走明確寫法,讀回依 <c>type</c> 分派後確認型別相符。
/// The converter for a concrete component type: writing is explicit, and reading dispatches on <c>type</c> then checks the type matches.
/// </summary>
/// <typeparam name="T">具體元件型別。The concrete component type.</typeparam>
#pragma warning disable CA1812 // 由工廠以反射具現化,分析器看不出來。Instantiated by reflection from the factory; the analyser cannot see it.
internal sealed class FlexComponentJsonConverter<T> : JsonConverter<T>
    where T : FlexComponent
#pragma warning restore CA1812
{
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return FlexReader.ReadComponent(document.RootElement) as T
            ?? throw new JsonException($"JSON 的 type 與 {typeof(T).Name} 不符,或含有未建模的欄位。The JSON's type does not match {typeof(T).Name}, or it carries unmodelled fields.");
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.WriteTo(writer);
    }
}

/// <summary>
/// 非多型的 Flex 輔助物件(文字片段、漸層、樣式)共用的轉換器基底。
/// The shared converter base for the non-polymorphic Flex helper objects (span, gradient, styles).
/// </summary>
/// <typeparam name="T">物件型別。The object type.</typeparam>
internal abstract class FlexObjectJsonConverter<T> : JsonConverter<T>
    where T : class
{
    private readonly Func<JsonElement, T> _read;
    private readonly Action<T, Utf8JsonWriter> _write;

    /// <summary>
    /// 建立轉換器。
    /// Creates the converter.
    /// </summary>
    /// <param name="read">讀回的方法。Reads the object back.</param>
    /// <param name="write">寫出的方法。Writes the object.</param>
    protected FlexObjectJsonConverter(Func<JsonElement, T> read, Action<T, Utf8JsonWriter> write)
    {
        _read = read;
        _write = write;
    }

    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);

        try
        {
            return _read(document.RootElement);
        }
        catch (FlexNotModelledException exception)
        {
            throw new JsonException(exception.Message, exception);
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        _write(value, writer);
    }
}

/// <summary>
/// <see cref="FlexSpan"/> 的轉換器。
/// The converter for <see cref="FlexSpan"/>.
/// </summary>
internal sealed class FlexSpanJsonConverter : FlexObjectJsonConverter<FlexSpan>
{
    /// <summary>
    /// 建立轉換器。
    /// Creates the converter.
    /// </summary>
    public FlexSpanJsonConverter()
        : base(FlexSpan.Read, static (value, writer) => value.WriteTo(writer))
    {
    }
}

/// <summary>
/// <see cref="FlexLinearGradient"/> 的轉換器。
/// The converter for <see cref="FlexLinearGradient"/>.
/// </summary>
internal sealed class FlexLinearGradientJsonConverter : FlexObjectJsonConverter<FlexLinearGradient>
{
    /// <summary>
    /// 建立轉換器。
    /// Creates the converter.
    /// </summary>
    public FlexLinearGradientJsonConverter()
        : base(FlexLinearGradient.Read, static (value, writer) => value.WriteTo(writer))
    {
    }
}

/// <summary>
/// <see cref="FlexBlockStyle"/> 的轉換器。
/// The converter for <see cref="FlexBlockStyle"/>.
/// </summary>
internal sealed class FlexBlockStyleJsonConverter : FlexObjectJsonConverter<FlexBlockStyle>
{
    /// <summary>
    /// 建立轉換器。
    /// Creates the converter.
    /// </summary>
    public FlexBlockStyleJsonConverter()
        : base(FlexBlockStyle.Read, static (value, writer) => value.WriteTo(writer))
    {
    }
}

/// <summary>
/// <see cref="FlexBubbleStyles"/> 的轉換器。
/// The converter for <see cref="FlexBubbleStyles"/>.
/// </summary>
internal sealed class FlexBubbleStylesJsonConverter : FlexObjectJsonConverter<FlexBubbleStyles>
{
    /// <summary>
    /// 建立轉換器。
    /// Creates the converter.
    /// </summary>
    public FlexBubbleStylesJsonConverter()
        : base(FlexBubbleStyles.Read, static (value, writer) => value.WriteTo(writer))
    {
    }
}
