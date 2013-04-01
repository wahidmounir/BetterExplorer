﻿//    This file is part of QTTabBar, a shell extension for Microsoft
//    Windows Explorer.
//    Copyright (C) 2007-2010  Quizo, Paul Accisano
//
//    QTTabBar is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.
//
//    QTTabBar is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//    GNU General Public License for more details.
//
//    You should have received a copy of the GNU General Public License
//    along with QTTabBar.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using FileOperations;
using Microsoft.WindowsAPICodePack.Controls.WindowsForms;
using Microsoft.WindowsAPICodePack.Shell.FileOperations;
using WindowsHelper;

namespace Microsoft.WindowsAPICodePack.Shell.ExplorerBrowser {
  class HookLibManager {
      public static bool IsCustomDialog = false;
      //public static ExplorerBrowser Browser;
        private static IntPtr _hHookLib;
        public static SynchronizationContext SyncContext;
        private static readonly int[] HookStatus = Enumerable.Repeat(-1, Enum.GetNames(typeof(Hooks)).Length).ToArray();
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void HookLibCallback(int hookId, int retcode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate bool NewWindowCallback(IntPtr pIdl);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate bool CopyItemCallback(IFileOperation pThis, IntPtr SourceItems, IntPtr DestinationFolder);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate bool MoveItemCallback(IntPtr SourceItems, IntPtr DestinationFolder);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate bool RenameItemCallback(IntPtr SourceItems, String DestinationName);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private delegate bool DeleteItemCallback(IntPtr SourceItems);

        [StructLayout(LayoutKind.Sequential)]
        private struct CallbackStruct {
            public HookLibCallback cbHookResult;
            public NewWindowCallback cbNewWindow;
            public CopyItemCallback cbCopyItem;
            public MoveItemCallback cbMoveItem;
            public RenameItemCallback cbRenameItem;
            public DeleteItemCallback cbDeleteItem;
        }

        private static readonly CallbackStruct callbackStruct = new CallbackStruct() {
            cbHookResult = HookResult,
            cbCopyItem = CopyItem,
            cbMoveItem = MoveItem,
            cbRenameItem = RenameItem,
            cbDeleteItem = DeleteItem,
            
        };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int InitHookLibDelegate(CallbackStruct fpHookResult);

        public enum HookCheckPoint{
            Initial,
        }

        // Unmarked hooks exist only to set other hooks.
        private enum Hooks {
            CoCreateInstance = 0,           // Treeview Middle-click; FileCopyOperations
            RegisterDragDrop,               // DragDrop
            SHCreateShellFolderView,
            BrowseObject,                   // Control Panel dialog OK/Cancel buttons
            CreateViewWindow3,              // Header in all Views
            MessageSFVCB,                   // Refresh = clear text
            UiaReturnRawElementProvider,
            QueryInterface,                 // Scrolling Lag workaround
            TravelToEntry,                  // Clear Search bar = back
            OnActivateSelection,            // Recently activated files
            SetNavigationState,             // Breadcrumb Bar Middle-click
            ShowWindow,                     // New Explorer window capturing
            UpdateWindowList,               // Compatibility with SHOpenFolderAndSelectItems
            DeleteItem,
            RenameItem,
            MoveItem,
            CopyItem,
        }

        public static void Initialize() {
            if(_hHookLib != IntPtr.Zero) return;
            string installPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string filename = IntPtr.Size == 8 ? "BEHookLib64.dll" : "BEHookLib32.dll";
            _hHookLib = WindowsAPI.LoadLibrary(Path.Combine(installPath, filename));
            int retcode = -1;
            if(_hHookLib == IntPtr.Zero) {
                int error = Marshal.GetLastWin32Error();
                //TODO: error should be logged;
            }
            else {
                IntPtr pFunc = WindowsAPI.GetProcAddress(_hHookLib, "Initialize");
                if(pFunc != IntPtr.Zero) {
                    InitHookLibDelegate initialize = (InitHookLibDelegate)
                            Marshal.GetDelegateForFunctionPointer(pFunc, typeof(InitHookLibDelegate));
                    try {
                        retcode = initialize(callbackStruct);
                    }
                    catch(Exception e) {
                        //TODO: Erors should be logged
                    }

                }
            }

            if(retcode == 0) return;
            //TODO: here Errors should be logged
        }

        private static void HookResult(int hookId, int retcode) {
            lock(callbackStruct.cbHookResult) {
                HookStatus[hookId] = retcode;
            }
        }


      private static bool CopyItem(IFileOperation pthis, IntPtr sourceItems, IntPtr destinationFolder)
      {
          if (!IsCustomDialog)
          {
              return false;
          }

          var destinationObject = Marshal.GetObjectForIUnknown(destinationFolder);
          var sourceObject = Marshal.GetObjectForIUnknown(sourceItems);
          var u = SyncContext;
          var shellobj = ShellObjectFactory.Create((IShellItem) destinationObject);
          var destinationLocation = shellobj.ParsingName;
          shellobj.Dispose();
          u.Post((o) =>
              {
                  var sourceItemsCollection =
                      ShellObjectCollection.FromDataObject(
                          (System.Runtime.InteropServices.ComTypes.IDataObject) sourceObject)
                                           .Select(c => c.ParsingName)
                                           .ToArray();
                  var win = System.Windows.Application.Current.MainWindow;

                  var tempWindow = new Shell.FileOperations.FileOperation(sourceItemsCollection, destinationLocation);
                  var currentDialog = win.OwnedWindows.OfType<FileOperationDialog>().SingleOrDefault();

                  if (currentDialog == null)
                  {
                      currentDialog = new FileOperationDialog();
                      tempWindow.ParentContents = currentDialog;
                      currentDialog.Owner = win;

                      tempWindow.Visibility = Visibility.Collapsed;
                      currentDialog.Contents.Add(tempWindow);
                  }
                  else
                  {
                      tempWindow.ParentContents = currentDialog;
                      tempWindow.Visibility = Visibility.Collapsed;
                      currentDialog.Contents.Add(tempWindow);
                  }

              }, null);
          return true;
      }

      private static bool MoveItem(IntPtr sourceItems, IntPtr destinationFolder) {
        if (!IsCustomDialog)
          return false;

        var destinationObject = Marshal.GetObjectForIUnknown(destinationFolder);
        var sourceObject = Marshal.GetObjectForIUnknown(sourceItems);
        var sourceItemsCollection = ShellObjectCollection.FromDataObject((System.Runtime.InteropServices.ComTypes.IDataObject)sourceObject).Select(c => c.ParsingName).ToArray();
        var destinationLocation = ShellObjectFactory.Create((IShellItem)destinationObject).ParsingName;
        SyncContext.Post((o) => {
          var tempWindow = new Shell.FileOperations.FileOperation(sourceItemsCollection, destinationLocation, OperationType.Move);
          var currentDialog = System.Windows.Application.Current.MainWindow.OwnedWindows.OfType<FileOperationDialog>().SingleOrDefault();
          if (currentDialog == null) {
            currentDialog = new FileOperationDialog();
            tempWindow.ParentContents = currentDialog;
            currentDialog.Owner = System.Windows.Application.Current.MainWindow;
            tempWindow.Visibility = Visibility.Collapsed;
            currentDialog.Contents.Add(tempWindow);
          } else {
            tempWindow.ParentContents = currentDialog;
            tempWindow.Visibility = Visibility.Collapsed;
            currentDialog.Contents.Add(tempWindow);
          }
        }, null);
        return true;
      }

      private static bool RenameItem(IntPtr SourceItems, String DestinationName) {
        return false;
      }

      private static bool DeleteItem(IntPtr sourceItems) {
        if (!IsCustomDialog)
          return false;

          var isMoveToRB = Control.ModifierKeys != Keys.Shift;

          var sourceObject = Marshal.GetObjectForIUnknown(sourceItems);

          var confirmationDialog = new FODeleteDialog();
          confirmationDialog.MessageCaption = "Removal confirmation";
          var sourceItemsCollection = ShellObjectCollection.FromDataObject((System.Runtime.InteropServices.ComTypes.IDataObject)sourceObject).Select(c => c.ParsingName).ToArray();
          var win = System.Windows.Application.Current.MainWindow;
          if (sourceItemsCollection.Count() == 1)
          {
            ShellObject item = ShellObject.FromParsingName(sourceItemsCollection[0]);
            item.Thumbnail.CurrentSize = new Size(96, 96);
            confirmationDialog.MessageIcon = item.Thumbnail.BitmapSource;
            confirmationDialog.MessageText = isMoveToRB
                                               ? "Are you sure you want to move " +
                                                 item.GetDisplayName(DisplayNameType.Default) + " to Recycle Bin?"
                                               : "Are you sure you want to remove " +
                                                 item.GetDisplayName(DisplayNameType.Default) + " permanently?";
          }
          else
          {
              confirmationDialog.MessageText = isMoveToRB
                                               ? "Are you sure you want to move selected " +
                                                 sourceItemsCollection.Count() + " items to Recycle Bin?"
                                               : "Are you sure you want to remove selected " +
                                                 sourceItemsCollection.Count() + " items permanently?";
          }

        confirmationDialog.Owner = win;
        if (confirmationDialog.ShowDialog() == true){
          var tempWindow =
            new Shell.FileOperations.FileOperation(sourceItemsCollection, String.Empty,
                                                                                OperationType.Delete,
                                                                                isMoveToRB);
          var currentDialog = win.OwnedWindows.OfType<FileOperationDialog>().SingleOrDefault();

          if (currentDialog == null)
          {
            currentDialog = new FileOperationDialog();
            tempWindow.ParentContents = currentDialog;
            currentDialog.Owner = win;

            tempWindow.Visibility = Visibility.Collapsed;
            currentDialog.Contents.Add(tempWindow);
          }
          else
          {
            tempWindow.ParentContents = currentDialog;
            tempWindow.Visibility = Visibility.Collapsed;
            currentDialog.Contents.Add(tempWindow);
          }
        }
        return true;
      }

      

      public static void DeleteToRecycleBin(ShellObject[] SelectedItems) {
        string Files = "";
        foreach (ShellObject selectedItem in SelectedItems) {
          if (Files == "") {
            Files = selectedItem.ParsingName;
          } else
            Files = String.Format("{0}\0{1}", Files, selectedItem.ParsingName);
        }
        RecybleBin.Send(Files);
      }
        public static void CheckHooks() {
            //TODO:
        }
    }
}