// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

#if NET
namespace Microsoft.Tools.WindowsInstallerXml.Cab
{
    using System;
    using System.Diagnostics;
    using System.IO;

    internal static class ExternalCabTool
    {
        internal static bool UseExternalCabTools
        {
            get { return '\\' != Path.DirectorySeparatorChar; }
        }

        internal static string FindTool(string environmentVariable, string fileName)
        {
            string configuredPath = Environment.GetEnvironmentVariable(environmentVariable);
            if (!String.IsNullOrEmpty(configuredPath))
            {
                return configuredPath;
            }

            string path = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (String.IsNullOrEmpty(directory))
                {
                    continue;
                }

                string candidate = Path.Combine(directory, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new WixException(WixErrors.FileNotFound(null, fileName));
        }

        internal static int Run(string toolPath, string workingDirectory, params string[] arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(toolPath)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            if (!String.IsNullOrEmpty(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using (Process process = Process.Start(startInfo))
            {
                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }
}
#endif
