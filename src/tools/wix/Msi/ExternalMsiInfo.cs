// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

#if NET
namespace Microsoft.Tools.WindowsInstallerXml.Msi
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using Microsoft.Tools.WindowsInstallerXml.Cab;

    /// <summary>
    /// Reads MSI database metadata with msitools on platforms without msi.dll.
    /// </summary>
    internal sealed class ExternalMsiInfo
    {
        private readonly Dictionary<string, List<Dictionary<string, string>>> tables;

        private ExternalMsiInfo()
        {
            this.tables = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
            this.Properties = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        internal int WordCount { get; private set; }

        internal Dictionary<string, string> Properties { get; private set; }

        internal static ExternalMsiInfo Read(string packagePath)
        {
            string msiinfo = ExternalCabTool.FindTool("WIX_MSIINFO_PATH", "msiinfo");
            ExternalMsiInfo info = new ExternalMsiInfo();
            HashSet<string> tableNames = ReadTableNames(msiinfo, packagePath);

            string[] tablesToRead = new string[]
            {
                "_SummaryInformation",
                "Property",
                "Upgrade",
                "Feature",
                "FeatureComponents",
                "Media",
                "Directory",
                "Component",
                "File",
                "WixDependencyProvider",
            };

            foreach (string tableName in tablesToRead)
            {
                if (tableNames.Contains(tableName))
                {
                    List<Dictionary<string, string>> rows = ParseIdt(RunMsiInfo(msiinfo, "export", packagePath, tableName));
                    info.tables.Add(tableName, rows);
                }
            }

            foreach (Dictionary<string, string> row in info.GetTable("_SummaryInformation"))
            {
                if (15 == GetInteger(row, "PropertyId"))
                {
                    info.WordCount = GetInteger(row, "Value");
                    break;
                }
            }

            foreach (Dictionary<string, string> row in info.GetTable("Property"))
            {
                string property = GetString(row, "Property");
                if (!String.IsNullOrEmpty(property))
                {
                    info.Properties[property] = GetString(row, "Value");
                }
            }

            return info;
        }

        internal bool HasTable(string tableName)
        {
            return this.tables.ContainsKey(tableName);
        }

        internal List<Dictionary<string, string>> GetTable(string tableName)
        {
            List<Dictionary<string, string>> rows;
            if (this.tables.TryGetValue(tableName, out rows))
            {
                return rows;
            }

            return new List<Dictionary<string, string>>();
        }

        internal bool HasProperty(string property)
        {
            return this.Properties.ContainsKey(property);
        }

        internal string GetProperty(string property)
        {
            string value;
            this.Properties.TryGetValue(property, out value);
            return NullIfEmpty(value);
        }

        internal static string GetString(Dictionary<string, string> row, string column)
        {
            string value;
            row.TryGetValue(column, out value);
            return NullIfEmpty(value);
        }

        internal static int GetInteger(Dictionary<string, string> row, string column)
        {
            string value = GetString(row, column);
            if (String.IsNullOrEmpty(value))
            {
                return 0;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        internal static int? GetNullableInteger(Dictionary<string, string> row, string column)
        {
            string value = GetString(row, column);
            if (String.IsNullOrEmpty(value))
            {
                return null;
            }

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static string NullIfEmpty(string value)
        {
            return String.IsNullOrEmpty(value) ? null : value;
        }

        private static HashSet<string> ReadTableNames(string msiinfo, string packagePath)
        {
            HashSet<string> tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string output = RunMsiInfo(msiinfo, "tables", packagePath);

            foreach (string line in output.Replace("\r\n", "\n").Split('\n'))
            {
                string tableName = line.Trim();
                if (0 < tableName.Length)
                {
                    tableNames.Add(tableName);
                }
            }

            return tableNames;
        }

        private static List<Dictionary<string, string>> ParseIdt(string output)
        {
            List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();
            string[] lines = output.Replace("\r\n", "\n").Split('\n');
            if (lines.Length < 4)
            {
                return rows;
            }

            string[] columnNames = lines[0].TrimEnd('\r').Split('\t');
            for (int i = 3; i < lines.Length; ++i)
            {
                string line = lines[i].TrimEnd('\r');
                if (0 == line.Length)
                {
                    continue;
                }

                string[] values = line.Split('\t');
                Dictionary<string, string> row = new Dictionary<string, string>(StringComparer.Ordinal);

                for (int j = 0; j < columnNames.Length; ++j)
                {
                    row[columnNames[j]] = j < values.Length ? values[j] : null;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static string RunMsiInfo(string msiinfo, params string[] arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo(msiinfo)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (0 != process.ExitCode)
                {
                    throw new WixException(WixErrors.UnexpectedException(
                        String.Format(CultureInfo.InvariantCulture, "{0} exited with code {1}: {2}", msiinfo, process.ExitCode, error),
                        "InvalidOperationException",
                        Environment.StackTrace));
                }

                return output;
            }
        }
    }
}
#endif
