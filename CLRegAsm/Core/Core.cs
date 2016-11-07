// Core.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using CL.RegAsm.Utils;
using Microsoft.Win32;

namespace CL.RegAsm
{
    /// <summary>
    /// Core class of the CLRegAsm program. Parses command-line arguments and
    /// performs registration/unregistration tasks. Called by all versions of
    /// CLRegAsm, regardless of the target .NET Framework.
    /// </summary>
    internal sealed class Core : MarshalByRefObject
    {
        /// Value of the MAX_PATH constant from Windows headers. Represents
        /// the maximum size of a path that can be returned by most Win32 file functions.
        private const int MAX_PATH = 260;
        
        /// REGKIND enum used for LoadTypeLibEx.
        private enum REGKIND
        {
            REGKIND_DEFAULT,
            REGKIND_REGISTER,
            REGKIND_NONE,
        }

        /// Win32 function to search for a file on the PATH and the current folder.
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        private static extern int SearchPath(string path, string fileName, string extension,
            int numBufferChars, StringBuilder buffer, int[] filePart);

        /// Win32 function to load a type library from a file.
        [DllImport("oleaut32.dll", CharSet=CharSet.Unicode, PreserveSig=false)]
        private static extern void LoadTypeLibEx(string typeLibName, REGKIND regKind, out ITypeLib typeLib);

        /// Parameters of the current sandboxed run.
        private Params prms;

        /// Console to use for output. Will use params to determine what to output.
        private CLConsole console;

        /// Assembly that we're working with. Loaded during the sandboxed run.
        private Assembly assembly;

        /// Registration services helper object.
        private RegistrationServices regServices;

        /// <summary>
        /// Runs the ClpArgAsm registration/unregistration tasks according to the
        /// given program arguments.
        /// </summary>
        /// <param name="args">Program arguments.</param>
        /// <returns>Program exit code. 0 if everything went well.</returns>
        public static int Run(string[] args)
        {
            // Assume everything will work.
            int exitCode = 0;

            // First parse pre-sandbox arguments to have access to parameters
            // needed before anything else.
            PreSandboxParams preSandboxPrms = new PreSandboxParams();
            CommandLineArguments.Parser.ParseArguments(args, preSandboxPrms, msg => { });

            // Create console to use in this method to output according to params.
            CLConsole console = new CLConsole(preSandboxPrms);

            // Show copyright information except if user told us not to.
            if (!preSandboxPrms.nologo) {
                ShowCopyright(console);
            }

            try {
                // Now parse all parameters and check if user wants help. If parameter issues
                // are encountered, they will be displayed here.
                Params prms = new Params();
                bool wantsHelp = CommandLineArguments.Parser.ParseHelp(args);
                bool validParams = CommandLineArguments.Parser.ParseArguments(args, prms);
                if (!wantsHelp && validParams) {
                    // Create a new AppDomain to perform registration/unregistration tasks,
                    // so that if something goes horribly wrong in the assembly we'll be loading,
                    // we won't be affected by it. Set the ApplicationBase to the current directory
                    // from where we've been invoked, since it might contain dependencies.
                    AppDomain domain = AppDomain.CreateDomain("CLRegAsm.Core", null,
                        new AppDomainSetup { ApplicationBase = Environment.CurrentDirectory });

                    // Create an instance of this class to execute in the new AppDomain.
                    Core sandboxedCore = (Core) domain.CreateInstanceFromAndUnwrap(
                        Assembly.GetExecutingAssembly().CodeBase, typeof(Core).FullName);
                    if (sandboxedCore == null) {
                        throw new CoreException(String.Format("Type \"{0}\" not found when trying to " +
                            "create it in sandboxed AppDomain", typeof(Core).FullName));
                    }

                    // Execute in sandboxed AppDomain.
                    exitCode = sandboxedCore.RunSandboxed(args, prms);
                } else {
                    // Display usage information.
                    if (!validParams) {
                        console.WriteLine();
                    }
                    ShowUsage(console);
                }
            } catch (Exception e) {
                // An error occured. Print it to stderr, then return
                // a non-zero error code to indicate the error.
                console.PrintException(e);
                exitCode = 1;
            }
            
            return exitCode;
        }

