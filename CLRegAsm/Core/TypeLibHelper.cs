// TypeLibHelper.cs
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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using CL.RegAsm.Utils;

namespace CL.RegAsm
{
    /// <summary>
    /// Helper to deal with exporting and registering type libraries from assemblies.
    /// </summary>
    internal sealed class TypeLibHelper
    {
        // REGKIND enum for LoadTypeLibEx.
        private enum REGKIND
        {
            REGKIND_DEFAULT,
            REGKIND_REGISTER,
            REGKIND_NONE,
        }

        /// Win32 API to load a type library from disk. See MSDN for documentation.
        [DllImport("oleaut32.dll", CharSet=CharSet.Unicode, PreserveSig=false)]
        private static extern void LoadTypeLibEx(string typeLibName, REGKIND regKind, out ITypeLib typeLib);

        /// Win32 API to register a type library. See MSDN for documentation.
        [DllImport("oleaut32.dll", CharSet=CharSet.Unicode, PreserveSig=false)]
        private static extern void RegisterTypeLib(ITypeLib typeLib, string typeLibName, string helpDirs);

        /// Win32 API to unregister a type library. See MSDN for documentation.
        [DllImport("oleaut32.dll", CharSet=CharSet.Unicode, PreserveSig=false)]
        private static extern void UnRegisterTypeLib(ref Guid libId, short majorVersion, short minorVersion,
            int lcid, System.Runtime.InteropServices.ComTypes.SYSKIND sysKind);

        /// Console to use to log events.
        private CLConsole console;

        /// Flags determining how to export type libs.
        private TypeLibExporterFlags flags;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="console">Console to use to log events.
        /// Cannot be <c>null</c>.</param>
        /// <param name="prms"><see cref="Params"/> that determine how
        /// type libraries are exported from assemblies. Cannot be
        /// <c>null</c>. </param>
        /// <exception cref="ArgumentNullException"><paramref name="console"/>
        /// or <paramref name="prms"/> is <c>null</c>.</exception>
        public TypeLibHelper(CLConsole console, Params prms)
        {
            // Validate input.
            if (console == null) {
                throw new ArgumentNullException("console");
            }
            if (prms == null) {
                throw new ArgumentNullException("prms");
            }

            // Save reference to console.
            this.console = console;

            // Determine exporter flags.
            if (prms.useRegisteredOnly) {
                this.flags = TypeLibExporterFlags.OnlyReferenceRegistered;
            } else {
                this.flags = TypeLibExporterFlags.None;
            }
        }

        /// <summary>
        /// Registers the given type library. This will add entries to the
        /// registry allowing other libraries to refer to the TLB.
        /// </summary>
        /// <param name="typeLibName">Full path to the type library.
        /// Cannot be <c>null</c> or <see cref="String.Empty"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeLibName"/>
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="typeLibName"/>
        /// is equal to <see cref="String.Empty"/>.</exception>
        /// <exception cref="CLRegAsmException">Loading or registering of
        /// type library <paramref name="typeLibName"/> has failed.</exception>
        public void RegisterTypeLibrary(string typeLibName)
        {
            // Validate input.
            if (typeLibName == null) {
                throw new ArgumentNullException("typeLibName");
            }
            if (typeLibName.Length == 0) {
                throw new ArgumentException("typeLibName cannot be empty", "typeLibName");
            }

            // Load type lib from disk and register it in one swoop.
            try {
                ITypeLib tlb;
                LoadTypeLibEx(typeLibName, REGKIND.REGKIND_REGISTER, out tlb);
            } catch (Exception e) {
                throw new CLRegAsmException(String.Format("Failed to load and register " +
                    "type library \"{0}\"", typeLibName), e);
            }

            // Log normal message to tell we've registered the type lib.
            console.WriteLine("Type library in file \"{0}\" has been registered.", typeLibName);
        }

        /// <summary>
        /// Exports the type library of the given assembly, then registers
        /// the type library. Will recursively export and register TLBs of
        /// referenced assemblies as needed.
        /// </summary>
        /// <param name="assembly"><see cref="System.Reflection.Assembly"/>
        /// to export. Cannot be <c>null</c>.</param>
        /// <param name="typeLibName">Full path to type library to export.
        /// Cannot be <c>null</c>, empty or equal to the path of
        /// <paramref name="assembly"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="assembly"/>
        /// or <paramref name="typeLibName"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="typeLibName"/>
        /// is <see cref="String.Empty"/>.
        /// 
        /// OR
        /// 
        /// <paramref name="typeLibName"/> is equal to the local path
        /// of <paramref name="assembly"/>.</exception>
        public void ExportAndRegisterTypeLibrary(Assembly assembly, string typeLibName)
        {
            // Validate input.
            if (assembly == null) {
                throw new ArgumentNullException("assembly");
            }
            if (typeLibName == null) {
                throw new ArgumentNullException("typeLibName");
            }
            if (typeLibName.Length == 0) {
                throw new ArgumentException("Type library name cannot be empty.", "typeLibName");
            }
            string assemblyName = AssemblyTools.GetAssemblyPath(assembly);
            if (String.Compare(assemblyName, typeLibName, true) == 0) {
                throw new ArgumentException("Type library cannot overwrite assembly.", "typeLibName");
            }

            // Export the type lib, then register it.
            ITypeLib tlb = ExportTypeLibrary(assembly, typeLibName);
            RegisterTypeLibrary(tlb, typeLibName);

            // Log normal message to tell we've registered the type lib.
            console.WriteLine("Type library \"{0}\" exported from assembly \"{1}\" " +
                "and registered successfully.", assemblyName, typeLibName);
        }

