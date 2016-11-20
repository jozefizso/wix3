// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

using System.Runtime.CompilerServices;
using Xunit.Extensions;

namespace WixTest.Tests.Tools.Common.Logo
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using WixTest;
    using Xunit;

    /// <summary>
    /// Test how different Wix tools handle the NoLogo switch.
    /// </summary>
    public class LogoTests : WixTests
    {
        private static readonly string LogoOutputRegexString = @"Windows\ Installer\ XML\ Toolset\ {0}\ version\ 3\.\d+\.\d+.\d+" + Environment.NewLine + Regex.Escape("Copyright (c) .NET Foundation and contributors. All rights reserved.");

        public static IEnumerable<object[]> WixToolsData
        {
            get
            {
                // force call to static WixTestBase() constructor to ensure Settings.WixToolsDirectory is set
                RuntimeHelpers.RunClassConstructor(typeof(WixTestBase).TypeHandle);

                return new[]
                {
                    new object[] {new Candle()},
                    new object[] {new Dark()},
                    new object[] {new Light()},
                    new object[] {new Lit()},
                    new object[] {new Pyro()},
                    new object[] {new Smoke()},
                    new object[] {new Torch()}
                };
            }
        }

        [Description("Verify that different Wix tools print the Logo information.")]
        [Priority(2)]
        [Theory]
        [PropertyData(nameof(WixToolsData))]
        public void PrintLogo(WixTool wixTool)
        {
            wixTool.NoLogo = false;
            wixTool.SetOutputFileIfNotSpecified = false;
            wixTool.ExpectedOutputRegexs.Add(new Regex(string.Format(LogoTests.LogoOutputRegexString, Regex.Escape(wixTool.ToolDescription))));
            wixTool.Run();
        }

        [Description("Verify that different Wix tools do not print the Logo information.")]
        [Priority(2)]
        [Theory]
        [PropertyData(nameof(WixToolsData))]
        public void PrintWithoutLogo(WixTool wixTool)
        {
            bool missingLogo = false;
            string errorMessage = string.Empty;

            wixTool.NoLogo = true;
            wixTool.SetOutputFileIfNotSpecified = false;

            Result result = wixTool.Run();

            Regex LogoOutputRegex = new Regex("(.)*" + string.Format(LogoTests.LogoOutputRegexString, Regex.Escape(wixTool.ToolDescription)) + "(.)*");

            if (LogoOutputRegex.IsMatch(result.StandardOutput))
            {
                missingLogo = true;
                errorMessage += string.Format("Wix Tool {0} prints the Logo information with -nologo set.{1}", wixTool.ToolDescription, Environment.NewLine);
            }

            Assert.False(missingLogo, errorMessage);
        }


        //[NamedFact]
        [Description("Verify that logo is printed before any other warnings/messages.")]
        [Priority(2)]
        [Theory]
        [PropertyData(nameof(WixToolsData))]
        public void LogoPrintingOrder(WixTool wixTool)
        {
            bool missingLogo = false;
            string errorMessage = string.Empty;

            wixTool.NoLogo = false;
            wixTool.SetOutputFileIfNotSpecified = false;
            wixTool.OtherArguments = " -InvalidCommandLineArgument";
            Result result = wixTool.Run();
            Regex LogoOutputRegex = new Regex(string.Format(LogoTests.LogoOutputRegexString, Regex.Escape(wixTool.ToolDescription)) + "(.)*");

            if (!LogoOutputRegex.IsMatch(result.StandardOutput))
            {
                missingLogo = true;
                errorMessage += string.Format("Wix Tool {0} Logo information does not show as the first line with -nologo set.{1}", wixTool.ToolDescription, Environment.NewLine);
            }

            Assert.False(missingLogo, errorMessage);
        }
    }
}