        /// <summary>
        /// Runs the CLRegAsm registration/unregistration tasks in a sandboxed
        /// <see cref="AppDomain"/>. Called from <see cref="Core.Run(string[])"/>.
        /// </summary>
        /// <param name="sandboxedArgs">Raw program arguments. Use only to redirect to
        /// other executables; otherwise, use <paramref name="sandboxPrms"/></param>
        /// <param name="sandboxedPrms">Sandboxed <see cref="Params"/> object
        /// determining run parameters.</param>
        /// <returns>Exit code to return from the CLRegAsm tool.</returns>
        /// <exception cref="ArgumentNullException">Either <paramref name="sandboxedArgs"/>
        /// or <paramref name="sandboxedPrms"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Could not locate the assembly
        /// specified in <paramref name="sandboxPrms"/>.
        /// 
        /// OR
        /// 
        /// Registry file specified in <paramref name="sandboxPrms"/>
        /// overwrites the assembly file.
        /// 
        /// OR
        /// 
        /// Type library file specified in <paramref name="sandboxPrms"/>
        /// but assembly file has an embedded type library.
        /// 
        /// OR
        /// 
        /// Type library file specified in <paramref name="sandboxPrms"/>
        /// overwrites the assembly file.</exception>
        /// <exception cref="CoreException">The assembly specified in
        /// <paramref name="sandboxPrms"/> has an invalid format.
        /// (InnerException will then be a <see cref="BadImageFormatException"/>)
        /// 
        /// OR
        /// 
        /// The assembly specified in <paramref name="sandboxPrms"/>
        /// could not be found. (InnerException will then be a
        /// <see cref="FileNotFoundException"/>)
        /// 
        /// OR
        /// 
        /// Access was denied to the registry keys that need to be updated.
        /// (InnerException will then be a <see cref="UnauthorizedAccessException"/>)
        /// 
        /// OR
        /// 
        /// An exception was thrown from a user function in the assembly
        /// specified in <paramref name="sandboxPrms"/>. (InnerException will then
        /// be a <see cref="TargetInvocationException"/>)
        /// 
        /// OR
        /// 
        /// An exception was thrown in a static constructor in the assembly
        /// specified in <paramref name="sandboxPrms"/>. (InnerException will then
        /// be a <see cref="ReflectionTypeLoadException"/>)</exception>
        private int RunSandboxed(string[] sandboxedArgs, Params sandboxedPrms)
        {
            // This will be changed if an error occurs.
            int exitCode = 0;

            // Make sure params are valid and save them in instance variable.
            if (sandboxedArgs == null) {
                throw new ArgumentNullException("sandboxedArgs");
            }
            if (sandboxedPrms == null) {
                throw new ArgumentNullException("sandboxedPrms");
            }
            sandboxedPrms.Validate();
            prms = sandboxedPrms;

            // Create our console object to use for output.
            console = new CLConsole(prms);

            // Validate assembly path.
            string fullAssemblyPath = Path.GetFullPath(prms.assemblyName);
            if (File.Exists(fullAssemblyPath)) {
                // Found the assembly easily, take a note of its absolute path.
                prms.assemblyName = fullAssemblyPath;
            } else {
                // Assembly not found - perhaps user specified a filename only.
                // Let's try to locate this in the current folder or on the PATH.
                StringBuilder buffer = new StringBuilder(MAX_PATH);
                if (SearchPath(null, prms.assemblyName, null, buffer.Capacity + 1, buffer, null) != 0) {
                    // Found the assembly by search, update params.
                    prms.assemblyName = buffer.ToString();
                } else {
                    // We really can't find the assembly; bail out.
                    throw new ArgumentException(String.Format("Could not locate assembly \"{0}\"",
                        prms.assemblyName), "assemblyName");
                }
            }
            // Also convert it to its full path.
            prms.assemblyName = new FileInfo(prms.assemblyName).FullName;

            // Compute path of regfile.
            if (prms.registryFile != null) {
                if (prms.registryFile.Length == 0) {
                    // Use assembly name, replacing extension with .reg.
                    prms.registryFile = Path.ChangeExtension(prms.assemblyName, ".reg");
                    Debug.Assert(Directory.Exists(Path.GetDirectoryName(prms.registryFile)));
                } else {
                    // Get full path to specified regfile.
                    prms.registryFile = new FileInfo(prms.registryFile).FullName;

                    // Make sure regfile path is different from assembly path.
                    if (String.Compare(prms.registryFile, prms.assemblyName, true) == 0) {
                        throw new ArgumentException(String.Format("Registry file \"{0}\" " +
                            "overwrites assembly file", prms.registryFile));
                    }

                    // Create directory that will contain regfile if it doesn't exist.
                    Directory.CreateDirectory(Path.GetDirectoryName(prms.registryFile));
                }
            }

            // Compute path of TLB according to user-provided value and whether
            // the assembly has an embedded TLB.
            if (prms.typeLibrary != null) {
                if (prms.typeLibrary.Length == 0) {
                    // TLB name not specified. If assembly has an embedded TLB,
                    // user assembly name as TLB name. Otherwise, compute a TLB
                    // name using the assembly name, ending with .tlb.
                    if (AssemblyHasEmbeddedTypeLibrary(prms.assemblyName)) {
                        prms.typeLibrary = prms.assemblyName;
                    } else {
                        prms.typeLibrary = Path.ChangeExtension(prms.assemblyName, ".tlb");
                    }
                    Debug.Assert(Directory.Exists(Path.GetDirectoryName(prms.typeLibrary)));
                } else {
                    if (AssemblyHasEmbeddedTypeLibrary(prms.assemblyName)) {
                        // Specifying a TLB name is invalid if assembly has an embedded TLB.
                        throw new ArgumentException("Assembly \"{0}\" has an embedded type library; " +
                            "cannot specify a new type library name.");
                    }

                    // Get full path to TLB file. If user did not specify a full path,
                    // assume file will sit in the same directory as the assembly.
                    if (Path.GetDirectoryName(prms.typeLibrary).Length != 0) {
                        prms.typeLibrary = new FileInfo(prms.typeLibrary).FullName;
                    } else {
                        prms.typeLibrary = Path.Combine(Path.GetDirectoryName(prms.assemblyName), prms.typeLibrary);
                    }

                    // Make sure TLB path is different from assembly path.
                    if (String.Compare(prms.typeLibrary, prms.assemblyName, true) == 0) {
                        throw new ArgumentException(String.Format("Type library \"{0}\" " +
                            "overwrites assembly file", prms.typeLibrary));
                    }

                    // Create TLB directory if it doesn't exist.
                    Directory.CreateDirectory(Path.GetDirectoryName(prms.typeLibrary));
                }
            }

            // Set up context, which will take care of per-user registration/unregistration.
            using (Context context = new Context(prms.perUser && prms.registryFile == null)) {
                try {
                    // Load the assembly we're going to be working with.
                    // If we load it to generate a regfile, load it for reflection only.
                    Debug.Assert(assembly == null);
                    try {
                        if (prms.registryFile == null) {
                            assembly = Assembly.LoadFrom(prms.assemblyName);
                        } else {
                            assembly = Assembly.ReflectionOnlyLoadFrom(prms.assemblyName);
                        }
                    } catch (BadImageFormatException bife) {
                        throw new CoreException(String.Format("Invalid assembly: \"{0}\"", prms.assemblyName), bife);
                    } catch (FileNotFoundException fnfe) {
                        throw new CoreException(String.Format("Could not locate assembly \"{0}\"", prms.assemblyName), fnfe);
                    }

                    // If user asked for the codebase option, check if the assembly has a strong name.
                    // If it doesn't, it's not fatal but we output a warning.
                    if (prms.setCodeBase && assembly.GetName().GetPublicKey().Length == 0) {
                        console.WriteLine("Warning: the /codebase option was meant to be used with strong-named assemblies only.");
                    }

                    // Create registration services helper object. We need it for just about everything.
                    Debug.Assert(regServices == null);
                    regServices = new RegistrationServices();

                    // Here we switch depending on what we need to perform.
                    if (prms.registryFile == null) {
                        // Check if we register or unregister.
                        if (!prms.unregister) {
                            // Need to register assembly.
                            try {
                                AssemblyRegistrationFlags regFlags;
                                if (prms.setCodeBase) {
                                    regFlags = AssemblyRegistrationFlags.SetCodeBase;
                                } else {
                                    regFlags = AssemblyRegistrationFlags.None;
                                }
                                bool registered = regServices.RegisterAssembly(assembly, regFlags);
                                if (registered) {
                                    console.WriteLine("Assembly \"{0}\" registered successfully.", prms.assemblyName);
                                } else {
                                    console.WriteLine("Assembly \"{0}\" contains no registrable types.", prms.assemblyName);
                                }
                            } catch (UnauthorizedAccessException uae) {
                                // User doesn't have access to the needed registry keys.
                                throw new CoreException("Access denied while trying to update registry " +
                                    "during assembly registration", uae);
                            }

                            // Also export and register type library if needed.
                            if (prms.typeLibrary != null) {
                                ExportAndRegisterTypeLibrary();
                            }
                        } else {
                            // Need to unregister assembly. setCodeBase doesn't matter here.
                            try {
                                bool unregistered = regServices.UnregisterAssembly(assembly);
                                if (unregistered) {
                                    console.WriteLine("Assembly \"{0}\" unregistered successfully.", prms.assemblyName);
                                } else {
                                    console.WriteLine("Assembly \"{0}\" contains no unregistrable types.", prms.assemblyName);
                                }
                            } catch (UnauthorizedAccessException uae) {
                                // User doesn't have access to the needed registry keys.
                                throw new CoreException("Access denied while trying to update registry " +
                                    "during assembly unregistration", uae);
                            }

                            // Also unregister type library if needed.
                            if (prms.typeLibrary != null) {
                                UnregisterTypeLibrary();
                            }
                        }
                    } else {
                        // User asked us to generate regfile.

                        // Hook our delegate to the ReflectionOnlyAssemblyResolve of the
                        // sandboxed AppDomain. It will be called to load references of our assembly.
                        AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += ResolveReflectionOnlyAssembly;
                        try {
                            // Generate the regfile. Will use instance members to get information.
                            exitCode = GenerateRegistryFile();
                        } finally {
                            // Unhook from the ReflectionOnlyAssemblyResolve now that we no longer need it.
                            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= ResolveReflectionOnlyAssembly;
                        }
                    }
                } catch (TargetInvocationException tie) {
                    // Error in a user-defined function during assembly registration/unregistration
                    // (for example, in a ComRegisterFunction).
                    throw new CoreException(String.Format("Error in user-defined function from assembly \"{0}\"",
                        assembly.GetName().Name), tie);
                } catch (ReflectionTypeLoadException rtle) {
                    // Error while loading a class in the assembly (possibly in a static constructor).
                    StringBuilder builder = new StringBuilder();
                    builder.AppendFormat("Error while loading types in assembly \"{0}\": ", assembly.GetName().Name);
                    foreach (Exception loaderEx in rtle.LoaderExceptions) {
                        builder.AppendLine();
                        builder.AppendFormat("  {0}", loaderEx.Message);
                    }
                    throw new CoreException(builder.ToString(), rtle);
                }
            }

            return exitCode;
        }