        /// <summary>
        /// Unregisters the given type library. This will remove entries
        /// pertaining to the types in the type library from the registry.
        /// </summary>
        /// <param name="typeLibName">Full path to type library to unregister.
        /// Cannot be <c>null</c> or <see cref="String.Empty"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="typeLibName"/>
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="typeLibName"/>
        /// is <see cref="String.Empty"/>.</exception>
        /// <exception cref="CLRegAsmException">Loading of type library
        /// <paramref name="typeLibName"/> has failed.
        /// 
        /// OR
        /// 
        /// Unregistering of type library <paramref name="typeLibName"/>
        /// has failed.</exception>
        public void UnregisterTypeLibrary(string typeLibName)
        {
            // Validate input.
            if (typeLibName == null) {
                throw new ArgumentNullException("typeLibName");
            }
            if (typeLibName.Length == 0) {
                throw new ArgumentException("Type library name cannot be empty.", "typeLibName");
            }

            // Load the type lib without registering it.
            ITypeLib tlb;
            try {
                LoadTypeLibEx(typeLibName, REGKIND.REGKIND_NONE, out tlb);
            } catch (Exception e) {
                throw new CLRegAsmException(String.Format("Failed to load type library \"{0}\"",
                    typeLibName), e);
            }

            try {
                // Retrive TLIBATTR structure which we need to unregister.
                IntPtr pTLibAttr = IntPtr.Zero;
                tlb.GetLibAttr(out pTLibAttr);
                try {
                    System.Runtime.InteropServices.ComTypes.TYPELIBATTR tlibAttr =
                        (System.Runtime.InteropServices.ComTypes.TYPELIBATTR) Marshal.PtrToStructure(
                            pTLibAttr, typeof(System.Runtime.InteropServices.ComTypes.TYPELIBATTR));

                    // Now we can unregister the type library.
                    UnRegisterTypeLib(ref tlibAttr.guid, tlibAttr.wMajorVerNum,
                        tlibAttr.wMinorVerNum, tlibAttr.lcid, tlibAttr.syskind);
                } finally {
                    if (pTLibAttr != IntPtr.Zero) {
                        tlb.ReleaseTLibAttr(pTLibAttr);
                    }
                }
            } catch (Exception e) {
                throw new CLRegAsmException(String.Format("Failed to unregister type library \"{0}\"",
                    typeLibName), e);
            }

            // Log normal message telling we've unregistered the type lib.
            console.WriteLine("Type library \"{0}\" unregistered successfully.", typeLibName);
        }

        /// <summary>
        /// Exports the type library of the given assembly to the given path.
        /// Will recursively export and register TLBs of referenced assemblies
        /// as needed.
        /// </summary>
        /// <param name="assembly"><see cref="System.Reflection.Assembly"/>
        /// to export. Cannot be <c>null</c>.</param>
        /// <param name="typeLibName">Full path to type library to export.
        /// Cannot be <c>null</c>, empty or equal to the path of
        /// <paramref name="assembly"/>.</param>
        /// <returns>Type library reference.</returns>
        /// <exception cref="CLRegAsmException">Failed to save type library
        /// <paramref name="typeLibName"/> to disk.</exception>
        private ITypeLib ExportTypeLibrary(Assembly assembly, string typeLibName)
        {
            Debug.Assert(assembly != null);
            Debug.Assert(!String.IsNullOrEmpty(typeLibName));
            Debug.Assert(String.Compare(AssemblyTools.GetAssemblyPath(assembly), typeLibName, true) != 0);

            // Create type lib converter and export the type lib.
            // This will call us back recursively if referenced assemblies'
            // type libs need to be exported and registered also.
            TypeLibConverter tlbConverter = new TypeLibConverter();
            ITypeLib tlb = (ITypeLib) tlbConverter.ConvertAssemblyToTypeLib(assembly,
                typeLibName, flags, new ConverterCallback(this));

            // Save all changes, which will save the data to disk.
            try {
                (tlb as ICreateTypeLib).SaveAllChanges();
            } catch (Exception e) {
                throw new CLRegAsmException(String.Format("Could not save type library \"{0}\" to disk",
                    typeLibName), e);
            }

            Debug.Assert(tlb != null);
            return tlb;
        }

