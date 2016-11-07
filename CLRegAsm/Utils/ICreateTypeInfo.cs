// ICreateTypeInfo.cs
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
    /// Definition of the <c>ICreateTypeInfo</c> interface from the
    /// OLE Automation API. This interface is defined in oaidl.idl.
    /// </summary>
    [ComVisible(false)]
    [Guid("00020405-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    internal interface ICreateTypeInfo
    {
        void SetGuid(ref Guid guid);
        void SetTypeFlags([In] uint typeFlags);
        void SetDocString([In] string doc);
        void SetHelpContext([In] uint helpContext);
        void SetVersion([In] ushort major, [In] ushort minor);
        void AddRefTypeInfo(System.Runtime.InteropServices.ComTypes.ITypeInfo typeInfo, [Out] out uint hRefType);
        void AddFuncDesc([In] uint index, ref System.Runtime.InteropServices.ComTypes.FUNCDESC funcDesc);
        void AddImplType([In] uint index, [In] uint hRefType);
        void SetImplTypeFlags([In] uint index, [In] int implTypeFlags);
        void SetAlignment([In] ushort alignment);
        void SetSchema([In] string schema);
        void AddVarDesc([In] uint index, ref System.Runtime.InteropServices.ComTypes.VARDESC varDesc);
        void SetFuncAndParamNames([In] uint index, [In] string names, [In] uint cNames);
        void SetVarName([In] uint index, [In] string name);
        void SetTypeDescAlias(ref System.Runtime.InteropServices.ComTypes.TYPEDESC typeDescAlias);
        void DefineFuncAsDllEntry([In] uint index, [In] string dllName, [In] string procName);
        void SetFuncDocString([In] uint index, [In] string docString);
        void SetVarDocString([In] uint index, [In] string docString);
        void SetFuncHelpContext([In] uint index, [In] uint helpContext);
        void SetVarHelpContext([In] uint index, [In] uint helpContext);
        void SetMops([In] uint index, [In] string mops);
        void SetTypeIdldesc(ref System.Runtime.InteropServices.ComTypes.IDLDESC idlDesc);
        void LayOut();
    }
}