        /// <summary>
        /// Checks whether the given assembly has an embedded type library.
        /// </summary>
        /// <param name="assemblyName">Full path to assembly to check.
        /// Cannot be <c>null</c>.</param>
        /// <returns><c>true</c> if the assembly has an embedded type library,
        /// <c>false</c> otherwise.</returns>
        private bool AssemblyHasEmbeddedTypeLibrary(string assemblyName)
        {
            Debug.Assert(assemblyName != null);

            // Try loading the type library from the assembly. If it
            // doesn't work, we'll know there's none.
            ITypeLib typeLib = null;
            try {
                LoadTypeLibEx(assemblyName, REGKIND.REGKIND_NONE, out typeLib);
            } catch {
            }
            return typeLib != null;
        }

        /// <summary>
        /// Checks whether the given assembly was imported from a type library.
        /// </summary>
        /// <param name="assemblyToCheck"><see cref="System.Reflection.Assembly"/>
        /// to check. Cannot be <c>null</c>.</param>
        /// <returns><c>true</c> if <paramref name="assemblyToCheck"/> was
        /// imported from a type library, <c>false</c> otherwise.</returns>
        private bool AssemblyIsImportedFromTypeLibrary(Assembly assemblyToCheck)
        {
            Debug.Assert(assemblyToCheck != null);

            // Check if the assembly has the ImportedFromTypeLib attribute.
            bool importedFromTypeLib = false;
            IList<CustomAttributeData> attribs = CustomAttributeData.GetCustomAttributes(assemblyToCheck);
            foreach (CustomAttributeData attrib in attribs) {
                if (attrib.Constructor.DeclaringType == typeof(ImportedFromTypeLibAttribute)) {
                    importedFromTypeLib = true;
                    break;
                }
            }
            return importedFromTypeLib;
        }

