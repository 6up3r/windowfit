/*
 * windowfit-launcher.exe: the logon task's action. It starts windowfit.exe from its own folder with only the system's
 * environment names, waits for it and returns its exit code, so Task Scheduler's restart policy still sees windowfit;
 * its own failures return an HRESULT, which "Last Run Result" spells out and windowfit never exits with.
 * The task runs elevated with the user's environment, which HKCU\Environment lets any unelevated process of that user
 * write; a .NET process reads CORECLR_*, DOTNET_* and COR_* from it (profilers, startup hooks) and would load their
 * code as administrator. This launcher has no runtime that reads them and passes none of them on.
 */
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdlib.h>
#include <wchar.h>

/* Inherited as they are: none of them names a program or code to load. SystemRoot, windir, ComSpec, PATHEXT and Path
 * are built below. ReleaseCheck.SetupEnvironment keeps the same names for the setup an update starts. */
static const wchar_t *const Kept[] = {
    L"ALLUSERSPROFILE", L"APPDATA", L"CommonProgramFiles", L"CommonProgramFiles(x86)", L"CommonProgramW6432",
    L"COMPUTERNAME", L"HOMEDRIVE", L"HOMEPATH", L"LOCALAPPDATA", L"NUMBER_OF_PROCESSORS", L"OS",
    L"PROCESSOR_ARCHITECTURE", L"PROCESSOR_IDENTIFIER", L"PROCESSOR_LEVEL", L"PROCESSOR_REVISION",
    L"ProgramData", L"ProgramFiles", L"ProgramFiles(x86)", L"ProgramW6432", L"PUBLIC", L"SystemDrive", L"TEMP", L"TMP",
    L"USERDOMAIN", L"USERNAME", L"USERPROFILE",
};

#define MaxVars (sizeof Kept / sizeof Kept[0] + 5)
#define MaxValue 32767

typedef struct {
    const wchar_t *name;
    wchar_t *value;
} Var;

static int CompareNames(const void *a, const void *b)
{
    return _wcsicmp(((const Var *)a)->name, ((const Var *)b)->name);
}

static wchar_t *Copy(const wchar_t *s)
{
    size_t n = wcslen(s) + 1;
    wchar_t *c = (wchar_t *)malloc(n * sizeof(wchar_t));
    if (c)
        wmemcpy(c, s, n);
    return c;
}

/* The block CreateProcessW takes: name=value strings sorted by name, case-insensitive, then an empty string. */
static wchar_t *EnvironmentBlock(void)
{
    static Var vars[MaxVars];
    static wchar_t buffer[MaxValue];
    size_t count = 0, size = 1, i;
    wchar_t windows[MAX_PATH];
    UINT n = GetSystemWindowsDirectoryW(windows, MAX_PATH);

    if (n == 0 || n >= MAX_PATH)
        return NULL;
    for (i = 0; i < sizeof Kept / sizeof Kept[0]; i++) {
        DWORD len = GetEnvironmentVariableW(Kept[i], buffer, MaxValue);
        if (len > 0 && len < MaxValue && (vars[count].value = Copy(buffer)) != NULL)
            vars[count++].name = Kept[i];
    }
    /* From the system, not the environment: a user variable can override even these. */
    vars[count].name = L"SystemRoot";
    vars[count++].value = Copy(windows);
    vars[count].name = L"windir";
    vars[count++].value = Copy(windows);
    if (swprintf(buffer, MaxValue, L"%ls\\System32\\cmd.exe", windows) < 0)
        return NULL;
    vars[count].name = L"ComSpec";
    vars[count++].value = Copy(buffer);
    vars[count].name = L"PATHEXT";
    vars[count++].value = Copy(L".COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC");
    if (swprintf(buffer, MaxValue, L"%ls\\System32;%ls;%ls\\System32\\Wbem;%ls\\System32\\WindowsPowerShell\\v1.0\\",
                 windows, windows, windows, windows) < 0)
        return NULL;
    vars[count].name = L"Path";
    vars[count++].value = Copy(buffer);
    for (i = 0; i < count; i++) {
        if (!vars[i].value)
            return NULL;
        size += wcslen(vars[i].name) + 1 + wcslen(vars[i].value) + 1;
    }
    qsort(vars, count, sizeof(Var), CompareNames);

    wchar_t *block = (wchar_t *)malloc(size * sizeof(wchar_t)), *at = block;
    if (!block)
        return NULL;
    for (i = 0; i < count; i++) {
        size_t name = wcslen(vars[i].name), value = wcslen(vars[i].value);
        wmemcpy(at, vars[i].name, name);
        at += name;
        *at++ = L'=';
        wmemcpy(at, vars[i].value, value);
        at += value;
        *at++ = L'\0';
    }
    *at = L'\0';
    return block;
}

static int Failure(DWORD error)
{
    return (int)HRESULT_FROM_WIN32(error ? error : ERROR_GEN_FAILURE);
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previous, PWSTR commandLine, int show)
{
    wchar_t dir[MAX_PATH], exe[MAX_PATH];
    DWORD n = GetModuleFileNameW(NULL, dir, MAX_PATH), code;
    wchar_t *slash, *block;
    STARTUPINFOW startup = {sizeof startup};
    PROCESS_INFORMATION process;
    JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits = {0};
    HANDLE job;

    (void)instance;
    (void)previous;
    (void)commandLine;
    (void)show;
    if (n == 0 || n >= MAX_PATH || (slash = wcsrchr(dir, L'\\')) == NULL)
        return Failure(GetLastError());
    *slash = L'\0';
    if (swprintf(exe, MAX_PATH, L"%ls\\windowfit.exe", dir) < 0)
        return Failure(ERROR_FILENAME_EXCED_RANGE);
    if ((block = EnvironmentBlock()) == NULL)
        return Failure(ERROR_BAD_ENVIRONMENT);
    /* windowfit runs in a job that ends it when this launcher ends (the task's End), while the processes it starts,
     * an update's setup among them, break away and outlive both. */
    job = CreateJobObjectW(NULL, NULL);
    limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK;
    if (!job || !SetInformationJobObject(job, JobObjectExtendedLimitInformation, &limits, sizeof limits))
        return Failure(GetLastError());
    if (!CreateProcessW(exe, NULL, NULL, NULL, FALSE, CREATE_UNICODE_ENVIRONMENT | CREATE_SUSPENDED, block, dir, &startup,
                        &process))
        return Failure(GetLastError());
    if (!AssignProcessToJobObject(job, process.hProcess)) {
        code = (DWORD)Failure(GetLastError());
        TerminateProcess(process.hProcess, code);
        return (int)code;
    }
    ResumeThread(process.hThread);
    WaitForSingleObject(process.hProcess, INFINITE);
    if (!GetExitCodeProcess(process.hProcess, &code))
        code = (DWORD)Failure(GetLastError());
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    CloseHandle(job);
    return (int)code;
}