        /// <summary>
        /// Registers the given type library. This will add entries to the
        /// registry allowing other libraries to refer to the TLB.
        /// </summary>
        /// <param name="tlb">Type library to register. Must have been
        /// loaded via <see cref="LoadTypeLibEx"/> or other means.
        /// Cannot be <c>null</c>.</param>
        /// <param name="typeLibName">Full path to type library.
        /// Cannot be <c>null</c> or <see cref="String.Empty"/>.</param>
        /// <exception cref="CLRegAsmException">Registering of type library
        /// <paramref name="tlb"/> has failed.</exception>
        private void RegisterTypeLibrary(ITypeLib tlb, string typeLibName)
        {
            Debug.Assert(tlb != null);
            Debug.Assert(!String.IsNullOrEmpty(typeLibName));

            try {
                RegisterTypeLib(tlb, typeLibName, Path.GetDirectoryName(typeLibName));
            } catch (Exception e) {
                throw new CLRegAsmException(String.Format("Failed to register type library \"{0}\"",
                    typeLibName), e);
            }

        }

        /// <summary>
        /// Internal class used during type library exporting. Will call back the
        /// <see cref="TypeLibHelper"/> if a referenced type library needs to be
        /// recursively exported.
        /// </summary>
        private sealed class ConverterCallback : ITypeLibExporterNotifySink
        {
            /// Parent type lib helper object.
            private TypeLibHelper parent;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="parent">Parent <see cref="TypeLibHelper"/>
            /// to call back for recursive exporting. Cannot be <c>null</c>. </param>
            public ConverterCallback(TypeLibHelper parent)
            {
                Debug.Assert(parent != null);

                this.parent = parent;
            }

            #region ITypeLibExporterNotifySink Members

            /// <summary>
            /// Reports an event during type library exporting.
            /// </summary>
            /// <param name="eventKind">Kind of event reported.</param>
            /// <param name="eventCode">Event code. 0 if the event is
            /// simply information, otherwise an HRESULT representing
            /// the warning or error code.</param>
            /// <param name="eventMsg">Event message.</param>
            public void ReportEvent(ExporterEventKind eventKind, int eventCode, string eventMsg)
            {
                // What we output depends on the kind of event we have.
                switch (eventKind) {
                    case ExporterEventKind.NOTIF_TYPECONVERTED: {
                        // A type has been converted; log in verbose mode.
                        parent.console.Verbose.WriteLine(eventMsg);
                        break;
                    }
                    case ExporterEventKind.NOTIF_CONVERTWARNING: {
                        // A converter warning; log as normal output.
                        parent.console.WriteLine(eventMsg);
                        break;
                    }
                    case ExporterEventKind.ERROR_REFTOINVALIDASSEMBLY: {
                        // An error occured, but for us, it's a bit like
                        // a warning, because failing to export will not
                        // kill the process. Log as normal output.
                        parent.console.WriteLine(eventMsg);
                        break;
                    }
                    default: {
                        Debug.Fail(String.Format("Invalid event kind: {0}", eventKind.ToString()));
                        break;
                    }
                }
            }

            /// <summary>
            /// Resolves a reference during the exporting process. This means that the
            /// type library references the type library in another assembly. We must
            /// call back our <see cref="TypeLibHelper"/> to do this.
            /// </summary>
            /// <param name="assembly">Assembly to resolve.</param>
            /// <returns>Type library for the specified <paramref name="assembly"/>.
            /// Must implement the <see cref="ITypeLib"/> interface.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="assembly"/>
            /// is <c>null</c>.</exception>
            public object ResolveRef(Assembly assembly)
            {
                ITypeLib tlb = null;

                // Validate input.
                if (assembly == null) {
                    throw new ArgumentNullException("assembly");
                }

                // We'll export the type lib next to the assembly.
                string assemblyName = assembly.GetName().Name;
                string typeLibName = Path.ChangeExtension(Path.Combine(Path.GetDirectoryName(
                    AssemblyTools.GetAssemblyPath(assembly)), assemblyName), ".tlb");
                
                // Log recursive references in verbose mode.
                parent.console.Verbose.WriteLine("Recursively exporting and registering type " +
                    "library of assembly \"{0}\" to \"{1}\".", assemblyName, typeLibName);

                // Call back parent to export and register the type lib.
                tlb = parent.ExportTypeLibrary(assembly, typeLibName);
                parent.RegisterTypeLibrary(tlb, typeLibName);

                Debug.Assert(tlb != null);
                return tlb;
            }

            #endregion
        }
    }
}