        /// <summary>
        /// Called when the user passes the <c>/tlb</c> argument. If needed, exports
        /// the assembly to the specified type library, then registers the type library.
        /// </summary>
        private void ExportAndRegisterTypeLibrary()
        {
            Debug.Assert(prms.typeLibrary != null);

            // If the tlb is in the assembly, register it right away.
            // Otherwise, export and register it, which could cause
            // recursive registrations.
            TypeLibHelper tlbHelper = new TypeLibHelper(console, prms);
            if (String.Compare(prms.typeLibrary, prms.assemblyName, true) == 0) {
                tlbHelper.RegisterTypeLibrary(prms.typeLibrary);
            } else {
                tlbHelper.ExportAndRegisterTypeLibrary(assembly, prms.typeLibrary);
            }
        }

        /// <summary>
        /// Called when the user passes the <c>/tlb</c> argument in combination
        /// with <c>/unregister</c>. Locates and unregisters the type library.
        /// </summary>
        private void UnregisterTypeLibrary()
        {
            Debug.Assert(prms.typeLibrary != null);
            Debug.Assert(prms.unregister);

            // Check if the assembly was imported from a type lib in the first place;
            // if so, we can't unregister it - user needs to unregister the originating type lib.
            if (AssemblyIsImportedFromTypeLibrary(assembly)) {
                console.WriteLine("Cannot unregister type library of assembly \"{0}\" because " +
                    "it has been imported from another type library.", assembly.GetName().Name);
                console.WriteLine("Please unregister the originating type library instead.");
            } else {
                // Unregister the type lib using type lib helper.
                new TypeLibHelper(console, prms).UnregisterTypeLibrary(prms.typeLibrary);
            }
        }

