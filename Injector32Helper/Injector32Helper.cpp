#include <windows.h>
#include <cstdio>

int wmain(int argc, wchar_t* argv[])
{
    if (argc < 3) return 2;

    DWORD pid = (DWORD)_wtoi(argv[1]);
    const wchar_t* dllPath = argv[2];
    SIZE_T pathBytes = (wcslen(dllPath) + 1) * sizeof(wchar_t);

    HANDLE hProcess = OpenProcess(PROCESS_ALL_ACCESS, FALSE, pid);
    if (!hProcess) return 3;

    LPVOID remote = VirtualAllocEx(hProcess, NULL, pathBytes, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
    if (!remote) { CloseHandle(hProcess); return 4; }

    if (!WriteProcessMemory(hProcess, remote, dllPath, pathBytes, NULL))
    {
        VirtualFreeEx(hProcess, remote, 0, MEM_RELEASE);
        CloseHandle(hProcess);
        return 5;
    }

    LPVOID loadLib = (LPVOID)GetProcAddress(GetModuleHandleW(L"kernel32.dll"), "LoadLibraryW");
    if (!loadLib)
    {
        VirtualFreeEx(hProcess, remote, 0, MEM_RELEASE);
        CloseHandle(hProcess);
        return 6;
    }

    HANDLE hThread = CreateRemoteThread(hProcess, NULL, 0,
        (LPTHREAD_START_ROUTINE)loadLib, remote, 0, NULL);
    if (!hThread)
    {
        VirtualFreeEx(hProcess, remote, 0, MEM_RELEASE);
        CloseHandle(hProcess);
        return 7;
    }

    WaitForSingleObject(hThread, 15000);

    DWORD exitCode = 0;
    GetExitCodeThread(hThread, &exitCode);

    CloseHandle(hThread);
    VirtualFreeEx(hProcess, remote, 0, MEM_RELEASE);
    CloseHandle(hProcess);

    return exitCode != 0 ? 0 : 1;
}