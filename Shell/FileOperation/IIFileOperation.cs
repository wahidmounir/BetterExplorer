﻿using BExplorer.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using BExplorer.Shell._Plugin_Interfaces;

namespace BExplorer.Shell {
	public class IIFileOperation : IDisposable {
		private static readonly Guid CLSID_FileOperation = new Guid("3ad05575-8857-4850-9277-11b85bdb8e09");
		private static readonly Type _fileOperationType = Type.GetTypeFromCLSID(CLSID_FileOperation);

		private bool _disposed;
		private IFileOperation _fileOperation;
		private FileOperationProgressSink _callbackSink;
		private uint _sinkCookie;

		public IIFileOperation() : this(null, false) { }
		public IIFileOperation(IntPtr owner, Boolean isRecycle) : this(null, owner, isRecycle) { }
		public IIFileOperation(IntPtr owner) : this(null, owner, false) { }
		public IIFileOperation(Boolean isRecycle) : this(null, isRecycle) { }
		public IIFileOperation(FileOperationProgressSink callbackSink, Boolean isRecycle) : this(callbackSink, IntPtr.Zero, isRecycle) { }
		public IIFileOperation(FileOperationProgressSink callbackSink, IntPtr owner, Boolean isRecycle) {
			_callbackSink = callbackSink;
			_fileOperation = (IFileOperation)Activator.CreateInstance(_fileOperationType);

			var Flags = isRecycle ? FileOperationFlags.FOF_NOCONFIRMMKDIR | FileOperationFlags.FOF_ALLOWUNDO : FileOperationFlags.FOF_NOCONFIRMMKDIR;
			_fileOperation.SetOperationFlags(Flags);

			if (_callbackSink != null) _sinkCookie = _fileOperation.Advise(_callbackSink);
			if (owner != IntPtr.Zero) _fileOperation.SetOwnerWindow((uint)owner);
		}

		public void CopyItem(IShellItem source, IListItemEx destination) {
			ThrowIfDisposed();
			var shellItem = FileSystemListItem.ToFileSystemItem(IntPtr.Zero, new ShellItem(source).Pidl);
			if (shellItem.Parent.Equals(destination)) {
				_fileOperation.SetOperationFlags(FileOperationFlags.FOF_NOCONFIRMMKDIR | FileOperationFlags.FOF_RENAMEONCOLLISION);
			}
			_fileOperation.CopyItem(source, destination.ComInterface, null, null);
		}

		public void MoveItem(IShellItem source, IShellItem destination, string newName) {
			ThrowIfDisposed();
			_fileOperation.MoveItem(source, destination, newName, null);
		}

		public void RenameItem(IShellItem source, string newName) {
			ThrowIfDisposed();
			_fileOperation.RenameItem(source, newName, null);
		}

		public void DeleteItem(IShellItem source) {
			ThrowIfDisposed();
			_fileOperation.DeleteItem(source, null);
		}

		public void PerformOperations() {
			ThrowIfDisposed();
			try {
				_fileOperation.PerformOperations();
			}
			catch {
			}
		}

		public bool GetAnyOperationAborted() {
			ThrowIfDisposed();
			return this._fileOperation.GetAnyOperationsAborted();
		}

		private void ThrowIfDisposed() {
			if (_disposed) throw new ObjectDisposedException(GetType().Name);
		}

		public void Dispose() {
			if (!_disposed) {
				_disposed = true;
				if (_callbackSink != null) _fileOperation.Unadvise(_sinkCookie);
				Marshal.FinalReleaseComObject(_fileOperation);
			}
		}
	}
}