        /// <summary>
        /// Generates the registry file (.reg) asked for by the user via the
        /// <c>/regfile</c> command-line option.
        /// </summary>
        /// <returns>Exit code to return from the CLRegAsm tool.</returns>
        /// <exception cref="CoreException">User asked for codebase but provided assembly
        /// has no codebase.</exception>
        private int GenerateRegistryFile()
        {
            // This will be changed if an error occurs
            int exitCode = 0;

            // Get list of registrable types in the assembly.
            Type[] registrableTypes = regServices.GetRegistrableTypesInAssembly(assembly);

            // Check if assembly is a primary interop assembly.
            IList<CustomAttributeData> attribs = CustomAttributeData.GetCustomAttributes(assembly);
            bool assemblyIsPrimaryInterop = false;
            foreach (CustomAttributeData attrib in attribs) {
                if (attrib.Constructor.DeclaringType == typeof(PrimaryInteropAssemblyAttribute)) {
                    assemblyIsPrimaryInterop = true;
                    break;
                }
            }

            if (registrableTypes.Length != 0 || assemblyIsPrimaryInterop) {
                // Get codebase if needed.
                string codebase = null;
                if (prms.setCodeBase) {
                    codebase = assembly.CodeBase;
                    if (String.IsNullOrEmpty(codebase)) {
                        throw new CoreException(String.Format("Assembly \"{0}\" has no codebase; " +
                            "cannot include codebase in registry file.", prms.assemblyName));
                    }
                }

                // Get root key name depending on whether this is per-user or per-machine.
                string rootKeyName;
                if (prms.perUser) {
                    rootKeyName = String.Format(@"{0}\Software\Classes", Registry.CurrentUser.Name);
                } else {
                    rootKeyName = Registry.ClassesRoot.Name;
                }

                // Create regfile writer.
                Debug.Assert(!String.IsNullOrEmpty(prms.registryFile));
                using (RegistryFileWriter writer = new RegistryFileWriter(prms.registryFile)) {
                    // Write each registrable type to the file.
                    foreach (Type type in registrableTypes) {
                        if (type.IsValueType) {
                            // Value type, like struct.
                            WriteValueTypeInRegistryFile(type, codebase, rootKeyName, writer);
                        } else if (regServices.TypeRepresentsComType(type)) {
                            // Com-imported type.
                            WriteComImportInRegistryFile(type, codebase, rootKeyName, writer);
                        } else {
                            // Class type.
                            WriteClassInRegistryFile(type, codebase, rootKeyName, writer);
                        }
                    }

                    // Write primary interop assembly info.
                    foreach (CustomAttributeData attrib in attribs) {
                        if (attrib.Constructor.DeclaringType == typeof(PrimaryInteropAssemblyAttribute)) {
                            WritePrimaryInteropAssemblyInRegistryFile(attrib, codebase, rootKeyName, writer);
                            break;
                        }
                    }
                }

                // We're done, tell the user.
                console.WriteLine("Registry file \"{0}\" generated successfully.", prms.registryFile);
            } else if (!prms.silent) {
                // No registrable types and assembly isn't a primary interop assembly; can't continue.
                console.Error.WriteLine("Assembly \"{0}\" has no registrable types and is not a " +
                    "primary interop assembly: cannot generate registry file.", prms.assemblyName);
                exitCode = 500;
            }

            return exitCode;
        }

        /// <summary>
        /// Writes the entries needed to register a value type (e.g. structs) to
        /// the registry file.
        /// </summary>
        /// <param name="type">Type to write.</param>
        /// <param name="codebase">Optional codebase, if it needs to be written.</param>
        /// <param name="rootKeyName">Name of root key where to add types.</param>
        /// <param name="writer"><see cref="RegistryKeyWriter"/> used to write
        /// to the registry file.</param>
        private void WriteValueTypeInRegistryFile(Type type, string codebase,
            string rootKeyName, RegistryFileWriter writer)
        {
            Debug.Assert(type != null);
            Debug.Assert(!String.IsNullOrEmpty(rootKeyName));
            Debug.Assert(writer != null);

            // Value types are written to the Classes\Record key, using the type's GUID and its version.
            string keyPath = String.Format(@"{0}\Record\{1}\{2}", rootKeyName,
                type.GUID.ToString("B").ToUpper(CultureInfo.InvariantCulture), assembly.GetName().Version);
            using (RegistryKeyWriter keyWriter = writer.AddKey(keyPath)) {
                keyWriter.AddValue("Class", type.FullName);
                keyWriter.AddValue("Assembly", assembly.FullName);
                keyWriter.AddValue("RuntimeVersion", assembly.ImageRuntimeVersion);
                if (!String.IsNullOrEmpty(codebase)) {
                    keyWriter.AddValue("CodeBase", codebase);
                }
            }
        }

