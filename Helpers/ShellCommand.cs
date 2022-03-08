using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Cliptok.Helpers
{
    internal class ShellCommand
    {
        public static OSPlatform GetOSPlatform()
        {
            // Default to "Unknown" platform.
            OSPlatform osPlatform = OSPlatform.Create("Unknown");

            // Check if it's windows 
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            osPlatform = isWindows ? OSPlatform.Windows : osPlatform;
            // Check if it's osx 
            bool isOSX = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            osPlatform = isOSX ? OSPlatform.OSX : osPlatform;
            // Check if it's Linux 
            bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            osPlatform = isLinux ? OSPlatform.Linux : osPlatform;
            // Check if it's FreeBSD
            bool isBSD = RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD);
            osPlatform = isBSD ? OSPlatform.FreeBSD : osPlatform;
            return osPlatform;
        }

        public static ShellResult RunShellCommand(String command)
        {
            string fileName;
            string arguments;

            string escapedArgs = command.Replace("\"", "\\\"");
            if (GetOSPlatform() == OSPlatform.Windows)
            {
                fileName = Environment.GetEnvironmentVariable("COMSPEC");
                // this shouldnt ever fail but so many people have cursed setups
                if (!System.IO.File.Exists(fileName))
                {
                    fileName = "C:\\Windows\\system32\\cmd.exe";
                }
                arguments = $"/C \"{escapedArgs}\" 2>&1";
            }
            else
            {
                fileName = Environment.GetEnvironmentVariable("SHELL");
                if (!System.IO.File.Exists(fileName))
                {
                    fileName = "/bin/sh";
                }
                arguments = $"-c \"{escapedArgs} 2>&1\"";
            }


            Process proc = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                }
            };

            proc.Start();
            string result = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            return new ShellResult(proc, result);

        }

        public struct ShellResult
        {
            public Process proc;
            public String result;

            public ShellResult(Process proce, String res)
            {
                proc = proce;
                result = res;
            }
        }

    }
}
