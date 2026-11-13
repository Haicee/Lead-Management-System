using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using LeadManagement.Desktop.Models;
using LeadManagement.Desktop.Services;

namespace LeadManagement.Desktop.Views
{
    /// <summary>
    /// Independent, resizable window for previewing client calling scripts.
    /// Supports rich styling from Word (.docx) including Bold, Italic, Underline,
    /// Highlights, Background Shading, and Colors without requiring Microsoft Word installed.
    /// Also supports PDF and plain text documents.
    /// </summary>
    public partial class ScriptViewerWindow : Window
    {
        private readonly ClientModel _client;
        private readonly string _rawContent;
        private readonly string _extractedText;
        private double _currentFontSize = 14;

        public ScriptViewerWindow(ClientModel client, string? rawContent)
        {
            InitializeComponent();
            _client = client;
            _rawContent = rawContent ?? string.Empty;

            // Set Header details
            Title = $"{client.FullName} — Calling Script";
            TxtClientTitle.Text = $"{client.FullName} — Calling Script";
            TxtFileName.Text = string.IsNullOrWhiteSpace(client.ScriptName) ? "Untitled Script" : client.ScriptName;
            TxtFileSize.Text = string.IsNullOrWhiteSpace(client.ScriptSize) ? "" : client.ScriptSize;
            TxtFileType.Text = (Path.GetExtension(client.ScriptName ?? "")).TrimStart('.').ToUpperInvariant();
            if (string.IsNullOrEmpty(TxtFileType.Text)) TxtFileType.Text = "TXT";

            // Extract plain text for search and copy
            _extractedText = ScriptReaderService.ExtractText(client.ScriptName ?? "script.txt", _rawContent);

            // Build and assign rich FlowDocument
            TxtScriptBody.Document = ScriptReaderService.BuildFlowDocument(client.ScriptName ?? "script.docx", _rawContent);
            UpdateFontSize();
        }

        private void BtnPinTop_Checked(object sender, RoutedEventArgs e)
        {
            Topmost = true;
            TxtStatus.Text = "📌 Pinned on Top • Window will stay visible over Zoom and other apps.";
        }

        private void BtnPinTop_Unchecked(object sender, RoutedEventArgs e)
        {
            Topmost = false;
            TxtStatus.Text = "Ready • Unpinned.";
        }

        private void BtnFontInc_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFontSize < 28)
            {
                _currentFontSize += 1.5;
                UpdateFontSize();
            }
        }

        private void BtnFontDec_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFontSize > 10)
            {
                _currentFontSize -= 1.5;
                UpdateFontSize();
            }
        }

        private void UpdateFontSize()
        {
            TxtScriptBody.FontSize = _currentFontSize;
            TxtFontSize.Text = $"{_currentFontSize:0.#}pt";
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtSearch.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(query))
            {
                TxtScriptBody.Selection.Select(TxtScriptBody.Document.ContentStart, TxtScriptBody.Document.ContentStart);
                TxtStatus.Text = "Ready • You can resize or keep this window open while making calls.";
                return;
            }

            TextRange? match = FindTextInDocument(TxtScriptBody.Document.ContentStart, query);
            if (match != null)
            {
                TxtScriptBody.Focus();
                TxtScriptBody.Selection.Select(match.Start, match.End);
                var frameworkElement = match.Start.Parent as FrameworkContentElement;
                frameworkElement?.BringIntoView();
                TxtStatus.Text = $"Found '{query}' in script.";
            }
            else
            {
                TxtStatus.Text = $"'{query}' not found.";
            }
        }

        private TextRange? FindTextInDocument(TextPointer start, string query)
        {
            TextPointer current = start;
            while (current != null && current.CompareTo(TxtScriptBody.Document.ContentEnd) < 0)
            {
                if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = current.GetTextInRun(LogicalDirection.Forward);
                    int index = textRun.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                    if (index >= 0)
                    {
                        TextPointer matchStart = current.GetPositionAtOffset(index);
                        TextPointer matchEnd = current.GetPositionAtOffset(index + query.Length);
                        if (matchStart != null && matchEnd != null)
                        {
                            return new TextRange(matchStart, matchEnd);
                        }
                    }
                }
                current = current.GetNextContextPosition(LogicalDirection.Forward);
            }
            return null;
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = string.Empty;
            TxtStatus.Text = "Ready • You can resize or keep this window open while making calls.";
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string textToCopy = _extractedText;
                if (string.IsNullOrWhiteSpace(textToCopy) && TxtScriptBody.Document != null)
                {
                    var range = new TextRange(TxtScriptBody.Document.ContentStart, TxtScriptBody.Document.ContentEnd);
                    textToCopy = range.Text;
                }

                if (!string.IsNullOrEmpty(textToCopy))
                {
                    Clipboard.SetText(textToCopy);
                    TxtStatus.Text = "✓ Script copied to clipboard!";
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Copy error: {ex.Message}";
            }
        }

        private void BtnOpenExternal_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string fileName = _client.ScriptName ?? "Calling_Script.txt";
                if (string.IsNullOrWhiteSpace(fileName)) fileName = "Calling_Script.txt";

                string tempFolder = Path.Combine(Path.GetTempPath(), "LMS_Scripts");
                Directory.CreateDirectory(tempFolder);
                string tempPath = Path.Combine(tempFolder, fileName);

                string ext = Path.GetExtension(fileName).ToLowerInvariant();
                bool isText = ext == ".txt" || ext == ".md" || ext == ".rtf" || ext == ".csv";

                if (isText)
                {
                    File.WriteAllText(tempPath, _extractedText);
                }
                else
                {
                    byte[] bytes = ScriptReaderService.IsBase64(_rawContent) 
                        ? Convert.FromBase64String(_rawContent) 
                        : System.Text.Encoding.UTF8.GetBytes(_rawContent);
                    File.WriteAllBytes(tempPath, bytes);
                }

                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                TxtStatus.Text = $"Opened in system viewer: '{fileName}'";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Could not open file: {ex.Message}";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