        /// <summary>
        /// Writes the entries needed to register a COM-imported type (e.g. a type
        /// with the <see cref="System.Runtime.InteropServices.ComImportAttribute"/>)
        /// to the registry file.
        /// </summary>
        /// <param name="type">Type to write.</param>
        /// <param name="codebase">Optional codebase, if it needs to be written.</param>
        /// <param name="rootKeyName">Name of root key where to add types.</param>
        /// <param name="writer"><see cref="RegistryFileWriter"/> used to write to the
        /// registry file.</param>
        private void WriteComImportInRegistryFile(Type type, string codebase,
            string rootKeyName, RegistryFileWriter writer)
        {
            Debug.Assert(type != null);
            Debug.Assert(!String.IsNullOrEmpty(rootKeyName));
            Debug.Assert(writer != null);

            // For COM imports, we write the CLSID, but no ProgID.
            string keyPath = String.Format(@"{0}\CLSID\{1}\InprocServer32", rootKeyName,
                type.GUID.ToString("B").ToUpper(CultureInfo.InvariantCulture));
            using (RegistryKeyWriter keyWriter = writer.AddKey(keyPath)) {
                keyWriter.AddValue("Class", type.FullName);
                keyWriter.AddValue("Assembly", assembly.FullName);
                keyWriter.AddValue("RuntimeVersion", assembly.ImageRuntimeVersion);
                if (!String.IsNullOrEmpty(codebase)) {
                    keyWriter.AddValue("CodeBase", codebase);
                }
            }
            // Also write versioned key.
            keyPath += String.Format(@"\{0}", assembly.GetName().Version);
            using(RegistryKeyWriter keyWriter = writer.AddKey(keyPath)) {
                keyWriter.AddValue("Class", type.FullName);
                keyWriter.AddValue("Assembly", assembly.FullName);
                keyWriter.AddValue("RuntimeVersion", assembly.ImageRuntimeVersion);
                if (!String.IsNullOrEmpty(codebase)) {
                    keyWriter.AddValue("CodeBase", codebase);
                }
            }
        }

        /// <summary>
        /// Writes the entries needed to register a class type to the registry file.
        /// </summary>
        /// <param name="type">Type to write.</param>
        /// <param name="codebase">Optional codebase, if it needs to be written.</param>
        /// <param name="rootKeyName">Name of root key where to add types.</param>
        /// <param name="writer"><see cref="RegistryFileWriter"/> used to write to the
        /// registry file.</param>
        private void WriteClassInRegistryFile(Type type, string codebase,
            string rootKeyName, RegistryFileWriter writer)
        {
            Debug.Assert(type != null);
            Debug.Assert(!String.IsNullOrEmpty(rootKeyName));
            Debug.Assert(writer != null);

            // For class types, we add ProgID entries AND CLSID entries.
            string clsid = type.GUID.ToString("B").ToUpper(CultureInfo.InvariantCulture);
            string progId = regServices.GetProgIdForType(type);
            string keyPath;
            
            // First ProgID if class has one. Link it to CLSID.
            if (!String.IsNullOrEmpty(progId)) {
                keyPath = String.Format(@"{0}\{1}", rootKeyName, progId);
                using (RegistryKeyWriter keyWriter = writer.AddKey(keyPath)) {
                    keyWriter.AddDefaultValue(type.FullName);
                }
                keyPath += @"\CLSID";
                using (RegistryKeyWriter keyWriter = writer.AddKey(keyPath)) {
                    keyWriter.AddDefaultValue(clsid);
                }
            }

            // Now CLSID. First write class name in root CLSID key.
            keyPath = String.Format(@"{0}\CLSID\{1}", rootKeyName, clsid);
            using (RegistryKeyWriter keyWriter = writer.AddKey(keyPath)) {
                keyWriter.AddDefaultValue(type.FullName);
            }

            // Now write all infos in InprocServer32.
            keyPath += @"\InprocServer32";
            using (RegistryKeyWriter keyWriter = writer.AddKey(keyPath)) {
                keyWriter.AddDefaultValue("mscoree.dll");
                keyWriter.AddValue("ThreadingModel", "Both");
                keyWriter.AddValue("Class", type.FullName);
                keyWriter.AddValue("Assembly", assembly.FullName);
                keyWriter.AddValue("RuntimeVersion", assembly.ImageRuntimeVersion);
                if (codebase != null) {
                    keyWriter.AddValue("CodeBase", codebase);
                }
            }

            // Also write versioned key.
            keyPath += String.Format(@"\{0}", assembly.GetName().Version);
            using (RegistryKeyWriter keyWriter = writer.AddKey(keyPath)) {
                keyWriter.AddValue("Class", type.FullName);
                keyWriter.AddValue("Assembly", assembly.FullName);
                keyWriter.AddValue("RuntimeVersion", assembly.ImageRuntimeVersion);
                if (codebase != null) {
                    keyWriter.AddValue("CodeBase", codebase);
                }
            }

            // If class has a ProgID, link the CLSID to it.
            if (!String.IsNullOrEmpty(progId)) {
                keyPath = String.Format(@"{0}\CLSID\{1}\ProgId", rootKeyName, clsid);
                using (RegistryKeyWriter keyWriter = writer.AddKey(keyPath)) {
                    keyWriter.AddDefaultValue(progId);
                }
            }

            // Finally, add implemented categories.
            keyPath = String.Format(@"{0}\CLSID\{1}\Implemented Categories\{2}", rootKeyName, clsid,
                regServices.GetManagedCategoryGuid().ToString("B").ToUpper(CultureInfo.InvariantCulture));
            writer.AddEmptyKey(keyPath);
        }

