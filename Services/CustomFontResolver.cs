using PdfSharpCore.Fonts;
using System.Collections.Generic;
using System.IO;

public class CustomFontResolver : IFontResolver
{
    private static readonly string FontFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts");

    public byte[] GetFont(string faceName)
    {
        var filePath = Path.Combine(FontFolder, "NotoSansJP-Regular.ttf"); // ここは使いたいフォントに応じて変更
        return File.ReadAllBytes(filePath);
    }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        return new FontResolverInfo("NotoSansJP");
    }

    public string DefaultFontName => "NotoSansJP";
}
