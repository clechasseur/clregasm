// Params.cs
// (c) 2016, Charles Lechasseur
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using CommandLineArguments;

namespace CL.RegAsm
{
    /// <summary>
    /// Class containing parameters of the CLRegAsm core processing that are
    /// needed before sandboxing in another AppDomain, for command-line
    /// processing. These will be parsed first, before parsing all arguments.
    /// </summary>
    /// <remarks>
    /// Designed to be used with <see cref="CommandLineArguments.Parser"/>.
    /// </remarks>
    [Serializable]
    public class PreSandboxParams
    {
        /// <summary>
        /// If <c>true</c>, CLRegAsm will not print a header before execution.
        /// </summary>
        [Argument(ArgumentType.AtMostOnce, ShortName="n", LongName="nologo", HelpText="Skips copyright information")]
        public bool nologo;

        /// <summary>
        /// If <c>true</c>, CLRegAsm operates in silent mode, not displaying
        /// success or error messages.
        /// </summary>
        [Argument(ArgumentType.AtMostOnce, ShortName="s", LongName="silent", HelpText="Silent mode; no messages are displayed")]
        public bool silent;

        /// <summary>
        /// If <c>true</c>, CLRegAsm will display additional information during
        /// the registration/unregistration process, such as assembly loading.
        /// </summary>
        [Argument(ArgumentType.AtMostOnce, ShortName="v", LongName="verbose", HelpText="Displays more information")]
        public bool verbose;

        /// <summary>
        /// If <c>true</c>, CLRegAsm will display debug information. This is
        /// probably not useful to most users.
        /// </summary>
        [Argument(ArgumentType.AtMostOnce, ShortName="d", LongName="debug", HelpText="Displays debugging information")]
        public bool debug;
        
        /// <summary>
        /// Validates that values in the fields of this object are compatible.
        /// Some command-line arguments can be incompatible.
        /// </summary>
        /// <exception cref="ArgumentException">The value of a field is
        /// incompatible with the value of one or more other fields.</exception>
        public virtual void Validate()
        {
            // No incompatible fields in this class
        }
    }

    /// <summary>
    /// Class containing all parameters of the CLRegAsm core processing, for
    /// command-line processing.
    /// </summary>
    /// <remarks>
    /// Designed to be used with <see cref="CommandLineArguments.Parser"/>.
    /// </remarks>
    [Serializable]
    public sealed class Params : PreSandboxParams
    {
        /// <summary>
        /// Name of the assembly to process.
        /// </summary>
        [DefaultArgument(ArgumentType.AtMostOnce | ArgumentType.Required, LongName="AssemblyName", HelpText="Name of assembly to process")]
        public string assemblyName;

        /// <summary>
        /// Whether to unregister the assembly types instead of registering them.
        /// </summary>
        /// <remarks>
        /// Cannot be combined with <see cref="RegistryFile"/>.
        /// </remarks>
        /// <seealso cref="RegistryFile"/>
        [Argument(ArgumentType.AtMostOnce, ShortName="u", LongName="unregister", HelpText="Unregister assembly types instead of registering them")]
        public bool unregister;

        /// <summary>
        /// Name of the type library to export to. If specified, the assembly types
        /// will be exported to a type library and the resulting TLB will be registered.
        /// </summary>
        /// <remarks>
        /// Cannot be combined with <see cref="RegistryFile"/>. If user did not specify
        /// a type library name, will be set to <see cref="String.Empty"/>.
        /// </remarks>
        /// <seealso cref="RegistryFile"/>
        [Argument(ArgumentType.AtMostOnce | ArgumentType.OptionalValue, UnspecifiedValue="", ShortName="", LongName="tlb", HelpText="Create type library with assembly types and register it")]
        public string typeLibrary;

        /// <summary>
        /// Name of the registry file to export to. If specified, the registry file will contain
        /// all registry changes that would be applied if the assembly types were
        /// registered, but no actual registration occurs.
        /// </summary>
        /// <remarks>
        /// Cannot be combined with <see cref="Unregister"/> or <see cref="TypeLibrary"/>. If user
        /// did not specify a registry file name, will be set to <see cref="String.Empty"/>.
        /// </remarks>
        /// <seealso cref="Unregister"/>
        /// <seealso cref="TypeLibrary"/>
        [Argument(ArgumentType.AtMostOnce | ArgumentType.OptionalValue, UnspecifiedValue="", ShortName="", LongName="regfile", HelpText="Creates a registry file with assembly types instead of registering them. Cannot be combined with /u or /tlb.")]
        public string registryFile;

        /// <summary>
        /// Whether to write the assembly's CodeBase to the registry (or registry file).
        /// </summary>
        [Argument(ArgumentType.AtMostOnce, ShortName="c", LongName="codebase", HelpText="Adds assembly codebase to registry")]
        public bool setCodeBase;

        /// <summary>
        /// Whether to perform per-user registration/unregistration. Assembly types will be registered
        /// in HKEY_CURRENT_USER\Software\Classes instead of HKEY_LOCAL_MACHINE\Software\Classes.
        /// If combined with <see cref="registryFile"/>, the registry file will contain entries pointing
        /// to HKEY_CURRENT_USER\Software\Classes.
        /// </summary>
        [Argument(ArgumentType.AtMostOnce, ShortName="U", LongName="user", HelpText="Registers assembly for current user only. If combined with /regfile, registry file will contain user entries.")]
        public bool perUser;

        /// <summary>
        /// If <c>true</c>, only referenced assemblies that are already registered
        /// will be used.
        /// </summary>
        [Argument(ArgumentType.AtMostOnce, ShortName="", LongName="registered", HelpText="Limit referenced assemblies to assemblies that are already registered")]
        public bool useRegisteredOnly;

        /// <summary>
        /// List of additional directories where to look for referenced assemblies
        /// when loading the assembly to write a regfile.
        /// </summary>
        /// <seealso cref="registryFile"/>
        [Argument(ArgumentType.MultipleUnique, ShortName="", LongName="asmpath", HelpText="Look for referenced assemblies in this directory; use with /regfile only")]
        public string[] assemblyPaths;

        /// <inheritDoc/>
        public override void Validate()
        {
            base.Validate();

            // regfile cannot be combined with unregister or tlb
            if (registryFile != null && unregister) {
                throw new ArgumentException("Cannot combine /regfile with /unregister");
            }
            if (registryFile != null && typeLibrary != null) {
                throw new ArgumentException("Cannot combine /regfile with /tlb");
            }

            // asmpath can only be used with regfile
            if (assemblyPaths != null && assemblyPaths.Length != 0 && registryFile == null) {
                throw new ArgumentException("/asmpath can only be used with /regfile");
            }
        }
    }
}