        /// <summary>
        /// Writes the entries needed to register our assembly as a primary
        /// interop assembly to the registry file.
        /// </summary>
        /// <param name="attrib"><see cref="System.Reflection.CustomAttributeData"/>
        /// for the assembly's <see cref="System.Runtime.InteropServices.PrimaryInteropAssemblyAttribute"/>.</param>
        /// <param name="codebase">Optional codebase, if it needs to be written.</param>
        /// <param name="rootKeyName">Name of root key where to add types.</param>
        /// <param name="writer"><see cref="RegistryFileWriter"/> used to write to the
        /// registry file.</param>
        /// <seealso cref="System.Runtime.InteropServices.PrimaryInteropAssemblyAttribute"/>
        private void WritePrimaryInteropAssemblyInRegistryFile(CustomAttributeData attrib,
            string codebase, string rootKeyName, RegistryFileWriter writer)
        {
            Debug.Assert(attrib != null);
            Debug.Assert(!String.IsNullOrEmpty(rootKeyName));
            Debug.Assert(writer != null);

            // For this, we add data to the Classes\TypeLib key, using TypeLib ID and version,
            // pulled from the PrimaryInteropAssemblyAttribute (and inexplicably expressed as hexadecimal values).
            string keyPath = String.Format(@"{0}\TypeLib\{1}\{2}.{3}", rootKeyName,
                Marshal.GetTypeLibGuidForAssembly(assembly).ToString("B").ToUpper(CultureInfo.InvariantCulture),
                ((int) attrib.ConstructorArguments[0].Value).ToString("x", CultureInfo.InvariantCulture),
                ((int) attrib.ConstructorArguments[1].Value).ToString("x", CultureInfo.InvariantCulture));
            using (RegistryKeyWriter keyWriter = writer.AddKey(keyPath)) {
                keyWriter.AddValue("PrimaryInteropAssemblyName", assembly.FullName);
                if (!String.IsNullOrEmpty(codebase)) {
                    keyWriter.AddValue("PrimaryInteropAssemblyCodeBase", codebase);
                }
            }
        }

