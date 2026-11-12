using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LeadManagement.Desktop.Services
{
    /// <summary>
    /// Service to integrate Zoom Workplace Phone click-to-dial functionality.
    /// Handles process detection, warm background startup, phone number sanitization,
    /// and click-to-dial dispatch via the zoomphonecall:// and tel:// protocols.
    /// </summary>
    public static class ZoomPhoneService
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        private static readonly string[] PossibleZoomPaths =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zoom", "bin", "Zoom.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zoom", "bin", "Zoom.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zoom", "bin", "Zoom.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Zoom", "bin", "Zoom.exe")
        };

        /// <summary>
        /// Locates the installed Zoom.exe on the user's machine.
        /// </summary>
        public static string? GetZoomExecutablePath()
        {
            foreach (var path in PossibleZoomPaths)
            {
                if (File.Exists(path))
                    return path;
            }
            return null;
        }

        /// <summary>
        /// Checks if any Zoom Workplace process is currently active.
        /// </summary>
        public static bool IsZoomRunning()
        {
            try
            {
                var procs = Process.GetProcessesByName("Zoom");
                return procs.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures Zoom is open in the background or desktop so dialing doesn't suffer cold-start delays.
        /// </summary>
        public static void EnsureZoomRunning()
        {
            try
            {
                if (IsZoomRunning()) return;

                var zoomPath = GetZoomExecutablePath();
                if (!string.IsNullOrEmpty(zoomPath))
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = zoomPath,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    };
                    Process.Start(psi);
                }
            }
            catch
            {
                // Soft-fail: if auto-start fails, the URI handler will still attempt launch on dial
            }
        }

        /// <summary>
        /// Attempts to bring the Zoom Workplace main window into foreground focus.
        /// </summary>
        public static void BringZoomToFront()
        {
            try
            {
                var procs = Process.GetProcessesByName("Zoom");
                foreach (var p in procs)
                {
                    if (p.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(p.MainWindowHandle, SW_RESTORE);
                        SetForegroundWindow(p.MainWindowHandle);
                        break;
                    }
                }
            }
            catch
            {
                // Silently ignore window focus errors
            }
        }

        /// <summary>
        /// Cleans and formats a phone number string for dialer dispatch.
        /// </summary>
        public static string CleanPhoneNumber(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var trimmed = raw.Trim();
            if (trimmed.StartsWith("+"))
            {
                return "+" + Regex.Replace(trimmed.Substring(1), @"[^\d]", "");
            }

            return Regex.Replace(trimmed, @"[^\d]", "");
        }

        /// <summary>
        /// Dispatches a click-to-dial command to Zoom Phone.
        /// Automatically ensures Zoom is running, navigates to the Zoom Phone dialer,
        /// and stages the call.
        /// </summary>
        public static (bool Success, string? ErrorMessage) Dial(string? rawPhoneNumber)
        {
            if (string.IsNullOrWhiteSpace(rawPhoneNumber))
            {
                return (false, "Please provide a valid phone number before calling.");
            }

            var cleaned = CleanPhoneNumber(rawPhoneNumber);
            if (string.IsNullOrEmpty(cleaned) || cleaned.Length < 3)
            {
                return (false, $"'{rawPhoneNumber}' does not contain a valid phone number.");
            }

            // Ensure Zoom is running in the background or desktop
            EnsureZoomRunning();

            // First attempt: registered zoomphonecall protocol
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = $"zoomphonecall:{cleaned}",
                    UseShellExecute = true
                };
                Process.Start(psi);

                // Bring Zoom to the front so the user sees the dialer active
                Task.Delay(300).ContinueWith(_ => BringZoomToFront());
                return (true, null);
            }
            catch (Exception ex)
            {
                // Second attempt fallback: standard tel: protocol
                try
                {
                    var fallbackPsi = new ProcessStartInfo
                    {
                        FileName = $"tel:{cleaned}",
                        UseShellExecute = true
                    };
                    Process.Start(fallbackPsi);
                    Task.Delay(300).ContinueWith(_ => BringZoomToFront());
                    return (true, null);
                }
                catch
                {
                    return (false, $"Could not open Zoom dialer: {ex.Message}");
                }
            }
        }
    }
}

