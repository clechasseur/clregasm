// ICreateTypeLib.cs
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
using System.Runtime.InteropServices;

namespace CL.RegAsm.Utils
{
    /// <summary>
    /// Definition of the <c>ICreateTypeLib</c> interface from the
    /// OLE Automation API. This interface is defined in oaidl.idl.
    /// </summary>
    [ComVisible(false)]
    [Guid("00020406-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface ICreateTypeLib
    {
        void CreateTypeInfo([In] string name, [In] System.Runtime.InteropServices.ComTypes.TYPEKIND kind,
            [Out] out ICreateTypeInfo createTypeInfo);
        void SetName([In] string name);
        void SetVersion([In] ushort major, [In] ushort minor);
        void SetGuid(ref Guid guid);
        void SetDocString([In] string doc);
        void SetHelpFileName([In] string helpFileName);
        void SetHelpContext([In] uint helpContext);
        void SetLcid([In] uint lcid);
        void SetLibFlags([In] uint libFlags);
        void SaveAllChanges();
    }
}
