// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixTest.Tests.Tools.Candle.Help
{
    using System;
    using WixTest;
    
    /// <summary>
    /// Test how Candle handles the ? switch.
    /// </summary>
    public class HelpTests : WixTests
    {
        [NamedFact]
        [Description("Verify that Candle prints the appropriate help text.")]
        [Priority(1)]
        public void PrintHelp()
        {
            Candle candle = new Candle();
            this.AddExpectedHelpText(candle);
            candle.Run();
        }

        /// <summary>
        /// Add expected help text to candle object.
        /// </summary>
        /// <param name="candle">Candle object.</param>
        private void AddExpectedHelpText(Candle candle)
        {
            candle.ExpectedOutputStrings.Add("usage:  candle.exe [-?] [-nologo] [-out outputFile] sourceFile [sourceFile ...] [@responseFile]");
            candle.ExpectedOutputStrings.Add("-arch      set architecture defaults for package, components, etc.");
            candle.ExpectedOutputStrings.Add("           values: x86, x64, or ia64 (default: x86)");
            candle.ExpectedOutputStrings.Add("-d<name>[=<value>]  define a parameter for the preprocessor");
            candle.ExpectedOutputStrings.Add("-ext <extension>  extension assembly or \"class, assembly\"");
            candle.ExpectedOutputStrings.Add("-fips      enables FIPS compliant algorithms");
            candle.ExpectedOutputStrings.Add("-I<dir>    add to include search path");
            candle.ExpectedOutputStrings.Add("-nologo    skip printing candle logo information");
            candle.ExpectedOutputStrings.Add("-o[ut]     specify output file (default: write to current directory)");
            candle.ExpectedOutputStrings.Add("-p<file>   preprocess to a file (or stdout if no file supplied)");
            candle.ExpectedOutputStrings.Add("-pedantic  show pedantic messages");
            candle.ExpectedOutputStrings.Add("-platform  (deprecated alias for -arch)");
            candle.ExpectedOutputStrings.Add("-sfdvital  suppress marking files as Vital by default (deprecated)");
            candle.ExpectedOutputStrings.Add("-ss        suppress schema validation of documents (performance boost) (deprecated)");
            candle.ExpectedOutputStrings.Add("-sw[N]     suppress all warnings or a specific message ID");
            candle.ExpectedOutputStrings.Add("           (example: -sw1009 -sw1103)");
            candle.ExpectedOutputStrings.Add("-swall     suppress all warnings (deprecated)");
            candle.ExpectedOutputStrings.Add("-trace     show source trace for errors, warnings, and verbose messages (deprecated)");
            candle.ExpectedOutputStrings.Add("-v         verbose output");
            candle.ExpectedOutputStrings.Add("-wx[N]     treat all warnings or a specific message ID as an error");
            candle.ExpectedOutputStrings.Add("           (example: -wx1009 -wx1103)");
            candle.ExpectedOutputStrings.Add("-wxall     treat all warnings as errors (deprecated)");
            candle.ExpectedOutputStrings.Add("-? | -help this help information");
            candle.ExpectedOutputStrings.Add("For more information see: http://wixtoolset.org/");
        }
    }
}
