// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

#if NET
namespace Microsoft.Tools.WindowsInstallerXml.Msi
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using Microsoft.Tools.WindowsInstallerXml.Cab;
    using Microsoft.Tools.WindowsInstallerXml.Msi.Interop;

    /// <summary>
    /// Builds MSI databases with msitools on platforms without msi.dll.
    /// </summary>
    internal static class ExternalMsiBuilder
    {
        internal static bool UseExternalMsiTools
        {
            get { return '\\' != Path.DirectorySeparatorChar; }
        }

        internal static bool CanBuild(Output output)
        {
            return OutputType.Product == output.Type && 0 == output.SubStorages.Count;
        }

        internal static void GenerateDatabase(Output output, string databaseFile, int codepage, IMessageHandler messageHandler, string baseDirectory, bool keepAddedColumns)
        {
            string msibuild = ExternalCabTool.FindTool("WIX_MSIBUILD_PATH", "msibuild");
            string databaseDirectory = Path.GetDirectoryName(databaseFile);

            if (!String.IsNullOrEmpty(databaseDirectory))
            {
                Directory.CreateDirectory(databaseDirectory);
            }

            Directory.CreateDirectory(baseDirectory);

            if (File.Exists(databaseFile))
            {
                File.Delete(databaseFile);
            }

            foreach (Table table in output.Tables)
            {
                bool hasObjectColumn = TableHasObjectColumn(table);

                if (table.Definition.IsUnreal && "_Streams" != table.Name)
                {
                    continue;
                }

                if ("_Streams" != table.Name)
                {
                    if (hasObjectColumn)
                    {
                        StageObjectFiles(table, messageHandler, baseDirectory);
                    }

                    string idtPath = WriteIdtFile(codepage, messageHandler, table, baseDirectory, keepAddedColumns, hasObjectColumn ? ObjectFieldHandling.ImportName : ObjectFieldHandling.Normal);
                    RunMsiBuild(msibuild, baseDirectory, databaseFile, "-i", idtPath);
                }

                if ("_Streams" == table.Name && hasObjectColumn)
                {
                    AddStreams(msibuild, databaseFile, table, messageHandler, baseDirectory);
                    table.Rows.Clear();
                }
            }
        }

        private enum ObjectFieldHandling
        {
            Normal,
            ImportName,
        }

        private static bool TableHasObjectColumn(Table table)
        {
            foreach (ColumnDefinition columnDefinition in table.Definition.Columns)
            {
                if (ColumnType.Object == columnDefinition.Type)
                {
                    return true;
                }
            }

            return false;
        }

        private static string WriteIdtFile(int codepage, IMessageHandler messageHandler, Table table, string baseDirectory, bool keepAddedColumns, ObjectFieldHandling objectFieldHandling)
        {
            string idtPath = Path.Combine(baseDirectory, String.Concat(table.Name, ".idt"));
            Encoding encoding;

            if (Encoding.UTF8.CodePage == codepage)
            {
                encoding = new UTF8Encoding(false, true);
            }
            else
            {
                if (0 == codepage)
                {
                    codepage = Encoding.ASCII.CodePage;
                }

                encoding = Encoding.GetEncoding(codepage, new EncoderExceptionFallback(), new DecoderExceptionFallback());
            }

            using (StreamWriter writer = new StreamWriter(idtPath, false, encoding))
            {
                WriteIdtDefinition(writer, messageHandler, table, keepAddedColumns, objectFieldHandling);
            }

            return idtPath;
        }

        [SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "This method writes IDT format control characters.")]
        private static void WriteIdtDefinition(StreamWriter writer, IMessageHandler messageHandler, Table table, bool keepAddedColumns, ObjectFieldHandling objectFieldHandling)
        {
            byte[] rowBytes;
            Encoding convertEncoding = Encoding.GetEncoding(writer.Encoding.CodePage);

            if (table.Definition.IsUnreal)
            {
                return;
            }

            if (TableDefinition.MaxColumnsInRealTable < table.Definition.Columns.Count)
            {
                throw new WixException(WixErrors.TooManyColumnsInRealTable(table.Definition.Name, table.Definition.Columns.Count, TableDefinition.MaxColumnsInRealTable));
            }

            writer.Write(table.Definition.ToIdtDefinition(keepAddedColumns));
            writer.Flush();

            BufferedStream bufferedStream = new BufferedStream(writer.BaseStream);
            foreach (Row row in table.Rows)
            {
                string rowString = RowToIdtDefinition(table, row, keepAddedColumns, objectFieldHandling);

                try
                {
                    rowBytes = writer.Encoding.GetBytes(rowString);
                }
                catch (EncoderFallbackException)
                {
                    rowBytes = convertEncoding.GetBytes(rowString);
                    messageHandler.OnMessage(WixErrors.InvalidStringForCodepage(row.SourceLineNumbers, Convert.ToString(writer.Encoding.WindowsCodePage, CultureInfo.InvariantCulture)));
                }

                bufferedStream.Write(rowBytes, 0, rowBytes.Length);
            }

            bufferedStream.Flush();
        }

        private static string RowToIdtDefinition(Table table, Row row, bool keepAddedColumns, ObjectFieldHandling objectFieldHandling)
        {
            bool first = true;
            StringBuilder idtRow = new StringBuilder();
            string objectImportName = ObjectFieldHandling.ImportName == objectFieldHandling ? GetStreamNameSuffix(table, row).ToString() : null;

            foreach (Field field in row.Fields)
            {
                if (field.Column.Added && !keepAddedColumns)
                {
                    break;
                }

                if (first)
                {
                    first = false;
                }
                else
                {
                    idtRow.Append('\t');
                }

                if (ColumnType.Object == field.Column.Type && ObjectFieldHandling.ImportName == objectFieldHandling)
                {
                    if (null != field.Data)
                    {
                        idtRow.Append(ToIdtValue(field.Column, objectImportName));
                    }
                }
                else
                {
                    idtRow.Append(field.ToIdtValue());
                }
            }

            idtRow.Append("\r\n");
            return idtRow.ToString();
        }

        private static void StageObjectFiles(Table table, IMessageHandler messageHandler, string workingDirectory)
        {
            foreach (Row row in table.Rows)
            {
                StringBuilder streamName = GetStreamName(table, row);
                bool needStream = RowNeedsStream(table, row);

                if (!needStream)
                {
                    continue;
                }

                if (MsiInterop.MsiMaxStreamNameLength < streamName.Length)
                {
                    messageHandler.OnMessage(WixErrors.StreamNameTooLong(row.SourceLineNumbers, table.Name, streamName.ToString(), streamName.Length));
                    continue;
                }

                string objectImportName = GetStreamNameSuffix(table, row).ToString();
                string targetPath = Path.Combine(Path.Combine(workingDirectory, table.Name), objectImportName);
                string targetDirectory = Path.GetDirectoryName(targetPath);

                if (!String.IsNullOrEmpty(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                for (int i = 0; i < table.Definition.Columns.Count; i++)
                {
                    if (ColumnType.Object == table.Definition.Columns[i].Type && null != row[i])
                    {
                        string sourcePath = (string)row[i];

                        if (!File.Exists(sourcePath))
                        {
                            throw new WixException(WixErrors.FileNotFound(row.SourceLineNumbers, sourcePath));
                        }

                        File.Copy(sourcePath, targetPath, true);
                    }
                }
            }
        }

        private static bool RowNeedsStream(Table table, Row row)
        {
            for (int i = 0; i < table.Definition.Columns.Count; i++)
            {
                if (ColumnType.Object == table.Definition.Columns[i].Type && null != row[i])
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddStreams(string msibuild, string databaseFile, Table table, IMessageHandler messageHandler, string workingDirectory)
        {
            foreach (Row row in table.Rows)
            {
                StringBuilder streamName = GetStreamName(table, row);
                bool needStream = RowNeedsStream(table, row);

                if (!needStream)
                {
                    continue;
                }

                if (MsiInterop.MsiMaxStreamNameLength < streamName.Length)
                {
                    messageHandler.OnMessage(WixErrors.StreamNameTooLong(row.SourceLineNumbers, table.Name, streamName.ToString(), streamName.Length));
                    continue;
                }

                for (int i = 0; i < table.Definition.Columns.Count; i++)
                {
                    if (ColumnType.Object == table.Definition.Columns[i].Type && null != row[i])
                    {
                        RunMsiBuild(msibuild, workingDirectory, databaseFile, "-a", streamName.ToString(), (string)row[i]);
                    }
                }
            }
        }

        private static StringBuilder GetStreamName(Table table, Row row)
        {
            StringBuilder streamName = new StringBuilder();

            if ("_Streams" != table.Name)
            {
                streamName.Append(table.Name);
            }

            StringBuilder suffix = GetStreamNameSuffix(table, row);

            if (0 < suffix.Length)
            {
                if (0 < streamName.Length)
                {
                    streamName.Append(".");
                }

                streamName.Append(suffix);
            }

            return streamName;
        }

        private static StringBuilder GetStreamNameSuffix(Table table, Row row)
        {
            StringBuilder suffix = new StringBuilder();

            for (int i = 0; i < table.Definition.Columns.Count; i++)
            {
                ColumnDefinition columnDefinition = table.Definition.Columns[i];

                switch (columnDefinition.Type)
                {
                    case ColumnType.Localized:
                    case ColumnType.Preserved:
                    case ColumnType.String:
                        if (columnDefinition.IsPrimaryKey)
                        {
                            if (0 < suffix.Length)
                            {
                                suffix.Append(".");
                            }

                            suffix.Append((string)row[i]);
                        }
                        break;
                }
            }

            return suffix;
        }

        private static string ToIdtValue(ColumnDefinition columnDefinition, string value)
        {
            if (null != value && columnDefinition.EscapeIdtCharacters)
            {
                value = value.Replace('\t', '\x10');
                value = value.Replace('\r', '\x11');
                value = value.Replace('\n', '\x19');
            }

            return value;
        }

        private static void RunMsiBuild(string msibuild, string workingDirectory, string databaseFile, params string[] arguments)
        {
            string[] allArguments = new string[arguments.Length + 1];
            allArguments[0] = databaseFile;
            Array.Copy(arguments, 0, allArguments, 1, arguments.Length);

            int exitCode = ExternalCabTool.Run(msibuild, workingDirectory, allArguments);
            if (0 != exitCode)
            {
                throw new WixException(WixErrors.UnexpectedException(
                    String.Format(CultureInfo.InvariantCulture, "{0} exited with code {1} while building '{2}'.", Path.GetFileName(msibuild), exitCode, databaseFile),
                    "InvalidOperationException",
                    Environment.StackTrace));
            }
        }
    }
}
#endif
