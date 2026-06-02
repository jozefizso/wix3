// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace Microsoft.Tools.WindowsInstallerXml.Cab
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using Microsoft.Tools.WindowsInstallerXml.Cab.Interop;

    /// <summary>
    /// Wrapper class around interop with wixcab.dll to extract files from a cabinet.
    /// </summary>
    public sealed class WixExtractCab : IDisposable
    {
        private bool disposed;
#if NET
        private readonly bool useExternalCabTool;
#endif

        /// <summary>
        /// Creates a cabinet extractor.
        /// </summary>
        public WixExtractCab()
        {
#if NET
            this.useExternalCabTool = ExternalCabTool.UseExternalCabTools;
            if (this.useExternalCabTool)
            {
                return;
            }
#endif
            NativeMethods.ExtractCabBegin();
        }

        /// <summary>
        /// Destructor for cabinet extraction.
        /// </summary>
        ~WixExtractCab()
        {
            this.Dispose();
        }

        /// <summary>
        /// Extracts all the files from a cabinet to a directory.
        /// </summary>
        /// <param name="cabinetFile">Cabinet file to extract from.</param>
        /// <param name="extractDir">Directory to extract files to.</param>
        public void Extract(string cabinetFile, string extractDir) 
        {
            if (null == cabinetFile)
            {
                throw new ArgumentNullException("cabinetFile");
            }

            if (null == extractDir)
            {
                throw new ArgumentNullException("extractDir");
            }

            if (this.disposed)
            {
                throw new ObjectDisposedException("WixExtractCab");
            }

#if NET
            if (this.useExternalCabTool)
            {
                Directory.CreateDirectory(extractDir);

                string cabextract = ExternalCabTool.FindTool("WIX_CABEXTRACT_PATH", "cabextract");
                int exitCode = ExternalCabTool.Run(cabextract, null, "-q", "-d", extractDir, cabinetFile);
                if (0 != exitCode)
                {
                    throw new WixException(WixErrors.CabExtractionFailed(cabinetFile, extractDir));
                }

                return;
            }
#endif

            if (!extractDir.EndsWith("\\", StringComparison.Ordinal))
            {
                extractDir = String.Concat(extractDir, "\\");
            }

            NativeMethods.ExtractCab(cabinetFile, extractDir);
        }

        /// <summary>
        /// Disposes the managed and unmanaged objects in this object.
        /// </summary>
        public void Dispose()
        {
            if (!this.disposed)
            {
#if NET
                if (this.useExternalCabTool)
                {
                    GC.SuppressFinalize(this);
                    this.disposed = true;
                    return;
                }
#endif
                NativeMethods.ExtractCabFinish();

                GC.SuppressFinalize(this);
                this.disposed = true;
            }
        }
    }
}