        /// <summary>
        /// Resolves an assembly in the reflection-only context. This will be called
        /// for references of our assembly when loaded via
        /// <see cref="System.Reflection.Assembly.ReflectionOnlyLoadFrom(string)"/>.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">Event arguments. It contains the name of the
        /// unresolved assembly.</param>
        /// <returns><see cref="Assembly"/> instance corresponding to the requested
        /// assembly, loaded in the reflection-only context. This cannot be
        /// <c>null</c>; if an error occurs while locating or loading the assembly,
        /// an exception will be thrown.</returns>
        /// <exception cref="FileNotFoundException">Could not find the dependent assembly
        /// specified in <paramref name="args"/>.</exception>
        private Assembly ResolveReflectionOnlyAssembly(object sender, ResolveEventArgs args)
        {
            Assembly reqAssembly = null;

            // Check if user provided assembly paths.
            if (prms.assemblyPaths == null || prms.assemblyPaths.Length == 0) {
                // No assembly paths, attempt to load the assembly as-is.
                reqAssembly = Assembly.ReflectionOnlyLoad(AppDomain.CurrentDomain.ApplyPolicy(args.Name));
            } else {
                // Look for assembly in each of the provided paths.
                // First look for DLL assemblies, then for EXEs.
                string fileNameWithoutExt = new AssemblyName(args.Name).Name;
                for (int i = 0; reqAssembly == null && i < prms.assemblyPaths.Length; ++i) {
                    string asmPath = prms.assemblyPaths[i];
                    string dllFilePath = Path.Combine(asmPath, fileNameWithoutExt + ".dll");
                    string exeFilePath = Path.Combine(asmPath, fileNameWithoutExt + ".exe");
                    if (File.Exists(dllFilePath)) {
                        reqAssembly = Assembly.ReflectionOnlyLoadFrom(dllFilePath);
                    } else if (File.Exists(exeFilePath)) {
                        reqAssembly = Assembly.ReflectionOnlyLoadFrom(exeFilePath);
                    }
                }
            }

            // If we reach this point and we didn't find the assembly, throw.
            if (reqAssembly == null) {
                throw new FileNotFoundException(String.Format("Could not find assembly " +
                    "dependency \"{0}\"", args.Name), args.Name);
            }

            // Log assembly loading in verbose mode.
            console.Verbose.WriteLine("Assembly \"{0}\" loaded at {1} (Name: {2})", args.Name,
                reqAssembly.FullName, reqAssembly.CodeBase);

            return reqAssembly;
        }

        /// <summary>
        /// Displays a header describing the program and displaying copyright
        /// information.
        /// </summary>
        /// <param name="console"><see cref="CLConsole"/> to use to output.
        /// Cannot be <c>null</c>.</param>
        private static void ShowCopyright(CLConsole console)
        {
            Debug.Assert(console != null);

            console.WriteLine("CL's .NET Assembly Registration Tool, version {0} (.NET {1})",
                GetThisAssemblyVersionAsString(), GetThisAssemblyRutimeVersion());
            console.WriteLine(GetThisAssemblyCopyrightInfo());
            console.WriteLine();
        }

        /// <summary>
        /// Displays usage information to the console.
        /// </summary>
        /// <param name="console"><see cref="CLConsole"/> to use to output.
        /// Cannot be <c>null</c>.</param>
        private static void ShowUsage(CLConsole console)
        {
            Debug.Assert(console != null);

            // The parser lib doesn't display a sample syntax, so display a little header to include one.
            console.WriteLine("Syntax: {0} <AssemblyName> [Options]",
                Path.GetFileName(Assembly.GetExecutingAssembly().Location));

            // Now display the usage info returned by the parser.
            console.WriteLine("Options:");
            console.WriteLine(CommandLineArguments.Parser.ArgumentsUsage(typeof(Params)));
        }

        /// <summary>
        /// Returns a string version of the <see cref="Version"/> of this assembly.
        /// </summary>
        /// <returns>String representation of this assembly's version info.</returns>
        private static string GetThisAssemblyVersionAsString()
        {
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            int numComponents;
            if (version.Revision > 0) {
                numComponents = 4;
            } else if (version.Build > 0) {
                numComponents = 3;
            } else {
                numComponents = 2;
            }
            return version.ToString(numComponents);
        }

        /// <summary>
        /// Returns a string containing the .NET Framework version required by
        /// this assembly (e.g. the "target framework").
        /// </summary>
        /// <returns>Target .NET Framework of this assembly as a string.</returns>
        private static string GetThisAssemblyRutimeVersion()
        {
            return Assembly.GetExecutingAssembly().ImageRuntimeVersion;
        }

        /// <summary>
        /// Returns a string containing the copyright information of this assembly.
        /// </summary>
        /// <returns>Copyright information of this assembly.</returns>
        private static string GetThisAssemblyCopyrightInfo()
        {
            object[] attribs = Assembly.GetExecutingAssembly().GetCustomAttributes(
                typeof(AssemblyCopyrightAttribute), true);
            Debug.Assert(attribs != null && attribs.Length > 0);
            return (attribs[0] as AssemblyCopyrightAttribute).Copyright;
        }
    }

    /// <summary>
    /// Exception class used by the <see cref="Core"/> class when
    /// an error occurs during execution.
    /// </summary>
    [Serializable]
    public class CoreException : CLRegAsmException
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public CoreException()
            : base()
        {
        }

        /// <summary>
        /// Constructor with exception message.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public CoreException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructor with exception message and inner exception.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="innerException">Inner exception.</param>
        public CoreException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
