using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace LeadManagement.Desktop.Services
{
    /// <summary>
    /// Extracts text and builds rich FlowDocuments from uploaded calling scripts (TXT, DOCX, PDF, RTF, MD).
    /// Preserves Bold, Italic, Underline, Strikethrough, Highlight, Background Shading, and Colors from Word documents.
    /// Works without Microsoft Word or Adobe Reader installed.
    /// </summary>
    public static class ScriptReaderService
    {
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        /// <summary>
        /// Builds a rich FlowDocument for display in a RichTextBox.
        /// </summary>
        public static FlowDocument BuildFlowDocument(string fileName, string? content)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI, Inter, Arial"),
                Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
                Background = Brushes.Transparent
            };

            if (string.IsNullOrWhiteSpace(content))
            {
                var p = new Paragraph(new Run("(The script document is empty)"))
                {
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                doc.Blocks.Add(p);
                return doc;
            }

            string ext = Path.GetExtension(fileName).ToLowerInvariant();

            // 1. DOCX -> Rich XML parsing
            if (ext == ".docx")
            {
                try
                {
                    byte[] bytes = IsBase64(content) ? Convert.FromBase64String(content) : Encoding.UTF8.GetBytes(content);
                    return BuildFlowDocumentFromDocx(bytes);
                }
                catch (Exception ex)
                {
                    var pErr = new Paragraph(new Run($"[Error parsing Word formatting: {ex.Message}]"))
                    {
                        Foreground = Brushes.IndianRed,
                        Margin = new Thickness(0, 0, 0, 12)
                    };
                    doc.Blocks.Add(pErr);
                    // Add plain text fallback
                    string fallback = ExtractText(fileName, content);
                    doc.Blocks.Add(new Paragraph(new Run(fallback)));
                    return doc;
                }
            }

            // 2. Plain Text / PDF / Other -> Build clean plain paragraph blocks
            string text = ExtractText(fileName, content);
            string[] paragraphs = text.Replace("\r\n", "\n").Split(new[] { "\n\n" }, StringSplitOptions.None);

            foreach (var paraText in paragraphs)
            {
                string trimmed = paraText.Trim('\r', '\n');
                if (string.IsNullOrEmpty(trimmed)) continue;

                var p = new Paragraph
                {
                    Margin = new Thickness(0, 0, 0, 10),
                    LineHeight = 22
                };
                p.Inlines.Add(new Run(trimmed));
                doc.Blocks.Add(p);
            }

            if (doc.Blocks.Count == 0)
            {
                doc.Blocks.Add(new Paragraph(new Run("(Empty script)")));
            }

            return doc;
        }

        /// <summary>
        /// Parses OpenXML word/document.xml to construct a WPF FlowDocument with bold, italic, highlights, etc.
        /// </summary>
        private static FlowDocument BuildFlowDocumentFromDocx(byte[] bytes)
        {
            var doc = new FlowDocument
            {
                PagePadding = new Thickness(0),
                FontFamily = new FontFamily("Segoe UI, Inter, Arial"),
                Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
                Background = Brushes.Transparent
            };

            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            if (entry == null)
            {
                doc.Blocks.Add(new Paragraph(new Run("(Empty or unsupported Word document structure)")));
                return doc;
            }

            using var entryStream = entry.Open();
            var xDoc = XDocument.Load(entryStream);

            foreach (var pElem in xDoc.Descendants(W + "p"))
            {
                var para = new Paragraph
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    LineHeight = 22
                };

                // Check paragraph alignment
                var jc = pElem.Element(W + "pPr")?.Element(W + "jc")?.Attribute(W + "val")?.Value;
                if (!string.IsNullOrEmpty(jc))
                {
                    para.TextAlignment = jc.ToLowerInvariant() switch
                    {
                        "center" => TextAlignment.Center,
                        "right" => TextAlignment.Right,
                        "both" => TextAlignment.Justify,
                        _ => TextAlignment.Left
                    };
                }

                // Process all runs and hyperlinks containing runs in order
                bool hasContent = false;
                foreach (var child in pElem.Elements())
                {
                    if (child.Name == W + "r")
                    {
                        if (ProcessRunElement(child, para.Inlines))
                        {
                            hasContent = true;
                        }
                    }
                    else if (child.Name == W + "hyperlink")
                    {
                        foreach (var rChild in child.Elements(W + "r"))
                        {
                            if (ProcessRunElement(rChild, para.Inlines, isHyperlink: true))
                            {
                                hasContent = true;
                            }
                        }
                    }
                }

                if (hasContent)
                {
                    doc.Blocks.Add(para);
                }
                else
                {
                    // Empty paragraph gives natural vertical spacing
                    doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 0, 0, 6) });
                }
            }

            if (doc.Blocks.Count == 0)
            {
                doc.Blocks.Add(new Paragraph(new Run("(Word document contains no extractable text)")));
            }

            return doc;
        }

        private static bool ProcessRunElement(XElement rElem, InlineCollection inlines, bool isHyperlink = false)
        {
            var rPr = rElem.Element(W + "rPr");

            // Extract styles
            bool isBold = CheckBooleanProperty(rPr?.Element(W + "b"));
            bool isItalic = CheckBooleanProperty(rPr?.Element(W + "i"));
            bool isUnderline = rPr?.Element(W + "u") != null &&
                               rPr.Element(W + "u")?.Attribute(W + "val")?.Value != "none";
            bool isStrike = CheckBooleanProperty(rPr?.Element(W + "strike")) ||
                            CheckBooleanProperty(rPr?.Element(W + "dstrike"));

            Brush? backgroundBrush = GetHighlightOrShadingBrush(rPr);
            Brush? foregroundBrush = GetTextColorBrush(rPr);

            if (isHyperlink && foregroundBrush == null)
            {
                foregroundBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
                isUnderline = true;
            }

            bool contentAdded = false;

            // Gather in-run elements (text, tabs, line breaks)
            foreach (var elem in rElem.Elements())
            {
                if (elem.Name == W + "t")
                {
                    string text = elem.Value;
                    if (!string.IsNullOrEmpty(text))
                    {
                        Run run = new Run(text);

                        if (isBold) run.FontWeight = FontWeights.Bold;
                        if (isItalic) run.FontStyle = FontStyles.Italic;

                        if (isUnderline && isStrike)
                        {
                            var decors = new TextDecorationCollection();
                            decors.Add(TextDecorations.Underline);
                            decors.Add(TextDecorations.Strikethrough);
                            run.TextDecorations = decors;
                        }
                        else if (isUnderline)
                        {
                            run.TextDecorations = TextDecorations.Underline;
                        }
                        else if (isStrike)
                        {
                            run.TextDecorations = TextDecorations.Strikethrough;
                        }

                        if (foregroundBrush != null)
                        {
                            run.Foreground = foregroundBrush;
                        }

                        if (backgroundBrush != null)
                        {
                            run.Background = backgroundBrush;
                        }

                        inlines.Add(run);
                        contentAdded = true;
                    }
                }
                else if (elem.Name == W + "tab")
                {
                    inlines.Add(new Run("    "));
                    contentAdded = true;
                }
                else if (elem.Name == W + "br")
                {
                    inlines.Add(new LineBreak());
                    contentAdded = true;
                }
            }

            return contentAdded;
        }

        private static bool CheckBooleanProperty(XElement? elem)
        {
            if (elem == null) return false;
            var val = elem.Attribute(W + "val")?.Value;
            if (val == null) return true; // <w:b/> with no attr means true
            return val != "0" && !val.Equals("false", StringComparison.OrdinalIgnoreCase);
        }

        private static Brush? GetHighlightOrShadingBrush(XElement? rPr)
        {
            if (rPr == null) return null;

            // 1. Highlight element (<w:highlight w:val="yellow"/>)
            var hlElem = rPr.Element(W + "highlight");
            if (hlElem != null)
            {
                string? val = hlElem.Attribute(W + "val")?.Value?.ToLowerInvariant();
                Brush? hlBrush = MapHighlightColor(val);
                if (hlBrush != null) return hlBrush;
            }

            // 2. Shading element (<w:shd w:fill="002060"/> or <w:shd w:fill="FFFF00"/>)
            var shdElem = rPr.Element(W + "shd");
            if (shdElem != null)
            {
                string? fill = shdElem.Attribute(W + "fill")?.Value;
                if (!string.IsNullOrWhiteSpace(fill) && fill != "auto" && fill != "none")
                {
                    var brush = ParseHexColorBrush(fill);
                    if (brush != null) return brush;
                }
            }

            return null;
        }

        private static Brush? MapHighlightColor(string? val)
        {
            if (string.IsNullOrEmpty(val) || val == "none") return null;

            return val switch
            {
                "yellow" => new SolidColorBrush(Color.FromRgb(0xFE, 0xF0, 0x8A)),      // Soft vibrant yellow
                "green" => new SolidColorBrush(Color.FromRgb(0xBB, 0xF7, 0xD0)),       // Light green
                "cyan" => new SolidColorBrush(Color.FromRgb(0xBA, 0xE6, 0xFD)),        // Light cyan/sky
                "magenta" => new SolidColorBrush(Color.FromRgb(0xFB, 0xCF, 0xE8)),     // Soft magenta/pink
                "blue" => new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)),        // Rich blue (like [1] GK Stage)
                "darkblue" => new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A)),    // Deep navy blue
                "red" => new SolidColorBrush(Color.FromRgb(0xFE, 0xCA, 0xCA)),         // Soft red
                "darkred" => new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B)),     // Dark red
                "darkgreen" => new SolidColorBrush(Color.FromRgb(0x16, 0x65, 0x34)),   // Dark green
                "darkyellow" => new SolidColorBrush(Color.FromRgb(0xCA, 0x8A, 0x04)),  // Mustard/dark yellow
                "darkgray" => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),    // Gray
                "lightgray" => new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),   // Light gray
                "black" => new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00)),
                _ => null
            };
        }

        private static Brush? GetTextColorBrush(XElement? rPr)
        {
            if (rPr == null) return null;

            var colorElem = rPr.Element(W + "color");
            if (colorElem != null)
            {
                string? val = colorElem.Attribute(W + "val")?.Value;
                if (!string.IsNullOrWhiteSpace(val) && val != "auto")
                {
                    return ParseHexColorBrush(val);
                }
            }

            return null;
        }

        private static SolidColorBrush? ParseHexColorBrush(string hex)
        {
            hex = hex.Trim().TrimStart('#');
            if (hex.Length == 6)
            {
                if (byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                    byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                    byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
                {
                    var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
                    brush.Freeze();
                    return brush;
                }
            }
            else if (hex.Length == 8)
            {
                if (byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out byte a) &&
                    byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                    byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                    byte.TryParse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber, null, out byte b))
                {
                    var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                    brush.Freeze();
                    return brush;
                }
            }
            return null;
        }

        /// <summary>
        /// Extracts readable plain text from uploaded calling scripts (TXT, DOCX, PDF, RTF, MD).
        /// </summary>
        public static string ExtractText(string fileName, string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return "(The script document is empty)";

            string ext = Path.GetExtension(fileName).ToLowerInvariant();

            // 1. Plain text / Markdown / RTF / CSV
            if (ext == ".txt" || ext == ".md" || ext == ".csv" || ext == ".log")
            {
                if (!IsBase64(content))
                    return content;

                try
                {
                    byte[] raw = Convert.FromBase64String(content);
                    return Encoding.UTF8.GetString(raw);
                }
                catch
                {
                    return content;
                }
            }

            // 2. Word Document (.docx)
            if (ext == ".docx")
            {
                try
                {
                    byte[] bytes = IsBase64(content) ? Convert.FromBase64String(content) : Encoding.UTF8.GetBytes(content);
                    return ExtractTextFromDocx(bytes);
                }
                catch (Exception ex)
                {
                    return $"[Unable to parse Word document formatting: {ex.Message}]\n\nFallback to raw text if available.";
                }
            }

            // 3. PDF Document (.pdf)
            if (ext == ".pdf")
            {
                try
                {
                    byte[] bytes = IsBase64(content) ? Convert.FromBase64String(content) : Encoding.UTF8.GetBytes(content);
                    return ExtractTextFromPdf(bytes);
                }
                catch (Exception ex)
                {
                    return $"[Unable to extract text from PDF document: {ex.Message}]";
                }
            }

            // 4. Default: If not base64, return as string
            if (!IsBase64(content))
                return content;

            try
            {
                byte[] raw = Convert.FromBase64String(content);
                return Encoding.UTF8.GetString(raw);
            }
            catch
            {
                return $"(Binary document: {fileName}. Click 'Open with App' to view in default system viewer.)";
            }
        }

        private static string ExtractTextFromDocx(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            if (entry == null) return "(Empty or unsupported Word document structure)";

            using var entryStream = entry.Open();
            var doc = XDocument.Load(entryStream);

            var sb = new StringBuilder();
            foreach (var para in doc.Descendants(W + "p"))
            {
                var paraText = new StringBuilder();
                foreach (var textElem in para.Descendants(W + "t"))
                {
                    paraText.Append(textElem.Value);
                }

                if (paraText.Length > 0)
                {
                    sb.AppendLine(paraText.ToString());
                    sb.AppendLine();
                }
            }

            string result = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "(Word document contains no extractable text)" : result;
        }

        private static string ExtractTextFromPdf(byte[] bytes)
        {
            using var doc = PdfDocument.Open(bytes);
            var sb = new StringBuilder();

            int pageNumber = 1;
            foreach (var page in doc.GetPages())
            {
                if (doc.NumberOfPages > 1)
                {
                    sb.AppendLine($"--- PAGE {pageNumber} ---");
                }

                string text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.AppendLine(text);
                }
                sb.AppendLine();
                pageNumber++;
            }

            string result = sb.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "(PDF document contains no extractable text or consists purely of scanned images)" : result;
        }

        public static bool IsBase64(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (s.Length % 4 != 0) return false;
            if (s.StartsWith("JVBERi") || s.StartsWith("UEsDB")) return true;
            return (s.Length > 50 && (s.Contains("+") || s.Contains("/") || s.EndsWith("=")));
        }
    }
}
